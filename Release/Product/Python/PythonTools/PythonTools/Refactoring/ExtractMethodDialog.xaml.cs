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
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.PythonTools.Refactoring {
    /// <summary>
    /// Interaction logic for ExtractMethodDialog.xaml
    /// </summary>
    internal partial class ExtractMethodDialog : DialogWindowVersioningWorkaround {
        private bool _firstActivation;

        public ExtractMethodDialog(ExtractMethodRequestView viewModel) {
            InitializeComponent();

            DataContext = viewModel;

            _firstActivation = true;
        }

        protected override void OnActivated(System.EventArgs e) {
            base.OnActivated(e);
            if (_firstActivation) {
                _methodName.Focus();
                _methodName.SelectAll();
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
