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
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Interaction logic for DiagnosticsWindow.xaml
    /// </summary>
    internal partial class DiagnosticsWindow : DialogWindowVersioningWorkaround {
        private readonly IServiceProvider _provider;
        private Task<string> _info;

        public DiagnosticsWindow() {
            InitializeComponent();
        }

        public DiagnosticsWindow(IServiceProvider provider, Task<string> info) : this() {
            _provider = provider;

            Cursor = Cursors.Wait;
            ForceCursor = true;

            _info = info;
            _info.ContinueWith(InfoTask_Complete);
        }

        protected override void OnClosed(EventArgs e) {
            base.OnClosed(e);
            _info = null;
        }

        private void InfoTask_Complete(Task<string> task) {
            // Silence all exceptions
            var ex = task.Exception;

            if (_info == null) {
                return;
            }

            if (!Dispatcher.CheckAccess()) {
                Dispatcher.InvokeAsync(() => InfoTask_Complete(task)).Task.DoNotWait();
                return;
            }

            Cursor = Cursors.Arrow;
            ForceCursor = false;

            if (task.IsCanceled) {
                return;
            }

            InfoTextBox.Text = Data;
            Mouse.UpdateCursor();
            CommandManager.InvalidateRequerySuggested();
        }

        private string Data {
            get {
                if (IsRunning) {
                    return null;
                }
                if (_info.Exception != null) {
                    return _info.Exception.ToUnhandledExceptionMessage(GetType());
                }
                return _info.Result;
            }
        }

        private bool IsRunning => _info != null && !(_info.IsCompleted || _info.IsCanceled || _info.IsFaulted);

        private void Save_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = !IsRunning;
            e.Handled = true;
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e) {
            var path = _provider.BrowseForFileSave(
                new WindowInteropHelper(this).Handle,
                Strings.DiagnosticsWindow_TextFileFilter,
                PathUtils.GetAbsoluteFilePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    Strings.DiagnosticsWindow_DefaultFileName.FormatUI(DateTime.Now)
                )
            );

            if (string.IsNullOrEmpty(path)) {
                return;
            }

            try {
                TaskDialog.CallWithRetry(
                    _ => File.WriteAllText(path, _info.Result),
                    _provider,
                    Strings.ProductTitle,
                    Strings.FailedToSaveDiagnosticInfo,
                    Strings.ErrorDetail,
                    Strings.Retry,
                    Strings.Cancel
                );

                Process.Start("explorer.exe", "/select," + ProcessOutput.QuoteSingleArgument(path)).Dispose();
            } catch (OperationCanceledException) {
            }
        }

        private void Copy_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = !IsRunning;
            e.Handled = true;
        }

        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e) {
            Clipboard.SetText(Data);
        }

        private void Close_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e) {
            Close();
        }
    }
}
