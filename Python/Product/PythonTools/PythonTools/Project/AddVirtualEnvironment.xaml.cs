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

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Interaction logic for AddInterpreter.xaml
    /// </summary>
    partial class AddVirtualEnvironment : DialogWindowVersioningWorkaround {
        const double OutputTextBoxWidth = 350.0;

        readonly AddVirtualEnvironmentView _view;

        private AddVirtualEnvironment(PythonProjectNode project, IInterpreterOptionsService service) {
            _view = new AddVirtualEnvironmentView(project.ProjectHome, service);
            _view.PropertyChanged += View_PropertyChanged;
            DataContext = _view;

            InitializeComponent();
        }

        public static AddVirtualEnvironmentView ShowDialog(
            PythonProjectNode project,
            IInterpreterOptionsService service,
            bool browseForExisting = false) {
            var wnd = new AddVirtualEnvironment(project, service);

            if (browseForExisting) {
                var path = PythonToolsPackage.Instance.BrowseForDirectory(IntPtr.Zero, project.ProjectHome);
                if (string.IsNullOrEmpty(path)) {
                    return null;
                }
                wnd._view.VirtualEnvName = path;
                if (wnd._view.WillAddVirtualEnv) {
                    return wnd._view;
                }
                wnd.InvalidBrowsePathLabel.Visibility = Visibility.Visible;
            }

            wnd.VirtualEnvPathTextBox.ScrollToEnd();
            wnd.VirtualEnvPathTextBox.SelectAll();
            wnd.VirtualEnvPathTextBox.Focus();
            if (wnd.ShowModal() ?? false) {
                return wnd._view;
            } else {
                return null;
            }
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            Wpf.Commands.CanExecute(this, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            Wpf.Commands.Executed(this, sender, e);
        }

        private void Close_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = _view.WillCreateVirtualEnv || _view.WillAddVirtualEnv;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e) {
            DialogResult = true;
            Close();
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

    [ValueConversion(typeof(bool), typeof(Visibility))]
    sealed class BoolToVisibleConverter : IValueConverter {
        public Visibility Else {
            get;
            set;
        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return (value as bool? ?? true) ? Visibility.Visible : Else;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
