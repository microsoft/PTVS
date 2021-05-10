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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Infrastructure.Commands;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.InterpreterList {
    [Guid(PythonConstants.InterpreterListToolWindowGuid)]
    sealed class InterpreterListToolWindow : ToolWindowPane {
        private IServiceProvider _site;
        private UIThreadBase _uiThread;
        private PythonToolsService _pyService;
        private Redirector _outputWindow;
        private IVsStatusbar _statusBar;
        private readonly object _commandsLock = new object();
        private readonly Dictionary<Command, MenuCommand> _commands = new Dictionary<Command, MenuCommand>();

        private readonly Dictionary<EnvironmentView, string> _cachedScriptPaths;

        public InterpreterListToolWindow(IServiceProvider services) : base(services) {
            ToolBar = new CommandID(CommonGuidList.guidPythonToolsCmdSet, PkgCmdIDList.EnvWindowToolbar);

            _site = services;
            _cachedScriptPaths = new Dictionary<EnvironmentView, string>();
        }

        protected override void OnCreate() {
            base.OnCreate();

            _pyService = _site.GetPythonToolsService();
            _uiThread = _site.GetUIThread();

            _pyService.InteractiveOptions.Changed += InteractiveOptions_Changed;

            // TODO: Get PYEnvironment added to image list
            BitmapImageMoniker = KnownMonikers.DockPanel;
            Caption = Strings.Environments;

            _outputWindow = OutputWindowRedirector.GetGeneral(_site);
            Debug.Assert(_outputWindow != null);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;

            var list = new ToolWindow();
            list.ViewCreated += List_ViewCreated;
            list.ViewSelected += List_ViewSelected;
            list.Site = _site;
            try {
                list.TelemetryLogger = _pyService.Logger;
            } catch (Exception ex) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInteractiveWindow,
                OpenInteractiveWindow_Executed,
                OpenInteractiveWindow_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInteractiveScripts,
                OpenInteractiveScripts_Executed,
                OpenInteractiveScripts_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentPathsExtension.StartInterpreter,
                StartInterpreter_Executed,
                StartInterpreter_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentPathsExtension.StartWindowsInterpreter,
                StartInterpreter_Executed,
                StartInterpreter_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                ApplicationCommands.Help,
                OnlineHelp_Executed,
                OnlineHelp_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                ToolWindow.UnhandledException,
                UnhandledException_Executed,
                UnhandledException_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInPowerShell,
                OpenInPowerShell_Executed,
                OpenInPowerShell_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInCommandPrompt,
                OpenInCommandPrompt_Executed,
                OpenInCommandPrompt_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentPathsExtension.OpenInBrowser,
                OpenInBrowser_Executed,
                OpenInBrowser_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.Delete,
                DeleteEnvironment_Executed,
                DeleteEnvironment_CanExecute
            ));

            RegisterCommands(
                CommandAsyncToOleMenuCommandShimFactory.CreateCommand(CommonGuidList.guidPythonToolsCmdSet, (int)PkgCmdIDList.cmdidAddEnvironment, new AddEnvironmentCommand(this))
            );

            Content = list;
        }

        internal void RegisterCommands(params MenuCommand[] commands) {
            _uiThread.MustBeCalledFromUIThreadOrThrow();
            if (GetService(typeof(IMenuCommandService)) is OleMenuCommandService mcs) {
                foreach (var command in commands) {
                    mcs.AddCommand(command);
                }
            }
        }

        private void InteractiveOptions_Changed(object sender, EventArgs e) {
            lock (_cachedScriptPaths) {
                _cachedScriptPaths.Clear();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private string GetScriptPath(EnvironmentView view) {
            if (view == null) {
                return null;
            }

            string path;
            lock (_cachedScriptPaths) {
                if (_cachedScriptPaths.TryGetValue(view, out path)) {
                    return path;
                }
            }

            try {
                path = _uiThread.Invoke(() => PythonInteractiveEvaluator.GetScriptsPath(
                    _site,
                    view.Description,
                    view.Factory.Configuration,
                    false
                ));
            } catch (DirectoryNotFoundException) {
                path = null;
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                view.Dispatcher.BeginInvoke((Action)(() => ex.ReportUnhandledException(_site, GetType())), DispatcherPriority.ApplicationIdle);
                path = null;
            }

            lock (_cachedScriptPaths) {
                _cachedScriptPaths[view] = path;
            }
            return path;
        }

        private void OpenInteractiveScripts_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            e.CanExecute = path != null;
            e.Handled = true;
        }

        private bool EnsureScriptDirectory(string path) {
            if (string.IsNullOrEmpty(path)) {
                return false;
            }

            if (!Directory.Exists(path)) {
                try {
                    Directory.CreateDirectory(path);
                    File.WriteAllText(PathUtils.GetAbsoluteFilePath(path, "readme.txt"), Strings.ReplScriptPathReadmeContents);
                } catch (IOException) {
                    // CreateDirectory may throw FileNotFoundException
                    // instead of UnauthorizedAccessException (https://github.com/dotnet/corefx/issues/26561),
                    // so handle both.
                    return false;
                } catch (UnauthorizedAccessException) {
                    return false;
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    TaskDialog.ForException(_site, ex, issueTrackerUrl: Strings.IssueTrackerUrl).ShowModal();
                    return false;
                }
            }
            return true;
        }

        private void OpenInteractiveScripts_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            if (!EnsureScriptDirectory(path)) {
                return;
            }

            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;
            psi.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            psi.Arguments = "\"" + path + "\"";

            Process.Start(psi)?.Dispose();
            e.Handled = true;
        }

        private bool QueryIPythonEnabled(EnvironmentView view) {
            var path = GetScriptPath(view);
            return path != null && File.Exists(PathUtils.GetAbsoluteFilePath(path, "mode.txt"));
        }

        private void SetIPythonEnabled(EnvironmentView view, bool enable) {
            var path = GetScriptPath(view);
            if (!EnsureScriptDirectory(path)) {
                return;
            }

            try {
                path = PathUtils.GetAbsoluteFilePath(path, "mode.txt");
                if (enable) {
                    File.WriteAllText(path, Strings.ReplScriptPathIPythonModeTxtContents);
                } else {
                    if (File.Exists(path)) {
                        File.Delete(path);
                    }
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, issueTrackerUrl: Strings.IssueTrackerUrl).ShowModal();
            }
        }

        private void List_ViewCreated(object sender, EnvironmentViewEventArgs e) {
            var view = e.View;
            if (view.Factory == null) {
                return;
            }

            view.IPythonModeEnabledSetter = SetIPythonEnabled;
            view.IPythonModeEnabledGetter = QueryIPythonEnabled;
        }

        private void List_ViewSelected(object sender, EnvironmentViewEventArgs e) {
            var view = e.View;
            if (view.Factory == null || view.ExtensionsCreated) {
                return;
            }

            // We used to create all the extensions up front in List_ViewCreated
            // but that slowed down initialization of the tool window considerably
            // due to the package manager extension in particular.
            // We now create the extensions only if they are likely to be used,
            // the first time an environment is selected in the list view.
            view.ExtensionsCreated = true;

            foreach (var pm in (_site.GetComponentModel().GetService<IInterpreterOptionsService>()?.GetPackageManagers(view.Factory)).MaybeEnumerate()) {
                try {
                    var pep = new PipExtensionProvider(view.Factory, pm);
                    pep.QueryShouldElevate += PipExtensionProvider_QueryShouldElevate;
                    pep.OperationStarted += PipExtensionProvider_OperationStarted;
                    pep.OutputTextReceived += PipExtensionProvider_OutputTextReceived;
                    pep.ErrorTextReceived += PipExtensionProvider_ErrorTextReceived;
                    pep.OperationFinished += PipExtensionProvider_OperationFinished;
                    view.Extensions.Add(pep);
                } catch (NotSupportedException) {
                }
            }

            var model = _site.GetComponentModel();
            if (model != null) {
                try {
                    foreach (var provider in model.GetExtensions<IEnvironmentViewExtensionProvider>()) {
                        try {
                            var ext = provider.CreateExtension(view);
                            if (ext != null) {
                                view.Extensions.Add(ext);
                            }
                        } catch (Exception ex) {
                            LogLoadException(provider, ex);
                        }
                    }
                } catch (Exception ex2) {
                    LogLoadException(null, ex2);
                }
            }
        }

        private void LogLoadException(IEnvironmentViewExtensionProvider provider, Exception ex) {
            string message;
            if (provider == null) {
                message = Strings.ErrorLoadingEnvironmentViewExtensions.FormatUI(ex);
            } else {
                message = Strings.ErrorLoadingEnvironmentViewExtension.FormatUI(provider.GetType().FullName, ex);
            }

            Debug.Fail(message);
            var log = _site.GetService(typeof(SVsActivityLog)) as IVsActivityLog;
            if (log != null) {
                log.LogEntry(
                    (uint)__ACTIVITYLOG_ENTRYTYPE.ALE_ERROR,
                    Strings.ProductTitle,
                    message
                );
            }
        }

        private void PipExtensionProvider_QueryShouldElevate(object sender, QueryShouldElevateEventArgs e) {
            try {
                e.Elevate = VsPackageManagerUI.ShouldElevate(_site, e.Factory.Configuration, "pip");
            } catch (OperationCanceledException) {
                e.Cancel = true;
            }
        }

        private void PipExtensionProvider_OperationStarted(object sender, OutputEventArgs e) {
            if (_statusBar != null) {
                _statusBar.SetText(e.Data);
            }
            if (_pyService.GeneralOptions.ShowOutputWindowForPackageInstallation) {
                _outputWindow.ShowAndActivate();
            }
        }

        private void PipExtensionProvider_OutputTextReceived(object sender, OutputEventArgs e) {
            _outputWindow.WriteLine(e.Data.TrimEndNewline());
        }

        private void PipExtensionProvider_ErrorTextReceived(object sender, OutputEventArgs e) {
            _outputWindow.WriteErrorLine(e.Data.TrimEndNewline());
        }

        private void PipExtensionProvider_OperationFinished(object sender, OperationFinishedEventArgs e) {
            if (_pyService.GeneralOptions.ShowOutputWindowForPackageInstallation) {
                _outputWindow.ShowAndActivate();
            }
        }

        private void UnhandledException_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is ExceptionDispatchInfo;
        }

        private void UnhandledException_Executed(object sender, ExecutedRoutedEventArgs e) {
            var ex = (ExceptionDispatchInfo)e.Parameter;
            Debug.Assert(ex != null, "Unhandled exception with no exception object");

            var td = TaskDialog.ForException(_site, ex.SourceException, string.Empty, Strings.IssueTrackerUrl);
            td.Title = Strings.ProductTitle;
            td.ShowModal();
        }

        private void OpenInteractiveWindow_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null &&
                view.Factory != null &&
                view.Factory.Configuration != null &&
                File.Exists(view.Factory.Configuration.InterpreterPath);
        }

        private void OpenInteractiveWindow_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;
            var config = view.Factory.Configuration;

            var replId = PythonReplEvaluatorProvider.GetEvaluatorId(config);

            var compModel = _site.GetComponentModel();
            var service = compModel.GetService<InteractiveWindowProvider>();
            IVsInteractiveWindow window;

            // TODO: Figure out another way to get the project
            //var provider = _service.KnownProviders.OfType<LoadedProjectInterpreterFactoryProvider>().FirstOrDefault();
            //var vsProject = provider == null ?
            //    null :
            //    provider.GetProject(factory);
            //PythonProjectNode project = vsProject == null ? null : vsProject.GetPythonProject();
            try {
                window = service.OpenOrCreate(replId);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, Strings.ErrorOpeningInteractiveWindow, Strings.IssueTrackerUrl).ShowModal();
                return;
            }

            window?.Show(true);
        }

        private void StartInterpreter_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null && File.Exists(e.Command == EnvironmentPathsExtension.StartInterpreter ?
                view.Factory.Configuration.InterpreterPath :
                view.Factory.Configuration.GetWindowsInterpreterPath());
            e.Handled = true;
        }

        private void StartInterpreter_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                var view = (EnvironmentView)e.Parameter;

                var config = new LaunchConfiguration(view.Factory.Configuration) {
                    PreferWindowedInterpreter = (e.Command == EnvironmentPathsExtension.StartWindowsInterpreter),
                    WorkingDirectory = view.Factory.Configuration.GetPrefixPath(),
                    SearchPaths = new List<string>()
                };

                var sln = (IVsSolution)_site.GetService(typeof(SVsSolution));
                foreach (var pyProj in sln.EnumerateLoadedPythonProjects()) {
                    if (pyProj.InterpreterConfigurations.Contains(config.Interpreter)) {
                        config.SearchPaths.AddRange(pyProj.GetSearchPaths());
                    }
                }

                config.LaunchOptions[PythonConstants.NeverPauseOnExit] = "true";

                Process.Start(Debugger.DebugLaunchHelper.CreateProcessStartInfo(_site, config))?.Dispose();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, Strings.ErrorStartingInterpreter, Strings.IssueTrackerUrl).ShowModal();
                return;
            }
        }

        private void OnlineHelp_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _site != null;
            e.Handled = true;
        }

        private void OnlineHelp_Executed(object sender, ExecutedRoutedEventArgs e) {
            VisualStudioTools.CommonPackage.OpenWebBrowser(_site, PythonToolsPackage.InterpreterHelpUrl);
            e.Handled = true;
        }

        private static readonly string[] PathSuffixes = new[] { "", "Scripts" };

        private static string GetPathEntries(EnvironmentView view) {
            if (!Directory.Exists(view?.PrefixPath)) {
                return null;
            }

            return string.Join(";", PathSuffixes
                .Select(s => PathUtils.GetAbsoluteDirectoryPath(view.PrefixPath, s))
                .Where(Directory.Exists));
        }

        private void DeleteEnvironment_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view?.CanBeDeleted == true;
            e.Handled = true;
        }

        private void DeleteEnvironment_Executed(object sender, ExecutedRoutedEventArgs e) {
            // TODO: this is assuming that all environments that CanBeDeleted are conda environments, which may not be true in the future
            var view = e.Parameter as EnvironmentView;
            var result = MessageBox.Show(
                EnvironmentsList.Resources.EnvironmentPathsExtensionDeleteConfirmation.FormatUI(view.Configuration.GetPrefixPath()),
                EnvironmentsList.Resources.ProductTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );
            if (result != MessageBoxResult.Yes) {
                return;
            }

            var compModel = _site.GetService(typeof(SComponentModel)) as IComponentModel;
            var registry = compModel.GetService<IInterpreterRegistryService>();
            var mgr = CondaEnvironmentManager.Create(_site);
            mgr.DeleteAsync(
                view.Configuration.GetPrefixPath(),
                new CondaEnvironmentManagerUI(_outputWindow),
                CancellationToken.None
            ).HandleAllExceptions(_site, GetType()).DoNotWait();
        }

        class CondaEnvironmentManagerUI : ICondaEnvironmentManagerUI {
            private readonly Redirector _window;

            public CondaEnvironmentManagerUI(Redirector window) {
                _window = window;
            }

            public void OnErrorTextReceived(ICondaEnvironmentManager sender, string text) {
                _window.WriteErrorLine(text.TrimEndNewline());
            }

            public void OnOperationFinished(ICondaEnvironmentManager sender, string operation, bool success) {
            }

            public void OnOperationStarted(ICondaEnvironmentManager sender, string operation) {
                _window.ShowAndActivate();
            }

            public void OnOutputTextReceived(ICondaEnvironmentManager sender, string text) {
                _window.WriteLine(text.TrimEndNewline());
            }
        }

        private void OpenInCommandPrompt_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = Directory.Exists(view?.PrefixPath);
            e.Handled = true;
        }

        private void OpenInCommandPrompt_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                var view = (EnvironmentView)e.Parameter;

                var paths = GetPathEntries(view);
                var pathCmd = string.IsNullOrEmpty(paths) ? "" : string.Format("set PATH={0};%PATH% & ", paths);
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe")) {
                    Arguments = string.Join(" ", new[] {
                    "/S",
                    "/K",
                    pathCmd + string.Format("title {0} environment", view.Description)
                }.Select(ProcessOutput.QuoteSingleArgument)),
                    WorkingDirectory = view.PrefixPath
                };

                Process.Start(psi)?.Dispose();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, Strings.ErrorOpeningCommandPrompt, Strings.IssueTrackerUrl).ShowModal();
                return;
            }
        }

        private void OpenInPowerShell_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = Directory.Exists(view?.PrefixPath);
            e.Handled = true;
        }

        private void OpenInPowerShell_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                var view = (EnvironmentView)e.Parameter;

                var paths = GetPathEntries(view);
                var pathCmd = string.IsNullOrEmpty(paths) ? "" : string.Format("$env:PATH='{0};' + $env:PATH; ", paths);
                var psi = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) {
                    Arguments = string.Join(" ", new[] {
                    "-NoLogo",
                    "-NoExit",
                    "-Command",
                    pathCmd + string.Format("(Get-Host).UI.RawUI.WindowTitle = '{0} environment'", view.Description)
                }.Select(ProcessOutput.QuoteSingleArgument)),
                    WorkingDirectory = view.PrefixPath
                };

                Process.Start(psi)?.Dispose();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, Strings.ErrorOpeningPowershell, Strings.IssueTrackerUrl).ShowModal();
                return;
            }
        }

        private void OpenInBrowser_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is string;
            e.Handled = true;
        }

        private void OpenInBrowser_Executed(object sender, ExecutedRoutedEventArgs e) {
            PythonToolsPackage.OpenWebBrowser(_site, (string)e.Parameter);
        }

        internal static async System.Threading.Tasks.Task OpenAtAsync(IServiceProvider site, string viewId, Type extension) {
            var service = (IPythonToolsToolWindowService)site?.GetService(typeof(IPythonToolsToolWindowService));
            if (service == null) {
                Debug.Fail("Failed to get environment list window");
                return;
            }

            var wnd = await service.GetWindowPaneAsync(typeof(InterpreterListToolWindow), true) as InterpreterListToolWindow;
            if (!(wnd?.Content is ToolWindow envs)) {
                Debug.Fail("Failed to get environment list window");
                return;
            }

            ErrorHandler.ThrowOnFailure((wnd.Frame as IVsWindowFrame)?.Show() ?? 0);

            SelectEnvAndExt(envs, viewId, extension, 3);
        }

        internal static async System.Threading.Tasks.Task OpenAtAsync(IServiceProvider site, IPythonInterpreterFactory interpreter, Type extension = null) {
            var service = (IPythonToolsToolWindowService)site?.GetService(typeof(IPythonToolsToolWindowService));
            if (service == null) {
                Debug.Fail("Failed to get environment list window");
                return;
            }

            var wnd = await service.GetWindowPaneAsync(typeof(InterpreterListToolWindow), true) as InterpreterListToolWindow;
            if (!(wnd?.Content is ToolWindow envs)) {
                Debug.Fail("Failed to get environment list window");
                return;
            }

            ErrorHandler.ThrowOnFailure((wnd.Frame as IVsWindowFrame)?.Show() ?? 0);

            if (extension == null) {
                SelectEnv(envs, interpreter, 3);
            } else {
                SelectEnvAndExt(envs, interpreter, extension, 3);
            }
        }

        private static void SelectEnv(ToolWindow envs, IPythonInterpreterFactory interpreter, int retries) {
            if (retries <= 0) {
                Debug.Fail("Failed to select environment after multiple retries");
                return;
            }
            var select = envs.IsLoaded ? envs.Environments.OfType<EnvironmentView>().FirstOrDefault(e => e.Factory == interpreter) : null;
            if (select == null) {
                envs.Dispatcher.InvokeAsync(() => SelectEnv(envs, interpreter, retries - 1), DispatcherPriority.Background);
                return;
            }

            envs.Environments.MoveCurrentTo(select);
        }

        private static void SelectEnvAndExt(ToolWindow envs, IPythonInterpreterFactory interpreter, Type extension, int retries) {
            if (retries <= 0) {
                Debug.Fail("Failed to select environment/extension after multiple retries");
                return;
            }
            var select = envs.IsLoaded ? envs.Environments.OfType<EnvironmentView>().FirstOrDefault(e => e.Factory == interpreter) : null;
            if (select == null) {
                envs.Dispatcher.InvokeAsync(() => SelectEnvAndExt(envs, interpreter, extension, retries - 1), DispatcherPriority.Background);
                return;
            }

            envs.OnViewSelected(select);

            var ext = select?.Extensions.FirstOrDefault(e => e != null && extension.IsEquivalentTo(e.GetType()));

            envs.Environments.MoveCurrentTo(select);
            if (ext != null) {
                var exts = envs.Extensions;
                if (exts != null && exts.Contains(ext)) {
                    exts.MoveCurrentTo(ext);
                    ((ext as IEnvironmentViewExtension)?.WpfObject as ICanFocus)?.Focus();
                }
            }
        }

        private static void SelectEnvAndExt(ToolWindow envs, string viewId, Type extension, int retries) {
            if (retries <= 0) {
                Debug.Fail("Failed to select environment/extension after multiple retries");
                return;
            }
            var select = envs.IsLoaded ? envs.Environments.OfType<EnvironmentView>().FirstOrDefault(e => e.Configuration.Id == viewId) : null;
            if (select == null) {
                envs.Dispatcher.InvokeAsync(() => SelectEnvAndExt(envs, viewId, extension, retries - 1), DispatcherPriority.Background);
                return;
            }

            envs.OnViewSelected(select);

            var ext = select?.Extensions.FirstOrDefault(e => e != null && extension.IsEquivalentTo(e.GetType()));

            envs.Environments.MoveCurrentTo(select);
            if (ext != null) {
                var exts = envs.Extensions;
                if (exts != null && exts.Contains(ext)) {
                    exts.MoveCurrentTo(ext);
                    ((ext as IEnvironmentViewExtension)?.WpfObject as ICanFocus)?.Focus();
                }
            }
        }
    }
}
