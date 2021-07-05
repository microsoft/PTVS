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
    /// <summary>
    /// Classifies regions for REPL error output spans.  These are always classified as errors.
    /// </summary>
    class ReplOutputClassifier : IClassifier {
        private readonly ReplOutputClassifierProvider _provider;
        internal static object ColorKey = new object();
        private readonly ITextBuffer _buffer;

        public ReplOutputClassifier(ReplOutputClassifierProvider provider, ITextBuffer buffer) {
            _provider = provider;
            _buffer = buffer;
            _buffer.Changed += _buffer_Changed;
        }

        private void _buffer_Changed(object sender, TextContentChangedEventArgs e) {
            if (e.After.Length == 0) {
                // screen was cleared, remove color mappings...
                _buffer.Properties[ColorKey] = new List<ColoredSpan>();
            }
        }

        #region IClassifier Members

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged {
            add { }
            remove { }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            List<ColoredSpan> coloredSpans;
            if (!_buffer.Properties.TryGetProperty(ColorKey, out coloredSpans)) {
                return new ClassificationSpan[0];
            }

            List<ClassificationSpan> classifications = new List<ClassificationSpan>();

            int startIndex = coloredSpans.BinarySearch(new ColoredSpan(span, ConsoleColor.White), SpanStartComparer.Instance);
            if (startIndex < 0) {
                startIndex = ~startIndex - 1;
                if (startIndex < 0) {
                    startIndex = 0;
                }
            }

            int spanEnd = span.End.Position;
            for (int i = startIndex; i < coloredSpans.Count && coloredSpans[i].Span.Start < spanEnd; i++) {
                IClassificationType type;
                if (coloredSpans[i].Color != null &&
                    _provider._classTypes.TryGetValue(coloredSpans[i].Color.Value, out type)) {
                    var overlap = span.Overlap(coloredSpans[i].Span);
                    if (overlap != null) {
                        classifications.Add(new ClassificationSpan(overlap.Value, type));
                    }
                }
            }

            return classifications;
        }

        private sealed class SpanStartComparer : IComparer<ColoredSpan> {
            internal static SpanStartComparer Instance = new SpanStartComparer();

            public int Compare(ColoredSpan x, ColoredSpan y) {
                return x.Span.Start - y.Span.Start;
            }
        }

        #endregion
    }

    internal sealed class ColoredSpan {
        public readonly Span Span;
        public readonly ConsoleColor? Color;

        public ColoredSpan(Span span, ConsoleColor? color) {
            Span = span;
            Color = color;
        }
    }
}
