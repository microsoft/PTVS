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

using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to send selected text from a buffer to the remote REPL window.
    /// </summary>
    class RemoveImportsCommand : Command {
        private readonly System.IServiceProvider _serviceProvider;
        private readonly bool _allScopes;

        public RemoveImportsCommand(System.IServiceProvider serviceProvider, bool allScopes) {
            _serviceProvider = serviceProvider;
            _allScopes = allScopes;
        }

        public override async void DoCommand(object sender, EventArgs args) {
            var view = CommonPackage.GetActiveTextView(_serviceProvider);
            var analyzer = view?.GetAnalyzerAtCaret(_serviceProvider);

            if (analyzer == null) {
                // Can sometimes race with initializing the analyzer (probably
                // only in tests), so delay slightly until we get an analyzer
                for (int retries = 10; retries > 0 && analyzer == null; --retries) {
                    await Task.Delay(10);
                    view = CommonPackage.GetActiveTextView(_serviceProvider);
                    analyzer = view?.GetAnalyzerAtCaret(_serviceProvider);
                }
            }

            var pythonCaret = view?.GetPythonCaret();
            if (analyzer == null || !pythonCaret.HasValue) {
                Debug.Fail("Executed RemoveImportsCommand with invalid view");
                return;
            }

            await analyzer.RemoveImportsAsync(pythonCaret.Value, _allScopes);
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var view = CommonPackage.GetActiveTextView(_serviceProvider);
            var analyzer = view?.GetAnalyzerAtCaret(_serviceProvider);
            var pythonCaret = view?.GetPythonCaret();
            if (view != null && analyzer != null && pythonCaret.HasValue) {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = true;
                };
            }
        }

        public override int CommandId {
            get {
                return _allScopes ? (int)PkgCmdIDList.cmdidRemoveImports : (int)PkgCmdIDList.cmdidRemoveImportsCurrentScope;
            }
        }
    }
}
