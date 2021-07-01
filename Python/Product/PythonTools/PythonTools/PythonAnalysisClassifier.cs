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

using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools
{
    using AP = AnalysisProtocol;

    struct CachedClassification
    {
        public ITrackingSpan Span;
        public string Classification;

        public CachedClassification(ITrackingSpan span, string classification)
        {
            Span = span;
            Classification = classification;
        }
    }

    /// <summary>
    /// Provides classification based upon the AST and analysis.
    /// </summary>
    internal class PythonAnalysisClassifier : IClassifier, IPythonTextBufferInfoEventSink
    {
        private AP.AnalysisClassification[] _spanCache;
        private int _spanFromVersion;
        private readonly object _spanCacheLock = new object();
        private readonly PythonAnalysisClassifierProvider _provider;

        internal PythonAnalysisClassifier(PythonAnalysisClassifierProvider provider)
        {
            _provider = provider;
        }

        private async Task OnNewAnalysisAsync(PythonTextBufferInfo sender, AnalysisEntry entry)
        {
            if (!_provider._colorNames || entry == null)
            {
                bool raise = false;
                lock (_spanCacheLock)
                {
                    if (_spanCache != null)
                    {
                        _spanCache = null;
                        raise = true;
                    }
                }

                if (raise)
                {
                    OnNewClassifications(sender.CurrentSnapshot);
                }
                return;
            }


            var classifications = await entry.Analyzer.GetAnalysisClassificationsAsync(sender, _provider._colorNamesWithAnalysis, entry);

            if (classifications != null)
            {
                Debug.WriteLine("Received {0} classifications", classifications.classifications?.Length ?? 0);

                lock (_spanCacheLock)
                {
                    // sort the spans by starting position so we can use binary search when handing them out
                    _spanCache = classifications.classifications
                        .MaybeEnumerate()
                        .OrderBy(c => c.startLine)
                        .ThenBy(c => c.startColumn)
                        .Distinct(ClassificationComparer.Instance)
                        .ToArray();
                    _spanFromVersion = classifications.version;
                }

                if (_spanCache != null)
                {
                    OnNewClassifications(sender.CurrentSnapshot);
                }
            }
        }

        private class ClassificationComparer : IEqualityComparer<AP.AnalysisClassification>
        {
            public static readonly IEqualityComparer<AP.AnalysisClassification> Instance = new ClassificationComparer();

            public bool Equals(AP.AnalysisClassification x, AP.AnalysisClassification y)
            {
                return x.startLine == y.startLine &&
                    x.startColumn == y.startColumn &&
                    x.endLine == y.endLine &&
                    x.endColumn == y.endColumn &&
                    x.type == y.type;
            }

            public int GetHashCode(AP.AnalysisClassification obj) => obj.startLine << 8 + obj.startColumn;
        }

        private void OnNewClassifications(ITextSnapshot snapshot)
        {
            ClassificationChanged?.Invoke(this, new ClassificationChangedEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
        }

        public event EventHandler<ClassificationChangedEventArgs> ClassificationChanged;

        class IndexComparer : IComparer
        {
            public static readonly IComparer Instance = new IndexComparer();

            private IndexComparer() { }

            public int Compare(object x, object y)
            {
                return GetStart(x).CompareTo(GetStart(y));
            }

            private SourceLocation GetStart(object value)
            {
                if (value is AP.AnalysisClassification xClass)
                {
                    return new SourceLocation(xClass.startLine, xClass.startColumn);
                }
                else if (value is SourceLocation s)
                {
                    return s;
                }
                else
                {
                    Debug.Fail($"Unexpected value {value ?? "(null)"} ({value?.GetType().FullName ?? "null"})");
                    throw new InvalidCastException();
                }
            }
        }

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var classifications = new List<ClassificationSpan>();
            var snapshot = span.Snapshot;

            if (span.Length <= 0 || snapshot.IsReplBufferWithCommand())
            {
                return classifications;
            }

            var bi = PythonTextBufferInfo.TryGetForBuffer(snapshot.TextBuffer);
            if (bi == null)
            {
                return classifications;
            }

            AP.AnalysisClassification[] spans;
            int fromVersion;
            lock (_spanCacheLock)
            {
                spans = _spanCache;
                fromVersion = _spanFromVersion;
            }

            if (spans == null)
            {
                if (_provider._colorNames)
                {
                    if (bi?.AnalysisEntry != null && bi.AnalysisEntry.IsAnalyzed)
                    {
                        // Trigger the request so we get info on first open
                        OnNewAnalysisAsync(bi, bi.AnalysisEntry).HandleAllExceptions(bi.Services.Site, GetType()).DoNotWait();
                    }
                }

                return classifications;
            }
            if (spans.Length == 0)
            {
                return classifications;
            }

            // find where in the spans we should start scanning from (they're sorted by
            // starting position in the old buffer)
            var start = bi.LocationTracker.Translate(span.Start.ToSourceLocation(), snapshot, fromVersion);
            var end = bi.LocationTracker.Translate(span.End.ToSourceLocation(), snapshot, fromVersion);
            var startIndex = Array.BinarySearch(spans, start, IndexComparer.Instance);
            if (startIndex < 0)
            {
                startIndex = ~startIndex - 1;
                if (startIndex < 0)
                {
                    startIndex = 0;
                }
            }

            for (int i = startIndex; i < spans.Length; i++)
            {
                var spanSpan = new SourceSpan(
                    new SourceLocation(spans[i].startLine, spans[i].startColumn),
                    new SourceLocation(spans[i].endLine, spans[i].endColumn)
                );
                if (spanSpan.Start > end)
                {
                    // we're past the span our caller is interested in, stop scanning...
                    break;
                }

                var cs = bi.LocationTracker.Translate(spanSpan, fromVersion, snapshot);

                string typeName = ToVsClassificationName(spans[i]);

                IClassificationType classificationType;
                if (typeName != null &&
                    _provider.CategoryMap.TryGetValue(typeName, out classificationType))
                {
                    classifications.Add(new ClassificationSpan(cs, classificationType));
                }
            }

            return classifications;
        }

        private static string ToVsClassificationName(AP.AnalysisClassification classification)
        {
            string typeName = null;
            switch (classification.type)
            {
                case "keyword": typeName = PredefinedClassificationTypeNames.Keyword; break;
                case "class": typeName = PythonPredefinedClassificationTypeNames.Class; break;
                case "function": typeName = PythonPredefinedClassificationTypeNames.Function; break;
                case "module": typeName = PythonPredefinedClassificationTypeNames.Module; break;
                case "parameter": typeName = PythonPredefinedClassificationTypeNames.Parameter; break;
                case "docstring": typeName = PythonPredefinedClassificationTypeNames.Documentation; break;
                case "regexliteral": typeName = PythonPredefinedClassificationTypeNames.RegularExpression; break;
            }

            return typeName;
        }

        Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e)
        {
            if (e.Event == PythonTextBufferInfoEvents.NewAnalysis)
            {
                return OnNewAnalysisAsync(sender, e.AnalysisEntry);
            }
            else if (e.Event == PythonTextBufferInfoEvents.NewTextBufferInfo)
            {
                var entry = sender.AnalysisEntry;
                if (entry != null)
                {
                    return OnNewAnalysisAsync(sender, entry);
                }
            }
            return Task.CompletedTask;
        }

        public PythonAnalysisClassifierProvider Provider
        {
            get
            {
                return _provider;
            }
        }
    }
}
