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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools {
    using AP = AnalysisProtocol;

    struct CachedClassification {
        public ITrackingSpan Span;
        public string Classification;

        public CachedClassification(ITrackingSpan span, string classification) {
            Span = span;
            Classification = classification;
        }
    }

    /// <summary>
    /// Provides classification based upon the AST and analysis.
    /// </summary>
    internal class PythonAnalysisClassifier : IClassifier {
        private AP.AnalysisClassification[] _spanCache;
        private readonly object _spanCacheLock = new object();
        private readonly PythonAnalysisClassifierProvider _provider;
        private readonly ITextBuffer _buffer;
        private LocationTracker _spanTranslator;

        internal PythonAnalysisClassifier(PythonAnalysisClassifierProvider provider, ITextBuffer buffer) {
            buffer.ContentTypeChanged += BufferContentTypeChanged;

            _provider = provider;
            _buffer = buffer;
            _buffer.RegisterForNewAnalysis(OnNewAnalysis);
        }

        private async void OnNewAnalysis(AnalysisEntry entry) {
            if (!_provider._colorNames) {
                bool raise = false;
                lock (_spanCacheLock) {
                    if (_spanCache != null) {
                        _spanCache = null;
                        raise = true;
                    }
                }

                if (raise) {
                    OnNewClassifications(_buffer.CurrentSnapshot);
                }
                return;
            }

            var classifications = await entry.Analyzer.GetAnalysisClassificationsAsync(
                entry,
                _buffer,
                _provider._colorNamesWithAnalysis
            );

            if (classifications != null) {
                Debug.WriteLine("Received {0} classifications", classifications.Data.classifications.Length);

                lock (_spanCacheLock) {
                    // sort the spans by starting position so we can use binary search when handing them out
                    _spanCache = classifications.Data.classifications.OrderBy(c => c.start).ToArray();
                    _spanTranslator = classifications.GetTracker(classifications.Data.version);
                }

                if (_spanTranslator != null) {
                    OnNewClassifications(_buffer.CurrentSnapshot);
                }
            }
        }

        private void OnNewClassifications(ITextSnapshot snapshot) {
            ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        class IndexComparer : IComparer {
            public static readonly IndexComparer Instance = new IndexComparer();

            public int Compare(object x, object y) {
                int xValue = GetStart(x), yValue = GetStart(y);

                return xValue - yValue;
            }

            private static int GetStart(object value) {
                int indexValue;

                AP.AnalysisClassification xClass = value as AP.AnalysisClassification;
                if (xClass != null) {
                    indexValue = xClass.start;
                } else {
                    indexValue = (int)value;
                }

                return indexValue;
            }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span) {
            var classifications = new List<ClassificationSpan>();
            var snapshot = span.Snapshot;

            AP.AnalysisClassification[] spans;
            LocationTracker spanTranslator;
            lock (_spanCacheLock) {
                spans = _spanCache;
                spanTranslator = _spanTranslator;
            }

            if (span.Length <= 0 || span.Snapshot.IsReplBufferWithCommand() || spans == null || spanTranslator == null) {
                return classifications;
            }

            // find where in the spans we should start scanning from (they're sorted by
            // starting position in the old buffer)
            var start = spanTranslator.TranslateBack(span.Start);
            var end = spanTranslator.TranslateBack(span.End);
            var startIndex = Array.BinarySearch(spans, start, IndexComparer.Instance);
            if (startIndex < 0) {
                startIndex = ~startIndex - 1;
                if (startIndex < 0) {
                    startIndex = 0;
                }
            }

            for (int i = startIndex; i < spans.Length; i++) {
                if (spans[i].start > end) {
                    // we're past the span our caller is interested in, stop scanning...
                    break;
                }

                var classification = spans[i];
                var cs = spanTranslator.TranslateForward(new Span(classification.start, classification.length));

                string typeName = ToVsClassificationName(classification);

                IClassificationType classificationType;
                if (typeName != null &&
                    _provider.CategoryMap.TryGetValue(typeName, out classificationType)) {
                    classifications.Add(
                        new ClassificationSpan(
                            new SnapshotSpan(snapshot, cs),
                            classificationType
                        )
                    );
                }
            }

            return classifications;
        }

        private static string ToVsClassificationName(AP.AnalysisClassification classification) {
            string typeName = null;
            switch (classification.type) {
                case "keyword": typeName = PredefinedClassificationTypeNames.Keyword; break;
                case "class": typeName = PythonPredefinedClassificationTypeNames.Class; break;
                case "function": typeName = PythonPredefinedClassificationTypeNames.Function; break;
                case "module": typeName = PythonPredefinedClassificationTypeNames.Module; break;
                case "parameter": typeName = PythonPredefinedClassificationTypeNames.Parameter; break;
            }

            return typeName;
        }

        public PythonAnalysisClassifierProvider Provider {
            get {
                return _provider;
            }
        }

        #region Private Members

        private void BufferContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
            _spanCache = null;
            _buffer.ContentTypeChanged -= BufferContentTypeChanged;
            _buffer.Properties.RemoveProperty(typeof(PythonAnalysisClassifier));
            _buffer.UnregisterForNewAnalysis(OnNewAnalysis);
        }

        #endregion
    }

    internal static partial class ClassifierExtensions {
        public static PythonAnalysisClassifier GetPythonAnalysisClassifier(this ITextBuffer buffer) {
            PythonAnalysisClassifier res;
            if (buffer.Properties.TryGetProperty(typeof(PythonAnalysisClassifier), out res)) {
                return res;
            }
            return null;
        }
    }
}
