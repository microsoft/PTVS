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
using Microsoft.PythonTools.Django.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Django.Intellisense {
    class DjangoIntellisenseController : IIntellisenseController, IOleCommandTarget {
        private readonly DjangoIntellisenseControllerProvider _provider;
        private readonly ITextView _textView;
        private IOleCommandTarget _oldTarget;
        private ICompletionSession _activeSession;

        public DjangoIntellisenseController(DjangoIntellisenseControllerProvider intellisenseControllerProvider, ITextView textView) {
            _provider = intellisenseControllerProvider;
            _textView = textView;
            AttachKeyboardFilter();
        }

        #region IIntellisenseController Members

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            DjangoProject project;
            if (_textView.Properties.TryGetProperty<DjangoProject>(typeof(DjangoProject), out project)) {
                subjectBuffer.Properties.AddProperty(typeof(DjangoProject), project);
            }
        }

        public void Detach(ITextView textView) {
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            subjectBuffer.Properties.RemoveProperty(typeof(DjangoProject));
        }

        #endregion

        #region IOleCommandTarget Members

        // we need this because VS won't give us certain keyboard events as they're handled before our key processor.  These
        // include enter and tab both of which we want to complete.

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

        #endregion

        internal IVsEditorAdaptersFactoryService AdaptersFactory {
            get {
                return _provider._adaptersFactory;
            }
        }

        internal ICompletionBroker CompletionBroker {
            get {
                return _provider._broker;
            }
        }

        private void Dismiss() {
            if (_activeSession != null) {
                _activeSession.Dismiss();
            }
        }

        internal void TriggerCompletionSession(bool completeWord) {
            Dismiss();

            _activeSession = CompletionBroker.TriggerCompletion(_textView);

            if (_activeSession != null) {
                if (completeWord &&
                    _activeSession.CompletionSets.Count == 1 &&
                    _activeSession.CompletionSets[0].Completions.Count == 1) {
                    _activeSession.Commit();
                    _activeSession = null;
                } else {
                    _activeSession.Dismissed += OnCompletionSessionDismissedOrCommitted;
                    _activeSession.Committed += OnCompletionSessionDismissedOrCommitted;
                }
            }
        }

        private void OnCompletionSessionDismissedOrCommitted(object sender, System.EventArgs e) {
            // We've just been told that our active session was dismissed.  We should remove all references to it.
            _activeSession.Committed -= OnCompletionSessionDismissedOrCommitted;
            _activeSession.Dismissed -= OnCompletionSessionDismissedOrCommitted;
            _activeSession = null;
        }

        #region IOleCommandTarget Members

        public int Exec(ref System.Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, System.IntPtr pvaIn, System.IntPtr pvaOut) {
            if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                    case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                    case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                    //case VSConstants.VSStd2KCmdID.PARAMINFO:
                        DjangoIntellisenseController controller;
                        if (_textView.Properties.TryGetProperty<DjangoIntellisenseController>(typeof(DjangoIntellisenseController), out controller)) {
                            controller.TriggerCompletionSession((VSConstants.VSStd2KCmdID)nCmdID == VSConstants.VSStd2KCmdID.COMPLETEWORD);
                            return VSConstants.S_OK;
                        }
                        return VSConstants.S_OK;
                    case VSConstants.VSStd2KCmdID.TAB:
                        if (_activeSession != null && !_activeSession.IsDismissed) {
                            _activeSession.Commit();
                            return VSConstants.S_OK;
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.RETURN:
                        if (_activeSession != null) {
                            if (PythonToolsPackage.Instance.AdvancedEditorOptionsPage.EnterCommitsIntellisense &&
                                !_activeSession.IsDismissed &&
                                _activeSession.SelectedCompletionSet.SelectionStatus.IsSelected) {

                                // If the user has typed all of the characters as the completion and presses
                                // enter we should dismiss & let the text editor receive the enter.  For example 
                                // when typing "import sys[ENTER]" completion starts after the space.  After typing
                                // sys the user wants a new line and doesn't want to type enter twice.

                                bool enterOnComplete = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.AddNewLineAtEndOfFullyTypedWord &&
                                         EnterOnCompleteText();

                                _activeSession.Commit();

                                if (!enterOnComplete) {
                                    return VSConstants.S_OK;
                                }
                            } else {
                                _activeSession.Dismiss();
                            }
                        }
                        break;
                    case VSConstants.VSStd2KCmdID.TYPECHAR:
                        var ch = (char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn);
                
                        if (_activeSession != null && !_activeSession.IsDismissed) {
                            if (_activeSession.SelectedCompletionSet.SelectionStatus.IsSelected &&
                                PythonToolsPackage.Instance.AdvancedEditorOptionsPage.CompletionCommittedBy.IndexOf(ch) != -1) {
                                _activeSession.Commit();
                            } else if (!IsIdentifierChar(ch)) {
                                _activeSession.Dismiss();
                            }
                        }

                        if (PythonToolsPackage.Instance.AutoListMembers &&
                            (ch == '.' || ch == ' ' || ch == '|')) {
                            // insert the ., then trigger...
                            int res = _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                            if (ErrorHandler.Succeeded(res)) {
                                if (_activeSession != null && !_activeSession.IsDismissed) {
                                    _activeSession.Dismiss();
                                }

                                TriggerCompletionSession(false);
                                return VSConstants.S_OK;
                            }
                        }
                        break;

                }
            }
            return _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool EnterOnCompleteText() {
            SnapshotPoint? point = _activeSession.GetTriggerPoint(_textView.TextBuffer.CurrentSnapshot);
            if (point.HasValue) {
                int chars = _textView.Caret.Position.BufferPosition.Position - point.Value.Position;
                var selectionStatus = _activeSession.SelectedCompletionSet.SelectionStatus;
                if (chars == selectionStatus.Completion.InsertionText.Length) {
                    string text = _textView.TextSnapshot.GetText(point.Value.Position, chars);

                    if (String.Compare(text, selectionStatus.Completion.InsertionText, true) == 0) {
                        return true;
                    }
                }
            }

            return false;
        }
        private static bool IsIdentifierChar(char ch) {
            return ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
        }

        public int QueryStatus(ref System.Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, System.IntPtr pCmdText) {
            if (pguidCmdGroup == CommonConstants.Std2KCmdGroupGuid) {
                for (int i = 0; i < cCmds; i++) {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd2KCmdID.SHOWMEMBERLIST:
                        case VSConstants.VSStd2KCmdID.COMPLETEWORD:
                        //case VSConstants.VSStd2KCmdID.PARAMINFO:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            return VSConstants.S_OK;

                    }
                }
            }
            return _oldTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion
    }
}
