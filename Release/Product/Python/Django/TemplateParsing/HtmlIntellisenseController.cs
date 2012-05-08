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


using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class HtmlIntellisenseController : IIntellisenseController, IOleCommandTarget {
        private readonly HtmlIntellisenseControllerProvider _provider;
        private readonly ITextView _textView;
        private ICompletionSession _curSession;
        private IOleCommandTarget _oldTarget;

        public HtmlIntellisenseController(HtmlIntellisenseControllerProvider htmlIntellisenseControlerProvider, ITextView textView) {
            _provider = htmlIntellisenseControlerProvider;
            _textView = textView;
        }

        #region IIntellisenseController Members

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
        }

        public void Detach(ITextView textView) {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
        }

        #endregion

        internal ICompletionBroker CompletionBroker {
            get {
                return _provider._CompletionBroker;
            }
        }

        internal IVsEditorAdaptersFactoryService AdaptersFactory {
            get {
                return _provider._adaptersFactory;
            }
        }

        internal void AttachKeyboardFilter() {
            if (_oldTarget == null) {
                var viewAdapter = AdaptersFactory.GetViewAdapter(_textView);
                if (viewAdapter != null) {
                    ErrorHandler.ThrowOnFailure(viewAdapter.AddCommandFilter(this, out _oldTarget));
                }
            }
        }

        private void DetachKeyboardFilter() {
            if (_oldTarget != null) {
                ErrorHandler.ThrowOnFailure(AdaptersFactory.GetViewAdapter(_textView).RemoveCommandFilter(this));
                _oldTarget = null;
            }
        }

        #region IOleCommandTarget Members

        public int Exec(ref System.Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, System.IntPtr pvaIn, System.IntPtr pvaOut) {
            if (pguidCmdGroup == VSConstants.VSStd2K) {
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        var ch = (char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn);
                        if (_curSession != null && !_curSession.IsDismissed) {
                            if (_curSession.SelectedCompletionSet.SelectionStatus.IsSelected &&
                                IsCompletionChar(ch)) {
                                _curSession.Commit();
                            }
                        }
                        switch (ch) {
                            case '<':
                                if (_curSession != null) {
                                    _curSession.Dismiss();
                                }
                                _curSession = CompletionBroker.TriggerCompletion(_textView);
                                if (_curSession != null) {
                                    _curSession.Dismissed += CurSessionDismissedOrCommitted;
                                    _curSession.Committed += CurSessionDismissedOrCommitted;
                                }
                                break;
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                    case VSConstants.VSStd2KCmdID.TAB:
                        if (_curSession != null && !_curSession.IsDismissed) {
                            _curSession.Commit();
                            return VSConstants.S_OK;
                        }
                        break;
                }
            }

            return _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool IsCompletionChar(char ch) {
            return ch == '/' || ch == '>' || ch == '<' || ch == ';' || char.IsWhiteSpace(ch);
        }

        private static bool IsIdentifierChar(char ch) {
            return ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
        }

        void CurSessionDismissedOrCommitted(object sender, System.EventArgs e) {
            _curSession = null;
        }

        public int QueryStatus(ref System.Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, System.IntPtr pCmdText) {
            return _oldTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion
    }
}
