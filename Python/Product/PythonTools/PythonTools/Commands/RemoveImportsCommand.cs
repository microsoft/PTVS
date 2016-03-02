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
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

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
            var analyzer = view.GetAnalyzer(_serviceProvider);

            await analyzer.RemoveImports(view.TextBuffer, _allScopes);
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView(_serviceProvider);
            if (activeView != null && activeView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {                
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
