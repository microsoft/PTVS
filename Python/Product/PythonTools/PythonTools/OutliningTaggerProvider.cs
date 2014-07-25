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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.PythonTools {
    [Export(typeof(ITaggerProvider)), ContentType(PythonCoreConstants.ContentType)]
    [TagType(typeof(IOutliningRegionTag))]
    class OutliningTaggerProvider : ITaggerProvider {
        #region ITaggerProvider Members

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag {
            return (ITagger<T>)(buffer.GetOutliningTagger() ?? new OutliningTagger(buffer));
        }

        #endregion

        [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable",
            Justification = "Object is owned by VS and cannot be disposed")]
        internal class OutliningTagger : ITagger<IOutliningRegionTag> {
            private readonly ITextBuffer _buffer;
            private readonly Timer _timer;
            private bool _enabled, _eventHooked;

            public OutliningTagger(ITextBuffer buffer) {
                _buffer = buffer;
                _buffer.Properties[typeof(OutliningTagger)] = this;
                if (PythonToolsPackage.Instance != null) {
                    _enabled = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.EnterOutliningModeOnOpen;
                }
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
                        return ProcessSuite(spans, ast, ast.Body as SuiteStatement, snapCookie.Snapshot, true);
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

            private IEnumerable<ITagSpan<IOutliningRegionTag>> ProcessSuite(NormalizedSnapshotSpanCollection spans, PythonAst ast, SuiteStatement suite, ITextSnapshot snapshot, bool isTopLevel) {
                if (suite != null) {
                    // TODO: Binary search the statements?  The perf of this seems fine for the time being
                    // w/ a 5000+ line file though.
                    foreach (var statement in suite.Statements) {
                        SnapshotSpan? span = ShouldInclude(statement, spans);
                        if (span == null) {
                            continue;
                        }

                        FunctionDefinition funcDef = statement as FunctionDefinition;
                        if (funcDef != null) {
                            TagSpan tagSpan = GetFunctionSpan(ast, snapshot, funcDef);

                            if (tagSpan != null) {
                                yield return tagSpan;
                            }

                            // recurse into the class definition and outline it's members
                            foreach (var v in ProcessSuite(spans, ast, funcDef.Body as SuiteStatement, snapshot, false)) {
                                yield return v;
                            }
                        }

                        ClassDefinition classDef = statement as ClassDefinition;
                        if (classDef != null) {
                            TagSpan tagSpan = GetClassSpan(ast, snapshot, classDef);

                            if (tagSpan != null) {
                                yield return tagSpan;
                            }

                            // recurse into the class definition and outline it's members
                            foreach (var v in ProcessSuite(spans, ast, classDef.Body as SuiteStatement, snapshot, false)) {
                                yield return v;
                            }
                        }

                        if (isTopLevel) {
                            IfStatement ifStmt = statement as IfStatement;
                            if (ifStmt != null) {
                                TagSpan tagSpan = GetIfSpan(ast, snapshot, ifStmt);

                                if (tagSpan != null) {
                                    yield return tagSpan;
                                }
                            }

                            WhileStatement whileStatment = statement as WhileStatement;
                            if (whileStatment != null) {
                                TagSpan tagSpan = GetWhileSpan(ast, snapshot, whileStatment);

                                if (tagSpan != null) {
                                    yield return tagSpan;
                                }
                            }

                            ForStatement forStatement = statement as ForStatement;
                            if (forStatement != null) {
                                TagSpan tagSpan = GetForSpan(ast, snapshot, forStatement);

                                if (tagSpan != null) {
                                    yield return tagSpan;
                                }
                            }
                        }
                    }
                }
            }

            private static TagSpan GetForSpan(PythonAst ast, ITextSnapshot snapshot, ForStatement forStmt) {
                if (forStmt.List != null) {
                    return GetTagSpan(snapshot, forStmt.StartIndex, forStmt.EndIndex, forStmt.List.EndIndex);
                }
                return null;
            }

            private static TagSpan GetWhileSpan(PythonAst ast, ITextSnapshot snapshot, WhileStatement whileStmt) {
                return GetTagSpan(
                    snapshot, 
                    whileStmt.StartIndex, 
                    whileStmt.EndIndex, 
                    whileStmt.Test.EndIndex
                );
            }

            private static TagSpan GetIfSpan(PythonAst ast, ITextSnapshot snapshot, IfStatement ifStmt) {
                return GetTagSpan(snapshot, ifStmt.StartIndex, ifStmt.EndIndex, ifStmt.Tests[0].HeaderIndex);
            }

            private static TagSpan GetFunctionSpan(PythonAst ast, ITextSnapshot snapshot, FunctionDefinition funcDef) {
                return GetTagSpan(snapshot, funcDef.StartIndex, funcDef.EndIndex, funcDef.HeaderIndex, funcDef.Decorators);
            }

            private static TagSpan GetClassSpan(PythonAst ast, ITextSnapshot snapshot, ClassDefinition classDef) {
                return GetTagSpan(snapshot, classDef.StartIndex, classDef.EndIndex, classDef.HeaderIndex, classDef.Decorators);
            }

            private static TagSpan GetTagSpan(ITextSnapshot snapshot, int start, int end, int headerIndex, DecoratorStatement decorators = null) {
                TagSpan tagSpan = null;
                try {
                    if (decorators != null) {
                        // we don't want to collapse the decorators, we like them visible, so
                        // we base our starting position on where the decorators end.
                        start = decorators.EndIndex + 1;
                    }
                    int testLen = headerIndex - start + 1;
                    if (start != -1 && end != -1) {
                        int length = end - start - testLen;
                        if (length > 0) {
                            var span = GetFinalSpan(snapshot,
                                start + testLen,
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

        class TagSpan : ITagSpan<IOutliningRegionTag> {
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

        class OutliningTag : IOutliningRegionTag {
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
