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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.PythonTools.BuildTasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Navigation;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.InteractiveWindow;
#else
using Microsoft.VisualStudio.Repl;
#endif
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
#if DEV14_OR_LATER
    using IReplWindowProvider = InteractiveWindowProvider;
#endif
    sealed class CustomCommand : IAsyncCommand, IDisposable {
        private readonly PythonProjectNode _project;
        private readonly string _target;
        private readonly string _label;
        private readonly ErrorListProvider _errorListProvider;
        private bool _isDisposed, _canExecute;

        internal readonly string Verb;
        internal readonly uint AlternateCmdId;

        public const string ReplId = "2E918F01-ABA9-41A6-8345-5189FB4B5ABF";
        public const string PythonCommands = "PythonCommands";

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
            _project = project;
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

            _canExecute = !string.IsNullOrEmpty(target);
            Verb = "Project." + Regex.Replace(
                DisplayLabelWithoutAccessKeys,
                "[^a-z0-9]+",
                "",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
            );
            AlternateCmdId = AddNamedCommand(project.Site, Verb);

            _errorListProvider = new ErrorListProvider(_project.Site);
        }

        public void Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                if (!string.IsNullOrEmpty(Verb) && AlternateCmdId > 0) {
                    RemoveNamedCommand(ServiceProvider.GlobalProvider, Verb);
                }

                if (_errorListProvider != null) {
                    _errorListProvider.Dispose();
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
                0, new Guid[0]  // new [] { VSConstants.UICONTEXT.SolutionExists_guid }
            ));

            return cmdId;
        }

        private static void RemoveNamedCommand(IServiceProvider provider, string name) {
            var commands = provider.GetService(typeof(SVsProfferCommands)) as IVsProfferCommands3;
            if (commands != null) {
                ErrorHandler.ThrowOnFailure(commands.RemoveNamedCommand(name));
            }
        }

        private static string PerformSubstitutions(IPythonProject2 project, string label) {
            return Regex.Replace(label, @"\{(?<key>\w+)\}", m => {
                var key = m.Groups["key"].Value;
                if ("projectname".Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                    return Path.ChangeExtension(project.ProjectFile, null);
                } else if ("projectfile".Equals(key, StringComparison.InvariantCultureIgnoreCase)) {
                    return project.ProjectFile;
                }

                var instance = project.GetMSBuildProjectInstance();
                if (instance != null) {
                    var value = instance.GetPropertyValue(key);
                    if (!string.IsNullOrEmpty(value)) {
                        return value;
                    }
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
                return rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
            } catch (Exception ex) {
                ActivityLog.LogError(SR.ProductName, SR.GetString(SR.FailedToReadResource, assembly, ns, key, ex));
                return key;
            }
        }

        public static IEnumerable<CustomCommand> GetCommands(
            Microsoft.Build.Evaluation.Project project,
            PythonProjectNode projectNode
        ) {
            var commandNames = project.GetPropertyValue(PythonCommands);
            if (!string.IsNullOrEmpty(commandNames)) {
                foreach (var name in commandNames.Split(';').Where(n => !string.IsNullOrEmpty(n)).Distinct()) {
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
            IPythonProject2 projectNode
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

        public string Target { get { return _target; } }
        public string Label { get { return _label; } }
        public string DisplayLabel { get; private set; }

        public string DisplayLabelWithoutAccessKeys {
            get {
                // Changes "My &Command" into "My Command" while ensuring that
                // "C1 && C2" becomes "C1 & C2"
                return Regex.Replace(DisplayLabel, "&(.)", "$1");
            }
        }

        public bool CanExecute(object parameter) {
            if (!_canExecute) {
                return false;
            }

            return parameter == null ||
                parameter is IPythonProject2;
        }

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter) {
            var task = ExecuteAsync(parameter);
            var nestedFrame = new DispatcherFrame();
            task.ContinueWith(_ => nestedFrame.Continue = false);
            Dispatcher.PushFrame(nestedFrame);
            task.Wait();
        }

        public Task ExecuteAsync(object parameter) {
            var task = ExecuteWorker((parameter as PythonProjectNode) ?? _project);
            
            // Ensure the exception is observed.
            // The caller can check task.Exception to do their own reporting.
            task.ContinueWith(t => {
                try {
                    t.Wait();
                } catch (AggregateException ex) {
                    var exception = ex.InnerException;
                    if (exception is NoInterpretersException ||
                        exception is MissingInterpreterException ||
                        exception is TaskCanceledException) {
                        // No need to log this exception or disable the command.
                        return;
                    }

                    // Prevent the command from executing again until the project is
                    // reloaded.
                    _canExecute = false;
                    var evt = CanExecuteChanged;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }

                    // Log error to the ActivityLog.
                    ActivityLog.LogError(SR.ProductName, SR.GetString(SR.ErrorRunningCustomCommand, _target, ex));
                }
            });

            return task;
        }

        private class ErrorListRedirector : Redirector {
            private const string
                MessageGroupKey = "message",
                CodeGroupKey = "code",
                FileNameGroupKey = "filename",
                LineGroupKey = "line",
                ColumnGroupKey = "column";

            private readonly IVsHierarchy _hierarchy;
            private readonly string _workingDirectory;
            private readonly ErrorListProvider _errorListProvider;
            private readonly Regex _errorRegex, _warningRegex;
            private readonly IServiceProvider _serviceProvider;

            public ErrorListRedirector(IServiceProvider serviceProvider, IVsHierarchy hierarchy, string workingDirectory, ErrorListProvider errorListProvider, Regex errorRegex, Regex warningRegex) {
                _serviceProvider = serviceProvider;
                _hierarchy = hierarchy;
                _workingDirectory = workingDirectory;
                _errorListProvider = errorListProvider;
                _errorRegex = errorRegex;
                _warningRegex = warningRegex;
            }

            public override void WriteErrorLine(string s) {
                WriteLine(s);
            }

            public override void WriteLine(string s) {
                var errorCategory = TaskErrorCategory.Error;
                foreach (var regex in new[] { _errorRegex, _warningRegex }) {
                    if (regex != null) {
                        var m = regex.Match(s);
                        if (m.Success) {
                            int line, column;
                            int.TryParse(m.Groups[LineGroupKey].ToString(), out line);
                            int.TryParse(m.Groups[ColumnGroupKey].ToString(), out column);
                            string document = m.Groups[FileNameGroupKey].ToString();

                            var task = new ErrorTask {
                                Document = document,
                                HierarchyItem = _hierarchy,
                                Line = line - 1,
                                Column = column - 1,
                                ErrorCategory = errorCategory,
                                Text = m.Groups[MessageGroupKey].ToString()
                            };
                            task.Navigate += OnNavigate;
                            _errorListProvider.Tasks.Add(task);
                        }
                    }

                    errorCategory = TaskErrorCategory.Warning;
                }
            }

            public override void Show() {
                _errorListProvider.Show();
            }

            public override void ShowAndActivate() {
                _errorListProvider.Show();
                _errorListProvider.BringToFront();
            }

            private void OnNavigate(object sender, EventArgs e) {
                var task = sender as ErrorTask;
                if (task != null) {
                    string document;
                    try {
                        document = CommonUtils.GetAbsoluteFilePath(_workingDirectory, task.Document);
                    } catch (ArgumentException) {
                        // If it's not a valid path, then it's not a navigable error item.
                        return;
                    }
                    PythonToolsPackage.NavigateTo(_serviceProvider, document, Guid.Empty, task.Line, task.Column < 0 ? 0 : task.Column);
                }
            }
        }

        private async Task ExecuteWorker(PythonProjectNode project) {
            _errorListProvider.Tasks.Clear();

            var interpFactory = project.GetInterpreterFactoryOrThrow();
            var startInfo = GetStartInfo(project);

            var packagesToInstall = new List<string>();
            foreach (var pkg in startInfo.RequiredPackages) {
                if (!await Pip.IsInstalled(interpFactory, pkg)) {
                    packagesToInstall.Add(pkg);
                }
            }

            if (packagesToInstall.Any()) {
                var installMissingButton = new TaskDialogButton(
                    SR.GetString(SR.CustomCommandPrerequisitesInstallMissing),
                    SR.GetString(SR.CustomCommandPrerequisitesInstallMissingSubtext) + "\r\n\r\n" + string.Join("\r\n", packagesToInstall));
                var runAnywayButton = new TaskDialogButton(SR.GetString(SR.CustomCommandPrerequisitesRunAnyway));
                var doNotRunButton = new TaskDialogButton(SR.GetString(SR.CustomCommandPrerequisitesDoNotRun));

                var taskDialog = new TaskDialog(project.Site) {
                    Title = SR.ProductName,
                    MainInstruction = SR.GetString(SR.CustomCommandPrerequisitesInstruction),
                    Content = SR.GetString(SR.CustomCommandPrerequisitesContent, DisplayLabelWithoutAccessKeys),
                    AllowCancellation = true,
                    Buttons = { installMissingButton, runAnywayButton, doNotRunButton, TaskDialogButton.Cancel }
                };

                var selectedButton = taskDialog.ShowModal();
                if (selectedButton == installMissingButton) {
                    await Pip.Install(
                        project.Site,
                        interpFactory,
                        string.Join(" ", packagesToInstall),
                        false,
                        OutputWindowRedirector.GetGeneral(project.Site));
                } else if (selectedButton == runAnywayButton) {
                } else {
                    return;
                }
            }

            if (startInfo.TargetType == CreatePythonCommandItem.TargetTypePip) {
                if (startInfo.ExecuteInOutput) {
                    await Pip.Install(
                        _project.Site,
                        interpFactory,
                        string.Format("{0} {1}", startInfo.Filename, startInfo.Arguments),
                        project.Site,
                        false,
                        OutputWindowRedirector.GetGeneral(project.Site)
                    );
                    return;
                }

                // Rewrite start info to execute 
                startInfo.TargetType = CreatePythonCommandItem.TargetTypeModule;
                startInfo.AddArgumentAtStart(startInfo.Filename);
                startInfo.Filename = "pip";
            }

            if (startInfo.ExecuteInRepl) {
                if (await RunInRepl(project, startInfo)) {
                    return;
                }
            }

            startInfo.AdjustArgumentsForProcessStartInfo(GetInterpreterPath(project, false));

            if (startInfo.ExecuteInOutput) {
                RunInOutput(project, startInfo);
            } else {
                RunInConsole(project, startInfo);
            }
        }

#if DEBUG
        class TraceLogger : Microsoft.Build.Logging.ConsoleLogger {
            public TraceLogger()
                : base(Build.Framework.LoggerVerbosity.Detailed) {
                WriteHandler = s => Debug.Write(s);
            }
        }
#endif

        class StringLogger : Microsoft.Build.Logging.ConsoleLogger {
            public readonly List<string> Lines = new List<string>();

            public StringLogger()
                : base(Build.Framework.LoggerVerbosity.Normal) {
                WriteHandler = Lines.Add;
            }
        }

        private static IDictionary<string, TargetResult> BuildTarget(IPythonProject2 project, string target) {
            var config = project.GetMSBuildProjectInstance();
            if (config == null) {
                throw new ArgumentException("Project does not support MSBuild", "project");
            }

            IDictionary<string, TargetResult> outputs;

            var logger = new StringLogger();
#if DEBUG
            var loggers = new ILogger[] { new TraceLogger(), logger };
#else
            var loggers = new ILogger[] { logger };
#endif

            if (!config.Build(new[] { target }, loggers, Enumerable.Empty<ForwardingLoggerRecord>(), out outputs)) {
                var outputWindow = OutputWindowRedirector.Get(
                    project.Site,
                    VSConstants.OutputWindowPaneGuid.BuildOutputPane_guid,
                    "Build"
                );
                outputWindow.WriteErrorLine(SR.GetString(SR.ErrorBuildingCustomCommand, target));
                foreach (var line in logger.Lines) {
                    outputWindow.WriteErrorLine(line.TrimEnd('\r', '\n'));
                }
                throw new InvalidOperationException(SR.GetString(SR.ErrorBuildingCustomCommand, target));
            }

            return outputs;
        }

        public CommandStartInfo GetStartInfo(IPythonProject2 project) {
            var outputs = BuildTarget(project, _target);
            var config = PythonProjectLaunchProperties.Create(project);

            var item = outputs.Values
                .SelectMany(result => result.Items)
                .FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.ItemSpec) &&
                    !string.IsNullOrEmpty(i.GetMetadata(BuildTasks.CreatePythonCommandItem.TargetTypeKey))
                );

            if (item == null) {
                throw new InvalidOperationException(SR.GetString(SR.ErrorBuildingCustomCommand, _target));
            }

            var startInfo = new CommandStartInfo {
                Filename = item.ItemSpec,
                Arguments = item.GetMetadata(BuildTasks.CreatePythonCommandItem.ArgumentsKey),
                WorkingDirectory = item.GetMetadata(BuildTasks.CreatePythonCommandItem.WorkingDirectoryKey),
                EnvironmentVariables = PythonProjectLaunchProperties.ParseEnvironment(item.GetMetadata(BuildTasks.CreatePythonCommandItem.EnvironmentKey)),
                TargetType = item.GetMetadata(BuildTasks.CreatePythonCommandItem.TargetTypeKey),
                ExecuteIn = item.GetMetadata(BuildTasks.CreatePythonCommandItem.ExecuteInKey),
                RequiredPackages = item.GetMetadata(BuildTasks.CreatePythonCommandItem.RequiredPackagesKey).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            };

            try {
                startInfo.WorkingDirectory = CommonUtils.GetAbsoluteFilePath(project.ProjectHome, startInfo.WorkingDirectory);
            } catch (ArgumentException) {
            }

            string errorRegex = item.GetMetadata(BuildTasks.CreatePythonCommandItem.ErrorRegexKey);
            if (!string.IsNullOrEmpty(errorRegex)) {
                startInfo.ErrorRegex = new Regex(errorRegex);
            }

            string warningRegex = item.GetMetadata(BuildTasks.CreatePythonCommandItem.WarningRegexKey);
            if (!string.IsNullOrEmpty(warningRegex)) {
                startInfo.WarningRegex = new Regex(warningRegex);
            }

            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            startInfo.AddPropertiesAfter(PythonProjectLaunchProperties.Create(project));

            Debug.Assert(!string.IsNullOrEmpty(startInfo.WorkingDirectory));
            Debug.Assert(Path.IsPathRooted(startInfo.WorkingDirectory));

            return startInfo;
        }

        internal static string GetInterpreterPath(PythonProjectNode project, bool isWindows) {
            var factory = project.GetInterpreterFactoryOrThrow();

            return isWindows ?
                factory.Configuration.WindowsInterpreterPath :
                factory.Configuration.InterpreterPath;
        }

        private async Task<bool> RunInRepl(IPythonProject2 project, CommandStartInfo startInfo) {
            var executeIn = string.IsNullOrEmpty(startInfo.ExecuteIn) ? CreatePythonCommandItem.ExecuteInRepl : startInfo.ExecuteIn;
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
            
            var model = _project.Site.GetComponentModel();
            var replProvider = model.GetService<IReplWindowProvider>();
            if (replProvider == null) {
                return false;
            }

            var replWindow = replProvider.FindReplWindow(replWindowId);
            bool created = replWindow == null;
            if (created) {
                replWindow = replProvider.CreateReplWindow(
                    _project.Site.GetPythonContentType(),
                    replTitle,
                    typeof(PythonLanguageInfo).GUID,
                    replWindowId
                );
            }

            var replToolWindow = replWindow as ToolWindowPane;
            var replFrame = (replToolWindow != null) ? replToolWindow.Frame as IVsWindowFrame : null;

#if DEV14_OR_LATER
            var pyEvaluator = replWindow.InteractiveWindow.Evaluator as PythonReplEvaluator;
#else
            var pyEvaluator = replWindow.Evaluator as PythonReplEvaluator;
#endif
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

            var ipp3 = project as IPythonProject3;
            if (ipp3 != null) {
                options.InterpreterFactory = ipp3.GetInterpreterFactoryOrThrow();
            } else {
                options.InterpreterFactory = project.GetInterpreterFactory();
            }
            options.Project = project as PythonProjectNode;
            options._workingDir = startInfo.WorkingDirectory;
            options._envVars = startInfo.EnvironmentVariables;

            project.AddActionOnClose((object)replWindow, BasePythonReplEvaluator.CloseReplWindow);

            var pane = replWindow as ToolWindowPane;
            var frame = pane != null ? pane.Frame as IVsWindowFrame : null;
            if (frame != null) {
                ErrorHandler.ThrowOnFailure(frame.Show());
            }

            var result = await pyEvaluator.Reset(quiet: true);

            if (result.IsSuccessful) {
                try {
                    var filename = startInfo.Filename;
                    var arguments = startInfo.Arguments;

                    if (startInfo.IsScript) {
                        pyEvaluator.Window.WriteLine(string.Format("Executing {0} {1}", Path.GetFileName(filename), arguments));
                        Debug.WriteLine("Executing {0} {1}", filename, arguments);
                        result = await pyEvaluator.ExecuteFile(filename, arguments);
                    } else if (startInfo.IsModule) {
                        pyEvaluator.Window.WriteLine(string.Format("Executing -m {0} {1}", filename, arguments));
                        Debug.WriteLine("Executing -m {0} {1}", filename, arguments);
                        result = await pyEvaluator.ExecuteModule(filename, arguments);
                    } else if (startInfo.IsCode) {
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
                    ActivityLog.LogError(SR.ProductName, SR.GetString(SR.ErrorRunningCustomCommand, _label, ex));
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

        private async void RunInOutput(IPythonProject2 project, CommandStartInfo startInfo) {
            Redirector redirector = OutputWindowRedirector.GetGeneral(project.Site);
            if (startInfo.ErrorRegex != null || startInfo.WarningRegex != null) {
                redirector = new TeeRedirector(redirector, new ErrorListRedirector(_project.Site, project as IVsHierarchy, startInfo.WorkingDirectory, _errorListProvider, startInfo.ErrorRegex, startInfo.WarningRegex));
            }
            redirector.ShowAndActivate();

            using (var process = ProcessOutput.Run(
                startInfo.Filename,
                new[] { startInfo.Arguments },
                startInfo.WorkingDirectory,
                startInfo.EnvironmentVariables,
                false,
                redirector,
                quoteArgs: false
            )) {
                await process;
            }
        }

        private async void RunInConsole(IPythonProject2 project, CommandStartInfo startInfo) {
            using (var process = ProcessOutput.Run(
                startInfo.Filename,
                new[] { startInfo.Arguments },
                startInfo.WorkingDirectory,
                startInfo.EnvironmentVariables,
                true,
                null,
                quoteArgs: false
            )) {
                await process;
            }
        }
    }

    class CommandStartInfo : IProjectLaunchProperties {
        public string Filename;
        public string Arguments;
        public string WorkingDirectory;
        public IDictionary<string, string> EnvironmentVariables;
        public string ExecuteIn;
        public string TargetType;
        public Regex ErrorRegex, WarningRegex;
        public string[] RequiredPackages;

        public void AddArgumentAtStart(string argument) {
            Arguments = ProcessOutput.QuoteSingleArgument(argument) + " " + Arguments;
        }

        public void AddArgumentAtEnd(string argument) {
            Arguments += " " + ProcessOutput.QuoteSingleArgument(argument);
        }

        public bool ExecuteInRepl {
            get {
                return !string.IsNullOrEmpty(ExecuteIn) &&
                    ExecuteIn.StartsWith(CreatePythonCommandItem.ExecuteInRepl, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool ExecuteInOutput {
            get {
                return CreatePythonCommandItem.ExecuteInOutput.Equals(ExecuteIn, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool ExecuteInConsole {
            get {
                return CreatePythonCommandItem.ExecuteInConsole.Equals(ExecuteIn, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool ExecuteInConsoleAndPause {
            get {
                return CreatePythonCommandItem.ExecuteInConsolePause.Equals(ExecuteIn, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool ExecuteHidden {
            get {
                return  CreatePythonCommandItem.ExecuteInNone.Equals(ExecuteIn, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsScript {
            get {
                return CreatePythonCommandItem.TargetTypeScript.Equals(TargetType, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsModule {
            get {
                return CreatePythonCommandItem.TargetTypeModule.Equals(TargetType, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsCode {
            get {
                return CreatePythonCommandItem.TargetTypeCode.Equals(TargetType, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsExecuable {
            get {
                return CreatePythonCommandItem.TargetTypeExecutable.Equals(TargetType, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public bool IsPip {
            get {
                return CreatePythonCommandItem.TargetTypePip.Equals(TargetType, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Adjusts the options in this instance to be easily executed as an
        /// external process.
        /// 
        /// After this method returns, the following changes have been made:
        /// <list type="unordered">
        /// <item><see cref="Filename"/> is now an executable file.</item>
        /// <item><see cref="TargetType"/> is now <c>process</c>.</item>
        /// <item><see cref="ExecuteIn"/> is now either <c>console</c> or
        /// <c>output</c>.</item>
        /// </list>
        /// </summary>
        /// <param name="interpreterPath">Full path to the interpreter.</param>
        /// <param name="handleConsoleAndPause">
        /// If this and <see cref="ExecuteInConsoleAndPause"/> are true, changes
        /// <see cref="ExecuteIn"/> to <c>console</c> and updates 
        /// <see cref="Filename"/> and <see cref="Arguments"/> to handle the
        /// pause.
        /// </param>
        public void AdjustArgumentsForProcessStartInfo(
            string interpreterPath,
            bool handleConsoleAndPause = true,
            bool inheritGlobalEnvironmentVariables = true
        ) {
            if (inheritGlobalEnvironmentVariables) {
                var env = new Dictionary<string, string>();
                var globalEnv = Environment.GetEnvironmentVariables();
                foreach (var key in globalEnv.Keys) {
                    env[key.ToString()] = globalEnv[key].ToString();
                }

                if (EnvironmentVariables != null) {
                    foreach (var entry in EnvironmentVariables) {
                        env[entry.Key] = entry.Value;
                    }
                }

                EnvironmentVariables = env;
            }

            if (IsScript) {
                AddArgumentAtStart(Filename);
                Filename = interpreterPath;
            } else if (IsModule) {
                AddArgumentAtStart(Filename);
                AddArgumentAtStart("-m");
                Filename = interpreterPath;
            } else if (IsCode) {
                AddArgumentAtStart(Filename.Replace("\r\n", "\n"));
                AddArgumentAtStart("-c");
                Filename = interpreterPath;
            }
            TargetType = CreatePythonCommandItem.TargetTypeExecutable;

            if (ExecuteInRepl) {
                ExecuteIn = CreatePythonCommandItem.ExecuteInOutput;
            } else if (ExecuteInConsole) {
                if (handleConsoleAndPause) {
                    Arguments = string.Format(
                        "/C \"{0} {1}\" & if errorlevel 1 pause",
                        ProcessOutput.QuoteSingleArgument(Filename),
                        Arguments
                    );
                    Filename = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    ExecuteIn = CreatePythonCommandItem.ExecuteInConsole;
                }
            } else if (ExecuteInConsoleAndPause) {
                if (handleConsoleAndPause) {
                    Arguments = string.Format(
                        "/C \"{0} {1}\" & pause",
                        ProcessOutput.QuoteSingleArgument(Filename),
                        Arguments
                    );
                    Filename = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    ExecuteIn = CreatePythonCommandItem.ExecuteInConsole;
                }
            }

            if (EnvironmentVariables != null) {
                Arguments = Regex.Replace(Arguments, @"%(\w+)%", m => {
                    string envVar;
                    return EnvironmentVariables.TryGetValue(m.Groups[1].Value, out envVar) ? envVar : string.Empty;
                });
            }
        }

        string IProjectLaunchProperties.GetArguments() {
            return Arguments;
        }

        string IProjectLaunchProperties.GetWorkingDirectory() {
            return WorkingDirectory;
        }

        IDictionary<string, string> IProjectLaunchProperties.GetEnvironment(bool includeSearchPaths) {
            return EnvironmentVariables;
        }

        internal void AddPropertiesAfter(IProjectLaunchProperties projectLaunchProperties) {
            // Fill PYTHONPATH from interpreter settings before we load values from Environment metadata item,
            // so that commands can explicitly override it if they want to.
            var props = PythonProjectLaunchProperties.Merge(this, projectLaunchProperties);
            Arguments = props.GetArguments();
            WorkingDirectory = props.GetWorkingDirectory();
            EnvironmentVariables = props.GetEnvironment(true);
        }
    }
}
