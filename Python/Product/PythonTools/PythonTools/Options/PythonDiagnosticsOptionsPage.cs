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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Options {
    [ComVisible(true)]
    public class PythonDiagnosticsOptionsPage : PythonDialogPage {
        private PythonDiagnosticsOptionsControl _window;

        // replace the default UI of the dialog page w/ our own UI.
        protected override IWin32Window Window {
            get {
                if (_window == null) {
                    _window = new PythonDiagnosticsOptionsControl();
                    _window.CopyToClipboard += CopyToClipboard;
                    _window.SaveToFile += SaveToFile;
                    LoadSettingsFromStorage();
                }
                return _window;
            }
        }

        /// <summary>
        /// Resets settings back to their defaults. This should be followed by
        /// a call to <see cref="SaveSettingsToStorage"/> to commit the new
        /// values.
        /// </summary>
        public override void ResetSettings() {
            PyService.DiagnosticsOptions.Reset();
        }

        public override void LoadSettingsFromStorage() {
            PyService.DiagnosticsOptions.Load();

            // Synchronize UI with backing properties.
            _window?.SyncControlWithPageSettings(PyService);
        }

        public override void SaveSettingsToStorage() {
            // Synchronize backing properties with UI.
            _window?.SyncPageWithControlSettings(PyService);

            PyService.DiagnosticsOptions.Save();
        }

        private void CopyToClipboard(bool includeAnalysisLogs) {
            Cursor.Current = Cursors.WaitCursor;
            try {
                using (var log = new StringWriter()) {
                    PyService.GetDiagnosticsLog(log, includeAnalysisLogs);
                    Clipboard.SetText(log.ToString(), TextDataFormat.Text);
                }
            } finally {
                Cursor.Current = Cursors.Arrow;
            }

            MessageBox.Show(Strings.DiagnosticsLogCopiedToClipboard, Strings.ProductTitle);
        }

        private void SaveToFile(bool includeAnalysisLogs) {
            string initialPath = null;
            try {
                initialPath = PathUtils.GetAbsoluteFilePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Strings.DiagnosticsWindow_DefaultFileName.FormatUI(DateTime.Now)
                );
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Debug.Fail(ex.ToUnhandledExceptionMessage(GetType()));
            }

            var path = Site.BrowseForFileSave(
                _window.Handle,
                Strings.DiagnosticsWindow_TextFileFilter,
                initialPath
            );

            if (string.IsNullOrEmpty(path)) {
                return;
            }

            Cursor.Current = Cursors.WaitCursor;
            try {
                try {
                    TaskDialog.CallWithRetry(
                        _ => {
                            using (var log = new StreamWriter(path, false, new UTF8Encoding(false))) {
                                PyService.GetDiagnosticsLog(log, includeAnalysisLogs);
                            }
                        },
                        Site,
                        Strings.ProductTitle,
                        Strings.FailedToSaveDiagnosticInfo,
                        Strings.ErrorDetail,
                        Strings.Retry,
                        Strings.Cancel
                    );

                    if (File.Exists(path)) {
                        Process.Start(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
                            "/select," + ProcessOutput.QuoteSingleArgument(path)
                        )?.Dispose();
                    }
                } catch (OperationCanceledException) {
                }
            } finally {
                Cursor.Current = Cursors.Arrow;
            }
        }
    }
}
