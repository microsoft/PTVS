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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Input;
using Microsoft.PythonTools.Commands;
using Microsoft.PythonTools.EnvironmentsList;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;
using Microsoft.PythonTools.Repl;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.InteractiveWindow.Shell;
#else
using Microsoft.VisualStudio.Repl;
#endif

namespace Microsoft.PythonTools.InterpreterList {
    [Guid(PythonConstants.InterpreterListToolWindowGuid)]
    sealed class InterpreterListToolWindow : ToolWindowPane {
        private IServiceProvider _site;
        private PythonToolsService _pyService;
        private IInterpreterOptionsService _service;
        private Redirector _outputWindow;
        private IVsStatusbar _statusBar;

        public InterpreterListToolWindow() { }

        protected override void OnCreate() {
            base.OnCreate();

            _site = (IServiceProvider)this;

            _pyService = _site.GetPythonToolsService();

#if DEV14_OR_LATER
            // TODO: Get PYEnvironment added to image list
            BitmapImageMoniker = KnownMonikers.DockPanel;
#else
            BitmapResourceID = PythonConstants.ResourceIdForReplImages;
            BitmapIndex = 0;
#endif
            Caption = SR.GetString(SR.Environments);

            _service = _site.GetComponentModel().GetService<IInterpreterOptionsService>();
            
            _outputWindow = OutputWindowRedirector.GetGeneral(_site);
            Debug.Assert(_outputWindow != null);
            _statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar;
            
            var list = new ToolWindow();
            list.ViewCreated += List_ViewCreated;

            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInteractiveWindow,
                OpenInteractiveWindow_Executed,
                OpenInteractiveWindow_CanExecute
            ));
            list.CommandBindings.Add(new CommandBinding(
                EnvironmentView.OpenInteractiveOptions,
                OpenInteractiveOptions_Executed,
                OpenInteractiveOptions_CanExecute
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

            list.Service = _service;

            Content = list;
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
            td.Title = SR.ProductName;
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
            var factory = view.Factory;
#if DEV14_OR_LATER
            IVsInteractiveWindow window;
#else
            IReplWindow window;
#endif

            var provider = _service.KnownProviders.OfType<LoadedProjectInterpreterFactoryProvider>().FirstOrDefault();
            var vsProject = provider == null ?
                null :
                provider.GetProject(factory);
            var project = vsProject == null ? null : vsProject.GetPythonProject();
            try {
                window = ExecuteInReplCommand.EnsureReplWindow(_site, factory, project);
            } catch (InvalidOperationException ex) {
                MessageBox.Show(SR.GetString(SR.ErrorOpeningInteractiveWindow, ex), SR.ProductName);
                return;
            }
            if (window != null) {
                var pane = window as ToolWindowPane;
                if (pane != null) {
                    ErrorHandler.ThrowOnFailure(((IVsWindowFrame)pane.Frame).Show());
                }
                window.Focus();
            }
        }

        private void OpenInteractiveOptions_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var view = e.Parameter as EnvironmentView;
            e.CanExecute = view != null && view.Factory != null && view.Factory.CanBeConfigured();
        }

        private void OpenInteractiveOptions_Executed(object sender, ExecutedRoutedEventArgs e) {
            PythonToolsPackage.ShowOptionPage(
                _site,
                typeof(PythonInteractiveOptionsPage),
                ((EnvironmentView)e.Parameter).Factory
            );
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
            var factory = view.Factory;

            var psi = new ProcessStartInfo();
            psi.UseShellExecute = false;

            psi.FileName = e.Command == EnvironmentPathsExtension.StartInterpreter ?
                factory.Configuration.InterpreterPath :
                factory.Configuration.WindowsInterpreterPath;
            psi.WorkingDirectory = factory.Configuration.PrefixPath;

            var provider = _service.KnownProviders.OfType<LoadedProjectInterpreterFactoryProvider>().FirstOrDefault();
            var vsProject = provider == null ?
                null :
                provider.GetProject(factory);
            var project = vsProject == null ? null : vsProject.GetPythonProject();
            if (project != null) {
                psi.EnvironmentVariables[factory.Configuration.PathEnvironmentVariable] = 
                    string.Join(";", project.GetSearchPaths());
            } else {
                psi.EnvironmentVariables[factory.Configuration.PathEnvironmentVariable] = string.Empty;
            }

            Process.Start(psi);
        }

        private void OnlineHelp_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _site != null;
            e.Handled = true;
        }

        private void OnlineHelp_Executed(object sender, ExecutedRoutedEventArgs e) {
            CommonPackage.OpenVsWebBrowser(_site, PythonToolsPackage.InterpreterHelpUrl);
            e.Handled = true;
        }
    }
}
