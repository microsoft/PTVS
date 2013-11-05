using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    class CustomCommand {
        internal readonly string _target;
        internal readonly string _label;

        public const string ReplId = "2E918F01-ABA9-41A6-8345-5189FB4B5ABF";

        private static readonly Regex _customCommandLabelRegex = new Regex(
            @"resource\:
                        (?<assembly>.+?);
                        (?<namespace>.+?);
                        (?<key>.+)
                    $",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace
        );

        public CustomCommand(PythonProjectNode project, string target, string label) {
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

            CanExecute = !string.IsNullOrEmpty(target);
        }

        private static string LoadResourceFromAssembly(string assembly, string ns, string key) {
            try {
                var asm = System.Reflection.Assembly.Load(assembly);
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
                    try {
                        t.Wait();
                    } catch (AggregateException ex) {
                        throw ex.InnerException;
                    }
                } catch (Exception ex) {
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

            if (!string.IsNullOrEmpty(executeIn) &&
                executeIn.StartsWith("repl", StringComparison.InvariantCultureIgnoreCase)) {
                if (await RunInRepl(project, filename, arguments, workingDir, environmentVars, targetType, executeIn)) {
                    return;
                }
            }

            if ("script".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format("{0} {1}", ProcessOutput.QuoteSingleArgument(filename), arguments);
                filename = project.GetInterpreterFactory().Configuration.InterpreterPath;
            } else if ("module".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                arguments = string.Format("-m {0} {1}", filename, arguments);
                filename = project.GetInterpreterFactory().Configuration.InterpreterPath;
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
                        result = await pyEvaluator.ExecuteFile(filename, arguments);
                    } else if ("module".Equals(targetType, StringComparison.InvariantCultureIgnoreCase)) {
                        result = await pyEvaluator.ExecuteModule(filename, arguments);
                    } else {
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
