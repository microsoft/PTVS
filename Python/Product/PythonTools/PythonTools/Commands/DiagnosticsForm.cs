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
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Commands {
    public partial class DiagnosticsForm : Form {
        public DiagnosticsForm(string content) {
            InitializeComponent();
            _textBox.Text = content;
            _copy.Enabled = false;
            _save.Enabled = false;
            UseWaitCursor = true;
        }

        public void Ready(string content) {
            if (IsDisposed) {
                return;
            }

            _textBox.Text = content;
            _copy.Enabled = true;
            _save.Enabled = true;
            UseWaitCursor = false;
        }

        private void _ok_Click(object sender, EventArgs e) {
            Close();
        }

        private void _copy_Click(object sender, EventArgs e) {
            Clipboard.SetText(_textBox.Text);
        }

        private void _save_Click(object sender, EventArgs e) {
            var path = PythonToolsPackage.Instance.BrowseForFileSave(
                Handle,
                "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                CommonUtils.GetAbsoluteFilePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    string.Format("Diagnostic Info {0:yyyy-MM-dd'T'HHmmss}.txt", DateTime.Now)
                )
            );

            if (string.IsNullOrEmpty(path)) {
                return;
            }

            try {
                TaskDialog.CallWithRetry(
                    _ => File.WriteAllText(path, _textBox.Text),
                    PythonToolsPackage.Instance,
                    SR.ProductName,
                    SR.GetString(SR.FailedToSaveDiagnosticInfo),
                    SR.GetString(SR.ErrorDetail),
                    SR.GetString(SR.Retry),
                    SR.GetString(SR.Cancel)
                );

                Process.Start("explorer.exe", "/select," + ProcessOutput.QuoteSingleArgument(path));
            } catch (OperationCanceledException) {
            }
        }
    }
}
