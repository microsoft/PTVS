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
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Language {
    sealed class TextViewFilter : IOleCommandTarget, IVsTextViewFilter {
        private readonly PythonEditorServices _editorServices;
        private readonly IVsDebugger _debugger;
        private readonly IOleCommandTarget _next;
        private readonly IVsTextLines _vsTextLines;
        private readonly IWpfTextView _wpfTextView;

        public TextViewFilter(PythonEditorServices editorServices, IVsTextView vsTextView) {
            _editorServices = editorServices;
            _debugger = (IVsDebugger)_editorServices.Site.GetService(typeof(IVsDebugger));

            vsTextView.GetBuffer(out _vsTextLines);
            _wpfTextView = _editorServices.EditorAdaptersFactoryService.GetWpfTextView(vsTextView);

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
            if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < cCmds; i++) {
                    switch((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.MarkerCmd0:
                        case VSConstants.VSStd97CmdID.MarkerCmd1:
                        case VSConstants.VSStd97CmdID.MarkerCmd2:
                        case VSConstants.VSStd97CmdID.MarkerCmd3:
                        case VSConstants.VSStd97CmdID.MarkerCmd4:
                        case VSConstants.VSStd97CmdID.MarkerCmd5:
                        case VSConstants.VSStd97CmdID.MarkerCmd6:
                        case VSConstants.VSStd97CmdID.MarkerCmd7:
                        case VSConstants.VSStd97CmdID.MarkerCmd8:
                        case VSConstants.VSStd97CmdID.MarkerCmd9:
                        case VSConstants.VSStd97CmdID.MarkerEnd:
                            // marker commands are broken on projection buffers, hide them.
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            }
            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText) {
            if (!_wpfTextView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                pbstrText = null;
                return VSConstants.E_NOTIMPL;
            }

            if (pSpan.Length != 1) {
                throw new ArgumentException("Array parameter should contain exactly one TextSpan", "pSpan");
            }

            // Adjust the span to expression boundaries.

            var snapshot = _wpfTextView.TextSnapshot;
            var pt = new SourceLocation(pSpan[0].iStartLine + 1, pSpan[0].iStartIndex + 1);

            var bi = PythonTextBufferInfo.TryGetForBuffer(snapshot.TextBuffer);
            var analyzer = bi?.AnalysisEntry?.Analyzer;
            if (analyzer == null) {
                pbstrText = null;
                return VSConstants.E_FAIL;
            }

            SourceSpan? expr;
            try {
                expr = _editorServices.Site.GetUIThread().InvokeTaskSync(async () => {
                    return await analyzer.GetExpressionSpanAtPointAsync(bi, pt, ExpressionAtPointPurpose.Hover, TimeSpan.FromSeconds(1.0));
                }, CancellationTokens.After1s);
            } catch (OperationCanceledException) {
                expr = null;
            }

            if (expr == null) {
                pbstrText = null;
                return VSConstants.E_ABORT;
            }

            var span = expr.Value.ToSnapshotSpan(snapshot);

            return _debugger.GetDataTipValue(_vsTextLines, pSpan, span.GetText(), out pbstrText);
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }
    }
}
