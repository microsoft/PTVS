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
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using WpfCommands = Microsoft.VisualStudioTools.Wpf.Commands;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Interaction logic for AddInterpreter.xaml
    /// </summary>
    partial class AddVirtualEnvironment : DialogWindowVersioningWorkaround {
        public static readonly ICommand WebChooseInterpreter = new RoutedCommand();
        
        private readonly AddVirtualEnvironmentView _view;
        private Task _currentOperation;

        private AddVirtualEnvironment(AddVirtualEnvironmentView view) {
            _view = view;
            _view.PropertyChanged += View_PropertyChanged;
            DataContext = _view;

            InitializeComponent();
        }

        public static async Task ShowDialog(
            PythonProjectNode project,
            IInterpreterOptionsService service,
            bool browseForExisting = false
        ) {
            using (var view = new AddVirtualEnvironmentView(project, service, project.Interpreters.ActiveInterpreter)) {
                var wnd = new AddVirtualEnvironment(view);

                if (browseForExisting) {
                    var path = project.Site.BrowseForDirectory(IntPtr.Zero, project.ProjectHome);
                    if (string.IsNullOrEmpty(path)) {
                        throw new OperationCanceledException();
                    }
                    view.VirtualEnvName = path;
                    view.WillInstallRequirementsTxt = false;
                    await view.WaitForReady();
                    if (view.WillAddVirtualEnv) {
                        await view.Create();
                        return;
                    }

                    view.ShowBrowsePathError = true;
                    view.BrowseOrigPrefix = DerivedInterpreterFactory.GetOrigPrefixPath(path);
                }

                wnd.VirtualEnvPathTextBox.ScrollToEnd();
                wnd.VirtualEnvPathTextBox.SelectAll();
                wnd.VirtualEnvPathTextBox.Focus();

                wnd.ShowModal();
                var op = wnd._currentOperation;
                if (op != null) {
                    await op;
                }
            }
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            WpfCommands.CanExecute(this, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            WpfCommands.Executed(this, sender, e);
        }

        private void Close_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e) {
            try {
                DialogResult = false;
                Close();
            } catch (InvalidOperationException) {
                // Dialog is already closed by the user
            }
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _currentOperation == null && 
                (_view.WillCreateVirtualEnv || _view.WillAddVirtualEnv);
        }

        private async void Save_Executed(object sender, ExecutedRoutedEventArgs e) {
            Debug.Assert(_currentOperation == null);
            _currentOperation = _view.Create().HandleAllExceptions(SR.ProductName);
            
            await _currentOperation;

            _currentOperation = null;
            try {
                DialogResult = true;
                Close();
            } catch (InvalidOperationException) {
                // Dialog is already closed by the user
            }
        }

        private void WebChooseInterpreter_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void WebChooseInterpreter_Executed(object sender, ExecutedRoutedEventArgs e) {
            PythonToolsPackage.OpenWebBrowser(PythonToolsPackage.InterpreterHelpUrl);
            DialogResult = false;
            Close();
        }
    }
}
