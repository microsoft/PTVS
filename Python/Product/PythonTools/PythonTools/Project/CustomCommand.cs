/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    sealed class CustomCommand : IDisposable {
        // Internal so unit tests can access them
        internal readonly string _target;
        internal readonly string _label;
        private bool _isDisposed;

        internal readonly string Verb;
        internal readonly uint AlternateCmdId;

        public const string ReplId = "2E918F01-ABA9-41A6-8345-5189FB4B5ABF";

        private static readonly Regex _customCommandLabelRegex = new Regex(
            @"resource\:
                        (?<assembly>.+?);
                        (?<namespace>.+?);
                        (?<key>.+)
                    $",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        private CustomCommand(
            PythonProjectNode project,
            string target,
            string label
        ) {
            _target = target;
            _label = string.IsNullOrWhiteSpace(label) ? target : label;

            var match = _customCommandLabelRegex.Match(label);
            if (match.Success) {
                DisplayLabel = LoadResourceFromAssembly(
                    match.Groups["assembly"].Value,
                    match.Groups["namespace"].Value,
                    match.Groups["key"].Value
                );
            } else {
                DisplayLabel = _label;
            }
            DisplayLabel = PerformSubstitutions(project, DisplayLabel);

            CanExecute = !string.IsNullOrEmpty(target);
            Verb = "Project." + Regex.Replace(
                DisplayLabelWithoutAccessKeys,
                "[^a-z0-9]+",
                "",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            );
            AlternateCmdId = AddNamedCommand(project.Site, Verb);
        }

        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                if (!string.IsNullOrEmpty(Verb) && AlternateCmdId > 0) {
                    RemoveNamedCommand(ServiceProvider.GlobalProvider, Verb);
                }
            }
        }

        private static uint AddNamedCommand(IServiceProvider provider, string name, string tooltipText = null) {
            var commands = provider.GetService(typeof(SVsProfferCommands)) as IVsProfferCommands3;
            if (commands == null) {
                return 0;
            }

            var package = typeof(PythonToolsPackage).GUID;
            var cmdSet = GuidList.guidPythonToolsCmdSet;
            uint cmdId;

            ErrorHandler.ThrowOnFailure(commands.AddNamedCommand(
                ref package,
                ref cmdSet,
                name,
                out cmdId,
                name,
                name,
                tooltipText,
                null, 0, 0,     // no image, image id or image index
                0,              // default flags
                0, new Guid[0] // new [] { VSConstants.UICONTEXT.SolutionExists_guid }
            ));

            return cmdId;
        }

        private static void RemoveNamedCommand(IServiceProvider provider, string name) {
            var commands = provider.GetService(typeof(SVsProfferCommands)) as IVsProfferCommands3;
            if (commands != null) {
                ErrorHandler.ThrowOnFailure(commands.RemoveNamedCommand(name));
            }
        }

        private static string PerformSubstitutions(PythonProjectNode project, string label) {
            return Regex.Replace(label, @"\{(?<key>\w+)\}", m => {
                var key = m.Groups["key"].Value;
                if ("projectname".Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                    return Path.ChangeExtension(project.ProjectFile, null);
                } else if ("projectfile".Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                    return project.ProjectFile;
                }
                return m.Value;
            });
        }

        private static string LoadResourceFromAssembly(string assembly, string ns, string key) {
            try {
                var asmName = new System.Reflection.AssemblyName(assembly);
                System.Reflection.Assembly asm = null;
                if (asmName.FullName == asmName.Name) {
                    // A partial name was provided. If there is an assembly with
                    // matching name in the current AppDomain, assume that is
                    // the intended one.
                    asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => assembly == a.GetName().Name);
                }
                
                asm = asm ?? System.Reflection.Assembly.Load(asmName);
                var rm = new System.Resources.ResourceManager(ns, asm);
                return rm.GetString(key, CultureInfo.CurrentUICulture);
            } catch (Exception ex) {
                ActivityLog.LogError(
                    SR.GetString(SR.PythonToolsForVisualStudio),
                    SR.GetString(SR.FailedToReadResource, assembly, ns, key, ex)
                );
                return key;
            }
        }

        public static IEnumerable<CustomCommand> GetCommands(
            Microsoft.Build.Evaluation.Project project,
            PythonProjectNode projectNode
        ) {
            var commandNames = project.GetPropertyValue("PythonCommands");
            if (!string.IsNullOrEmpty(commandNames)) {
                foreach (var name in commandNames.Split(';').Where(n => !string.IsNullOrEmpty(n))) {
                    ProjectTargetInstance targetInstance;
                    if (!project.Targets.TryGetValue(name, out targetInstance)) {
                        continue;
                    }

                    var targetXml = (targetInstance.Location.File == project.FullPath) ?
                        project.Xml :
                        // TryOpen will only return targets that were already
                        // loaded in the current collection; otherwise, null.
                        ProjectRootElement.TryOpen(targetInstance.Location.File, project.ProjectCollection);

                    if (targetXml == null) {
                        continue;
                    }

                    var target = targetXml.Targets.FirstOrDefault(t => name.Equals(t.Name, StringComparison.OrdinalIgnoreCase));
                    if (target != null) {
                        yield return new CustomCommand(projectNode, target.Name, target.Label);
                    }
                }
            }
        }

        public static string GetCommandsDisplayLabel(
            Microsoft.Build.Evaluation.Project project,
            PythonProjectNode projectNode
        ) {
            var label = project.GetPropertyValue("PythonCommandsDisplayLabel") ?? string.Empty;
            
            var match = _customCommandLabelRegex.Match(label);
            if (match.Success) {
                label = LoadResourceFromAssembly(
                    match.Groups["assembly"].Value,
                    match.Groups["namespace"].Value,
                    match.Groups["key"].Value
                );
            }

            if (string.IsNullOrEmpty(label)) {
                return SR.GetString(SR.PythonMenuLabel);
            }

            return PerformSubstitutions(projectNode, label);
        }


        public string DisplayLabel { get; private set; }

        public string DisplayLabelWithoutAccessKeys {
            get {
                // Changes "My &Command" into "My Command" while ensuring that
                // "C1 && C2" becomes "C1 & C2"
                return Regex.Replace(DisplayLabel, "&(.)", "$1");
            }
        }

        public bool CanExecute { get; private set; }

        public System.Threading.Tasks.Task Execute(PythonProjectNode project) {
            var task = ExecuteWorker(project);
            
            // Ensure the exception is observed.
            // The caller can check task.Exception to do their own reporting.
            task.ContinueWith(t => {
                try {
                    t.Wait();
                } catch (AggregateException ex) {
                    var exception = ex.InnerException;
                    if (exception is NoInterpretersException) {
                        // No need to log this exception or disable the command.
                        return;
                    }

                    // Prevent the command from executing again until the project is
                    // reloaded.
                    CanExecute = false;

                    // Log error to the ActivityLog.
                    ActivityLog.LogError(
                        SR.GetString(SR.PythonToolsForVisualStudio),
                        SR.GetString(SR.ErrorRunningCustomCommand, _target, ex.ToString())
                    );
                }
            });

            return task;
        }

        private async System.Threading.Tasks.Task ExecuteWorker(PythonProjectNode project) {
            var config = project.CurrentConfig ??
                project.BuildProject.CreateProjectInstance(Microsoft.Build.Execution.ProjectInstanceSettings.Immutable);

            IDictionary<string, Microsoft.Build.Execution.TargetResult> outputs;

            if (!config.Build(
                new[] { _target },
#if DEBUG
                new Microsoft.Build.Framework.ILogger[] { new Microsoft.Build.Logging.ConsoleLogger(Build.Framework.LoggerVerbosity.Detailed) },
#else
                Enumerable.Empty<Microsoft.Build.Framework.ILogger>(),
#endif
                Enumerable.Empty<Microsoft.Build.Logging.ForwardingLoggerRecord>(),
                out outputs
            )) {
                throw new InvalidOperationException(SR.GetString(SR.ErrorBuildingCustomCommand, _target));
            }

            var item = outputs.Values
                .SelectMany(result => result.Items)
                .FirstOrDefault(i => 
                    !string.IsNullOrEmpty(i.ItemSpec) &&
                    !string.IsNullOrEmpty(i.GetMetadata(BuildTasks.PythonCommand.TargetTypeKey))
                );

            if (item == null) {
                throw new InvalidOperationException(SR.GetString(SR.ErrorBuildingCustomCommand, _target));
            }

            var targetType = item.GetMetadata(BuildTasks.PythonCommand.TargetTypeKey);
            var executeIn = item.GetMetadata(BuildTasks.PythonCommand.ExecuteInKey);
            var arguments = item.GetMetadata(BuildTasks.PythonCommand.ArgumentsKey);
            var workingDir = item.GetMetadata(BuildTasks.PythonCommand.WorkingDirectoryKey);
            var environment = item.GetMetadata(BuildTasks.PythonCommand.EnvironmentKey);

            var environmentVars = new Dictionary<string, string>();
            foreach (var line in environment.Split('\r', '\n')) {
                int equals = line.IndexOf('=');
                if (equals > 0) {
                    environmentVars[line.Substring(0, equals)] = line.Substring(equals + 1);
                }
            }

            if (string.IsNullOrEmpty(workingDir)) {
                workingDir = project.ProjectHome;
            }

            string filename = item.ItemSpec;
            var factory = project.GetInterpreterFactory();

            var interpreterService = PythonToolsPackage.ComponentModel.GetService<IInterpreterOptionsService>();
            if (factory == null || interpreterService == null || factory == interpreterService.NoInterpretersValue) {
                throw new NoInterpretersException();
            }

            if (!string.IsNullOrEmpty(executeIn) &&
                executeIn.StartsWith("repl", StringComparison.InvariantCultureIgnoreCase)) {
                if (await RunInRepl(project, filename, arguments, workingDir, environmentVars, targetType, executeIn)) {
                    return;
                }
            }

            if ("script".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format("{0} {1}", ProcessOutput.QuoteSingleArgument(filename), arguments);
                filename = factory.Configuration.InterpreterPath;
            } else if ("module".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format("-m {0} {1}", filename, arguments);
                filename = factory.Configuration.InterpreterPath;
            } else if ("code".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format("-c \"{0}\"", filename);
                filename = factory.Configuration.InterpreterPath;
            }

            if ("output".Equals(executeIn, StringComparison.InvariantCultureIgnoreCase)) {
                RunInOutput(project, filename, arguments, workingDir, environmentVars);
            } else if ("consolepause".Equals(executeIn, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format(
                    "/C \"{0} {1}\" & pause",
                    ProcessOutput.QuoteSingleArgument(filename),
                    arguments
                );
                filename = "cmd.exe";
                RunInConsole(project, filename, arguments, workingDir, environmentVars);
            } else {
                RunInConsole(project, filename, arguments, workingDir, environmentVars);
            }
        }

        private async Task<bool> RunInRepl(
            PythonProjectNode project,
            string filename,
            string arguments,
            string workingDir,
            Dictionary<string, string> environmentVars,
            string targetType,
            string executeIn
        ) {
            bool resetRepl = executeIn.StartsWith("R", StringComparison.InvariantCulture);

            var replTitle = executeIn.Substring(4).TrimStart(' ', ':');
            if (string.IsNullOrEmpty(replTitle)) {
                replTitle = SR.GetString(SR.CustomCommandReplTitle, DisplayLabelWithoutAccessKeys);
            } else {
                var match = _customCommandLabelRegex.Match(replTitle);
                if (match.Success) {
                    replTitle = LoadResourceFromAssembly(
                        match.Groups["assembly"].Value,
                        match.Groups["namespace"].Value,
                        match.Groups["key"].Value
                    );
                }
            }

            replTitle = PerformSubstitutions(project, replTitle);

            var replWindowId = PythonReplEvaluatorProvider.GetConfigurableReplId(ReplId + executeIn.Substring(4));
            
            var model = PythonToolsPackage.ComponentModel;
            var replProvider = model.GetService<IReplWindowProvider>();
            if (replProvider == null) {
                return false;
            }

            var replWindow = replProvider.FindReplWindow(replWindowId);
            bool created = replWindow == null;
            if (created) {
                replWindow = replProvider.CreateReplWindow(
                    PythonToolsPackage.Instance.ContentType,
                    replTitle,
                    typeof(PythonLanguageInfo).GUID,
                    replWindowId
                );
            }

            var replToolWindow = replWindow as ToolWindowPane;
            var replFrame = (replToolWindow != null) ? replToolWindow.Frame as IVsWindowFrame : null;

            var pyEvaluator = replWindow.Evaluator as PythonReplEvaluator;
            var options = (pyEvaluator != null) ? pyEvaluator.CurrentOptions as ConfigurablePythonReplOptions : null;
            if (options == null) {
                if (created && replFrame != null) {
                    // We created the window, but it isn't valid, so we'll close
                    // it again immediately.
                    replFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                }

                return false;
            }

            if (pyEvaluator.IsExecuting) {
                throw new InvalidOperationException(SR.GetString(SR.ErrorCommandAlreadyRunning));
            }

            options.InterpreterFactory = project.Interpreters.ActiveInterpreter;
            options.Project = project;
            options._workingDir = workingDir;
            if (environmentVars != null && environmentVars.Any()) {
                options._envVars = new Dictionary<string, string>(environmentVars);
            }

            project.AddAssociatedReplWindow(replWindow);
            var pane = replWindow as ToolWindowPane;
            var frame = pane != null ? pane.Frame as IVsWindowFrame : null;
            if (frame != null) {
                ErrorHandler.ThrowOnFailure(frame.Show());
            }

            var result = await pyEvaluator.Reset(quiet: true);

            if (result.IsSuccessful) {
                try {
                    if ("script".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                        pyEvaluator.Window.WriteLine(string.Format("Executing {0} {1}", Path.GetFileName(filename), arguments));
                        Debug.WriteLine("Executing {0} {1}", filename, arguments);
                        result = await pyEvaluator.ExecuteFile(filename, arguments);
                    } else if ("module".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                        pyEvaluator.Window.WriteLine(string.Format("Executing -m {0} {1}", filename, arguments));
                        Debug.WriteLine("Executing -m {0} {1}", filename, arguments);
                        result = await pyEvaluator.ExecuteModule(filename, arguments);
                    } else if ("code".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                        Debug.WriteLine("Executing -c \"{0}\"", filename, arguments);
                        result = await pyEvaluator.ExecuteText(filename);
                    } else {
                        pyEvaluator.Window.WriteLine(string.Format("Executing {0} {1}", Path.GetFileName(filename), arguments));
                        Debug.WriteLine("Executing {0} {1}", filename, arguments);
                        result = await pyEvaluator.ExecuteProcess(filename, arguments);
                    }

                    if (resetRepl) {
                        // We really close the backend, rather than resetting.
                        pyEvaluator.Close();
                    }
                } catch (Exception ex) {
                    ActivityLog.LogError(
                        SR.GetString(SR.PythonToolsForVisualStudio),
                        SR.GetString(SR.ErrorRunningCustomCommand, _label, ex)
                    );
                    var outWindow = OutputWindowRedirector.GetGeneral(project.Site);
                    if (outWindow != null) {
                        outWindow.WriteErrorLine(SR.GetString(SR.ErrorRunningCustomCommand, _label, ex));
                        outWindow.Show();
                    }
                }
                return true;
            }

            return false;
        }

        private static void RunInOutput(
            PythonProjectNode project,
            string filename,
            string arguments,
            string workingDir,
            Dictionary<string, string> environmentVars
        ) {
            var redirector = OutputWindowRedirector.GetGeneral(project.Site);
            redirector.ShowAndActivate();

            var process = ProcessOutput.Run(
                filename,
                new[] { arguments },
                workingDir,
                environmentVars,
                false,
                redirector,
                quoteArgs: false
            );
            process.Exited += (s, e) => process.Dispose();
        }

        private static void RunInConsole(
            PythonProjectNode project,
            string filename,
            string arguments,
            string workingDir,
            Dictionary<string, string> environmentVars
        ) {
            var process = ProcessOutput.Run(
                filename,
                new[] { arguments },
                workingDir,
                environmentVars,
                true,
                null,
                quoteArgs: false
            );
            process.Exited += (s, e) => process.Dispose();
        }
    }
}
