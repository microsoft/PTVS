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

using Microsoft.PythonTools.Editor.Core;

namespace Microsoft.PythonTools.Editor {
    /// <summary>
    /// Provides highlighting of matching braces in a text view.
    /// </summary>
    class BraceMatcher {
        private readonly ITextView _textView;
        private readonly PythonEditorServices _editorServices;
        private ITextBuffer _markedBuffer;
        private static TextMarkerTag _tag = new TextMarkerTag("Brace Matching (Rectangle)");

        /// <summary>
        /// Starts watching the provided text view for brace matching.  When new braces are inserted
        /// in the text or when the cursor moves to a brace the matching braces are highlighted.
        /// </summary>
        public static void WatchBraceHighlights(PythonEditorServices editorServices, ITextView view) {
            var matcher = new BraceMatcher(editorServices, view);

            // position changed only fires when the caret is explicitly moved, not from normal text edits,
            // so we track both changes and position changed.
            view.Caret.PositionChanged += matcher.CaretPositionChanged;
            view.TextBuffer.Changed += matcher.TextBufferChanged;
            view.Closed += matcher.TextViewClosed;
        }

        public BraceMatcher(PythonEditorServices editorServices, ITextView view) {
            _textView = view;
            _editorServices = editorServices;
        }

        private void TextViewClosed(object sender, EventArgs e) {
            _textView.Caret.PositionChanged -= CaretPositionChanged;
            _textView.TextBuffer.Changed -= TextBufferChanged;
            _textView.Closed -= TextViewClosed;
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
            return _editorServices.TextMarkerProviderFactory.GetTextMarkerTagger(buffer);
        }

        private bool HighlightBrace(BraceKind brace, int position, int direction) {
            var pt = _textView.BufferGraph.MapDownToInsertionPoint(
                new SnapshotPoint(_textView.TextBuffer.CurrentSnapshot, position),
                PointTrackingMode.Positive,
                EditorExtensions.IsPythonContent
            );

            if (pt == null) {
                return false;
            }

            return HighlightBrace(brace, pt.Value, direction);

        }

        private bool HighlightBrace(BraceKind brace, SnapshotPoint position, int direction) {
            var buffer = _editorServices.GetBufferInfo(position.Snapshot.TextBuffer);
            if (buffer == null) {
                return false;
            }

            var snapshot = position.Snapshot;
            var span = new SnapshotSpan(snapshot, position.Position - 1, 1);
            var originalSpan = span;

            if (!(buffer.GetTokenAtPoint(position)?.Trigger ?? Parsing.TokenTriggers.None).HasFlag(Parsing.TokenTriggers.MatchBraces)) {
                return false;
            }

            int depth = 0;
            foreach (var token in (direction > 0 ? buffer.GetTokensForwardFromPoint(position) : buffer.GetTokensInReverseFromPoint(position - 1))) {
                if (!token.Trigger.HasFlag(Parsing.TokenTriggers.MatchBraces)) {
                    continue;
                }

                var tspan = token.ToSnapshotSpan(snapshot);
                var txt = tspan.GetText();
                try {
                    if (IsSameBraceKind(txt, brace)) {
                        if (txt.IsCloseGrouping()) {
                            depth -= direction;
                        } else {
                            depth += direction;
                        }
                    }
                } catch (InvalidOperationException) {
                    return false;
                }

                if (depth == 0) {
                    RemoveExistingHighlights();
                    _markedBuffer = snapshot.TextBuffer;

                    // left brace
                    GetTextMarker(snapshot.TextBuffer).CreateTagSpan(snapshot.CreateTrackingSpan(tspan, SpanTrackingMode.EdgeExclusive), _tag);
                    // right brace
                    GetTextMarker(snapshot.TextBuffer).CreateTagSpan(snapshot.CreateTrackingSpan(new SnapshotSpan(snapshot, position - 1, 1), SpanTrackingMode.EdgeExclusive), _tag);
                    return true;
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
            if (string.IsNullOrEmpty(brace)) {
                throw new InvalidOperationException();
            }
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
    }
}
