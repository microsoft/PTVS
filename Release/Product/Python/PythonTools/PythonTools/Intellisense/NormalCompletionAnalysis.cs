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
using Microsoft.PythonTools.Repl;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
#if INTERACTIVE_WINDOW
    using IReplEvaluator = IInteractiveEngine;
#endif

    internal class NormalCompletionAnalysis : CompletionAnalysis {
        private readonly ITextSnapshot _snapshot;
        private readonly ProjectAnalyzer _analyzer;

        internal NormalCompletionAnalysis(ProjectAnalyzer analyzer, string text, int pos, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(text, pos, span, textBuffer, options) {
            _snapshot = snapshot;
            _analyzer = analyzer;
        }

        internal NormalCompletionAnalysis(ProjectAnalyzer analyzer, string text, int pos, ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer textBuffer, bool intersectMembers = true, bool hideAdvancedMembers = false, bool includeStatmentKeywords = false, bool includeExpressionKeywords = false)
            : this(analyzer, text, pos, snapshot, span, textBuffer, new CompletionOptions {
            IntersectMembers = intersectMembers,
            HideAdvancedMembers = hideAdvancedMembers,
            IncludeExpressionKeywords = includeExpressionKeywords,
            IncludeStatementKeywords = includeStatmentKeywords
        }) { }

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

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start1 = _stopwatch.ElapsedMilliseconds;

            MemberResult[] members;

            IReplEvaluator eval;
            PythonReplEvaluator pyReplEval = null;

            if (_snapshot.TextBuffer.Properties.TryGetProperty<IReplEvaluator>(typeof(IReplEvaluator), out eval)) {
                pyReplEval = eval as PythonReplEvaluator;
            }

            var analysis = GetAnalysisEntry();
            string fixedText = FixupCompletionText(Text);
            if (analysis != null && fixedText != null && (pyReplEval == null || !pyReplEval.LiveCompletionsOnly)) {
                lock (_analyzer) {
                    members = analysis.GetMembersByIndex(
                        fixedText,
                        _pos,
                        _options.MemberOptions
                    ).ToArray();
                }
            } else {
                members = new MemberResult[0];
            }

            if (pyReplEval != null && fixedText != null && _snapshot.TextBuffer.GetAnalyzer().ShouldEvaluateForCompletion(Text)) {
                var replStart = _stopwatch.ElapsedMilliseconds;
                if (members.Length == 0) {
                    members = pyReplEval.GetMemberNames(TextBuffer.GetAnalyzer(), fixedText);
                    if (members == null) {
                        members = new MemberResult[0];
                    }
                } else {
                    // prefer analysis members over live members but merge the two together.
                    Dictionary<string, MemberResult> memberDict = new Dictionary<string, MemberResult>();
                    var replMembers = pyReplEval.GetMemberNames(TextBuffer.GetAnalyzer(), fixedText);
                    if (replMembers != null) {
                        foreach (var member in replMembers) {
                            memberDict[member.Completion] = member;
                        }

                        foreach (var member in members) {
                            memberDict[member.Completion] = member;
                        }

                        members = memberDict.Values.ToArray();
                    }
                }
                Trace.WriteLine(String.Format("Repl {0} lookup time {1} for {2} members", this, _stopwatch.ElapsedMilliseconds - replStart, members.Length));
            }
            
            members = DoFilterCompletions(members);
            Array.Sort(members, ModuleSort);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} members", this, end - start1, members.Length));
            }

            var start = _stopwatch.ElapsedMilliseconds;
            
            var result = new PythonCompletionSet(
                Text,
                Text,
                _snapshot.CreateTrackingSpan(_pos, 0, SpanTrackingMode.EdgeInclusive),
                TransformMembers(glyphService, members),
                new Completion[0]);

            end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ (end - start1) > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} completion set time {1} total time {2}", this, end - start, end - start1));
            }

            return result;
        }

        private IEnumerable<Completion> TransformMembers(IGlyphService glyphService, MemberResult[] members) {
            return members.Select(m => PythonCompletion(glyphService, m));
        }

        private MemberResult[] DoFilterCompletions(MemberResult[] members) {
            if (_options.HideAdvancedMembers) {
                members = FilterCompletions(members, Text, (completion, filter) => completion.StartsWith(filter) && (!completion.StartsWith("__") || ! completion.EndsWith("__")));
            } else {
                members = FilterCompletions(members, Text, (x, y) => x.StartsWith(y));
            }
            return members;
        }

        internal static MemberResult[] FilterCompletions(MemberResult[] completions, string text, Func<string, string, bool> filterFunc) {
            var cut = text.LastIndexOfAny(new[] { '.', ']', ')' });
            var filter = (cut == -1) ? text : text.Substring(cut + 1);

            var result = new List<MemberResult>(completions.Length);
            foreach (var comp in completions) {
                if (filterFunc(comp.Completion, filter)) {
                    result.Add(comp.FilterCompletion(comp.Completion.Substring(filter.Length)));
                }
            }
            return result.ToArray();
        }

        internal static int ModuleSort(MemberResult x, MemberResult y) {
            return MemberSortComparison(x.Name, y.Name);
        }

        /// <summary>
        /// Sorts members for displaying in completion list.  The member sort
        /// moves all __x__ functions to the end of the list.  Members which
        /// start with a single underscore (private members) are sorted as if 
        /// they did not start with an underscore.
        /// </summary> 
        internal static int MemberSortComparison(string xName, string yName) {
            bool xUnder = xName.StartsWith("__") && xName.EndsWith("__");
            bool yUnder = yName.StartsWith("__") && yName.EndsWith("__");

            if (xUnder != yUnder) {
                // The one that starts with an underscore comes later
                return xUnder ? 1 : -1;
            }
            
            bool xSingleUnder = xName.StartsWith("_");
            bool ySingleUnder = yName.StartsWith("_");
            if (xSingleUnder != ySingleUnder) {
                // The one that starts with an underscore comes later
                return xSingleUnder ? 1 : -1;
            }

            return String.Compare(xName, yName, StringComparison.OrdinalIgnoreCase); 
        }

    }
}
