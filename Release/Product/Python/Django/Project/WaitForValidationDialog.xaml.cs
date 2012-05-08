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
using System.Threading;
using System.Windows;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.PlatformUI;
using System.Windows.Threading;
using System.Diagnostics;

namespace Microsoft.PythonTools.Django.Project {
    /// <summary>
    /// Interaction logic for WaitForCompleteAnalysisDialog.xaml
    /// </summary>
    partial class WaitForValidationDialog : DialogWindow {
        private Process _proc;

        public WaitForValidationDialog(Process process, string title) {
            _proc = process;
            InitializeComponent();
            Title = title;
        }

        public void SetText(string text) {
            _textBox.Text = text;
        }

        public void EnableOk() {
            _ok.IsEnabled = true;
            _cancel.IsEnabled = false;
        }

        private void _okButton_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void _cancelButton_Click(object sender, RoutedEventArgs e) {
            if (!_proc.HasExited) {
                try {
                    _proc.Kill();
                } catch(InvalidOperationException) {
                }
            }

            this.DialogResult = false;
            this.Close();
        }
    }
}
