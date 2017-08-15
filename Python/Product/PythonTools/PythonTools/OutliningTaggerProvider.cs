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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudioTools;

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
            return (ITagger<T>)_services.GetBufferInfo(buffer).GetOrCreateOutliningTagger(b => new OutliningTagger(_services, b));
        }

        #endregion

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
            Justification = "Object is owned by VS and cannot be disposed")]
        internal class OutliningTagger : ITagger<IOutliningRegionTag> {
            private readonly PythonTextBufferInfo _buffer;
            private readonly PythonEditorServices _services;
            private TagSpan[] _tags = Array.Empty<TagSpan>();
            private CancellationTokenSource _processing;
            private static readonly Regex _openingRegionRegex = new Regex(@"^\s*#\s*region($|\s+.*$)");
            private static readonly Regex _closingRegionRegex = new Regex(@"^\s*#\s*endregion($|\s+.*$)");

            public OutliningTagger(PythonEditorServices services, PythonTextBufferInfo buffer) {
                _services = services;
                _buffer = buffer;
                _buffer.OnNewParseTree += OnNewParseTree;
                Enabled = _services.Python?.AdvancedOptions.EnterOutliningModeOnOpen ?? true;
            }

            public bool Enabled { get; private set; }

            public void Enable() {
                Enabled = true;
                var snapshot = _buffer.CurrentSnapshot;
                var tagsChanged = TagsChanged;
                if (tagsChanged != null) {
                    tagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
                }
            }

            public void Disable() {
                Enabled = false;
                var snapshot = _buffer.CurrentSnapshot;
                var tagsChanged = TagsChanged;
                if (tagsChanged != null) {
                    tagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
                }
            }

            #region ITagger<IOutliningRegionTag> Members

            public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
                return _tags;
            }

            private void OnNewParseTree(object sender, EventArgs e) {
                _services.Site.GetUIThread().InvokeTask(() => UpdateTagsAsync(_buffer.AnalysisEntry))
                    .HandleAllExceptions(_services.Site, GetType())
                    .DoNotWait();
            }

            private async System.Threading.Tasks.Task UpdateTagsAsync(AnalysisEntry entry) {
                if (entry == null) {
                    return;
                }

                var snapshot = _buffer.CurrentSnapshot;
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
                        var outline = GetTagSpan(snapshot, openLine.Start, line.End);

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
                            yield return GetTagSpan(snapshot, cellStart.Start, cellEnd.End);
                        }
                        if (cellEnd.LineNumber + 1 < snapshot.LineCount) {
                            line = snapshot.GetLineFromLineNumber(cellEnd.LineNumber + 1);
                        } else {
                            break;
                        }
                    }
                }
            }

            internal static TagSpan GetTagSpan(ITextSnapshot snapshot, int start, int end, int headerIndex = -1) {
                TagSpan tagSpan = null;
                try {
                    // if the user provided a -1, we should figure out the end of the first line
                    if (headerIndex < 0) {
                        headerIndex = snapshot.GetLineFromPosition(start).End.Position;
                    }

                    if (start != -1 && end != -1) {
                        int length = end - headerIndex;
                        if (length > 0) {
                            Debug.Assert(start + length <= snapshot.Length, String.Format("{0} + {1} <= {2} end was {3}", start, length, snapshot.Length, end));
                            var span = GetFinalSpan(
                                snapshot,
                                headerIndex,
                                length
                            );

                            tagSpan = new TagSpan(
                                new SnapshotSpan(snapshot, span),
                                new OutliningTag(snapshot, span)
                            );
                        }
                    }
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

            public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

            #endregion
        }


        internal class TagSpan : ITagSpan<IOutliningRegionTag> {
            private readonly SnapshotSpan _span;
            private readonly OutliningTag _tag;

            public TagSpan(SnapshotSpan span, OutliningTag tag) {
                _span = span;
                _tag = tag;
            }

            #region ITagSpan<IOutliningRegionTag> Members

            public SnapshotSpan Span {
                get { return _span; }
            }

            public IOutliningRegionTag Tag {
                get { return _tag; }
            }

            #endregion
        }

        internal class OutliningTag : IOutliningRegionTag {
            private readonly ITextSnapshot _snapshot;
            private readonly Span _span;

            public OutliningTag(ITextSnapshot iTextSnapshot, Span span) {
                _snapshot = iTextSnapshot;
                _span = span;
            }

            #region IOutliningRegionTag Members

            public object CollapsedForm {
                get { return "..."; }
            }

            public object CollapsedHintForm {
                get {
                    string collapsedHint = _snapshot.GetText(_span);

                    string[] lines = collapsedHint.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    // remove any leading white space for the preview
                    if (lines.Length > 0) {
                        int smallestWhiteSpace = Int32.MaxValue;
                        for (int i = 0; i < lines.Length; i++) {
                            string curLine = lines[i];

                            for (int j = 0; j < curLine.Length; j++) {
                                if (curLine[j] != ' ') {
                                    smallestWhiteSpace = Math.Min(j, smallestWhiteSpace);
                                }
                            }
                        }

                        for (int i = 0; i < lines.Length; i++) {
                            if (lines[i].Length >= smallestWhiteSpace) {
                                lines[i] = lines[i].Substring(smallestWhiteSpace);
                            }
                        }

                        return String.Join("\r\n", lines);
                    }
                    return collapsedHint;
                }
            }

            public bool IsDefaultCollapsed {
                get { return false; }
            }

            public bool IsImplementation {
                get { return true; }
            }

            #endregion
        }
    }

    static class OutliningTaggerProviderExtensions {
        public static OutliningTaggerProvider.OutliningTagger GetOutliningTagger(this ITextView self) {
            return PythonTextBufferInfo.TryGetForBuffer(self.TextBuffer)?.OutliningTagger;
        }
    }
}
