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
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Profiling {
    /// <summary>
    /// Interaction logic for CompareReportsWindow.xaml
    /// </summary>
    public partial class CompareReportsWindow : Window {
        public CompareReportsWindow() {
            InitializeComponent();
        }

        public CompareReportsWindow(string baselineFile)
            : this() {
            _baselineFile.Text = baselineFile;
        }

        private void OkClick(object sender, RoutedEventArgs e) {
            if (!File.Exists(_baselineFile.Text)) {
                MessageBox.Show(String.Format("{0} does not exist, correct the filename or select Cancel.", _baselineFile.Text));
            } else if (!File.Exists(_comparisonFile.Text)) {
                MessageBox.Show(String.Format("{0} does not exist, correct the filename or select Cancel.", _comparisonFile.Text));
            } else {
                DialogResult = true;
                Close();
            }
        }

        private void CancelClick(object sender, RoutedEventArgs e) {
            Close();
        }

        private string OpenFileDialog() {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = PythonProfilingPackage.PerformanceFileFilter;
            dialog.CheckFileExists = true;
            var res = dialog.ShowDialog();
            if (res != null && res.Value) {
                return dialog.FileName;
            }
            return null;
        }

        private void BaselineBrowseClick(object sender, RoutedEventArgs e) {
            _baselineFile.Text = OpenFileDialog() ?? _baselineFile.Text;
        }

        private void CompareBrowseClick(object sender, RoutedEventArgs e) {
            _comparisonFile.Text = OpenFileDialog() ?? _comparisonFile.Text;
        }

        public string ComparisonUrl {
            get {
                return String.Format(
                    "vsp://diff/?baseline={0}&comparison={1}",
                    Uri.EscapeDataString(_baselineFile.Text),
                    Uri.EscapeDataString(_comparisonFile.Text)
                );
            }
        }
    }
}
