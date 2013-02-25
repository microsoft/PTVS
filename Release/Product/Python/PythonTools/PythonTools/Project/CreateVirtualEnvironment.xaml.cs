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

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Interaction logic for LaunchProfiling.xaml
    /// </summary>
    internal partial class CreateVirtualEnvironment : DialogWindowVersioningWorkaround {
        private readonly CreateVirtualEnvironmentView _viewModel;

        public CreateVirtualEnvironment(CreateVirtualEnvironmentView viewModel) {
            _viewModel = viewModel;

            InitializeComponent();

            DataContext = _viewModel;
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void FindInterpreterLocationClick(object sender, RoutedEventArgs e) {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.SelectedPath = _viewModel.Location;
            var res = dlg.ShowDialog();
            if (res == System.Windows.Forms.DialogResult.OK) {
                _viewModel.Location = dlg.SelectedPath;
            }
        }

    }
}
