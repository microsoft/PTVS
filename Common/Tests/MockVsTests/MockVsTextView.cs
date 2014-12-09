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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using TestUtilities.Mocks;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.VisualStudioTools.MockVsTests {
    public class MockVsTextView : IVsTextView, IFocusable {
        private readonly MockTextView _view;
        private readonly IEditorOperations _editorOps;
        private readonly IServiceProvider _serviceProvider;
        private IOleCommandTarget _commandTarget;

        public MockVsTextView(IServiceProvider serviceProvier, MockTextView view) {
            _view = view;
            _serviceProvider = serviceProvier;
            var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
            var editorOpsFact = compModel.GetService<IEditorOperationsFactoryService>();
            _editorOps = editorOpsFact.GetEditorOperations(_view);
            _commandTarget = new EditorCommandTarget(this);
        }

        public MockTextView View {
            get {
                return _view;
            }
        }

        public IIntellisenseSessionStack IntellisenseSessionStack {
            get {
                var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
                var stackMap = compModel.GetService<IIntellisenseSessionStackMapService>();
                return stackMap.GetStackForTextView(View);
            }
        }

        public IIntellisenseSession TopSession {
            get {
                return IntellisenseSessionStack.TopSession;
            }
        }

        public string Text {
            get {
                return View.TextBuffer.CurrentSnapshot.GetText();
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        struct VARIANT {
            [FieldOffset(0)]
            public ushort vt;
            [FieldOffset(8)]
            public IntPtr pdispVal;
            [FieldOffset(8)]
            public byte ui1;
            [FieldOffset(8)]
            public ushort ui2;
            [FieldOffset(8)]
            public uint ui4;
            [FieldOffset(8)]
            public ulong ui8;
            [FieldOffset(8)]
            public float r4;
            [FieldOffset(8)]
            public double r8;
        }

        public void Enter() {
            var guid = VSConstants.VSStd2K;
            _commandTarget.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.RETURN, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void Tab() {
            var guid = VSConstants.VSStd2K;
            _commandTarget.Exec(ref guid, (int)VSConstants.VSStd2KCmdID.TAB, 0, IntPtr.Zero, IntPtr.Zero);
        }

        public void Type(string text) {
            var guid = VSConstants.VSStd2K;
            var variantMem = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(VARIANT)));
            try {
                for (int i = 0; i < text.Length; i++) {
                    switch(text[i]) {
                        case '\r': Enter(); break;
                        case '\t': Tab(); break;
                        default:
                            Marshal.GetNativeVariantForObject((ushort)text[i], variantMem);
                            _commandTarget.Exec(
                                ref guid,
                                (int)VSConstants.VSStd2KCmdID.TYPECHAR,
                                0,
                                variantMem,
                                IntPtr.Zero
                            );
                            break;
                    }

                }
            } finally {
                Marshal.FreeCoTaskMem(variantMem);
            }
        }

        int IVsTextView.AddCommandFilter(IOleCommandTarget pNewCmdTarg, out IOleCommandTarget ppNextCmdTarg) {
            ppNextCmdTarg = _commandTarget;
            _commandTarget = pNewCmdTarg;
            return VSConstants.S_OK;
        }

        int IVsTextView.CenterColumns(int iLine, int iLeftCol, int iColCount) {
            throw new NotImplementedException();
        }

        int IVsTextView.CenterLines(int iTopLine, int iCount) {
            throw new NotImplementedException();
        }

        int IVsTextView.ClearSelection(int fMoveToAnchor) {
            throw new NotImplementedException();
        }

        int IVsTextView.CloseView() {
            throw new NotImplementedException();
        }

        int IVsTextView.EnsureSpanVisible(TextSpan span) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetBuffer(out IVsTextLines ppBuffer) {
            var compModel = (IComponentModel)_serviceProvider.GetService(typeof(SComponentModel));
            ppBuffer = (IVsTextLines)compModel.GetService<IVsEditorAdaptersFactoryService>().GetBufferAdapter(_view.TextBuffer);
            return VSConstants.S_OK;
        }

        int IVsTextView.GetCaretPos(out int piLine, out int piColumn) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetLineAndColumn(int iPos, out int piLine, out int piIndex) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetLineHeight(out int piLineHeight) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetNearestPosition(int iLine, int iCol, out int piPos, out int piVirtualSpaces) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetPointOfLineColumn(int iLine, int iCol, VisualStudio.OLE.Interop.POINT[] ppt) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetScrollInfo(int iBar, out int piMinUnit, out int piMaxUnit, out int piVisibleUnits, out int piFirstVisibleUnit) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetSelectedText(out string pbstrText) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetSelection(out int piAnchorLine, out int piAnchorCol, out int piEndLine, out int piEndCol) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetSelectionDataObject(out VisualStudio.OLE.Interop.IDataObject ppIDataObject) {
            throw new NotImplementedException();
        }

        TextSelMode IVsTextView.GetSelectionMode() {
            throw new NotImplementedException();
        }

        int IVsTextView.GetSelectionSpan(TextSpan[] pSpan) {
            throw new NotImplementedException();
        }

        int IVsTextView.GetTextStream(int iTopLine, int iTopCol, int iBottomLine, int iBottomCol, out string pbstrText) {
            throw new NotImplementedException();
        }

        IntPtr IVsTextView.GetWindowHandle() {
            throw new NotImplementedException();
        }

        int IVsTextView.GetWordExtent(int iLine, int iCol, uint dwFlags, TextSpan[] pSpan) {
            throw new NotImplementedException();
        }

        int IVsTextView.HighlightMatchingBrace(uint dwFlags, uint cSpans, TextSpan[] rgBaseSpans) {
            throw new NotImplementedException();
        }

        int IVsTextView.Initialize(IVsTextLines pBuffer, IntPtr hwndParent, uint InitFlags, INITVIEW[] pInitView) {
            throw new NotImplementedException();
        }

        int IVsTextView.PositionCaretForEditing(int iLine, int cIndentLevels) {
            throw new NotImplementedException();
        }

        int IVsTextView.RemoveCommandFilter(VisualStudio.OLE.Interop.IOleCommandTarget pCmdTarg) {
            throw new NotImplementedException();
        }

        int IVsTextView.ReplaceTextOnLine(int iLine, int iStartCol, int iCharsToReplace, string pszNewText, int iNewLen) {
            throw new NotImplementedException();
        }

        int IVsTextView.RestrictViewRange(int iMinLine, int iMaxLine, IVsViewRangeClient pClient) {
            throw new NotImplementedException();
        }

        int IVsTextView.SendExplicitFocus() {
            throw new NotImplementedException();
        }

        int IVsTextView.SetBuffer(IVsTextLines pBuffer) {
            throw new NotImplementedException();
        }

        int IVsTextView.SetCaretPos(int iLine, int iColumn) {
            throw new NotImplementedException();
        }

        int IVsTextView.SetScrollPosition(int iBar, int iFirstVisibleUnit) {
            throw new NotImplementedException();
        }

        int IVsTextView.SetSelection(int iAnchorLine, int iAnchorCol, int iEndLine, int iEndCol) {
            throw new NotImplementedException();
        }

        int IVsTextView.SetSelectionMode(TextSelMode iSelMode) {
            throw new NotImplementedException();
        }

        int IVsTextView.SetTopLine(int iBaseLine) {
            throw new NotImplementedException();
        }

        int IVsTextView.UpdateCompletionStatus(IVsCompletionSet pCompSet, uint dwFlags) {
            throw new NotImplementedException();
        }

        int IVsTextView.UpdateTipWindow(IVsTipWindow pTipWindow, uint dwFlags) {
            throw new NotImplementedException();
        }

        int IVsTextView.UpdateViewFrameCaption() {
            throw new NotImplementedException();
        }

        class EditorCommandTarget : IOleCommandTarget {
            private readonly MockVsTextView _view;

            public EditorCommandTarget(MockVsTextView view) {
                _view = view;
            }

            public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.TYPECHAR:
                            var ch = (char)(ushort)Marshal.GetObjectForNativeVariant(pvaIn);
                            _view._editorOps.InsertText(ch.ToString());
                            return VSConstants.S_OK;
                        case VSConstants.VSStd2KCmdID.RETURN:
                            _view._editorOps.InsertNewLine();
                            return VSConstants.S_OK;
                    }
                }
                return VSConstants.E_FAIL;
            }

            public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
                throw new NotImplementedException();
            }
        }

        public void GetFocus() {
            _view.OnGotAggregateFocus();
        }

        public void LostFocus() {
            _view.OnLostAggregateFocus();
        }
    }
}
