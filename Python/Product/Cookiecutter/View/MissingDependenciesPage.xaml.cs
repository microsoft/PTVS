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

namespace Microsoft.CookiecutterTools.View {
    /// <summary>
    /// Interaction logic for MissingDependenciesPage.xaml
    /// </summary>
    internal partial class MissingDependenciesPage : Page {
        public static readonly ICommand InstallPython = new RoutedCommand();

        public MissingDependenciesPage() {
            InitializeComponent();
        }

        private void InstallPython_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            var url = (string)e.Parameter;
            e.CanExecute = true;
            e.Handled = true;
        }

        private void InstallPython_Executed(object sender, ExecutedRoutedEventArgs e) {
            var url = (string)e.Parameter;
            Process.Start(UrlConstants.InstallPythonUrl)?.Dispose();
        }
    }
}
