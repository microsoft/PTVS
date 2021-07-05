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
using Microsoft.PythonTools.Intellisense;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools {
    [Export(typeof(ITaggerProvider)), ContentType(PythonCoreConstants.ContentType)]
    [TagType(typeof(IOutliningRegionTag))]
    class OutliningTaggerProvider : ITaggerProvider {
        private readonly PythonEditorServices _services;

        [ImportingConstructor]
        public OutliningTaggerProvider([Import] PythonEditorServices services) {
            _services = services;
        }

        #region ITaggerProvider Members

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            if (buffer.ContentType.IsOfType(CodeRemoteContentDefinition.CodeRemoteContentTypeName)) {
                return null;
            }

            return (ITagger<T>)_services.GetBufferInfo(buffer)
                .GetOrCreateSink(typeof(OutliningTagger), _ => new OutliningTagger(_services));
        }

        #endregion

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
            Justification = "Object is owned by VS and cannot be disposed")]
        internal class OutliningTagger : ITagger<IOutliningRegionTag>, IPythonTextBufferInfoEventSink {
            private readonly PythonEditorServices _services;
            private TagSpan[] _tags = Array.Empty<TagSpan>();
            private CancellationTokenSource _processing;
            private static readonly Regex _openingRegionRegex = new Regex(@"^\s*#\s*region($|\s+.*$)");
            private static readonly Regex _closingRegionRegex = new Regex(@"^\s*#\s*endregion($|\s+.*$)");

            public OutliningTagger(PythonEditorServices services) {
                _services = services;
                Enabled = _services.Python?.AdvancedOptions.EnterOutliningModeOnOpen ?? true;
            }

            public bool Enabled { get; private set; }

            public void Enable(ITextSnapshot snapshot) {
                Enabled = true;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
            }

            public void Disable(ITextSnapshot snapshot) {
                Enabled = false;
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
            }

            #region ITagger<IOutliningRegionTag> Members

            public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
                return _tags;
            }

            private async Task UpdateTagsAsync(PythonTextBufferInfo buffer, AnalysisEntry entry) {
                if (entry == null) {
                    return;
                }

                var snapshot = buffer.CurrentSnapshot;
                Interlocked.Exchange(ref _processing, null)?.Cancel();

                var tags = await entry.Analyzer.GetOutliningTagsAsync(snapshot);
                if (tags != null) {
                    var cts = new CancellationTokenSource();
                    Interlocked.Exchange(ref _processing, cts)?.Cancel();
                    try {
                        _tags = await System.Threading.Tasks.Task.Run(() => tags
                            .Concat(ProcessRegionTags(snapshot, cts.Token))
                            .Concat(ProcessCellTags(snapshot, cts.Token))
                            .ToArray(),
                            cts.Token
                        );
                    } catch (OperationCanceledException) {
                        return;
                    }
                } else if (_tags != null) {
                    _tags = Array.Empty<TagSpan>();
                } else {
                    return;
                }

                TagsChanged?.Invoke(
                    this,
                    new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length))
                );
            }

            internal static IEnumerable<TagSpan> ProcessRegionTags(ITextSnapshot snapshot, CancellationToken cancel) {
                Stack<ITextSnapshotLine> regions = new Stack<ITextSnapshotLine>();
                // Walk lines and attempt to find '#region'/'#endregion' tags
                foreach (var line in snapshot.Lines) {
                    cancel.ThrowIfCancellationRequested();
                    var lineText = line.GetText();
                    if (_openingRegionRegex.IsMatch(lineText)) {
                        regions.Push(line);
                    } else if (_closingRegionRegex.IsMatch(lineText) && regions.Count > 0) {
                        var openLine = regions.Pop();
                        var outline = GetTagSpan(openLine.Start, line.End);

                        yield return outline;
                    }
                }
            }

            internal static IEnumerable<TagSpan> ProcessCellTags(ITextSnapshot snapshot, CancellationToken cancel) {
                if (snapshot.LineCount == 0) {
                    yield break;
                }

                // Walk lines and attempt to find code cell tags
                var line = snapshot.GetLineFromLineNumber(0);
                int previousCellStart = -1;
                while (line != null) {
                    cancel.ThrowIfCancellationRequested();
                    var cellStart = CodeCellAnalysis.FindStartOfCell(line);
                    if (cellStart == null || cellStart.LineNumber == previousCellStart) {
                        if (line.LineNumber + 1 < snapshot.LineCount) {
                            line = snapshot.GetLineFromLineNumber(line.LineNumber + 1);
                        } else {
                            break;
                        }
                    } else {
                        previousCellStart = cellStart.LineNumber;
                        var cellEnd = CodeCellAnalysis.FindEndOfCell(cellStart, line);
                        if (cellEnd.LineNumber > cellStart.LineNumber) {
                            yield return GetTagSpan(cellStart.End, cellEnd.End);
                        }
                        if (cellEnd.LineNumber + 1 < snapshot.LineCount) {
                            line = snapshot.GetLineFromLineNumber(cellEnd.LineNumber + 1);
                        } else {
                            break;
                        }
                    }
                }
            }

            internal static TagSpan GetTagSpan(SnapshotPoint start, SnapshotPoint end) {
                TagSpan tagSpan = null;
                var snapshot = start.Snapshot;
                try {
                    SnapshotPoint hintEnd = end;
                    if (start.GetContainingLine().LineNumber + 5 < hintEnd.GetContainingLine().LineNumber) {
                        hintEnd = start.Snapshot.GetLineFromLineNumber(start.GetContainingLine().LineNumber + 5).End;
                    }

                    return new TagSpan(
                        new SnapshotSpan(start, end),
                        new SnapshotSpan(start, hintEnd)
                    );
                } catch (ArgumentException) {
                    // sometimes Python's parser gives us bad spans, ignore those and fix the parser
                    Debug.Assert(false, "bad argument when making span/tag");
                }

                Debug.Assert(tagSpan != null, "failed to create tag span with start={0} and end={0}".FormatUI(start, end));
                return tagSpan;
            }

            private static Span GetFinalSpan(ITextSnapshot snapshot, int start, int length) {
                int cnt = 0;
                var text = snapshot.GetText(start, length);

                // remove up to 2 \r\n's if we just end with these, this will leave a space between the methods
                while (length > 0 && ((Char.IsWhiteSpace(text[length - 1])) || ((text[length - 1] == '\r' || text[length - 1] == '\n') && cnt++ < 4))) {
                    length--;
                }
                return new Span(start, length);
            }

            private SnapshotSpan? ShouldInclude(Statement statement, NormalizedSnapshotSpanCollection spans) {
                if (spans.Count == 1 && spans[0].Length == spans[0].Snapshot.Length) {
                    // we're processing the entire snapshot
                    return spans[0];
                }

                for (int i = 0; i < spans.Count; i++) {
                    if (spans[i].IntersectsWith(Span.FromBounds(statement.StartIndex, statement.EndIndex))) {
                        return spans[i];
                    }
                }
                return null;
            }

            async Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
                if (e.Event == PythonTextBufferInfoEvents.NewParseTree) {
                    // TODO: Reconsider whether we process asynchronously and then marshal
                    // at the end.
                    await _services.Site.GetUIThread().InvokeTask(() => UpdateTagsAsync(sender, e.AnalysisEntry));
                }
            }

            public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

            #endregion
        }


        internal class TagSpan : ITagSpan<IOutliningRegionTag> {
            public TagSpan(SnapshotSpan span, SnapshotSpan? hintSpan) {
                Span = span;
                Tag = new OutliningTag(hintSpan ?? span.Start.GetContainingLine().Extent);
            }

            public SnapshotSpan Span { get; }

            public IOutliningRegionTag Tag { get; }
        }

        internal class OutliningTag : IOutliningRegionTag {
            private readonly SnapshotSpan _hintSpan;

            public OutliningTag(SnapshotSpan hintSpan) {
                _hintSpan = hintSpan;
            }

            public object CollapsedForm => "...";

            public object CollapsedHintForm => _hintSpan.GetText();

            public bool IsDefaultCollapsed => false;

            public bool IsImplementation => true;
        }
    }

    static class OutliningTaggerProviderExtensions {
        public static OutliningTaggerProvider.OutliningTagger GetOutliningTagger(this ITextView self) {
            return PythonTextBufferInfo.TryGetForBuffer(self.TextBuffer)?.TryGetSink(typeof(OutliningTaggerProvider.OutliningTagger))
                as OutliningTaggerProvider.OutliningTagger;
        }
    }
}
