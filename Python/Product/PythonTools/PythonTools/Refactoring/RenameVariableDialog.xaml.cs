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

using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Refactoring
{
    /// <summary>
    /// Interaction logic for RenameVariableDialog.xaml
    /// </summary>
    partial class RenameVariableDialog : DialogWindowVersioningWorkaround
    {
        private bool _firstActivation;

        public RenameVariableDialog(RenameVariableRequestView viewModel)
        {
            DataContext = viewModel;

            InitializeComponent();

            _firstActivation = true;
        }

        protected override void OnActivated(System.EventArgs e)
        {
            base.OnActivated(e);
            if (_firstActivation)
            {
                _newName.Focus();
                _newName.SelectAll();
                _firstActivation = false;
            }
        }

        private void OkClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}
