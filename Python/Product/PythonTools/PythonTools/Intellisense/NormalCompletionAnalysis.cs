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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.InteractiveWindow;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    internal class NormalCompletionAnalysis : CompletionAnalysis {
        private readonly ITextSnapshot _snapshot;

        internal NormalCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
            _snapshot = snapshot;
        }

        internal bool GetPrecedingExpression(out string parentExpression, out SnapshotSpan expressionExtent) {
            parentExpression = string.Empty;
            expressionExtent = default(SnapshotSpan);

            // We never want normal completions on space
            if (Session.GetTriggerCharacter() == ' ' && !Session.IsCompleteWordMode()) {
                return false;
            }

            var bi = PythonTextBufferInfo.TryGetForBuffer(_snapshot.TextBuffer);
            if (bi == null) {
                return false;
            }
            var span = Span.GetSpan(_snapshot);
            var expr = bi.GetExpressionAtPoint(span, GetExpressionOptions.EvaluateMembers);
            if (expr != null) {
                parentExpression = expr.Value.GetText() ?? "";
                expressionExtent = new SnapshotSpan(expr.Value.Start, span.End);
                return true;
            }

            expr = bi.GetExpressionAtPoint(span, GetExpressionOptions.Complete);
            if (expr != null) {
                expressionExtent = expr.Value;
                return true;
            }

            var tok = bi.GetTokenAtPoint(span.End);
            if (tok == null) {
                expressionExtent = span;
                return true;
            }

            switch (tok.Value.Category) {
                case TokenCategory.Comment:
                    return false;
                case TokenCategory.Delimiter:
                case TokenCategory.Grouping:
                case TokenCategory.Operator:
                case TokenCategory.WhiteSpace:
                    // Expect top-level completions after these
                    expressionExtent = span;
                    return true;
                //case TokenCategory.BuiltinIdentifier:
                case TokenCategory.Keyword:
                    // Expect filtered top-level completions here
                    // (but the return value is no different)
                    expressionExtent = span;
                    return true;
                case TokenCategory.Identifier:
                    // When preceded by a delimiter, grouping, or operator
                    var tok2 = bi.GetTokensInReverseFromPoint(span.End).Where(t => t.Category != TokenCategory.WhiteSpace && t.Category != TokenCategory.Comment).Take(2).ToArray();
                    if (tok2.Length == 2 && tok2[0].Category == tok.Value.Category) {
                        switch (tok2[1].Category) {
                            case TokenCategory.Delimiter:
                            case TokenCategory.Grouping:
                            case TokenCategory.Operator:
                                expressionExtent = span;
                                return true;
                        }
                    }
                    break;
            }

            return false;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start1 = _stopwatch.ElapsedMilliseconds;

            var interactiveWindow = _snapshot.TextBuffer.GetInteractiveWindow();
            var pyReplEval = interactiveWindow?.Evaluator as IPythonInteractiveIntellisense;

            var bufferInfo = GetBufferInfo();
            var analysis = bufferInfo?.AnalysisEntry;
            var analyzer = analysis?.Analyzer;

            if (analyzer == null) {
                return null;
            }

            IEnumerable<CompletionResult> members = null;

            var span = Span.GetSpan(bufferInfo.CurrentSnapshot);
            var point = span.Start;

            var location = VsProjectAnalyzer.TranslateIndex(
                point.Position,
                point.Snapshot,
                analysis
            );

            var completions = analyzer.WaitForRequest(analyzer.GetCompletionsAsync(analysis, location, _options.MemberOptions), "GetCompletions.GetMembers");

            if (completions.items == null) {
                return null;
            }

            members = completions.items.Select(c => new CompletionResult(
                c.label,
                c.insertText ?? c.label,
                c.documentation?.value,
                Enum.TryParse(c._kind, true, out PythonMemberType mt) ? mt : PythonMemberType.Unknown,
                null
            ));

            if (pyReplEval?.Analyzer != null && (string.IsNullOrEmpty(completions._expr) || pyReplEval.Analyzer.ShouldEvaluateForCompletion(completions._expr))) {
                var replMembers = pyReplEval.GetMemberNames(completions._expr ?? "");
                if (replMembers != null) {
                    if (members != null) {
                        members = members.Union(replMembers, CompletionComparer.MemberEquality);
                    } else {
                        members = replMembers;
                    }
                }
            }

            if (pyReplEval == null && (completions._allowSnippet ?? false)) {
                var expansions = analyzer.WaitForRequest(EditorServices.Python?.GetExpansionCompletionsAsync(), "GetCompletions.GetExpansions", null, 5);
                if (expansions != null) {
                    // Expansions should come first, so that they replace our keyword
                    // completions with the more detailed snippets.
                    if (members != null) {
                        members = expansions.Union(members, CompletionComparer.MemberEquality);
                    } else {
                        members = expansions;
                    }
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
            result.CommitByDefault = completions._commitByDefault ?? true;

            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} completion set time {1} total time {2}", this, end - start, end - start1));
            }

            return result;
        }
    }
}
