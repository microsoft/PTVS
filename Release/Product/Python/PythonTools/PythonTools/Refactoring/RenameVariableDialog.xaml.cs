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

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Interaction logic for RenameVariableDialog.xaml
    /// </summary>
    partial class RenameVariableDialog {
        private readonly string _originalName;

        public RenameVariableDialog(string originalName) {
            InitializeComponent();
            _newName.Text = originalName;
            _originalName = originalName;
            _newName.Focus();
            _newName.SelectAll();
            _ok.IsEnabled = false;
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            CloseTooltip();
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            CloseTooltip();
            Close();
        }

        private void CloseTooltip() {
            var toolTip = (ToolTip)_newName.ToolTip;
            toolTip.IsOpen = false;
        }

        internal RenameVariableRequest GetRenameVariableRequest() {
            return new RenameVariableRequest(
                _newName.Text,
                _previewChanges.IsChecked == true,
                _searchInComments.IsChecked == true,
                _searchInStrings.IsChecked == true
            );
        }

        private void _newName_TextChanged(object sender, TextChangedEventArgs e) {
            if (_ok != null) {
                if (!ExtractMethodDialog._validNameRegex.IsMatch(_newName.Text)) {
                    var toolTip = (ToolTip)_newName.ToolTip;
                    toolTip.Visibility = System.Windows.Visibility.Visible;
                    toolTip.IsOpen = true;
                    toolTip.IsEnabled = true;
                    toolTip.PlacementTarget = _newName;
                    _ok.IsEnabled = false;
                } else {
                    CloseTooltip();
                    _ok.IsEnabled = _originalName != _newName.Text;
                }
            }
        }
    }
}
