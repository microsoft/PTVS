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
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

#if DEV14_OR_LATER
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
#endif