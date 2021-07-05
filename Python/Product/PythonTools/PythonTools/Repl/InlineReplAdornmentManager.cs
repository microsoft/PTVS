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

namespace Microsoft.PythonTools.Repl {
    class InlineReplAdornmentManager : ITagger<IntraTextAdornmentTag> {
        private readonly ITextView _textView;
        private readonly List<Tuple<SnapshotPoint, UIElement>> _tags;
        private readonly Dispatcher _dispatcher;

        internal InlineReplAdornmentManager(ITextView textView) {
            _textView = textView;
            _tags = new List<Tuple<SnapshotPoint, UIElement>>();
            _dispatcher = Dispatcher.CurrentDispatcher;
            textView.TextBuffer.Changed += TextBuffer_Changed;
        }

        void TextBuffer_Changed(object sender, TextContentChangedEventArgs e) {
            if (e.After.Length == 0) {
                // screen was cleared...
                RemoveAll();
            }
        }

        public IEnumerable<ITagSpan<IntraTextAdornmentTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
            var result = new List<TagSpan<IntraTextAdornmentTag>>();
            for (int i = 0; i < _tags.Count; i++) {
                if (_tags[i].Item1.Snapshot != _textView.TextSnapshot) {
                    // update to the latest snapshot
                    _tags[i] = new Tuple<SnapshotPoint, UIElement>(
                        _tags[i].Item1.TranslateTo(_textView.TextSnapshot, PointTrackingMode.Negative),
                        _tags[i].Item2
                    );
                }

                var span = new SnapshotSpan(_textView.TextSnapshot, _tags[i].Item1, 0);
                bool intersects = false;
                foreach (var applicableSpan in spans) {
                    if (applicableSpan.TranslateTo(_textView.TextSnapshot, SpanTrackingMode.EdgeInclusive).IntersectsWith(span)) {
                        intersects = true;
                        break;
                    }
                }
                if (!intersects) {
                    continue;
                }
                var tag = new IntraTextAdornmentTag(_tags[i].Item2, null);
                result.Add(new TagSpan<IntraTextAdornmentTag>(span, tag));
            }
            return result;
        }

        public void AddAdornment(UIElement uiElement, SnapshotPoint targetLoc) {
            if (Dispatcher.CurrentDispatcher != _dispatcher) {
                _dispatcher.BeginInvoke(new Action(() => AddAdornment(uiElement, targetLoc)));
                return;
            }
            var targetLine = targetLoc.GetContainingLine();
            _tags.Add(new Tuple<SnapshotPoint, UIElement>(targetLoc, uiElement));
            var handler = TagsChanged;
            if (handler != null) {
                var span = new SnapshotSpan(_textView.TextSnapshot, targetLine.Start, targetLine.LengthIncludingLineBreak);
                var args = new SnapshotSpanEventArgs(span);
                handler(this, args);
            }
        }

        public IList<Tuple<SnapshotPoint, UIElement>> Adornments {
            get { return _tags; }
        }

        public void RemoveAll() {
            _tags.Clear();
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;
    }
}
