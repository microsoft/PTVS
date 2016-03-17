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
using System.Threading.Tasks;
using System.Windows;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Interaction logic for InstallPythonPackage.xaml
    /// </summary>
    partial class InstallPythonPackage : DialogWindowVersioningWorkaround {
        private readonly InstallPythonPackageView _view;

        public static InstallPythonPackageView ShowDialog(
            IServiceProvider serviceProvider,
            IPythonInterpreterFactory factory,
            IInterpreterRegistry service
        ) {
            var wnd = new InstallPythonPackage(serviceProvider, factory, service);
            if (wnd.ShowModal() ?? false) {
                return wnd._view;
            } else {
                return null;
            }
        }
        
        private InstallPythonPackage(
            IServiceProvider serviceProvider,
            IPythonInterpreterFactory factory,
            IInterpreterRegistry service
        ) {
            _view = new InstallPythonPackageView(
                serviceProvider,
                !Pip.IsSecureInstall(factory),
                Conda.CanInstall(factory, service)
            );
            DataContext = _view;

            InitializeComponent();
            _textBox.Focus();
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
