// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.PythonTools.BuildTasks;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
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
            var cmdSet = CommonGuidList.guidPythonToolsCmdSet;
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

        private static string PerformSubstitutions(IPythonProject project, string label) {
            return Regex.Replace(label, @"\{(?<key>\w+)\}", m => {
                var key = m.Groups["key"].Value;
                if ("projectname".Equals(key, StringComparison.OrdinalIgnoreCase)) {
                    return Path.ChangeExtension(project.ProjectFile, null);
                } else if ("projectfile".Equals(key, StringComparison.OrdinalIgnoreCase)) {
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
                ActivityLog.LogError(Strings.ProductTitle, Strings.FailedToReadResource.FormatUI(assembly, ns, key, ex));
                return key;
            }
        }

        public static IEnumerable<CustomCommand> GetCommands(
            Microsoft.Build.Evaluation.Project project,
            PythonProjectNode projectNode
        ) {
            var commandNames = project.GetPropertyValue(PythonCommands);
            if (!string.IsNullOrEmpty(commandNames)) {
                foreach (var name in commandNames.Split(';').Select(s => s.Trim()).Where(n => !string.IsNullOrEmpty(n)).Distinct()) {
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
            IPythonProject projectNode
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
                return Strings.PythonMenuLabel;
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
                parameter is IPythonProject;
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
                    ActivityLog.LogError(Strings.ProductTitle, Strings.ErrorRunningCustomCommand.FormatUI(_target, ex));
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
            private readonly Regex _errorRegex, _warningRegex, _messageRegex;
            private readonly IServiceProvider _serviceProvider;

            public ErrorListRedirector(IServiceProvider serviceProvider, IVsHierarchy hierarchy, string workingDirectory, ErrorListProvider errorListProvider, Regex errorRegex, Regex warningRegex, Regex messageRegex) {
                _serviceProvider = serviceProvider;
                _hierarchy = hierarchy;
                _workingDirectory = workingDirectory;
                _errorListProvider = errorListProvider;
                _errorRegex = errorRegex;
                _warningRegex = warningRegex;
                _messageRegex = messageRegex;
            }

            public override void WriteErrorLine(string s) {
                WriteLine(s);
            }

            public override void WriteLine(string s) {
                var errorCategory = TaskErrorCategory.Error;
                foreach (var regex in new[] { _errorRegex, _warningRegex, _messageRegex }) {
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

                    if (errorCategory == TaskErrorCategory.Error) {
                        errorCategory = TaskErrorCategory.Warning;
                    } else {
                        errorCategory = TaskErrorCategory.Message;
                    }

                }
            }

            public override void Show() {
                try {
                    _errorListProvider.Show();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }
            }

            public override void ShowAndActivate() {
                try {
                    _errorListProvider.Show();
                    _errorListProvider.BringToFront();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
                }
            }

            private void OnNavigate(object sender, EventArgs e) {
                var task = sender as ErrorTask;
                if (task != null) {
                    string document;
                    try {
                        document = PathUtils.GetAbsoluteFilePath(_workingDirectory, task.Document);
                    } catch (ArgumentException) {
                        // If it's not a valid path, then it's not a navigable error item.
                        return;
                    }
                    try {
                        PythonToolsPackage.NavigateTo(_serviceProvider, document, Guid.Empty, task.Line, task.Column < 0 ? 0 : task.Column);
                    } catch (FileNotFoundException ex) {
                        // Happens when file was deleted from the project.
                        MessageBox.Show(ex.Message, Strings.ProductTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                    } catch (ArgumentException) {
                        // Happens when file was deleted from disk but not from project.
                        // A descriptive message was already shown to the user, so don't show another.
                    }
                }
            }
        }

        private async Task ExecuteWorker(PythonProjectNode project) {
            _errorListProvider.Tasks.Clear();

            var interpFactory = project.GetInterpreterFactoryOrThrow();
            var startInfo = GetStartInfo(project);

            var packagesToInstall = new List<string>();
            var interpreterOpts = _project.Site.GetComponentModel().GetService<IInterpreterOptionsService>();
            var pm = interpreterOpts?.GetPackageManagers(interpFactory).FirstOrDefault();
            if (pm != null) {
                foreach (var pkg in startInfo.RequiredPackages) {
                    if (!(await pm.GetInstalledPackageAsync(PackageSpec.FromRequirement(pkg), CancellationToken.None)).IsValid) {
                        packagesToInstall.Add(pkg);
                    }
                }
            }

            if (packagesToInstall.Any()) {
                var installMissingButton = new TaskDialogButton(
                    Strings.CustomCommandPrerequisitesInstallMissing,
                    Strings.CustomCommandPrerequisitesInstallMissingSubtext + "\r\n\r\n" + string.Join("\r\n", packagesToInstall));
                var runAnywayButton = new TaskDialogButton(Strings.CustomCommandPrerequisitesRunAnyway);
                var doNotRunButton = new TaskDialogButton(Strings.CustomCommandPrerequisitesDoNotRun);

                var taskDialog = new TaskDialog(project.Site) {
                    Title = Strings.ProductTitle,
                    MainInstruction = Strings.CustomCommandPrerequisitesInstruction,
                    Content = Strings.CustomCommandPrerequisitesContent.FormatUI(DisplayLabelWithoutAccessKeys),
                    AllowCancellation = true,
                    Buttons = { installMissingButton, runAnywayButton, doNotRunButton, TaskDialogButton.Cancel }
                };

                var selectedButton = taskDialog.ShowModal();
                if (selectedButton == installMissingButton) {
                    var ui = new VsPackageManagerUI(project.Site);
                    if (!pm.IsReady) {
                        await pm.PrepareAsync(ui, CancellationToken.None);
                    }
                    await pm.InstallAsync(PackageSpec.FromArguments(string.Join(" ", packagesToInstall)), ui, CancellationToken.None);
                } else if (selectedButton == runAnywayButton) {
                } else {
                    throw new TaskCanceledException();
                }
            }

            if (startInfo.TargetType == PythonCommandTask.TargetTypePip) {
                if (startInfo.ExecuteInOutput && pm != null) {
                    var ui = new VsPackageManagerUI(project.Site);
                    if (!pm.IsReady) {
                        await pm.PrepareAsync(ui, CancellationToken.None);
                    }
                    await pm.InstallAsync(
                        PackageSpec.FromArguments(string.IsNullOrEmpty(startInfo.Arguments) ? startInfo.Filename : "{0} {1}".FormatUI(startInfo.Filename, startInfo.Arguments)),
                        ui,
                        CancellationToken.None
                    );
                    return;
                }

                // Rewrite start info to execute 
                startInfo.TargetType = PythonCommandTask.TargetTypeModule;
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

        internal static IDictionary<string, TargetResult> BuildTarget(IPythonProject project, string target) {
            var config = project.GetMSBuildProjectInstance();
            if (config == null) {
                throw new ArgumentException(Strings.ProjectDoesNotSupportedMSBuild, nameof(project));
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
                outputWindow.WriteErrorLine(Strings.ErrorBuildingCustomCommand.FormatUI(target));
                foreach (var line in logger.Lines) {
                    outputWindow.WriteErrorLine(line.TrimEnd('\r', '\n'));
                }
                throw new InvalidOperationException(Strings.ErrorBuildingCustomCommand.FormatUI(target));
            }

            return outputs;
        }

        public CommandStartInfo GetStartInfo(IPythonProject project) {
            var outputs = BuildTarget(project, _target);
            var config = project.GetLaunchConfigurationOrThrow();

            var item = outputs.Values
                .SelectMany(result => result.Items)
                .FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.ItemSpec) &&
                    !string.IsNullOrEmpty(i.GetMetadata(CreatePythonCommandItem.TargetTypeKey))
                );

            if (item == null) {
                throw new InvalidOperationException(Strings.ErrorBuildingCustomCommand.FormatUI(_target));
            }

            var startInfo = new CommandStartInfo(config.Interpreter) {
                Filename = item.ItemSpec,
                Arguments = item.GetMetadata(CreatePythonCommandItem.ArgumentsKey),
                WorkingDirectory = item.GetMetadata(CreatePythonCommandItem.WorkingDirectoryKey),
                TargetType = item.GetMetadata(CreatePythonCommandItem.TargetTypeKey),
                ExecuteIn = item.GetMetadata(CreatePythonCommandItem.ExecuteInKey),
                RequiredPackages = item.GetMetadata(CreatePythonCommandItem.RequiredPackagesKey).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            };

            var pathVar = config.Interpreter.PathEnvironmentVariable;
            var env = new Dictionary<string, string> {
                { pathVar, PathUtils.JoinPathList(config.SearchPaths) }
            };
            startInfo.EnvironmentVariables = PathUtils.MergeEnvironments(
                PathUtils.MergeEnvironments(env, config.Environment, pathVar),
                PathUtils.ParseEnvironment(item.GetMetadata(CreatePythonCommandItem.EnvironmentKey)),
                "Path", config.Interpreter.PathEnvironmentVariable
            );

            try {
                startInfo.WorkingDirectory = PathUtils.GetAbsoluteFilePath(project.ProjectHome, startInfo.WorkingDirectory);
            } catch (ArgumentException) {
            }

            string errorRegex = item.GetMetadata(CreatePythonCommandItem.ErrorRegexKey);
            if (!string.IsNullOrEmpty(errorRegex)) {
                startInfo.ErrorRegex = new Regex(errorRegex);
            }

            string warningRegex = item.GetMetadata(CreatePythonCommandItem.WarningRegexKey);
            if (!string.IsNullOrEmpty(warningRegex)) {
                startInfo.WarningRegex = new Regex(warningRegex);
            }

            string messageRegex = item.GetMetadata(CreatePythonCommandItem.MessageRegexKey);
            if (!string.IsNullOrEmpty(messageRegex)) {
                startInfo.MessageRegex = new Regex(messageRegex);
            }

            startInfo.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";

            Debug.Assert(!string.IsNullOrEmpty(startInfo.WorkingDirectory));
            Debug.Assert(Path.IsPathRooted(startInfo.WorkingDirectory));

            return startInfo;
        }

        internal static string GetInterpreterPath(PythonProjectNode project, bool isWindows) {
            var factory = project.GetInterpreterFactoryOrThrow();

            return isWindows ?
                factory.Configuration.GetWindowsInterpreterPath() :
                factory.Configuration.InterpreterPath;
        }

        private async Task<bool> RunInRepl(IPythonProject project, CommandStartInfo startInfo) {
            var executeIn = string.IsNullOrEmpty(startInfo.ExecuteIn) ? CreatePythonCommandItem.ExecuteInRepl : startInfo.ExecuteIn;
            bool resetRepl = executeIn.StartsWithOrdinal("R");

            var replTitle = executeIn.Substring(4).TrimStart(' ', ':');
            if (string.IsNullOrEmpty(replTitle)) {
                replTitle = Strings.CustomCommandReplTitle.FormatUI(DisplayLabelWithoutAccessKeys);
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

            var replWindowId = PythonReplEvaluatorProvider.GetTemporaryId(
                ReplId + executeIn.Substring(4),
                _project.GetInterpreterFactory().Configuration
            );

            var model = _project.Site.GetComponentModel();
            var replProvider = model.GetService<InteractiveWindowProvider>();
            if (replProvider == null) {
                return false;
            }

            bool created;
            var replWindow = replProvider.OpenOrCreateTemporary(replWindowId, replTitle, out created);

            // TODO: Find alternative way of closing repl window on Dev15
            var replFrame = (replWindow as ToolWindowPane)?.Frame as IVsWindowFrame;

            var interactive = replWindow.InteractiveWindow;
            var pyEvaluator = replWindow.InteractiveWindow.Evaluator as PythonInteractiveEvaluator;
            if (pyEvaluator == null) {
                if (created && replFrame != null) {
                    // We created the window, but it isn't valid, so we'll close
                    // it again immediately.
                    replFrame.CloseFrame((uint)__FRAMECLOSE.FRAMECLOSE_NoSave);
                }

                return false;
            }

            if (pyEvaluator.IsExecuting) {
                throw new InvalidOperationException(Strings.ErrorCommandAlreadyRunning);
            }

            pyEvaluator.ProjectMoniker = _project.GetMkDocument();
            pyEvaluator.Configuration = new LaunchConfiguration(startInfo.Interpreter) {
                WorkingDirectory = startInfo.WorkingDirectory,
                Environment = startInfo.EnvironmentVariables.ToDictionary(kv => kv.Key, kv => kv.Value)
            };
            pyEvaluator.Configuration.LaunchOptions[PythonInteractiveEvaluator.DoNotResetConfigurationLaunchOption] = "true";

            project.AddActionOnClose((object)replWindow, InteractiveWindowProvider.Close);

            replWindow.Show(true);

            var result = await pyEvaluator.ResetAsync(false, quiet: true);

            if (result.IsSuccessful) {
                try {
                    var filename = startInfo.Filename;
                    var arguments = startInfo.Arguments ?? string.Empty;

                    if (startInfo.IsScript) {
                        interactive.WriteLine(Strings.CustomCommandExecutingScript.FormatUI(Path.GetFileName(filename), arguments));
                        Debug.WriteLine("Executing {0} {1}", filename, arguments);
                        await pyEvaluator.ExecuteFileAsync(filename, arguments);
                    } else if (startInfo.IsModule) {
                        interactive.WriteLine(Strings.CustomCommandExecutingModule.FormatUI(filename, arguments));
                        Debug.WriteLine("Executing -m {0} {1}", filename, arguments);
                        await pyEvaluator.ExecuteModuleAsync(filename, arguments);
                    } else if (startInfo.IsCode) {
                        Debug.WriteLine("Executing -c \"{0}\"", filename, arguments);
                        await pyEvaluator.ExecuteCodeAsync(filename);
                    } else {
                        interactive.WriteLine(Strings.CustomCommandExecutingOther.FormatUI(Path.GetFileName(filename), arguments));
                        Debug.WriteLine("Executing {0} {1}", filename, arguments);
                        await pyEvaluator.ExecuteProcessAsync(filename, arguments);
                    }

                    if (resetRepl) {
                        // We really close the backend, rather than resetting.
                        pyEvaluator.Dispose();
                    }
                } catch (OperationCanceledException) {
                    // Swallow OperationCanceledException, it is normal for async operation to be cancelled
                    ActivityLog.LogInformation(Strings.ProductTitle, Strings.CustomCommandCanceled.FormatUI(_label));
                } catch (Exception ex) {
                    ActivityLog.LogError(Strings.ProductTitle, Strings.ErrorRunningCustomCommand.FormatUI(_label, ex));
                    var outWindow = OutputWindowRedirector.GetGeneral(project.Site);
                    if (outWindow != null) {
                        outWindow.WriteErrorLine(Strings.ErrorRunningCustomCommand.FormatUI(_label, ex));
                        outWindow.Show();
                    }
                }
                return true;
            }

            return false;
        }

        private async void RunInOutput(IPythonProject project, CommandStartInfo startInfo) {
            Redirector redirector = OutputWindowRedirector.GetGeneral(project.Site);
            if (startInfo.ErrorRegex != null || startInfo.WarningRegex != null || startInfo.MessageRegex != null) {
                redirector = new TeeRedirector(redirector, new ErrorListRedirector(_project.Site, project as IVsHierarchy, startInfo.WorkingDirectory, _errorListProvider, startInfo.ErrorRegex, startInfo.WarningRegex, startInfo.MessageRegex));
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

        private async void RunInConsole(IPythonProject project, CommandStartInfo startInfo) {
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

    class CommandStartInfo {
        public readonly InterpreterConfiguration Interpreter;
        public string Filename;
        public string Arguments;
        public string WorkingDirectory;
        public IDictionary<string, string> EnvironmentVariables;
        public string ExecuteIn;
        public string TargetType;
        public Regex ErrorRegex, WarningRegex, MessageRegex;
        public string[] RequiredPackages;

        public CommandStartInfo(InterpreterConfiguration interpreter) {
            Interpreter = interpreter;
        }

        public void AddArgumentAtStart(string argument) {
            if (string.IsNullOrEmpty(Arguments)) {
                Arguments = ProcessOutput.QuoteSingleArgument(argument);
            } else {
                Arguments = ProcessOutput.QuoteSingleArgument(argument) + " " + Arguments;
            }
        }

        public void AddArgumentAtEnd(string argument) {
            if (string.IsNullOrEmpty(Arguments)) {
                Arguments = ProcessOutput.QuoteSingleArgument(argument);
            } else {
                Arguments += " " + ProcessOutput.QuoteSingleArgument(argument);
            }
        }

        public bool ExecuteInRepl {
            get {
                return !string.IsNullOrEmpty(ExecuteIn) &&
                    ExecuteIn.StartsWithOrdinal(CreatePythonCommandItem.ExecuteInRepl, ignoreCase: true);
            }
        }

        public bool ExecuteInOutput {
            get {
                return CreatePythonCommandItem.ExecuteInOutput.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ExecuteInConsole {
            get {
                return PythonCommandTask.ExecuteInConsole.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ExecuteInConsoleAndPause {
            get {
                return PythonCommandTask.ExecuteInConsolePause.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ExecuteHidden {
            get {
                return PythonCommandTask.ExecuteInNone.Equals(ExecuteIn, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsScript {
            get {
                return PythonCommandTask.TargetTypeScript.Equals(TargetType, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsModule {
            get {
                return PythonCommandTask.TargetTypeModule.Equals(TargetType, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsCode {
            get {
                return PythonCommandTask.TargetTypeCode.Equals(TargetType, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsExecuable {
            get {
                return PythonCommandTask.TargetTypeExecutable.Equals(TargetType, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsPip {
            get {
                return PythonCommandTask.TargetTypePip.Equals(TargetType, StringComparison.OrdinalIgnoreCase);
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
            TargetType = PythonCommandTask.TargetTypeExecutable;

            if (ExecuteInRepl) {
                ExecuteIn = CreatePythonCommandItem.ExecuteInOutput;
            } else if (ExecuteInConsole) {
                if (handleConsoleAndPause) {
                    Arguments = string.Format(
                        "/C \"{0}{1}{2}\" & if errorlevel 1 pause",
                        ProcessOutput.QuoteSingleArgument(Filename),
                        string.IsNullOrEmpty(Arguments) ? string.Empty : " ",
                        Arguments ?? string.Empty
                    );
                    Filename = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    ExecuteIn = PythonCommandTask.ExecuteInConsole;
                }
            } else if (ExecuteInConsoleAndPause) {
                if (handleConsoleAndPause) {
                    Arguments = string.Format(
                        "/C \"{0}{1}{2}\" & pause",
                        ProcessOutput.QuoteSingleArgument(Filename),
                        string.IsNullOrEmpty(Arguments) ? string.Empty : " ",
                        Arguments ?? string.Empty
                    );
                    Filename = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    ExecuteIn = PythonCommandTask.ExecuteInConsole;
                }
            }

            if (EnvironmentVariables != null && !string.IsNullOrEmpty(Arguments)) {
                Arguments = Regex.Replace(Arguments, @"%(\w+)%", m => {
                    string envVar;
                    return EnvironmentVariables.TryGetValue(m.Groups[1].Value, out envVar) ? envVar : string.Empty;
                });
            }
        }

        private static string ChooseFirst(string x, string y) {
            if (string.IsNullOrEmpty(x)) {
                return y ?? string.Empty;
            }
            return x;
        }

        internal void AddPropertiesAfter(LaunchConfiguration config) {
            AddArgumentAtEnd(config.ScriptArguments);
            WorkingDirectory = ChooseFirst(WorkingDirectory, config.WorkingDirectory);

            EnvironmentVariables = PathUtils.MergeEnvironments(
                EnvironmentVariables.MaybeEnumerate(),
                config.Environment.MaybeEnumerate(),
                "PATH", config.Interpreter.PathEnvironmentVariable
            );
        }
    }
}
