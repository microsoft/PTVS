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
using System.Collections.Generic;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Editor {
    /// <summary>
    /// Provides highlighting of matching braces in a text view.
    /// </summary>
    public class BraceMatcher {
        private readonly ITextView _textView;
        private readonly IComponentModel _compModel;
        private ITextBuffer _markedBuffer;
        private static TextMarkerTag _tag = new TextMarkerTag("bracehighlight");

        /// <summary>
        /// Starts watching the provided text view for brace matching.  When new braces are inserted
        /// in the text or when the cursor moves to a brace the matching braces are highlighted.
        /// </summary>
        public static void WatchBraceHighlights(ITextView view, IComponentModel componentModel) {
            var matcher = new BraceMatcher(view, componentModel);

            // position changed only fires when the caret is explicitly moved, not from normal text edits,
            // so we track both changes and position changed.
            view.Caret.PositionChanged += matcher.CaretPositionChanged;
            view.TextBuffer.Changed += matcher.TextBufferChanged;
        }

        public BraceMatcher(ITextView view, IComponentModel componentModel) {
            _textView = view;
            _compModel = componentModel;
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
            RemoveExistingHighlights();

            UpdateBraceMatching(e.NewPosition.BufferPosition.Position);
        }

        private void TextBufferChanged(object sender, TextContentChangedEventArgs changed) {
            RemoveExistingHighlights();

            if (changed.Changes.Count == 1) {
                var newText = changed.Changes[0].NewText;
                if (newText == ")" || newText == "}" || newText == "]") {
                    UpdateBraceMatching(changed.Changes[0].NewPosition + 1);
                }
            }
        }

        private bool HasTags {
            get {
                return _markedBuffer != null;
            }
        }

        private void RemoveExistingHighlights() {
            if (HasTags) {
                RemoveExistingHighlights(_markedBuffer);
                _markedBuffer = null;
            }
        }

        private void UpdateBraceMatching(int pos) {
            if (pos != 0) {
                var prevCharText = _textView.TextBuffer.CurrentSnapshot.GetText(pos - 1, 1);
                if (prevCharText == ")" || prevCharText == "]" || prevCharText == "}") {
                    if (HighlightBrace(GetBraceKind(prevCharText), pos, -1)) {
                        return;
                    }
                }
            }

            if (pos != _textView.TextBuffer.CurrentSnapshot.Length) {
                var nextCharText = _textView.TextBuffer.CurrentSnapshot.GetText(pos, 1);
                if (nextCharText == "(" || nextCharText == "[" || nextCharText == "{") {
                    HighlightBrace(GetBraceKind(nextCharText), pos + 1, 1);
                }
            }
        }

        private void RemoveExistingHighlights(ITextBuffer buffer) {
            if (HasTags) {
                GetTextMarker(buffer).RemoveTagSpans(x => true);
            }
        }

        private SimpleTagger<TextMarkerTag> GetTextMarker(ITextBuffer buffer) {
            return _compModel.GetService<ITextMarkerProviderFactory>().GetTextMarkerTagger(buffer);
        }

        private bool HighlightBrace(BraceKind brace, int position, int direction) {
            var pt = _textView.BufferGraph.MapDownToInsertionPoint(
                new SnapshotPoint(_textView.TextBuffer.CurrentSnapshot, position),
                PointTrackingMode.Positive,
                PythonCoreConstants.IsPythonContent
            );

            if (pt == null) {
                return false;
            }

            return HighlightBrace(brace, pt.Value, direction);

        }

        private bool HighlightBrace(BraceKind brace, SnapshotPoint position, int direction) {
            var classifier = position.Snapshot.TextBuffer.GetPythonClassifier();
            var snapshot = position.Snapshot;
            var span = new SnapshotSpan(snapshot, position.Position - 1, 1);
            var originalSpan = span;

            var spans = classifier.GetClassificationSpans(span);
            // we don't highlight braces if we're in a comment or string literal
            if (spans.Count == 0 ||
                (spans.Count == 1 &&
                (!spans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.String) &&
                !spans[0].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Comment)))) {

                // find the opening span
                var curLine = snapshot.GetLineFromPosition(position);
                int curLineNo = curLine.LineNumber;

                if (direction == 1) {
                    span = new SnapshotSpan(snapshot, position, curLine.End.Position - position);
                } else {
                    span = new SnapshotSpan(curLine.Start, position - 1);
                }

                int depth = 1;
                for (; ; ) {
                    spans = classifier.GetClassificationSpans(span);
                    for (int i = direction == -1 ? spans.Count - 1 : 0; i >= 0 && i < spans.Count; i += direction) {
                        if (IsCloseSpan(spans, i)) {
                            if (IsSameBraceKind(spans[i].Span.GetText(), brace)) {
                                depth -= direction;
                            }
                        } else if (IsOpenSpan(spans, i)) {
                            if (IsSameBraceKind(spans[i].Span.GetText(), brace)) {
                                depth += direction;
                            }
                        }

                        if (depth == 0) {
                            RemoveExistingHighlights();
                            _markedBuffer = snapshot.TextBuffer;

                            // left brace
                            GetTextMarker(snapshot.TextBuffer).CreateTagSpan(snapshot.CreateTrackingSpan(spans[i].Span, SpanTrackingMode.EdgeExclusive), _tag);
                            // right brace
                            GetTextMarker(snapshot.TextBuffer).CreateTagSpan(snapshot.CreateTrackingSpan(new SnapshotSpan(snapshot, position - 1, 1), SpanTrackingMode.EdgeExclusive), _tag);
                            return true;
                        }
                    }

                    curLineNo += direction;
                    if (curLineNo < 0 || curLineNo >= snapshot.LineCount) {
                        break;
                    }

                    var line = snapshot.GetLineFromLineNumber(curLineNo);
                    span = new SnapshotSpan(line.Start, line.End);
                }
            }
            return false;
        }

        private enum BraceKind {
            Bracket,
            Paren,
            Brace
        }

        private static bool IsSameBraceKind(string brace, BraceKind kind) {
            return GetBraceKind(brace) == kind;
        }

        private static BraceKind GetBraceKind(string brace) {
            switch (brace[0]) {
                case '[':
                case ']': return BraceKind.Bracket;
                case '(':
                case ')': return BraceKind.Paren;
                case '{':
                case '}': return BraceKind.Bracket;
                default: throw new InvalidOperationException();
            }
        }

        private static bool IsOpenSpan(IList<VisualStudio.Text.Classification.ClassificationSpan> spans, int i) {
            return spans[i].ClassificationType == PythonClassifierProvider.Instance.OpenGroupingClassification ||
                (spans[i].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Operator) &&
                spans[i].Span.Length == 1 &&
                (spans[i].Span.GetText() == "{" || spans[i].Span.GetText() == "["));
        }

        private static bool IsCloseSpan(IList<VisualStudio.Text.Classification.ClassificationSpan> spans, int i) {
            return spans[i].ClassificationType == PythonClassifierProvider.Instance.CloseGroupingClassification ||
                (spans[i].ClassificationType.IsOfType(PredefinedClassificationTypeNames.Operator) &&
                spans[i].Span.Length == 1 &&
                (spans[i].Span.GetText() == "}" || spans[i].Span.GetText() == "]"));
        }
    }
}
