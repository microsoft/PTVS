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
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools {
    [Export(typeof(ITaggerProvider)), ContentType(PythonCoreConstants.ContentType)]
    [TagType(typeof(IOutliningRegionTag))]
    class OutliningTaggerProvider : ITaggerProvider {
        private readonly PythonToolsService _pyService;

        [ImportingConstructor]
        public OutliningTaggerProvider([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _pyService = serviceProvider.GetPythonToolsService();
        }

        #region ITaggerProvider Members

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            return (ITagger<T>)(buffer.GetOutliningTagger() ?? new OutliningTagger(_pyService, buffer));
        }

        #endregion

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
            Justification = "Object is owned by VS and cannot be disposed")]
        internal class OutliningTagger : ITagger<IOutliningRegionTag> {
            private readonly ITextBuffer _buffer;
            private readonly Timer _timer;
            private readonly PythonToolsService _pyService;
            private bool _enabled, _eventHooked;
            private static readonly Regex _openingRegionRegex = new Regex(@"^\s*#\s*region($|\s+.*$)");
            private static readonly Regex _closingRegionRegex = new Regex(@"^\s*#\s*endregion($|\s+.*$)");

            public OutliningTagger(PythonToolsService pyService, ITextBuffer buffer) {
                _pyService = pyService;
                _buffer = buffer;
                _buffer.Properties[typeof(OutliningTagger)] = this;
                _enabled = _pyService.AdvancedOptions.EnterOutliningModeOnOpen;
                _timer = new Timer(TagUpdate, null, Timeout.Infinite, Timeout.Infinite);
            }

            public bool Enabled {
                get {
                    return _enabled;
                }
            }

            public void Enable() {
                _enabled = true;
                var snapshot = _buffer.CurrentSnapshot;
                var tagsChanged = TagsChanged;
                if (tagsChanged != null) {
                    tagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
                }
            }

            public void Disable() {
                _enabled = false;
                var snapshot = _buffer.CurrentSnapshot;
                var tagsChanged = TagsChanged;
                if (tagsChanged != null) {
                    tagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
                }
            }

            #region ITagger<IOutliningRegionTag> Members

            public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
                IPythonProjectEntry entry;
                if (_enabled && _buffer.TryGetPythonProjectEntry(out entry)) {
                    if (!_eventHooked) {
                        entry.OnNewParseTree += OnNewParseTree;
                        _eventHooked = true;
                    }
                    PythonAst ast;
                    IAnalysisCookie cookie;
                    entry.GetTreeAndCookie(out ast, out cookie);
                    SnapshotCookie snapCookie = cookie as SnapshotCookie;

                    if (ast != null &&
                        snapCookie != null &&
                        snapCookie.Snapshot.TextBuffer == spans[0].Snapshot.TextBuffer) {   // buffer could have changed if file was closed and re-opened
                        return ProcessSuite(ast, snapCookie.Snapshot);
                    }
                }

                return new ITagSpan<IOutliningRegionTag>[0];
            }

            private void OnNewParseTree(object sender, EventArgs e) {
                IPythonProjectEntry entry;
                if (_buffer.TryGetPythonProjectEntry(out entry)) {
                    _timer.Change(300, Timeout.Infinite);
                }
            }

            private void TagUpdate(object unused) {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
                var snapshot = _buffer.CurrentSnapshot;
                var tagsChanged = TagsChanged;
                if (tagsChanged != null) {
                    tagsChanged(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, new Span(0, snapshot.Length))));
                }
            }

            private IEnumerable<ITagSpan<IOutliningRegionTag>> ProcessSuite(PythonAst ast, ITextSnapshot snapshot) {
                return ProcessOutliningTags(ast, snapshot).Concat(ProcessRegionTags(snapshot));
            }

            internal static IEnumerable<TagSpan> ProcessOutliningTags(PythonAst ast, ITextSnapshot snapshot) {
                var walker = new OutliningWalker(snapshot);
                ast.Walk(walker);
                return walker.TagSpans;
            }

            internal static IEnumerable<TagSpan> ProcessRegionTags(ITextSnapshot snapshot){
                Stack<ITextSnapshotLine> regions = new Stack<ITextSnapshotLine>();
                // Walk lines and attempt to find '#region'/'#endregion' tags
                foreach (var line in snapshot.Lines) {
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

            internal static TagSpan GetTagSpan(ITextSnapshot snapshot, int start, int end, int headerIndex = -1, DecoratorStatement decorators = null) {
                TagSpan tagSpan = null;
                try {
                    if (decorators != null) {
                        // we don't want to collapse the decorators, we like them visible, so
                        // we base our starting position on where the decorators end.
                        start = decorators.EndIndex + 1;
                    }

                    // if the user provided a -1, we should figure out the end of the first line
                    if (headerIndex < 0) {
                        int startLineEnd = snapshot.GetLineFromPosition(start).End.Position;
                        headerIndex = startLineEnd;
                    }

                    if (start != -1 && end != -1) {
                        int length = end - headerIndex;
                        if (length > 0) {
                            var span = GetFinalSpan(snapshot,
                                headerIndex,
                                length
                            );

                            tagSpan = new TagSpan(
                                new SnapshotSpan(snapshot, span),
                                new OutliningTag(snapshot, span, true)
                            );
                        }
                    }
                } catch (ArgumentException) {
                    // sometimes Python's parser gives us bad spans, ignore those and fix the parser
                    Debug.Assert(false, "bad argument when making span/tag");
                }

                return tagSpan;
            }

            private static Span GetFinalSpan(ITextSnapshot snapshot, int start, int length) {
                Debug.Assert(start + length <= snapshot.Length);
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

        class OutliningWalker : PythonWalker {
            public readonly List<TagSpan> TagSpans = new List<TagSpan>();
            readonly ITextSnapshot _snapshot;

            public OutliningWalker(ITextSnapshot snapshot) {
                this._snapshot = snapshot;
            }

            // Compound Statements: if, while, for, try, with, func, class, decorated
            public override bool Walk(IfStatement node) {
                if (node.ElseStatement != null) {
                    AddTagIfNecessaryShowLineAbove(node.ElseStatement, "else");
                }

                return base.Walk(node);
            }

            public override bool Walk(IfStatementTest node) {
                if (node.Test != null && node.Body != null) {
                    AddTagIfNecessary(node.Test.StartIndex, node.Body.EndIndex);
                    // Don't walk test condition.
                    node.Body.Walk(this);
                }
                return false;
            }

            public override bool Walk(WhileStatement node) {
                // Walk while statements manually so we don't traverse the test.
                // This prevents the test from being collapsed ever.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex, 
                        node.Body.EndIndex, 
                        _snapshot.GetLineFromPosition(node.StartIndex).End);
                    node.Body.Walk(this);
                }
                if (node.ElseStatement != null) {
                    AddTagIfNecessaryShowLineAbove(node.ElseStatement, "else");
                    node.ElseStatement.Walk(this);
                }
                return false;
            }

            public override bool Walk(ForStatement node) {
                // Walk for statements manually so we don't traverse the list.  
                // This prevents the list and/or left from being collapsed ever.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex, 
                        node.Body.EndIndex, 
                        _snapshot.GetLineFromPosition(node.StartIndex).End);
                    node.Body.Walk(this);
                }
                if (node.Else != null) {
                    AddTagIfNecessaryShowLineAbove(node.Else, "else");
                    node.Else.Walk(this);
                }
                return false;
            }

            public override bool Walk(TryStatement node) {
                if (node.Body != null) {
                    AddTagIfNecessaryShowLineAbove(node.Body, "try");
                }
                if (node.Handlers != null) {
                    foreach (var h in node.Handlers) {
                        AddTagIfNecessaryShowLineAbove(h, "except");
                    }
                }
                if (node.Finally != null) {
                    AddTagIfNecessaryShowLineAbove(node.Finally, "finally");
                }
                if (node.Else != null) {
                    AddTagIfNecessaryShowLineAbove(node.Else, "else");
                }

                return base.Walk(node);
            }

            public override bool Walk(WithStatement node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(FunctionDefinition node) {
                // Walk manually so collapsing is not enabled for params.
                if (node.Body != null) {
                    AddTagIfNecessary(
                        node.StartIndex, 
                        node.Body.EndIndex,
                        decorator: node.Decorators);
                    node.Body.Walk(this);
                }
               
                return false;
            }

            public override bool Walk(ClassDefinition node) {
                AddTagIfNecessary(node, node.HeaderIndex + 1, node.Decorators);

                return base.Walk(node);
            }

            // Not-Compound Statements
            public override bool Walk(CallExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(FromImportStatement node){
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ListExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(TupleExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(DictionaryExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(SetExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ParenthesisExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            public override bool Walk(ConstantExpression node) {
                AddTagIfNecessary(node);
                return base.Walk(node);
            }

            private void AddTagIfNecessary(Node node, int headerIndex = -1, DecoratorStatement decorator = null) {
                AddTagIfNecessary(node.StartIndex, node.EndIndex, headerIndex, decorator);
            }

            private void AddTagIfNecessary(int startIndex, int endIndex, int headerIndex = -1, DecoratorStatement decorator = null, int minLinesToCollapse = 3) {
                var startLine = _snapshot.GetLineFromPosition(startIndex).LineNumber;
                var endLine = _snapshot.GetLineFromPosition(endIndex).LineNumber;
                var lines = endLine - startLine + 1;

                // Collapse if more than 3 lines.
                if (lines >= minLinesToCollapse) {
                    TagSpan tagSpan = OutliningTagger.GetTagSpan(
                        _snapshot,
                        startIndex,
                        endIndex,
                        headerIndex,
                        decorator);
                    TagSpans.Add(tagSpan);
                }
            }

            /// <summary>
            /// Given a node and lineAboveText enable collapsing for line
            /// showing the line above text.
            /// </summary>
            /// <param name="node"></param>
            /// <param name="lineAboveText"></param>
            private void AddTagIfNecessaryShowLineAbove(Node node, string lineAboveText) {
                int line = _snapshot.GetLineNumberFromPosition(node.StartIndex);
                var startsWithElse = _snapshot.GetLineFromLineNumber(line).GetText().Trim().StartsWith(lineAboveText);
                if (startsWithElse) {
                    // this line starts with the 'above' line, don't adjust line
                    int startIndex = _snapshot.GetLineFromLineNumber(line).Start.Position;
                    AddTagIfNecessary(startIndex, node.EndIndex);
                } else {
                    // line is below the 'above' line
                    int startIndex = _snapshot.GetLineFromLineNumber(line - 1).Start.Position;
                    AddTagIfNecessary(startIndex, node.EndIndex);
                }
            }
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
            private readonly bool _isImplementation;

            public OutliningTag(ITextSnapshot iTextSnapshot, Span span, bool isImplementation) {
                _snapshot = iTextSnapshot;
                _span = span;
                _isImplementation = isImplementation;
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
                get { return _isImplementation; }
            }

            #endregion
        }
    }

    static class OutliningTaggerProviderExtensions {
        public static OutliningTaggerProvider.OutliningTagger GetOutliningTagger(this ITextView self) {
            return self.TextBuffer.GetOutliningTagger();
        }

        public static OutliningTaggerProvider.OutliningTagger GetOutliningTagger(this ITextBuffer self) {
            OutliningTaggerProvider.OutliningTagger res;
            if (self.Properties.TryGetProperty<OutliningTaggerProvider.OutliningTagger>(typeof(OutliningTaggerProvider.OutliningTagger), out res)) {
                return res;
            }
            return null;
        }
    }
}
