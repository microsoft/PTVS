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

using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Interaction logic for RenameVariableDialog.xaml
    /// </summary>
    partial class RenameVariableDialog : DialogWindowVersioningWorkaround {
        private bool _firstActivation;

        public RenameVariableDialog(RenameVariableRequestView viewModel) {
            DataContext = viewModel;

            InitializeComponent();

            _firstActivation = true;
        }

        protected override void OnActivated(System.EventArgs e) {
            base.OnActivated(e);
            if (_firstActivation) {
                _newName.Focus();
                _newName.SelectAll();
                _firstActivation = false;
            }
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }
    }
}
