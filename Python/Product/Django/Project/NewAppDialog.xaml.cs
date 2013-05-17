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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.VisualStudio.PlatformUI;

namespace Microsoft.PythonTools.Django.Project {
    /// <summary>
    /// Interaction logic for NewAppDialog.xaml
    /// </summary>
    partial class NewAppDialog : DialogWindowVersioningWorkaround {
        private readonly NewAppDialogViewModel _viewModel;

        public NewAppDialog() {
            InitializeComponent();

            DataContext = _viewModel = new NewAppDialogViewModel();

            _newAppName.Focus();
        }

        internal NewAppDialogViewModel ViewModel {
            get {
                return _viewModel;
            }
        }

        private void _ok_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void _cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }
    }
}
