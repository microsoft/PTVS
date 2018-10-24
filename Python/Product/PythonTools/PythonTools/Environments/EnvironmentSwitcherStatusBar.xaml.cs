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
using System.ComponentModel.Design;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Environments {
    public partial class EnvironmentSwitcherStatusBar : UserControl {
        private IServiceProvider _serviceProvider;

        public EnvironmentSwitcherStatusBar() : this(null) {
        }

        public EnvironmentSwitcherStatusBar(IServiceProvider serviceProvider) {
            InitializeComponent();
            _serviceProvider = serviceProvider;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);

            ShowMenu();

            e.Handled = true;
        }

        internal void ShowMenu() {
            var mcs = _serviceProvider?.GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (mcs != null) {
                var pt = PointToScreen(new Point(ActualWidth, 0));
                mcs.ShowContextMenu(new CommandID(GuidList.guidPythonToolsCmdSet, PythonConstants.EnvironmentStatusBarMenu), (int)pt.X, (int)pt.Y);
            }
        }
    }
}
