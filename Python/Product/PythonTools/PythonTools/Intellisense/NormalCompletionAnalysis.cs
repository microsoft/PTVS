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
using System.Linq;
using Microsoft.PythonTools.Analysis;
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

        internal NormalCompletionAnalysis(VsProjectAnalyzer analyzer, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
            _snapshot = snapshot;
            _analyzer = analyzer;
        }

        private string FixupCompletionText(string exprText) {
            if (exprText.EndsWith(".")) {
                exprText = exprText.Substring(0, exprText.Length - 1);
                if (exprText.Length == 0) {
                    // don't return all available members on empty dot.
                    return null;
                }
            } else {
                int cut = exprText.LastIndexOfAny(new[] { '.', ']', ')' });
                if (cut != -1) {
                    exprText = exprText.Substring(0, cut);
                } else {
                    exprText = String.Empty;
                }
            }
            return exprText;
        }

        internal string PrecedingExpression {
            get {
                var startSpan = _snapshot.CreateTrackingSpan(Span.GetSpan(_snapshot).Start.Position, 0, SpanTrackingMode.EdgeInclusive);
                var parser = new ReverseExpressionParser(_snapshot, _snapshot.TextBuffer, startSpan);
                using (var e = parser.GetEnumerator()) {
                    if (e.MoveNext() &&
                        e.Current != null &&
                        e.Current.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Number)) {
                        return null;
                    }
                }

                var sourceSpan = parser.GetExpressionRange();
                if (sourceSpan.HasValue && sourceSpan.Value.Length > 0) {
                    return sourceSpan.Value.GetText();
                }
                return string.Empty;
            }
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start1 = _stopwatch.ElapsedMilliseconds;

            var members = Enumerable.Empty<MemberResult>();

            IReplEvaluator eval;
            IPythonReplIntellisense pyReplEval = null;

            if (_snapshot.TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval)) {
                pyReplEval = eval as IPythonReplIntellisense;
            }

            var analysis = GetAnalysisEntry();
            var text = PrecedingExpression;
            if (text == null) {
                return null;
            } else if (text != string.Empty) {
                string fixedText = FixupCompletionText(text);
                if (analysis != null && fixedText != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                    lock (_analyzer) {
                        members = members.Concat(analysis.GetMembersByIndex(
                            fixedText,
                            VsProjectAnalyzer.TranslateIndex(
                                Span.GetEndPoint(_snapshot).Position,
                                _snapshot,
                                analysis
                            ),
                            _options.MemberOptions
                        ).ToArray());
                    }
                }

                if (pyReplEval != null && fixedText != null && _snapshot.TextBuffer.GetAnalyzer().ShouldEvaluateForCompletion(fixedText)) {
                    var replStart = _stopwatch.ElapsedMilliseconds;

                    var replMembers = pyReplEval.GetMemberNames(fixedText);
                    if (replMembers != null) {
                        members = members.Union(replMembers, CompletionComparer.MemberEquality);
                    }
                }
            } else {
                members = analysis.GetAllAvailableMembersByIndex(
                    VsProjectAnalyzer.TranslateIndex(
                        Span.GetStartPoint(_snapshot).Position,
                        _snapshot,
                        analysis
                    ),
                    _options.MemberOptions
                );
            }

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                var memberArray = members.ToArray();
                members = memberArray;
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} members", this, end - start1, members.Count()));
            }

            var start = _stopwatch.ElapsedMilliseconds;

            var result = new FuzzyCompletionSet(
                "Python",
                "Python",
                Span,
                members.Select(m => PythonCompletion(glyphService, m)),
                _options,
                CompletionComparer.UnderscoresLast);

            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} completion set time {1} total time {2}", this, end - start, end - start1));
            }

            return result;
        }

    }
}
