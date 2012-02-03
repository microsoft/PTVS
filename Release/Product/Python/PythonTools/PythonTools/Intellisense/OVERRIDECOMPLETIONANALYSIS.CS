/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba)
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
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is overriding a function.
    /// </summary>
    internal class OverrideCompletionAnalysis : CompletionAnalysis {
        internal OverrideCompletionAnalysis(string text, int pos, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(text, pos, span, textBuffer, options) {
        }

        internal static int OverrideSort(IOverloadResult x, IOverloadResult y) {
            return NormalCompletionAnalysis.MemberSortComparison(x.Name, y.Name);
        }

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*( *=.+)?", RegexOptions.IgnoreCase);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }

        internal static readonly Regex ValidArgumentName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase);
        internal static string GetSafeArgumentName(ParameterResult result) {
            var match = ValidArgumentName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }

        internal string MakeCompletionString(IOverloadResult result, string className) {
            var sb = new StringBuilder();

            sb.AppendLine(result.Name + "(" + string.Join(", ", result.Parameters.Select(GetSafeParameterName)) + "):");

            var span = Span.GetSpan(TextBuffer.CurrentSnapshot);
            int indentCount = span.Start - span.Start.GetContainingLine().Start - 4; // "def ".Length
            
            if (_options.ConvertTabsToSpaces) {
                sb.Append(' ', indentCount + _options.IndentSize);
            } else {
                sb.Append('\t', indentCount + 1);
            }

            if (result.Parameters.Length > 0) {
                var parameterString = string.Join(", ", result.Parameters.Skip(1).Select(GetSafeArgumentName));

                if (GetAnalysisEntry().ProjectState.LanguageVersion.Is3x()) {
                    sb.AppendFormat("return super().{0}({1})",
                        result.Name,
                        parameterString);
                } else if (!string.IsNullOrEmpty(className)) {
                    sb.AppendFormat("return super({0}, {1}).{2}({3})",
                        className,
                        result.Parameters.First().Name,
                        result.Name,
                        parameterString);
                } else {
                    sb.Append("pass");
                }
            } else {
                sb.Append("pass");
            }

            return sb.ToString();
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var analysis = GetAnalysisEntry();
            var cls = analysis.GetDefinitionTreeByIndex(_pos).LastOrDefault(member => member.MemberType == Interpreter.PythonMemberType.Class);
            var members = analysis.GetOverrideableByIndex(_pos).ToArray();
            Array.Sort(members, OverrideSort);
            var completions = members
                .Select(member => PythonCompletion(glyphService,
                    member.Name,
                    MakeCompletionString(member, cls.Name),
                    member.Documentation,
                    StandardGlyphGroup.GlyphGroupOverload));

            var res = new PythonCompletionSet(Text, Text, Span, completions, new Completion[0]);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} classes", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}