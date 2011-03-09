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

namespace Microsoft.VisualStudio.Repl {
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
        }

        #region IClassifier Members

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged {
            add { }
            remove { }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            List<OutputColors> colors;
            if (_buffer.Properties.TryGetProperty(ColorKey, out colors)) {
                List<ClassificationSpan> res = new List<ClassificationSpan>();
                int startIndex = colors.BinarySearch(
                    new OutputColors(span.Start, span.Length, ConsoleColor.White),
                    MyComparer.Instance
                );
                if (startIndex < 0) {
                    startIndex = ~startIndex;
                }

                for (int i = startIndex; i < colors.Count && ((colors[i].Start + colors[i].Length) >= span.Start); i++) {
                    if (span.IntersectsWith(new Span(colors[i].Start, colors[i].Length))) {
                        IClassificationType type;
                        if (_provider._classTypes.TryGetValue(colors[i].Color, out type)) {
                            SnapshotPoint start, end;

                            if (colors[i].Start < span.Start.Position) {
                                start = span.Start;
                            } else {
                                start = new SnapshotPoint(span.Snapshot, colors[i].Start);
                            }

                            int endPos = colors[i].Start + colors[i].Length;
                            if (span.End < endPos) {
                                end = span.End;
                            } else {
                                end = new SnapshotPoint(span.Snapshot, endPos);
                            }

                            res.Add(
                                new ClassificationSpan(
                                    new SnapshotSpan(start, end),
                                    type
                                )
                            );
                        }
                    }
                }
                return res;
            }
            return new ClassificationSpan[0];
        }

        class MyComparer : IComparer<OutputColors> {
            internal static MyComparer Instance = new MyComparer();

            #region IComparer<OutputColors> Members

            public int Compare(OutputColors x, OutputColors y) {
                return x.Start - y.Start;
            }

            #endregion
        }

        #endregion
    }
}
