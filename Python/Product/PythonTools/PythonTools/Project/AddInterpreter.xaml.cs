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
    partial class AddInterpreter : DialogWindowVersioningWorkaround {
        const double OutputTextBoxWidth = 350.0;

        readonly AddInterpreterView _view;

        private AddInterpreter(PythonProjectNode project, IInterpreterOptionsService service) {
            _view = new AddInterpreterView(service, project.Interpreters.GetInterpreterFactories());
            _view.PropertyChanged += View_PropertyChanged;
            DataContext = _view;

            InitializeComponent();
        }

        public static IEnumerable<IPythonInterpreterFactory> ShowDialog(
            PythonProjectNode project,
            IInterpreterOptionsService service) {
            var wnd = new AddInterpreter(project, service);
            if (wnd.ShowModal() ?? false) {
                return wnd._view.Interpreters.Where(iv => iv.IsSelected).Select(iv => iv.Interpreter);
            }
            return null;
        }

        private void View_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            CommandManager.InvalidateRequerySuggested();
        }

        private void Close_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
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
        }
    }
}
