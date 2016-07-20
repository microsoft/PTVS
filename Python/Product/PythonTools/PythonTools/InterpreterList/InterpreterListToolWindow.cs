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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.PythonTools.Debugger;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Imaging;
using Microsoft.PythonTools.InteractiveWindow.Shell;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.InterpreterList {
    [Guid(PythonConstants.InterpreterListToolWindowGuid)]
    sealed class InterpreterListToolWindow : ToolWindowPane {
        private IServiceProvider _site;
        private PythonToolsService _pyService;
        private Redirector _outputWindow;
        private IVsStatusbar _statusBar;

        public InterpreterListToolWindow() { }

        protected override void OnCreate() {
            base.OnCreate();

            _site = (IServiceProvider)this;

            _pyService = _site.GetPythonToolsService();

            // TODO: Get PYEnvironment added to image list
            BitmapImageMoniker = KnownMonikers.DockPanel;
            Caption = Strings.Environments;

            _outputWindow = OutputWindowRedirector.GetGeneral(_site);
            Debug.Assert(_outputWindow != null);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            
            var list = new ToolWindow();
            list.Site = _site;
            list.ViewCreated += List_ViewCreated;

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
                EnvironmentView.EnableIPythonInteractive,
                EnableIPythonInteractive_Executed,
                EnableIPythonInteractive_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.DisableIPythonInteractive,
                DisableIPythonInteractive_Executed,
                DisableIPythonInteractive_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentPathsExtension.OpenInBrowser,
                OpenInBrowser_Executed,
                OpenInBrowser_CanExecute
            ));

            Content = list;
        }

        private string GetScriptPath(EnvironmentView view) {
            if (view == null) {
                return null;
            }

            return PythonInteractiveEvaluator.GetScriptsPath(
                _site,
                view.Description,
                view.Factory.Configuration,
                false
            );
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
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    TaskDialog.ForException(_site, ex, issueTrackerUrl: PythonConstants.IssueTrackerUrl).ShowModal();
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
            psi.FileName = "explorer.exe";
            psi.Arguments = "\"" + path + "\"";

            Process.Start(psi).Dispose();
            e.Handled = true;
        }

        private void EnableIPythonInteractive_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            e.CanExecute = path != null && !File.Exists(PathUtils.GetAbsoluteFilePath(path, "mode.txt"));
            e.Handled = true;
        }

        private void EnableIPythonInteractive_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            if (!EnsureScriptDirectory(path)) {
                return;
            }

            path = PathUtils.GetAbsoluteFilePath(path, "mode.txt");
            try {
                File.WriteAllText(path, Strings.ReplScriptPathIPythonModeTxtContents);
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                TaskDialog.ForException(_site, ex, issueTrackerUrl: PythonConstants.IssueTrackerUrl).ShowModal();
                return;
            }

            e.Handled = true;
        }

        private void DisableIPythonInteractive_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            e.CanExecute = path != null && File.Exists(PathUtils.GetAbsoluteFilePath(path, "mode.txt"));
            e.Handled = true;
        }

        private void DisableIPythonInteractive_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = GetScriptPath(e.Parameter as EnvironmentView);
            if (!EnsureScriptDirectory(path)) {
                return;
            }

            path = PathUtils.GetAbsoluteFilePath(path, "mode.txt");
            if (File.Exists(path)) {
                try {
                    File.Delete(path);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    TaskDialog.ForException(_site, ex, issueTrackerUrl: PythonConstants.IssueTrackerUrl).ShowModal();
                    return;
                }
            }
        }

        private void List_ViewCreated(object sender, EnvironmentViewEventArgs e) {
            var view = e.View;
            var pep = new PipExtensionProvider(view.Factory);
            pep.GetElevateSetting += PipExtensionProvider_GetElevateSetting;
            pep.OperationStarted += PipExtensionProvider_OperationStarted;
            pep.OutputTextReceived += PipExtensionProvider_OutputTextReceived;
            pep.ErrorTextReceived += PipExtensionProvider_ErrorTextReceived;
            pep.OperationFinished += PipExtensionProvider_OperationFinished;

            view.Extensions.Add(pep);
            var _withDb = view.Factory as PythonInterpreterFactoryWithDatabase;
            if (_withDb != null) {
                view.Extensions.Add(new DBExtensionProvider(_withDb));
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

        private void PipExtensionProvider_GetElevateSetting(object sender, ValueEventArgs<bool> e) {
            e.Value = _pyService.GeneralOptions.ElevatePip;
        }

        private void PipExtensionProvider_OperationStarted(object sender, ValueEventArgs<string> e) {
            _outputWindow.WriteLine(e.Value);
            if (_statusBar != null) {
                _statusBar.SetText(e.Value);
            }
            if (_pyService.GeneralOptions.ShowOutputWindowForPackageInstallation) {
                _outputWindow.ShowAndActivate();
            }
        }

        private void PipExtensionProvider_OutputTextReceived(object sender, ValueEventArgs<string> e) {
            _outputWindow.WriteLine(e.Value);
        }

        private void PipExtensionProvider_ErrorTextReceived(object sender, ValueEventArgs<string> e) {
            _outputWindow.WriteErrorLine(e.Value);
        }

        private void PipExtensionProvider_OperationFinished(object sender, ValueEventArgs<string> e) {
            _outputWindow.WriteLine(e.Value);
            if (_statusBar != null) {
                _statusBar.SetText(e.Value);
            }
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
            if (ex.SourceException is PipException) {
                // Don't report Pip exceptions. The output messages have
                // already been handled.
                return;
            }

            var td = TaskDialog.ForException(_site, ex.SourceException, String.Empty, PythonConstants.IssueTrackerUrl);
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
                MessageBox.Show(Strings.ErrorOpeningInteractiveWindow.FormatUI(ex), Strings.ProductTitle);
                return;
            }

            window?.Show(true);
        }

        private void StartInterpreter_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null && File.Exists(e.Command == EnvironmentPathsExtension.StartInterpreter ?
                view.Factory.Configuration.InterpreterPath :
                view.Factory.Configuration.WindowsInterpreterPath);
            e.Handled = true;
        }

        private void StartInterpreter_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;

            var config = new LaunchConfiguration(view.Factory.Configuration) {
                PreferWindowedInterpreter = (e.Command == EnvironmentPathsExtension.StartWindowsInterpreter),
                WorkingDirectory = view.Factory.Configuration.PrefixPath,
                SearchPaths = new List<string>()
            };

            var sln = (IVsSolution)_site.GetService(typeof(SVsSolution));
            foreach (var pyProj in sln.EnumerateLoadedPythonProjects()) {
                if (pyProj.InterpreterConfigurations.Contains(config.Interpreter)) {
                    config.SearchPaths.AddRange(pyProj.GetSearchPaths());
                }
            }

            Process.Start(DebugLaunchHelper.CreateProcessStartInfo(_site, config)).Dispose();
        }

        private void OnlineHelp_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _site != null;
            e.Handled = true;
        }

        private void OnlineHelp_Executed(object sender, ExecutedRoutedEventArgs e) {
            VisualStudioTools.CommonPackage.OpenVsWebBrowser(_site, PythonToolsPackage.InterpreterHelpUrl);
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

        private void OpenInCommandPrompt_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = Directory.Exists(view?.PrefixPath);
            e.Handled = true;
        }

        private void OpenInCommandPrompt_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;

            var paths = GetPathEntries(view);
            var pathCmd = string.IsNullOrEmpty(paths) ? "" : string.Format("set PATH={0};%PATH% & ", paths);
            var psi = new ProcessStartInfo("cmd.exe");
            psi.Arguments = string.Join(" ", new[] {
                "/S",
                "/K",
                pathCmd + string.Format("title {0} environment", view.Description)
            }.Select(ProcessOutput.QuoteSingleArgument));
            psi.WorkingDirectory = view.PrefixPath;

            Process.Start(psi).Dispose();
        }

        private void OpenInPowerShell_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = Directory.Exists(view?.PrefixPath);
            e.Handled = true;
        }

        private void OpenInPowerShell_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = (EnvironmentView)e.Parameter;

            var paths = GetPathEntries(view);
            var pathCmd = string.IsNullOrEmpty(paths) ? "" : string.Format("$env:PATH='{0};' + $env:PATH; ", paths);
            var psi = new ProcessStartInfo("powershell.exe");
            psi.Arguments = string.Join(" ", new[] {
                "-NoLogo",
                "-NoExit",
                "-Command",
                pathCmd + string.Format("(Get-Host).UI.RawUI.WindowTitle = '{0} environment'", view.Description)
            }.Select(ProcessOutput.QuoteSingleArgument));
            psi.WorkingDirectory = view.PrefixPath;

            Process.Start(psi).Dispose();
        }

        private void OpenInBrowser_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is string;
            e.Handled = true;
        }

        private void OpenInBrowser_Executed(object sender, ExecutedRoutedEventArgs e) {
            PythonToolsPackage.OpenVsWebBrowser(_site, (string)e.Parameter);
        }
    }
}
