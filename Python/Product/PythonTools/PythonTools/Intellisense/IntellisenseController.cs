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
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudioTools.Project;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.PythonTools.Intellisense {

    internal sealed class IntellisenseController : IIntellisenseController, IOleCommandTarget {
        private readonly ITextView _textView;
        private readonly IntellisenseControllerProvider _provider;
        private readonly IIncrementalSearch _incSearch;
        private BufferParser _bufferParser;
        private ICompletionSession _activeSession;
        private ISignatureHelpSession _sigHelpSession;
        private IQuickInfoSession _quickInfoSession;
        private IOleCommandTarget _oldTarget;
        private IEditorOperations _editOps;

        /// <summary>
        /// Attaches events for invoking Statement completion 
        /// </summary>
        public IntellisenseController(IntellisenseControllerProvider provider, ITextView textView) {
            _textView = textView;
            _provider = provider;
            _editOps = provider._EditOperationsFactory.GetEditorOperations(textView);
            _incSearch = provider._IncrementalSearch.GetIncrementalSearch(textView);
            _textView.MouseHover += TextViewMouseHover;
            textView.Properties.AddProperty(typeof(IntellisenseController), this);  // added so our key processors can get back to us
        }

        internal void SetBufferParser(BufferParser bufferParser) {
            Utilities.CheckNotNull(bufferParser, "Cannot set buffer parser multiple times");
            _bufferParser = bufferParser;
        }

        private void TextViewMouseHover(object sender, MouseHoverEventArgs e) {
            if (_quickInfoSession != null && !_quickInfoSession.IsDismissed) {
                _quickInfoSession.Dismiss();
            }
            var pt = e.TextPosition.GetPoint(PythonCoreConstants.IsPythonContent, PositionAffinity.Successor);
            if (pt != null) {
                _quickInfoSession = _provider._QuickInfoBroker.TriggerQuickInfo(
                    _textView,
                    pt.Value.Snapshot.CreateTrackingPoint(pt.Value.Position, PointTrackingMode.Positive),
                    true);
            }
        }

        internal void TriggerQuickInfo() {
            if (_quickInfoSession != null && !_quickInfoSession.IsDismissed) {
                _quickInfoSession.Dismiss();
            }
            _quickInfoSession = _provider._QuickInfoBroker.TriggerQuickInfo(_textView);
        }

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            PropagateAnalyzer(subjectBuffer);

            Debug.Assert(_bufferParser != null, "SetBufferParser has not been called");
            BufferParser existingParser;
            if (!subjectBuffer.Properties.TryGetProperty(typeof(BufferParser), out existingParser)) {
                _bufferParser.AddBuffer(subjectBuffer);
            } else {
                // already connected to a buffer parser, we should have the same project entry
                Debug.Assert(_bufferParser._currentProjEntry == existingParser._currentProjEntry);
            }
        }

        public void PropagateAnalyzer(ITextBuffer subjectBuffer) {
            PythonReplEvaluator replEvaluator;
            if (_textView.Properties.TryGetProperty<PythonReplEvaluator>(typeof(PythonReplEvaluator), out replEvaluator)) {
                subjectBuffer.Properties.AddProperty(typeof(VsProjectAnalyzer), replEvaluator.ReplAnalyzer);
            }
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            // only disconnect if we own the buffer parser
            Debug.Assert(_bufferParser != null, "SetBufferParser has not been called");
            BufferParser existingParser;
            if (subjectBuffer.Properties.TryGetProperty<BufferParser>(typeof(BufferParser), out existingParser) &&
                --existingParser.AttachedViews == 0) {
                _bufferParser.RemoveBuffer(subjectBuffer);
            }
        }

        /// <summary>
        /// Detaches the events
        /// </summary>
        /// <param name="textView"></param>
        public void Detach(ITextView textView) {
            if (_textView == null) {
                throw new InvalidOperationException("Already detached from text view");
            }
            if (textView != _textView) {
                throw new ArgumentException("Not attached to specified text view", "textView");
            }

            _textView.MouseHover -= TextViewMouseHover;
            _textView.Properties.RemoveProperty(typeof(IntellisenseController));

            DetachKeyboardFilter();

            _bufferParser = null;
        }

        /// <summary>
        /// Triggers Statement completion when appropriate keys are pressed
        /// The key combination is CTRL-J or "."
        /// The intellisense window is dismissed when one presses ESC key
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnPreprocessKeyDown(object sender, TextCompositionEventArgs e) {
            // We should only receive pre-process events from our text view
            Debug.Assert(sender == _textView);

            // TODO: We should handle = for signature completion of keyword arguments

            string text = e.Text;
            if (text.Length == 1) {
                HandleChar(text[0]);
            }
        }

        private void HandleChar(char ch) {
            // We trigger completions when the user types . or space.  Called via our IOleCommandTarget filter
            // on the text view.
            //
            // We trigger signature help when we receive a "(".  We update our current sig when 
            // we receive a "," and we close sig help when we receive a ")".

            if (!_incSearch.IsActive) {
                switch (ch) {
                    case '@':
                    case '.':
                    case ' ':
                        if (PythonToolsPackage.Instance.LangPrefs.AutoListMembers) {
                            TriggerCompletionSession(false);
                        }
                        break;
                    case '(':
                        if (PythonToolsPackage.Instance.LangPrefs.AutoListParams) {
                            OpenParenStartSignatureSession();
                        }
                        break;
                    case ')':
                        if (_sigHelpSession != null) {
                            _sigHelpSession.Dismiss();
                            _sigHelpSession = null;
                        }

                        if (PythonToolsPackage.Instance.LangPrefs.AutoListParams) {
                            // trigger help for outer call if there is one
                            TriggerSignatureHelp();
                        }
                        break;
                    case '=':
                    case ',':
                        if (_sigHelpSession == null) {
                            if (PythonToolsPackage.Instance.LangPrefs.AutoListParams) {
                                CommaStartSignatureSession();
                            }
                        } else {
                            UpdateCurrentParameter();
                        }
                        break;
                }
            }
        }

        private bool Backspace() {
            if (_sigHelpSession != null) {
                if (_textView.Selection.IsActive && !_textView.Selection.IsEmpty) {
                    // when deleting a selection don't do anything to pop up signature help again
                    _sigHelpSession.Dismiss();
                    return false;
                }

                SnapshotPoint? caretPoint = _textView.BufferGraph.MapDownToFirstMatch(
                    _textView.Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    PythonCoreConstants.IsPythonContent,
                    PositionAffinity.Predecessor
                );

                if (caretPoint != null && caretPoint.Value.Position != 0) {
                    var deleting = caretPoint.Value.Snapshot[caretPoint.Value.Position - 1];
                    if (deleting == ',') {
                        caretPoint.Value.Snapshot.TextBuffer.Delete(new Span(caretPoint.Value.Position - 1, 1));
                        UpdateCurrentParameter();
                        return true;
                    } else if (deleting == '(' || deleting == ')') {
                        _sigHelpSession.Dismiss();
                        // delete the ( before triggering help again
                        caretPoint.Value.Snapshot.TextBuffer.Delete(new Span(caretPoint.Value.Position - 1, 1));

                        // Pop to an outer nesting of signature help
                        if (PythonToolsPackage.Instance.LangPrefs.AutoListParams) {
                            TriggerSignatureHelp();
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        private void OpenParenStartSignatureSession() {
            if (_activeSession != null) {
                _activeSession.Dismiss();
            }
            if (_sigHelpSession != null) {
                _sigHelpSession.Dismiss();
            }

            TriggerSignatureHelp();
        }

        private void CommaStartSignatureSession() {
            TriggerSignatureHelp();
        }

        /// <summary>
        /// Updates the current parameter for the caret's current position.
        /// 
        /// This will analyze the buffer for where we are currently located, find the current
        /// parameter that we're entering, and then update the signature.  If our current
        /// signature does not have enough parameters we'll find a signature which does.
        /// </summary>
        private void UpdateCurrentParameter() {
            if (_sigHelpSession == null) {
                // we moved out of the original span for sig help, re-trigger based upon the position
                TriggerSignatureHelp();
                return;
            }

            int position = _textView.Caret.Position.BufferPosition.Position;
            // we advance to the next parameter
            // TODO: Take into account params arrays
            // TODO: need to parse and see if we have keyword arguments entered into the current signature yet
            PythonSignature sig = _sigHelpSession.SelectedSignature as PythonSignature;
            if (sig != null) {
                var prevBuffer = sig.ApplicableToSpan.TextBuffer;
                var textBuffer = _textView.TextBuffer;

                var targetPt = _textView.BufferGraph.MapDownToFirstMatch(
                    new SnapshotPoint(_textView.TextBuffer.CurrentSnapshot, position),
                    PointTrackingMode.Positive,
                    PythonCoreConstants.IsPythonContent,
                    PositionAffinity.Successor
                );

                if (targetPt == null) {
                    return;
                }
                var span = targetPt.Value.Snapshot.CreateTrackingSpan(targetPt.Value.Position, 0, SpanTrackingMode.EdgeInclusive);

                var sigs = targetPt.Value.Snapshot.GetSignatures(span);
                bool retrigger = false;
                if (sigs.Signatures.Count == _sigHelpSession.Signatures.Count) {
                    for (int i = 0; i < sigs.Signatures.Count && !retrigger; i++) {
                        var leftSig = sigs.Signatures[i];
                        var rightSig = _sigHelpSession.Signatures[i];

                        if (leftSig.Parameters.Count == rightSig.Parameters.Count) {
                            for (int j = 0; j < leftSig.Parameters.Count; j++) {
                                var leftParam = leftSig.Parameters[j];
                                var rightParam = rightSig.Parameters[j];

                                if (leftParam.Name != rightParam.Name || leftParam.Documentation != rightParam.Documentation) {
                                    retrigger = true;
                                    break;
                                }
                            }
                        }

                        if (leftSig.Content != rightSig.Content || leftSig.Documentation != rightSig.Documentation) {
                            retrigger = true;
                        }
                    }
                } else {
                    retrigger = true;
                }

                if (retrigger) {
                    _sigHelpSession.Dismiss();
                    TriggerSignatureHelp();
                } else {
                    CommaFindBestSignature(sigs.ParameterIndex, sigs.LastKeywordArgument);
                }
            }
        }

        private void CommaFindBestSignature(int curParam, string lastKeywordArg) {
            // see if we have a signature which accomodates this...

            // TODO: We should also get the types of the arguments and use that to
            // pick the best signature when the signature includes types.
            var bestSig = _sigHelpSession.SelectedSignature as PythonSignature;
            if (bestSig != null) {
                for (int i = 0; i < bestSig.Parameters.Count; ++i) {
                    if (bestSig.Parameters[i].Name == lastKeywordArg ||
                        lastKeywordArg == null && (i == curParam || PythonSignature.IsParamArray(bestSig.Parameters[i].Name))
                    ) {
                        bestSig.SetCurrentParameter(bestSig.Parameters[i]);
                        _sigHelpSession.SelectedSignature = bestSig;
                        return;
                    }
                }
            }

            PythonSignature fallback = null;
            foreach (var sig in _sigHelpSession.Signatures.OfType<PythonSignature>().OrderBy(s => s.Parameters.Count)) {
                fallback = sig;
                for (int i = 0; i < sig.Parameters.Count; ++i) {
                    if (sig.Parameters[i].Name == lastKeywordArg ||
                        lastKeywordArg == null && (i == curParam || PythonSignature.IsParamArray(sig.Parameters[i].Name))
                    ) {
                        sig.SetCurrentParameter(sig.Parameters[i]);
                        _sigHelpSession.SelectedSignature = sig;
                        return;
                    }
                }
            }

            if (fallback != null) {
                fallback.SetCurrentParameter(null);
                _sigHelpSession.SelectedSignature = fallback;
            } else {
                _sigHelpSession.Dismiss();
            }
        }

        [ThreadStatic]
        internal static bool ForceCompletions;

        internal void TriggerCompletionSession(bool completeWord) {
            Dismiss();

            _activeSession = CompletionBroker.TriggerCompletion(_textView);

            if (_activeSession != null) {
                FuzzyCompletionSet set;
                if (completeWord &&
                    _activeSession.CompletionSets.Count == 1 &&
                    (set = _activeSession.CompletionSets[0] as FuzzyCompletionSet) != null &&
                    set.SelectSingleBest()) {
                    _activeSession.Commit();
                    _activeSession = null;
                } else {
                    _activeSession.Filter();
                    _activeSession.Dismissed += OnCompletionSessionDismissedOrCommitted;
                    _activeSession.Committed += OnCompletionSessionDismissedOrCommitted;
                }
            }
        }

        internal void TriggerSignatureHelp() {
            if (_sigHelpSession != null) {
                _sigHelpSession.Dismiss();
            }

            _sigHelpSession = SignatureBroker.TriggerSignatureHelp(_textView);

            if (_sigHelpSession != null) {
                _sigHelpSession.Dismissed += OnSignatureSessionDismissed;

                ISignature sig;
                if (_sigHelpSession.Properties.TryGetProperty(typeof(PythonSignature), out sig)) {
                    _sigHelpSession.SelectedSignature = sig;

                    IParameter param;
                    if (_sigHelpSession.Properties.TryGetProperty(typeof(PythonParameter), out param)) {
                        ((PythonSignature)sig).SetCurrentParameter(param);
                    }
                }
            }
        }

        private void OnCompletionSessionDismissedOrCommitted(object sender, System.EventArgs e) {
            // We've just been told that our active session was dismissed.  We should remove all references to it.
            _activeSession.Committed -= OnCompletionSessionDismissedOrCommitted;
            _activeSession.Dismissed -= OnCompletionSessionDismissedOrCommitted;
            _activeSession = null;
        }

        private void OnSignatureSessionDismissed(object sender, System.EventArgs e) {
            // We've just been told that our active session was dismissed.  We should remove all references to it.
            _sigHelpSession.Dismissed -= OnSignatureSessionDismissed;
            _sigHelpSession = null;
        }

        private void DeleteSelectedSpans() {
            if (!_textView.Selection.IsEmpty) {
                _editOps.Delete();
            }
        }

        private void Dismiss() {
            if (_activeSession != null) {
                _activeSession.Dismiss();
            }
        }

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

        internal ISignatureHelpBroker SignatureBroker {
            get {
                return _provider._SigBroker;
            }
        }

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

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (int)VSConstants.VSStd2KCmdID.TYPECHAR) {
                var ch = (char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn);
                
                if (_activeSession != null && !_activeSession.IsDismissed) {
                    if (_activeSession.SelectedCompletionSet.SelectionStatus.IsSelected &&
                        PythonToolsPackage.Instance.AdvancedEditorOptionsPage.CompletionCommittedBy.IndexOf(ch) != -1) {
                        _activeSession.Commit();
                    } else if (!IsIdentifierChar(ch)) {
                        _activeSession.Dismiss();
                    }
                }

                int res = _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                
                HandleChar((char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn));

                if (_activeSession != null && !_activeSession.IsDismissed) {
                    _activeSession.Filter();
                }

                return res;
            }

            if (_activeSession != null) {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.RETURN:
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
                            break;
                        case VSConstants.VSStd2KCmdID.TAB:
                            if (!_activeSession.IsDismissed) {
                                _activeSession.Commit();
                                return VSConstants.S_OK;
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                        case VSConstants.VSStd2KCmdID.DELETE:
                        case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                        case VSConstants.VSStd2KCmdID.DELETEWORDRIGHT:
                            int res = _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                            if (_activeSession != null && !_activeSession.IsDismissed) {
                                _activeSession.Filter();
                            }
                            return res;
                    }
                }
            } else if (_sigHelpSession != null) {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                            bool fDeleted = Backspace();
                            if (fDeleted) {
                                return VSConstants.S_OK;
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.LEFT:
                            _editOps.MoveToPreviousCharacter(false);
                            UpdateCurrentParameter();
                            return VSConstants.S_OK;
                        case VSConstants.VSStd2KCmdID.RIGHT:
                            _editOps.MoveToNextCharacter(false);
                            UpdateCurrentParameter();
                            return VSConstants.S_OK;
                        case VSConstants.VSStd2KCmdID.HOME:
                        case VSConstants.VSStd2KCmdID.BOL:
                        case VSConstants.VSStd2KCmdID.BOL_EXT:
                        case VSConstants.VSStd2KCmdID.EOL:
                        case VSConstants.VSStd2KCmdID.EOL_EXT:
                        case VSConstants.VSStd2KCmdID.END:
                        case VSConstants.VSStd2KCmdID.WORDPREV:
                        case VSConstants.VSStd2KCmdID.WORDPREV_EXT:
                        case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                            _sigHelpSession.Dismiss();
                            _sigHelpSession = null;
                            break;                        
                    }
                }
            }

            return _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private static bool IsIdentifierChar(char ch) {
            return ch == '_' || (ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9');
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

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            return _oldTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #endregion
    }
}

