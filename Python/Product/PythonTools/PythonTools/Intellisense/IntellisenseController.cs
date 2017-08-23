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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.IncrementalSearch;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.TextManager.Interop;
using IServiceProvider = System.IServiceProvider;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.PythonTools.Intellisense {

    internal sealed class IntellisenseController : IIntellisenseController, IOleCommandTarget {
        private readonly PythonEditorServices _services;
        private readonly ITextView _textView;
        private readonly IntellisenseControllerProvider _provider;
        private readonly IIncrementalSearch _incSearch;
        private readonly ExpansionClient _expansionClient;
        private readonly IVsExpansionManager _expansionMgr;
        private ICompletionSession _activeSession;
        private ISignatureHelpSession _sigHelpSession;
        private IQuickInfoSession _quickInfoSession;
        internal IOleCommandTarget _oldTarget;
        private IEditorOperations _editOps;
        private static readonly string[] _allStandardSnippetTypes = { ExpansionClient.Expansion, ExpansionClient.SurroundsWith };
        private static readonly string[] _surroundsWithSnippetTypes = { ExpansionClient.SurroundsWith, ExpansionClient.SurroundsWithStatement };

        public static readonly object SuppressErrorLists = new object();

        /// <summary>
        /// Attaches events for invoking Statement completion 
        /// </summary>
        public IntellisenseController(IntellisenseControllerProvider provider, ITextView textView) {
            _textView = textView;
            _provider = provider;
            _services = _provider.Services;
            _editOps = _services.EditOperationsFactory.GetEditorOperations(textView);
            _incSearch = _services.IncrementalSearch.GetIncrementalSearch(textView);
            _textView.MouseHover += TextViewMouseHover;
            if (textView.TextBuffer.IsPythonContent()) {
                try {
                    _expansionClient = new ExpansionClient(textView, _services);
                    _services.VsTextManager2.GetExpansionManager(out _expansionMgr);
                } catch (ArgumentException) {
                    // No expansion client for this buffer, but we can continue without it
                }
            }

            textView.Properties.AddProperty(typeof(IntellisenseController), this);  // added so our key processors can get back to us
            _textView.Closed += TextView_Closed;
        }

        private void TextView_Closed(object sender, EventArgs e) {
            Close();
        }

        internal void Close() {
            _textView.MouseHover -= TextViewMouseHover;
            _textView.Closed -= TextView_Closed;
            _textView.Properties.RemoveProperty(typeof(IntellisenseController));

            foreach (var buffer in _textView.BufferGraph.GetTextBuffers(_ => true)) {
                DisconnectSubjectBuffer(buffer);
            }
        }

        private void TextViewMouseHover(object sender, MouseHoverEventArgs e) {
            if (_quickInfoSession != null && !_quickInfoSession.IsDismissed) {
                _quickInfoSession.Dismiss();
            }

            TextViewMouseHoverWorker(e)
                .HandleAllExceptions(_services.Site, GetType())
                .DoNotWait();
        }

        private async Task TextViewMouseHoverWorker(MouseHoverEventArgs e) {
            var pt = e.TextPosition.GetPoint(EditorExtensions.IsPythonContent, PositionAffinity.Successor);
            if (pt != null) {
                if (_textView.TextBuffer.GetInteractiveWindow() != null &&
                    pt.Value.Snapshot.Length > 1 &&
                    pt.Value.Snapshot[0] == '$') {
                    // don't provide quick info on help, the content type doesn't switch until we have
                    // a complete command otherwise we shouldn't need to do this.
                    return;
                }

                AnalysisEntry entry;
                if (!_services.AnalysisEntryService.TryGetAnalysisEntry(_textView, pt.Value.Snapshot.TextBuffer, out entry)) {
                    return;
                }
                var t = entry.Analyzer.GetQuickInfoAsync(entry, _textView, pt.Value);
                var quickInfo = await Task.Run(() => entry.Analyzer.WaitForRequest(t, "GetQuickInfo", null, 2));

                QuickInfoSource.AddQuickInfo(_textView, quickInfo);

                if (quickInfo != null) {
                    var viewPoint = _textView.BufferGraph.MapUpToBuffer(
                        pt.Value,
                        PointTrackingMode.Positive,
                        PositionAffinity.Successor,
                        _textView.TextBuffer
                    );

                    if (viewPoint != null) {
                        _quickInfoSession = _services.QuickInfoBroker.TriggerQuickInfo(
                            _textView,
                            viewPoint.Value.Snapshot.CreateTrackingPoint(viewPoint.Value, PointTrackingMode.Positive),
                            true
                        );
                    }
                }
            }
        }

        internal void TriggerQuickInfo() {
            if (_quickInfoSession != null && !_quickInfoSession.IsDismissed) {
                _quickInfoSession.Dismiss();
            }
            _quickInfoSession = _services.QuickInfoBroker.TriggerQuickInfo(_textView);
        }

        private static object _intellisenseAnalysisEntry = new object();

        public void ConnectSubjectBuffer(ITextBuffer subjectBuffer) {
            ConnectSubjectBufferAsync(subjectBuffer)
                .HandleAllExceptions(_services.Site, GetType())
                .DoNotWait();
        }

        private async Task ConnectSubjectBufferAsync(ITextBuffer subjectBuffer) {
            var bi = _services.GetBufferInfo(subjectBuffer);
            var entry = bi.AnalysisEntry;

            if (entry == null) {
                ProjectAnalyzer analyzer;
                string filename;
                bool isTemporaryFile = false;
                if (!_services.AnalysisEntryService.TryGetAnalyzer(subjectBuffer, out analyzer, out filename)) {
                    // there's no analyzer for this file, but we can analyze it against either
                    // the default analyzer or some other analyzer (e.g. if it's a diff view, we want
                    // to analyze against the project we're diffing from).  But in either case this
                    // is just a temporary file which should be closed when the view is closed.
                    isTemporaryFile = true;
                    if (!_services.AnalysisEntryService.TryGetAnalyzer(_textView, out analyzer, out filename)) {
                        analyzer = _services.AnalysisEntryService.DefaultAnalyzer;
                    }
                }

                var vsAnalyzer = analyzer as VsProjectAnalyzer;
                if (vsAnalyzer == null) {
                    return;
                }

                bool suppressErrorList = _textView.Properties.ContainsProperty(SuppressErrorLists);
                entry = await vsAnalyzer.AnalyzeFileAsync(bi.Filename, null, isTemporaryFile, suppressErrorList);

                if (entry == null) {
                    throw new InvalidOperationException("Failed to create entry for buffer");
                }

                entry = bi.TrySetAnalysisEntry(entry, null);
            }

            var parser = entry.GetOrCreateBufferParser(_services);
            parser.AddBuffer(subjectBuffer);
            await parser.EnsureCodeSyncedAsync(subjectBuffer);
        }

        public void DisconnectSubjectBuffer(ITextBuffer subjectBuffer) {
            var bi = PythonTextBufferInfo.TryGetForBuffer(subjectBuffer);
            bi?.AnalysisEntry?.TryGetBufferParser()?.RemoveBuffer(subjectBuffer);
        }

        /// <summary>
        /// Detaches the events
        /// </summary>
        /// <param name="textView"></param>
        public void Detach(ITextView textView) {
            if (_textView == null) {
                throw new InvalidOperationException(Strings.IntellisenseControllerAlreadyDetachedException);
            }
            if (textView != _textView) {
                throw new ArgumentException(Strings.IntellisenseControllerNotAttachedToSpecifiedTextViewException, nameof(textView));
            }

            _textView.MouseHover -= TextViewMouseHover;
            _textView.Properties.RemoveProperty(typeof(IntellisenseController));

            DetachKeyboardFilter();
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

            string text = e.Text;
            if (text.Length == 1) {
                HandleChar(text[0]);
            }
        }

        private string GetTextBeforeCaret(int includeCharsAfter = 0) {
            var maybePt = _textView.Caret.Position.Point.GetPoint(_textView.TextBuffer, PositionAffinity.Predecessor);
            if (!maybePt.HasValue) {
                return string.Empty;
            }
            var pt = maybePt.Value + includeCharsAfter;

            var span = new SnapshotSpan(pt.GetContainingLine().Start, pt);
            return span.GetText();
        }

        private void HandleChar(char ch) {
            // We trigger completions when the user types . or space.  Called via our IOleCommandTarget filter
            // on the text view.
            //
            // We trigger signature help when we receive a "(".  We update our current sig when 
            // we receive a "," and we close sig help when we receive a ")".

            if (!_incSearch.IsActive) {
                var prefs = _services.Python.LangPrefs;

                var session = Volatile.Read(ref _activeSession);
                var sigHelpSession = Volatile.Read(ref _sigHelpSession);
                var literalSpan = GetStringLiteralSpan();
                if (literalSpan.HasValue &&
                    // UNDONE: Do not automatically trigger file path completions github#2352
                    //ShouldTriggerStringCompletionSession(prefs, literalSpan.Value) &&
                    (session?.IsDismissed ?? true)) {
                    //TriggerCompletionSession(false);
                    return;
                }

                switch (ch) {
                    case '@':
                        if (!string.IsNullOrWhiteSpace(GetTextBeforeCaret(-1))) {
                            break;
                        }
                        goto case '.';
                    case '.':
                    case ' ':
                        if (prefs.AutoListMembers && GetStringLiteralSpan() == null) {
                            TriggerCompletionSession(false);
                        }
                        break;
                    case '(':
                        if (prefs.AutoListParams && GetStringLiteralSpan() == null) {
                            OpenParenStartSignatureSession();
                        }
                        break;
                    case ')':
                        if (sigHelpSession != null) {
                            sigHelpSession.Dismiss();
                        }

                        if (prefs.AutoListParams) {
                            // trigger help for outer call if there is one
                            TriggerSignatureHelp();
                        }
                        break;
                    case '=':
                    case ',':
                        if (sigHelpSession == null) {
                            if (prefs.AutoListParams) {
                                CommaStartSignatureSession();
                            }
                        } else {
                            UpdateCurrentParameter();
                        }
                        break;
                    default:
                        if (Tokenizer.IsIdentifierStartChar(ch) &&
                            ((session?.CompletionSets.Count ?? 0) == 0)) {
                            bool commitByDefault;
                            if (ShouldTriggerIdentifierCompletionSession(out commitByDefault)) {
                                TriggerCompletionSession(false, commitByDefault);
                            }
                        }
                        break;
                }
            }
        }

        private SnapshotSpan? GetStringLiteralSpan() {
            var pyCaret = _textView.GetPythonCaret();
            var classifier = pyCaret?.Snapshot.TextBuffer.GetPythonClassifier();
            if (classifier == null) {
                return null;
            }

            var spans = classifier.GetClassificationSpans(new SnapshotSpan(pyCaret.Value.GetContainingLine().Start, pyCaret.Value));
            var token = spans.LastOrDefault();
            if (!(token?.ClassificationType.IsOfType(PredefinedClassificationTypeNames.String) ?? false)) {
                return null;
            }

            return token.Span;
        }

        private bool ShouldTriggerStringCompletionSession(LanguagePreferences prefs, SnapshotSpan span) {
            if (!prefs.AutoListMembers) {
                return false;
            }

            return StringLiteralCompletionList.CanComplete(span.GetText());
        }

        private bool ShouldTriggerIdentifierCompletionSession(out bool commitByDefault) {
            commitByDefault = true;

            if (!_services.Python.AdvancedOptions.AutoListIdentifiers ||
                !_services.Python.AdvancedOptions.AutoListMembers) {
                return false;
            }

            var caretPoint = _textView.GetPythonCaret();
            if (!caretPoint.HasValue) {
                return false;
            }

            var snapshot = caretPoint.Value.Snapshot;

            var statement = new ReverseExpressionParser(
                snapshot,
                snapshot.TextBuffer,
                snapshot.CreateTrackingSpan(caretPoint.Value.Position, 0, SpanTrackingMode.EdgeNegative)
            ).GetStatementRange();
            if (!statement.HasValue || caretPoint.Value <= statement.Value.Start) {
                return false;
            }

            var text = new SnapshotSpan(statement.Value.Start, caretPoint.Value).GetText();
            if (string.IsNullOrEmpty(text)) {
                return false;
            }

            AnalysisEntry entry;
            if (!_services.AnalysisEntryService.TryGetAnalysisEntry(_textView, snapshot.TextBuffer, out entry)) {
                return false;
            }

            var languageVersion = entry.Analyzer.LanguageVersion;
            PythonAst ast;
            using (var parser = Parser.CreateParser(new StringReader(text), languageVersion, new ParserOptions { Verbatim = true })) {
                ast = parser.ParseSingleStatement();
            }

            var walker = new ExpressionCompletionWalker(caretPoint.Value.Position - statement.Value.Start.Position);
            ast.Walk(walker);
            commitByDefault = walker.CommitByDefault;
            return walker.CanComplete;
        }

        private class ExpressionCompletionWalker : PythonWalker {
            public bool CanComplete = false;
            public bool CommitByDefault = true;
            private readonly int _caretIndex;

            public ExpressionCompletionWalker(int caretIndex) {
                _caretIndex = caretIndex;
            }

            private static bool IsActualExpression(Expression node) {
                return node != null && !(node is ErrorExpression);
            }

            private static bool IsActualExpression(IList<NameExpression> expressions) {
                return expressions != null && expressions.Count > 0;
            }

            private static bool IsActualExpression(IList<Expression> expressions) {
                return expressions != null &&
                    expressions.Count > 0 &&
                    !(expressions[0] is ErrorExpression);
            }

            private bool HasCaret(Node node) {
                return node.StartIndex <= _caretIndex && _caretIndex <= node.EndIndex;
            }

            public override bool Walk(ErrorExpression node) {
                return false;
            }

            public override bool Walk(AssignmentStatement node) {
                CanComplete = true;
                CommitByDefault = true;
                if (node.Right != null) {
                    node.Right.Walk(this);
                }
                return false;
            }

            public override bool Walk(Arg node) {
                CanComplete = true;
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(ClassDefinition node) {
                CanComplete = false;
                return base.Walk(node);
            }

            public override bool Walk(FunctionDefinition node) {
                CommitByDefault = true;
                if (node.Parameters != null) {
                    CanComplete = false;
                    foreach (var p in node.Parameters) {
                        p.Walk(this);
                    }
                }
                if (node.Decorators != null) {
                    node.Decorators.Walk(this);
                }
                if (node.ReturnAnnotation != null) {
                    CanComplete = true;
                    node.ReturnAnnotation.Walk(this);
                }
                if (node.Body != null && !(node.Body is ErrorStatement)) {
                    CanComplete = node.IsLambda;
                    node.Body.Walk(this);
                }

                return false;
            }

            public override bool Walk(Parameter node) {
                CommitByDefault = true;
                var afterName = node.Annotation ?? node.DefaultValue;
                CanComplete = afterName != null && afterName.StartIndex <= _caretIndex;
                return base.Walk(node);
            }

            public override bool Walk(ComprehensionFor node) {
                if (!IsActualExpression(node.Left) || HasCaret(node.Left)) {
                    CanComplete = false;
                    CommitByDefault = false;
                } else if (IsActualExpression(node.List)) {
                    CanComplete = true;
                    CommitByDefault = true;
                    node.List.Walk(this);
                }
                return false;
            }

            public override bool Walk(ComprehensionIf node) {
                CanComplete = true;
                CommitByDefault = true;
                return base.Walk(node);
            }

            private bool WalkCollection(Expression node, IEnumerable<Expression> items) {
                CanComplete = HasCaret(node);
                int count = 0;
                Expression last = null;
                foreach (var e in items) {
                    count += 1;
                    last = e;
                }
                if (count == 0) {
                    CommitByDefault = false;
                } else if (count == 1) {
                    last.Walk(this);
                    CommitByDefault = false;
                } else {
                    CommitByDefault = true;
                    last.Walk(this);
                }
                return false;
            }

            public override bool Walk(ListExpression node) {
                return WalkCollection(node, node.Items);
            }

            public override bool Walk(DictionaryExpression node) {
                return WalkCollection(node, node.Items);
            }

            public override bool Walk(SetExpression node) {
                return WalkCollection(node, node.Items);
            }

            public override bool Walk(TupleExpression node) {
                return WalkCollection(node, node.Items);
            }

            public override bool Walk(ParenthesisExpression node) {
                CanComplete = HasCaret(node);
                CommitByDefault = false;
                return base.Walk(node);
            }

            public override bool Walk(AssertStatement node) {
                CanComplete = IsActualExpression(node.Test);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(AugmentedAssignStatement node) {
                CanComplete = IsActualExpression(node.Right);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(AwaitExpression node) {
                CanComplete = IsActualExpression(node.Expression);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(DelStatement node) {
                CanComplete = IsActualExpression(node.Expressions);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(ExecStatement node) {
                CanComplete = IsActualExpression(node.Code);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(ExpressionStatement node) {
                if (node.Expression is TupleExpression) {
                    node.Expression.Walk(this);
                    CommitByDefault = false;
                    return false;
                } else if (node.Expression is ConstantExpression) {
                    node.Expression.Walk(this);
                    return false;
                } else if (node.Expression is ErrorExpression) {
                    // Might be an unfinished string literal, which we care about
                    node.Expression.Walk(this);
                    return false;
                }

                CanComplete = true;
                CommitByDefault = false;
                return base.Walk(node);
            }

            public override bool Walk(ForStatement node) {
                if (!IsActualExpression(node.Left) || HasCaret(node.Left)) {
                    CanComplete = false;
                    CommitByDefault = false;
                } else if (IsActualExpression(node.List)) {
                    CanComplete = true;
                    CommitByDefault = true;
                    node.List.Walk(this);
                }
                return false;
            }

            public override bool Walk(IfStatementTest node) {
                CanComplete = IsActualExpression(node.Test);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(GlobalStatement node) {
                CanComplete = IsActualExpression(node.Names);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(NonlocalStatement node) {
                CanComplete = IsActualExpression(node.Names);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(PrintStatement node) {
                CanComplete = IsActualExpression(node.Expressions);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(ReturnStatement node) {
                CanComplete = IsActualExpression(node.Expression);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(WhileStatement node) {
                CanComplete = IsActualExpression(node.Test);
                CommitByDefault = true;
                return base.Walk(node);
            }

            public override bool Walk(WithStatement node) {
                CanComplete = true;
                CommitByDefault = true;
                if (node.Items != null) {
                    var item = node.Items.LastOrDefault();
                    if (item != null) {
                        if (item.Variable != null) {
                            CanComplete = false;
                        } else {
                            item.Walk(this);
                        }
                    }
                }
                if (node.Body != null) {
                    node.Body.Walk(this);
                }
                return false;
            }

            public override bool Walk(YieldExpression node) {
                CommitByDefault = true;
                if (IsActualExpression(node.Expression)) {
                    // "yield" is valid and has implied None following it
                    var ce = node.Expression as ConstantExpression;
                    CanComplete = ce == null || ce.Value != null;
                }
                return base.Walk(node);
            }

            public override bool Walk(ConstantExpression node) {
                return false;
            }
        }

        private bool Backspace() {
            var sigHelpSession = Volatile.Read(ref _sigHelpSession);
            if (sigHelpSession != null) {
                if (_textView.Selection.IsActive && !_textView.Selection.IsEmpty) {
                    // when deleting a selection don't do anything to pop up signature help again
                    sigHelpSession.Dismiss();
                    return false;
                }

                SnapshotPoint? caretPoint = _textView.BufferGraph.MapDownToFirstMatch(
                    _textView.Caret.Position.BufferPosition,
                    PointTrackingMode.Positive,
                    EditorExtensions.IsPythonContent,
                    PositionAffinity.Predecessor
                );

                if (caretPoint != null && caretPoint.Value.Position != 0) {
                    var deleting = caretPoint.Value.Snapshot[caretPoint.Value.Position - 1];
                    if (deleting == ',') {
                        caretPoint.Value.Snapshot.TextBuffer.Delete(new Span(caretPoint.Value.Position - 1, 1));
                        UpdateCurrentParameter();
                        return true;
                    } else if (deleting == '(' || deleting == ')') {
                        sigHelpSession.Dismiss();
                        // delete the ( before triggering help again
                        caretPoint.Value.Snapshot.TextBuffer.Delete(new Span(caretPoint.Value.Position - 1, 1));

                        // Pop to an outer nesting of signature help
                        if (_services.Python.LangPrefs.AutoListParams) {
                            TriggerSignatureHelp();
                        }

                        return true;
                    }
                }
            }
            return false;
        }

        private void OpenParenStartSignatureSession() {
            Volatile.Read(ref _activeSession)?.Dismiss();
            Volatile.Read(ref _sigHelpSession)?.Dismiss();

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
            var sigHelpSession = Volatile.Read(ref _sigHelpSession);
            if (sigHelpSession == null) {
                // we moved out of the original span for sig help, re-trigger based upon the position
                TriggerSignatureHelp();
                return;
            }

            int position = _textView.Caret.Position.BufferPosition.Position;
            // we advance to the next parameter
            // TODO: Take into account params arrays
            // TODO: need to parse and see if we have keyword arguments entered into the current signature yet
            PythonSignature sig = sigHelpSession.SelectedSignature as PythonSignature;
            if (sig == null) {
                return;
            }

            var prevBuffer = sig.ApplicableToSpan.TextBuffer;

            var targetPt = _textView.BufferGraph.MapDownToFirstMatch(
                new SnapshotPoint(_textView.TextBuffer.CurrentSnapshot, position),
                PointTrackingMode.Positive,
                EditorExtensions.IsPythonContent,
                PositionAffinity.Successor
            );
            if (targetPt == null) {
                return;
            }

            var span = targetPt.Value.Snapshot.CreateTrackingSpan(targetPt.Value.Position, 0, SpanTrackingMode.EdgeInclusive);
            var sigs = _services.Python.GetSignatures(_textView, targetPt.Value.Snapshot, span);
            if (sigs == null) {
                return;
            }

            bool retrigger = false;
            if (sigs.Signatures.Count == sigHelpSession.Signatures.Count) {
                for (int i = 0; i < sigs.Signatures.Count && !retrigger; i++) {
                    var leftSig = sigs.Signatures[i];
                    var rightSig = sigHelpSession.Signatures[i];

                    if (leftSig.Parameters.Count == rightSig.Parameters.Count) {
                        for (int j = 0; j < leftSig.Parameters.Count; j++) {
                            var leftParam = leftSig.Parameters[j];
                            var rightParam = rightSig.Parameters[j];

                            if (leftParam == null || rightParam == null) {
                                continue;
                            }

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
                sigHelpSession.Dismiss();
                TriggerSignatureHelp();
            } else {
                CommaFindBestSignature(sigHelpSession, sigs.ParameterIndex, sigs.LastKeywordArgument);
            }
        }

        private static void CommaFindBestSignature(ISignatureHelpSession sigHelpSession, int curParam, string lastKeywordArg) {
            // see if we have a signature which accomodates this...

            // TODO: We should also get the types of the arguments and use that to
            // pick the best signature when the signature includes types.
            var bestSig = sigHelpSession.SelectedSignature as PythonSignature;
            if (bestSig != null) {
                for (int i = 0; i < bestSig.Parameters.Count; ++i) {
                    if (bestSig.Parameters[i].Name == lastKeywordArg ||
                        lastKeywordArg == null && (i == curParam || PythonSignature.IsParamArray(bestSig.Parameters[i].Name))
                    ) {
                        bestSig.SetCurrentParameter(bestSig.Parameters[i]);
                        sigHelpSession.SelectedSignature = bestSig;
                        return;
                    }
                }
            }

            PythonSignature fallback = null;
            foreach (var sig in sigHelpSession.Signatures.OfType<PythonSignature>().OrderBy(s => s.Parameters.Count)) {
                fallback = sig;
                for (int i = 0; i < sig.Parameters.Count; ++i) {
                    if (sig.Parameters[i].Name == lastKeywordArg ||
                        lastKeywordArg == null && (i == curParam || PythonSignature.IsParamArray(sig.Parameters[i].Name))
                    ) {
                        sig.SetCurrentParameter(sig.Parameters[i]);
                        sigHelpSession.SelectedSignature = sig;
                        return;
                    }
                }
            }

            if (fallback != null) {
                fallback.SetCurrentParameter(null);
                sigHelpSession.SelectedSignature = fallback;
            } else {
                sigHelpSession.Dismiss();
            }
        }

        [ThreadStatic]
        internal static bool ForceCompletions;

        private bool SelectSingleBestCompletion(ICompletionSession session) {
            if (session.CompletionSets.Count != 1) {
                return false;
            }
            var set = session.CompletionSets[0] as FuzzyCompletionSet;
            if (set == null) {
                return false;
            }
            set.Filter();
            if (set.SelectSingleBest()) {
                session.Commit();
                return true;
            }
            return false;
        }

        internal void TriggerCompletionSession(bool completeWord, bool? commitByDefault = null) {
            Dismiss();

            var session = _services.CompletionBroker.TriggerCompletion(_textView);

            if (session == null) {
                Volatile.Write(ref _activeSession, null);
            } else if (completeWord && SelectSingleBestCompletion(session)) {
                session.Commit();
            } else {
                if (commitByDefault.HasValue) {
                    foreach (var s in session.CompletionSets.OfType<FuzzyCompletionSet>()) {
                        s.CommitByDefault = commitByDefault.GetValueOrDefault();
                    }
                }
                session.Filter();
                session.Dismissed += OnCompletionSessionDismissedOrCommitted;
                session.Committed += OnCompletionSessionDismissedOrCommitted;
                Volatile.Write(ref _activeSession, session);
            }
        }

        internal void TriggerSignatureHelp() {
            Volatile.Read(ref _sigHelpSession)?.Dismiss();

            var sigHelpSession = _services.SignatureHelpBroker.TriggerSignatureHelp(_textView);

            if (sigHelpSession != null) {
                sigHelpSession.Dismissed += OnSignatureSessionDismissed;

                ISignature sig;
                if (sigHelpSession.Properties.TryGetProperty(typeof(PythonSignature), out sig)) {
                    sigHelpSession.SelectedSignature = sig;

                    IParameter param;
                    if (sigHelpSession.Properties.TryGetProperty(typeof(PythonParameter), out param)) {
                        ((PythonSignature)sig).SetCurrentParameter(param);
                    }
                }

                _sigHelpSession = sigHelpSession;
            }
        }

        private void OnCompletionSessionDismissedOrCommitted(object sender, EventArgs e) {
            // We've just been told that our active session was dismissed.  We should remove all references to it.
            var session = sender as ICompletionSession;
            if (session == null) {
                Debug.Fail("invalid type passed to event");
                return;
            }
            session.Committed -= OnCompletionSessionDismissedOrCommitted;
            session.Dismissed -= OnCompletionSessionDismissedOrCommitted;
            Interlocked.CompareExchange(ref _activeSession, null, session);
        }

        private void OnSignatureSessionDismissed(object sender, EventArgs e) {
            // We've just been told that our active session was dismissed.  We should remove all references to it.
            var session = sender as ISignatureHelpSession;
            if (session == null) {
                Debug.Fail("invalid type passed to event");
                return;
            }
            session.Dismissed -= OnSignatureSessionDismissed;
            Interlocked.CompareExchange(ref _sigHelpSession, null, session);
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

        #region IOleCommandTarget Members

        // we need this because VS won't give us certain keyboard events as they're handled before our key processor.  These
        // include enter and tab both of which we want to complete.

        internal void AttachKeyboardFilter() {
            if (_oldTarget == null) {
                var viewAdapter = _services.EditorAdaptersFactoryService.GetViewAdapter(_textView);
                if (viewAdapter != null) {
                    ErrorHandler.ThrowOnFailure(viewAdapter.AddCommandFilter(this, out _oldTarget));
                }
            }
        }

        private void DetachKeyboardFilter() {
            if (_oldTarget != null) {
                ErrorHandler.ThrowOnFailure(_services.EditorAdaptersFactoryService.GetViewAdapter(_textView).RemoveCommandFilter(this));
                _oldTarget = null;
            }
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            var session = Volatile.Read(ref _activeSession);
            ISignatureHelpSession sigHelpSession;

            if (pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (int)VSConstants.VSStd2KCmdID.TYPECHAR) {
                var ch = (char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn);

                if (session != null && !session.IsDismissed) {
                    if (session.SelectedCompletionSet != null &&
                        session.SelectedCompletionSet.SelectionStatus.IsSelected &&
                        _services.Python.AdvancedOptions.CompletionCommittedBy.IndexOf(ch) != -1) {

                        if ((ch == '\\' || ch == '/') && session.SelectedCompletionSet.Moniker == "PythonFilenames") {
                            // We want to dismiss filename completions on slashes
                            // rather than committing them. Then it will probably
                            // be retriggered after the slash is inserted.
                            session.Dismiss();
                        } else {
                            session.Commit();
                        }
                    } else if (!Tokenizer.IsIdentifierChar(ch)) {
                        session.Dismiss();
                    }
                }

                int res = _oldTarget != null ? _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) : VSConstants.S_OK;

                HandleChar((char)(ushort)System.Runtime.InteropServices.Marshal.GetObjectForNativeVariant(pvaIn));

                if (session != null && !session.IsDismissed) {
                    session.Filter();
                }

                return res;
            }

            if (session != null) {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.RETURN:
                            if (_services.Python.AdvancedOptions.EnterCommitsIntellisense &&
                                !session.IsDismissed &&
                                session.SelectedCompletionSet.SelectionStatus.IsSelected) {

                                // If the user has typed all of the characters as the completion and presses
                                // enter we should dismiss & let the text editor receive the enter.  For example 
                                // when typing "import sys[ENTER]" completion starts after the space.  After typing
                                // sys the user wants a new line and doesn't want to type enter twice.

                                bool enterOnComplete = _services.Python.AdvancedOptions.AddNewLineAtEndOfFullyTypedWord &&
                                        EnterOnCompleteText(session);

                                session.Commit();

                                if (!enterOnComplete) {
                                    return VSConstants.S_OK;
                                }
                            } else {
                                session.Dismiss();
                            }

                            break;
                        case VSConstants.VSStd2KCmdID.TAB:
                            if (!session.IsDismissed) {
                                session.Commit();
                                return VSConstants.S_OK;
                            }

                            break;
                        case VSConstants.VSStd2KCmdID.BACKSPACE:
                        case VSConstants.VSStd2KCmdID.DELETE:
                        case VSConstants.VSStd2KCmdID.DELETEWORDLEFT:
                        case VSConstants.VSStd2KCmdID.DELETEWORDRIGHT:
                            int res = _oldTarget != null ? _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut) : VSConstants.S_OK;
                            if (session != null && !session.IsDismissed) {
                                session.Filter();
                            }
                            return res;
                    }
                }
            } else if ((sigHelpSession = Volatile.Read(ref _sigHelpSession)) != null) {
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
                            sigHelpSession.Dismiss();
                            break;
                    }
                }
            } else {
                if (pguidCmdGroup == VSConstants.VSStd2K) {
                    switch ((VSConstants.VSStd2KCmdID)nCmdID) {
                        case VSConstants.VSStd2KCmdID.RETURN:
                            if (_expansionMgr != null && _expansionClient.InSession && ErrorHandler.Succeeded(_expansionClient.EndCurrentExpansion(false))) {
                                return VSConstants.S_OK;
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.TAB:
                            if (_expansionMgr != null && _expansionClient.InSession && ErrorHandler.Succeeded(_expansionClient.NextField())) {
                                return VSConstants.S_OK;
                            }
                            if (_textView.Selection.IsEmpty && _textView.Caret.Position.BufferPosition > 0) {
                                if (TryTriggerExpansion()) {
                                    return VSConstants.S_OK;
                                }
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.BACKTAB:
                            if (_expansionMgr != null && _expansionClient.InSession && ErrorHandler.Succeeded(_expansionClient.PreviousField())) {
                                return VSConstants.S_OK;
                            }
                            break;
                        case VSConstants.VSStd2KCmdID.SURROUNDWITH:
                        case VSConstants.VSStd2KCmdID.INSERTSNIPPET:
                            TriggerSnippet(nCmdID);
                            return VSConstants.S_OK;
                    }
                }
            }

            if (_oldTarget != null) {
                return _oldTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            return (int)Constants.OLECMDERR_E_UNKNOWNGROUP;
        }

        private void TriggerSnippet(uint nCmdID) {
            if (_expansionMgr != null) {
                string prompt;
                string[] snippetTypes;
                if ((VSConstants.VSStd2KCmdID)nCmdID == VSConstants.VSStd2KCmdID.SURROUNDWITH) {
                    prompt = Strings.SurroundWith;
                    snippetTypes = _surroundsWithSnippetTypes;
                } else {
                    prompt = Strings.InsertSnippet;
                    snippetTypes = _allStandardSnippetTypes;
                }

                _expansionMgr.InvokeInsertionUI(
                    GetViewAdapter(),
                    _expansionClient,
                    GuidList.guidPythonLanguageServiceGuid,
                    snippetTypes,
                    snippetTypes.Length,
                    0,
                    null,
                    0,
                    0,
                    prompt,
                    ">"
                );
            }
        }

        private bool TryTriggerExpansion() {
            if (_expansionMgr != null) {
                var snapshot = _textView.TextBuffer.CurrentSnapshot;
                var span = new SnapshotSpan(snapshot, new Span(_textView.Caret.Position.BufferPosition.Position - 1, 1));
                var classification = _textView.TextBuffer.GetPythonClassifier().GetClassificationSpans(span);
                if (classification.Count == 1) {
                    var clsSpan = classification.First().Span;
                    var text = classification.First().Span.GetText();

                    TextSpan[] textSpan = new TextSpan[1];
                    textSpan[0].iStartLine = clsSpan.Start.GetContainingLine().LineNumber;
                    textSpan[0].iStartIndex = clsSpan.Start.Position - clsSpan.Start.GetContainingLine().Start;
                    textSpan[0].iEndLine = clsSpan.End.GetContainingLine().LineNumber;
                    textSpan[0].iEndIndex = clsSpan.End.Position - clsSpan.End.GetContainingLine().Start;

                    string expansionPath, title;
                    int hr = _expansionMgr.GetExpansionByShortcut(
                        _expansionClient,
                        GuidList.guidPythonLanguageServiceGuid,
                        text,
                        GetViewAdapter(),
                        textSpan,
                        1,
                        out expansionPath,
                        out title
                    );
                    if (ErrorHandler.Succeeded(hr)) {
                        // hr may be S_FALSE if there are multiple expansions,
                        // so we don't want to InsertNamedExpansion yet. VS will
                        // pop up a selection dialog in this case.
                        if (hr == VSConstants.S_OK) {
                            return ErrorHandler.Succeeded(_expansionClient.InsertNamedExpansion(title, expansionPath, textSpan[0]));
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        private IVsTextView GetViewAdapter() {
            return _services.EditorAdaptersFactoryService.GetViewAdapter(_textView);
        }


        private bool EnterOnCompleteText(ICompletionSession session) {
            var selectionStatus = session.SelectedCompletionSet.SelectionStatus;
            var caret = _textView.Caret.Position.BufferPosition;
            var span = session.GetApplicableSpan(_textView.TextBuffer).GetSpan(caret.Snapshot);

            return caret == span.End &&
                span.Length == selectionStatus.Completion?.InsertionText.Length &&
                span.GetText() == selectionStatus.Completion.InsertionText;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == VSConstants.VSStd2K) {
                for (int i = 0; i < cCmds; i++) {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd2KCmdID.SURROUNDWITH:
                        case VSConstants.VSStd2KCmdID.INSERTSNIPPET:
                            if (_expansionMgr != null) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                                return VSConstants.S_OK;
                            }
                            break;
                    }
                }
            }

            if (_oldTarget != null) {
                return _oldTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            }
            return (int)Constants.OLECMDERR_E_UNKNOWNGROUP;
        }

        #endregion
    }
}

