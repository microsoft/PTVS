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
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Language {
    /// <summary>
    /// IVsTextViewFilter is implemented to statisfy new VS2012 requirement for debugger tooltips.
    /// Do not use this from VS2010, it will break debugger tooltips!
    /// </summary>
    public sealed class TextViewFilter : IOleCommandTarget, IVsTextViewFilter {
        private IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;
        private IVsDebugger _debugger;
        private readonly IOleCommandTarget _next;
        private readonly IVsTextLines _vsTextLines;
        private readonly IWpfTextView _wpfTextView;

        public TextViewFilter(IServiceProvider serviceProvider, IVsTextView vsTextView) {
            var compModel = (IComponentModel)serviceProvider.GetService(typeof(SComponentModel));
            _vsEditorAdaptersFactoryService = compModel.GetService<IVsEditorAdaptersFactoryService>();
            _debugger = (IVsDebugger)serviceProvider.GetService(typeof(IVsDebugger));

            vsTextView.GetBuffer(out _vsTextLines);
            _wpfTextView = _vsEditorAdaptersFactoryService.GetWpfTextView(vsTextView);

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
            var start = LineAndColumnNumberToSnapshotPoint(snapshot, pSpan[0].iStartLine, pSpan[0].iStartIndex);
            var end = LineAndColumnNumberToSnapshotPoint(snapshot, pSpan[0].iEndLine, pSpan[0].iEndIndex);

            // If this is a zero-length span (which it usually is, unless there's selection), adjust it
            // to cover one char to the right, since an empty span at the beginning of the expression does
            // not count as belonging to that expression;
            if (start == end && start.Position != snapshot.Length) {
                end += 1;
            }

            var snapshotSpan = new SnapshotSpan(start, end);
            var trackingSpan = snapshot.CreateTrackingSpan(snapshotSpan.Span, SpanTrackingMode.EdgeExclusive);
            var rep = new ReverseExpressionParser(snapshot, _wpfTextView.TextBuffer, trackingSpan);
            var exprSpan = rep.GetExpressionRange(forCompletion: false);
            if (exprSpan != null) {
                SnapshotPointToLineAndColumnNumber(exprSpan.Value.Start, out pSpan[0].iStartLine, out pSpan[0].iStartIndex);
                SnapshotPointToLineAndColumnNumber(exprSpan.Value.End, out pSpan[0].iEndLine, out pSpan[0].iEndIndex);
            } else {
                // If it's not an expression, suppress the tip.
                pbstrText = null;
                return VSConstants.E_FAIL;
            }

            return _debugger.GetDataTipValue(_vsTextLines, pSpan, null, out pbstrText);
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan) {
            return VSConstants.E_NOTIMPL;
        }

        private static SnapshotPoint LineAndColumnNumberToSnapshotPoint(ITextSnapshot snapshot, int lineNumber, int columnNumber) {
            var line = snapshot.GetLineFromLineNumber(lineNumber);
            var snapshotPoint = new SnapshotPoint(snapshot, line.Start + columnNumber);
            return snapshotPoint;
        }

        private static void SnapshotPointToLineAndColumnNumber(SnapshotPoint snapshotPoint, out int lineNumber, out int columnNumber) {
            var line = snapshotPoint.GetContainingLine();
            lineNumber = line.LineNumber;
            columnNumber = snapshotPoint.Position - line.Start.Position;
        }
    }
}
