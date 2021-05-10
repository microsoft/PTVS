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
using Microsoft.Python.Core.Disposables;
using Microsoft.Python.Parsing;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Microsoft.PythonTools.Editor {
    internal class BraceHighlightTagger : ITagger<TextMarkerTag>, IDisposable {
        private readonly IServiceProvider _site;
        private readonly ITextView _textView;
        private readonly ITextBuffer _buffer;
        private readonly DisposableBag _disposableBag;
        private SnapshotPoint? _currentChar;

        private static TextMarkerTag _tag = new TextMarkerTag("Brace Matching (Rectangle)");

        public BraceHighlightTagger(IServiceProvider site, ITextView textView, ITextBuffer buffer) {
            _site = site ?? throw new ArgumentNullException(nameof(site));
            _textView = textView ?? throw new ArgumentNullException(nameof(textView));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

            _currentChar = null;

            _textView.Caret.PositionChanged += CaretPositionChanged;
            _textView.LayoutChanged += ViewLayoutChanged;

            _disposableBag = new DisposableBag(GetType().Name);
            _disposableBag.Add(() => {
                _textView.Caret.PositionChanged -= CaretPositionChanged;
                _textView.LayoutChanged -= ViewLayoutChanged;
            });
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public void Dispose() {
            _disposableBag.TryDispose();
        }

        public IEnumerable<ITagSpan<TextMarkerTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            if (spans.Count == 0) {
                yield break;
            }

            if (!_currentChar.HasValue || _currentChar.Value.Position > _currentChar.Value.Snapshot.Length) {
                yield break;
            }

            // Hold on to a snapshot of the current character
            var current = _currentChar.Value;

            // If the requested snapshot isn't the same as the one the brace is on, translate our spans to the expected snapshot
            if (spans[0].Snapshot != current.Snapshot) {
                current = current.TranslateTo(spans[0].Snapshot, PointTrackingMode.Positive);
            }

            // Look before current position for an opening brace
            if (current != 0) {
                var prevCharText = _textView.TextBuffer.CurrentSnapshot.GetText(current.Position - 1, 1);
                if (prevCharText == ")" || prevCharText == "]" || prevCharText == "}") {
                    if (FindMatchingPair(GetBraceKind(prevCharText), current, -1, out var leftSpan, out var rightSpan)) {
                        yield return new TagSpan<TextMarkerTag>(leftSpan, _tag);
                        yield return new TagSpan<TextMarkerTag>(rightSpan, _tag);
                        yield break;
                    }
                }
            }

            // Look after current position for a closing brace
            if (current != _textView.TextBuffer.CurrentSnapshot.Length) {
                var nextCharText = _textView.TextBuffer.CurrentSnapshot.GetText(current.Position, 1);
                if (nextCharText == "(" || nextCharText == "[" || nextCharText == "{") {
                    if (FindMatchingPair(GetBraceKind(nextCharText), current + 1, 1, out var leftSpan, out var rightSpan)) {
                        yield return new TagSpan<TextMarkerTag>(leftSpan, _tag);
                        yield return new TagSpan<TextMarkerTag>(rightSpan, _tag);
                        yield break;
                    }
                }
            }
        }

        private void ViewLayoutChanged(object sender, TextViewLayoutChangedEventArgs e) {
            if (e.NewSnapshot != e.OldSnapshot) {
                UpdateAtCaretPosition(_textView.Caret.Position);
            }
        }

        private void CaretPositionChanged(object sender, CaretPositionChangedEventArgs e) {
            UpdateAtCaretPosition(e.NewPosition);
        }

        private void UpdateAtCaretPosition(CaretPosition caretPosition) {
            _currentChar = caretPosition.Point.GetPoint(_buffer, caretPosition.Affinity);

            if (!_currentChar.HasValue) {
                return;
            }

            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(_buffer.CurrentSnapshot, 0, _buffer.CurrentSnapshot.Length)));
        }

        private enum BraceKind {
            None,
            Bracket,
            Paren,
            Brace
        }

        private bool FindMatchingPair(BraceKind brace, SnapshotPoint position, int direction, out SnapshotSpan leftSpan, out SnapshotSpan rightSpan) {
            leftSpan = new SnapshotSpan(position, position);
            rightSpan = leftSpan;

            var buffer = PythonTextBufferInfo.ForBuffer(_site, position.Snapshot.TextBuffer);
            if (buffer == null) {
                return false;
            }

            if (!(buffer.GetTokenAtPoint(position)?.Trigger ?? TokenTriggers.None).HasFlag(TokenTriggers.MatchBraces)) {
                return false;
            }

            var snapshot = position.Snapshot;
            int depth = 0;
            foreach (var token in (direction > 0 ? buffer.GetTokensForwardFromPoint(position) : buffer.GetTokensInReverseFromPoint(position - 1))) {
                if (!token.Trigger.HasFlag(TokenTriggers.MatchBraces)) {
                    continue;
                }

                var tokenSpan = token.ToSnapshotSpan(snapshot);
                var txt = tokenSpan.GetText();
                var kind = GetBraceKind(txt);
                if (kind == BraceKind.None) {
                    return false;
                }

                if (kind == brace) {
                    if (txt.IsCloseGrouping()) {
                        depth -= direction;
                    } else {
                        depth += direction;
                    }
                }

                if (depth == 0) {
                    leftSpan = tokenSpan;
                    rightSpan = new SnapshotSpan(snapshot, position - 1, 1);
                    return true;
                }
            }

            return false;
        }

        private static BraceKind GetBraceKind(string brace) {
            if (string.IsNullOrEmpty(brace)) {
                return BraceKind.None;
            }

            switch (brace[0]) {
                case '[':
                case ']': return BraceKind.Bracket;
                case '(':
                case ')': return BraceKind.Paren;
                case '{':
                case '}': return BraceKind.Bracket;
                default: return BraceKind.None;
            }
        }
    }
}
