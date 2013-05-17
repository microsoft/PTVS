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
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Interaction logic for InstallPythonPackage.xaml
    /// </summary>
    partial class InstallPythonPackage : DialogWindowVersioningWorkaround {
        private readonly InstallPythonPackageView _view;
        public InstallPythonPackage(InstallPythonPackageView view) {
            _view = view;

            InitializeComponent();
            
            DataContext = view;

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
