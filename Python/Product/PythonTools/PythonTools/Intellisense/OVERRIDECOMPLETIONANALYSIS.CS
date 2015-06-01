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
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is overriding a function.
    /// </summary>
    internal class OverrideCompletionAnalysis : CompletionAnalysis {
        internal OverrideCompletionAnalysis(ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
        }

        private static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static string GetSafeParameterName(ParameterResult result, int index) {
            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                return GetSafeArgumentName(result, index) + " = " + result.DefaultValue;
            }
            return GetSafeArgumentName(result, index);
        }

        private static string GetSafeArgumentName(ParameterResult result, int index) {
            var match = ValidParameterName.Match(result.Name);

            if (match.Success) {
                return match.Value;
            } else if (result.Name.StartsWith("**")) {
                return "**kwargs";
            } else if (result.Name.StartsWith("*")) {
                return "*args";
            } else {
                return "arg" + index.ToString();
            }
        }

        private string MakeCompletionString(IOverloadResult result, string className) {
            var sb = new StringBuilder();

            sb.AppendLine(result.Name + "(" + string.Join(", ", result.Parameters.Select((p, i) => GetSafeParameterName(p, i))) + "):");

            var span = Span.GetSpan(TextBuffer.CurrentSnapshot);
            int defIndent = span.Start.GetContainingLine().GetText().IndexOf("def");
            
            if (_options.ConvertTabsToSpaces) {
                sb.Append(' ', defIndent + _options.IndentSize);
            } else {
                sb.Append('\t', defIndent / 8 + 1);
            }

            if (result.Parameters.Length > 0) {
                var parameterString = string.Join(", ", result.Parameters.Skip(1).Select((p, i) => GetSafeArgumentName(p, i + 1)));

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

            var line = Span.GetStartPoint(TextBuffer.CurrentSnapshot).GetContainingLine();
            var startPos = line.Start.Position + line.GetText().IndexOf("def");

            var analysis = GetAnalysisEntry();
            if (analysis == null) {
                return null;
            }

            var snapshot = TextBuffer.CurrentSnapshot;
            var pos = VsProjectAnalyzer.TranslateIndex(startPos, snapshot, analysis);

            var cls = analysis.GetDefinitionTree(pos).LastOrDefault(member => member.MemberType == Interpreter.PythonMemberType.Class);
            var members = analysis.GetOverrideable(pos).ToArray();
            var completions = members
                .Select(member => PythonCompletion(glyphService,
                    member.Name,
                    MakeCompletionString(member, cls.Name),
                    member.Documentation,
                    StandardGlyphGroup.GlyphGroupOverload));

            CompletionSet res;
            if (completions.Any()) {
                var set = new FuzzyCompletionSet(
                    "PythonOverrides",
                    "Python",
                    Span,
                    completions,
                    _options,
                    CompletionComparer.UnderscoresLast
                );
                set.CommitByDefault = false;
                res = set;
            } else {
                res = new CompletionSet();
            }

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} classes", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}