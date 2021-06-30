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
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Environments {
    public partial class AddVirtualEnvironmentControl : UserControl {
        public static readonly ICommand WebChooseInterpreter = new RoutedCommand();
        public static readonly ICommand ChangeLocation = new RoutedCommand();

        public AddVirtualEnvironmentControl() {
            InitializeComponent();
        }

        private AddVirtualEnvironmentView View => (AddVirtualEnvironmentView)DataContext;

        private void Browse_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.CanExecute(null, sender, e);
        }

        private void Browse_Executed(object sender, ExecutedRoutedEventArgs e) {
            Microsoft.VisualStudioTools.Wpf.Commands.Executed(null, sender, e);
        }

        private void ChangeLocation_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
        }

        private void ChangeLocation_Executed(object sender, ExecutedRoutedEventArgs e) {
            var view = ((AddVirtualEnvironmentControl)sender).View;

            Window window = null;

            var path = Dialogs.BrowseForDirectory(
                window == null ? IntPtr.Zero : new WindowInteropHelper(window).Handle,
                view.LocationPath
            );
            if (path != null) {
                view.LocationPath = path;
            }
        }
    }
}
