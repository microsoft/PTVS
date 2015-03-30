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
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
#endif

    internal class NormalCompletionAnalysis : CompletionAnalysis {
        private readonly ITextSnapshot _snapshot;
        private readonly VsProjectAnalyzer _analyzer;
        private readonly IServiceProvider _serviceProvider;

        internal NormalCompletionAnalysis(VsProjectAnalyzer analyzer, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options, IServiceProvider serviceProvider)
            : base(span, textBuffer, options) {
            _snapshot = snapshot;
            _analyzer = analyzer;
            _serviceProvider = serviceProvider;
        }

        internal bool GetPrecedingExpression(out string text, out SnapshotSpan statementExtent) {
            text = string.Empty;
            statementExtent = default(SnapshotSpan);

            var startSpan = _snapshot.CreateTrackingSpan(Span.GetSpan(_snapshot).Start.Position, 0, SpanTrackingMode.EdgeInclusive);
            var parser = new ReverseExpressionParser(_snapshot, _snapshot.TextBuffer, startSpan);
            using (var e = parser.GetEnumerator()) {
                if (e.MoveNext() &&
                    e.Current != null &&
                    e.Current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Number)) {
                    return false;
                }
            }

            var sourceSpan = parser.GetExpressionRange();
            if (sourceSpan.HasValue && sourceSpan.Value.Length > 0) {
                text = sourceSpan.Value.GetText();
                if (text.EndsWith(".")) {
                    text = text.Substring(0, text.Length - 1);
                    if (text.Length == 0) {
                        // don't return all available members on empty dot.
                        return false;
                    }
                } else {
                    int cut = text.LastIndexOfAny(new[] { '.', ']', ')' });
                    if (cut != -1) {
                        text = text.Substring(0, cut);
                    } else {
                        text = String.Empty;
                    }
                }
            }


            statementExtent = parser.GetStatementRange() ?? default(SnapshotSpan);

            return true;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start1 = _stopwatch.ElapsedMilliseconds;

            IEnumerable<MemberResult> members = null;
            IEnumerable<MemberResult> replMembers = null;

            IReplEvaluator eval;
            IPythonReplIntellisense pyReplEval = null;

            if (_snapshot.TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval)) {
                pyReplEval = eval as IPythonReplIntellisense;
            }

            var analysis = GetAnalysisEntry();

            string text;
            SnapshotSpan statementRange;
            if (!GetPrecedingExpression(out text, out statementRange)) {
                return null;
            } else if (string.IsNullOrEmpty(text)) {
                if (analysis != null) {
                    lock (_analyzer) {
                        var location = VsProjectAnalyzer.TranslateIndex(
                            statementRange.Start.Position,
                            statementRange.Snapshot,
                            analysis
                        );
                        var parameters = GetParameterNames(analysis, location)
                            .Select(n => new MemberResult(n, PythonMemberType.Field));
                        members = analysis.GetAllAvailableMembers(location, _options.MemberOptions)
                            .Union(parameters, CompletionComparer.MemberEquality);
                    }
                }

                if (pyReplEval != null) {
                    replMembers = pyReplEval.GetMemberNames(string.Empty);
                }
            } else {
                if (analysis != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                    lock (_analyzer) {
                        var location = VsProjectAnalyzer.TranslateIndex(
                            statementRange.Start.Position,
                            statementRange.Snapshot,
                            analysis
                        );

                        members = analysis.GetMembers(text, location, _options.MemberOptions);
                    }
                }

                if (pyReplEval != null && _snapshot.TextBuffer.GetAnalyzer(_serviceProvider).ShouldEvaluateForCompletion(text)) {
                    replMembers = pyReplEval.GetMemberNames(text);
                }
            }

            if (replMembers != null) {
                if (members != null) {
                    members = members.Union(replMembers, CompletionComparer.MemberEquality);
                } else {
                    members = replMembers;
                }
            }

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                if (members != null) {
                    var memberArray = members.ToArray();
                    members = memberArray;
                    Trace.WriteLine(String.Format("{0} lookup time {1} for {2} members", this, end - start1, members.Count()));
                } else {
                    Trace.WriteLine(String.Format("{0} lookup time {1} for zero members", this, end - start1));
                }
            }

            if (members == null) {
                // The expression is invalid so we shouldn't provide
                // a completion set at all.
                return null;
            }

            var start = _stopwatch.ElapsedMilliseconds;

            var result = new FuzzyCompletionSet(
                "Python",
                "Python",
                Span,
                members.Select(m => PythonCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast,
                matchInsertionText: true
            );

            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} completion set time {1} total time {2}", this, end - start, end - start1));
            }

            return result;
        }

        private IEnumerable<string> GetParameterNames(ModuleAnalysis analysis, SourceLocation location) {
            var argsParser = new ReverseExpressionParser(_snapshot, _snapshot.TextBuffer, Span);
            
            int index;
            SnapshotPoint? sigStart;
            string lastKeywordArg;
            bool isParameterName;
            var span = argsParser.GetExpressionRange(1, out index, out sigStart, out lastKeywordArg, out isParameterName);

            string target = null;
            if (sigStart.HasValue && string.IsNullOrEmpty(lastKeywordArg)) {
                var applicableTo = _snapshot.GetApplicableSpan(sigStart.Value.Position - 1);
                if (applicableTo != null) {
                    target = applicableTo.GetText(_snapshot);
                }
            }

            if (string.IsNullOrEmpty(target)) {
                return Enumerable.Empty<string>();
            }

            return analysis.GetSignatures(target, location)
                .SelectMany(s => s.Parameters)
                .Select(p => p.Name)
                .Distinct();
        }

        class FindCurrentCalleeWalker : PythonWalkerWithLocation {
            public FindCurrentCalleeWalker(int location) : base(location) { }

            public Expression Callee { get; private set; }

            public override bool Walk(CallExpression node) {
                if (base.Walk(node)) {
                    Callee = node.Target;
                    return true;
                }
                return false;
            }

            public override void PostWalk(CallExpression node) {
                base.PostWalk(node);
            }
        }


    }
}
