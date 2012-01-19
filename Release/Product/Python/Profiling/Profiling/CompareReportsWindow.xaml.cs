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
using System.Windows;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for CompareReportsWindow.xaml
    /// </summary>
    public partial class CompareReportsWindow : DialogWindow {
        private readonly CompareReportsView _viewModel;
        
        public CompareReportsWindow(CompareReportsView viewModel) {
            _viewModel = viewModel;

            InitializeComponent();

            DataContext = viewModel;
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private string OpenFileDialog() {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = _viewModel.PerformanceFileFilter;
            dialog.CheckFileExists = true;
            bool res = dialog.ShowDialog() ?? false;
            if (res) {
                return dialog.FileName;
            }
            return null;
        }

        private void BaselineBrowseClick(object sender, RoutedEventArgs e) {
            var newFile = OpenFileDialog();
            if (!string.IsNullOrEmpty(newFile)) {
                _viewModel.BaselineFile = newFile;
            }
        }

        private void CompareBrowseClick(object sender, RoutedEventArgs e) {
            var newFile = OpenFileDialog();
            if (!string.IsNullOrEmpty(newFile)) {
                _viewModel.ComparisonFile = newFile;
            }
        }
    }
}
