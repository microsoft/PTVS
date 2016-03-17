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
using System.Windows;
using System.Windows.Input;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.EnvironmentsList.Host {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        readonly IInterpreterOptionsService _service = new InterpreterOptionsService(null);


        public MainWindow() {
            InitializeComponent();

            EnvironmentsToolWindow.ViewCreated += EnvironmentsToolWindow_ViewCreated;
            EnvironmentsToolWindow.Service = _service; // TODO: Should be sited.
        }

        void EnvironmentsToolWindow_ViewCreated(object sender, EnvironmentViewEventArgs e) {
            e.View.Extensions.Add(new PipExtensionProvider(e.View.Factory));
            var withDb = e.View.Factory as PythonInterpreterFactoryWithDatabase;
            if (withDb != null) {
                e.View.Extensions.Add(new DBExtensionProvider(withDb));
            }
        }


        private void OpenInteractiveWindow_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveWindow_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive window for " + ((EnvironmentView)e.Parameter).Description);
        }

        private void UnhandledException_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void UnhandledException_Executed(object sender, ExecutedRoutedEventArgs e) {
            System.Diagnostics.Debug.WriteLine((Exception)e.Parameter);
        }

        private void OpenInteractiveOptions_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = e.Parameter is EnvironmentView;
        }

        private void OpenInteractiveOptions_Executed(object sender, ExecutedRoutedEventArgs e) {
            MessageBox.Show("Opening interactive options for " + ((EnvironmentView)e.Parameter).Description);
        }

    }
}
