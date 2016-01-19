// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;

namespace Microsoft.PythonTools.Commands {
    public partial class DiagnosticsForm : Form {
        private readonly IServiceProvider _provider;

        [Obsolete("Use IServiceProvider overload")]
        public DiagnosticsForm(string content)
#pragma warning disable 0618
            : this(PythonToolsPackage.Instance, content) {
#pragma warning restore 0618
        }

        public DiagnosticsForm(IServiceProvider serviceProvider, string content) {
            _provider = serviceProvider;
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
            var path = _provider.BrowseForFileSave(
                Handle,
                "Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                PathUtils.GetAbsoluteFilePath(
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
                    _provider,
                    Strings.ProductTitle,
                    Strings.FailedToSaveDiagnosticInfo,
                    Strings.ErrorDetail,
                    Strings.Retry,
                    Strings.Cancel
                );

                Process.Start("explorer.exe", "/select," + ProcessOutput.QuoteSingleArgument(path));
            } catch (OperationCanceledException) {
            }
        }
    }
}
