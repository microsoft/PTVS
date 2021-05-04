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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Navigation {
    sealed class TextViewFilter : IOleCommandTarget, IVsTextViewFilter {
        private readonly IOleCommandTarget _next;

        public TextViewFilter(IVsTextView vsTextView) {
            ErrorHandler.ThrowOnFailure(vsTextView.AddCommandFilter(this, out _next));
        }

        #region IOleCommandTarget Members

        /// <summary>
        /// Called from VS when we should handle a command or pass it on.
        /// </summary>
        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            return _next.Exec(pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        /// <summary>
        /// Called from VS to see what commands we support.  
        /// </summary>
        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == Common.CommonGuidList.guidPythonToolsCmdSet) {
                for (uint i = 0; i < cCmds; i++) {
                    lock (CommonPackage.CommandsLock) {
                        foreach (var command in CommonPackage.Commands.Keys) {
                            if (command.CommandId == prgCmds[i].cmdID) {
                                int? res = command.EditFilterQueryStatus(ref prgCmds[i], pCmdText);
                                if (res != null) {
                                    return res.Value;
                                }
                            }
                        }
                    }
                }
            }
            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText) {
            pbstrText = null;

            // TODO: Check debugger datatips work
            return VSConstants.E_NOTIMPL;
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }
    }
}
