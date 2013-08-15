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

        internal static readonly Regex ValidParameterName = new Regex(@"^(\*|\*\*)?[a-z_][a-z0-9_]*", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        internal static string GetSafeParameterName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            var name = match.Success ? match.Value : result.Name;
            if (!string.IsNullOrEmpty(result.DefaultValue)) {
                name += " = " + result.DefaultValue;
            }
            return name;
        }

        internal static string GetSafeArgumentName(ParameterResult result) {
            var match = ValidParameterName.Match(result.Name);

            return match.Success ? match.Value : result.Name;
        }

        internal string MakeCompletionString(IOverloadResult result, string className) {
            var sb = new StringBuilder();

            sb.AppendLine(result.Name + "(" + string.Join(", ", result.Parameters.Select(GetSafeParameterName)) + "):");

            var span = Span.GetSpan(TextBuffer.CurrentSnapshot);
            int defIndent = span.Start.GetContainingLine().GetText().IndexOf("def");
            
            if (_options.ConvertTabsToSpaces) {
                sb.Append(' ', defIndent + _options.IndentSize);
            } else {
                sb.Append('\t', defIndent / 8 + 1);
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

            var line = Span.GetStartPoint(TextBuffer.CurrentSnapshot).GetContainingLine();
            int lineStart = line.Start.Position;
            int defIndent = line.GetText().IndexOf("def");
            
            var pyAnalysis = TextBuffer.GetPythonAnalysis();
            PythonAst ast;
            IAnalysisCookie cookie;
            pyAnalysis.GetTreeAndCookie(out ast, out cookie);
            var sCookie = cookie as SnapshotCookie;
            if (sCookie != null) {
                // Map the analysis position to its location in the analyzed
                // snapshot, but keep the indent level unchanged. This ensures
                // that if the user has typed 'def' before the analysis is
                // updated they will get completions for the correct class.
                if (sCookie.Snapshot.TextBuffer != TextBuffer) {
                    IBufferGraph graph;
                    if (sCookie.Snapshot.TextBuffer.Properties.TryGetProperty(typeof(IBufferGraph), out graph)) {
                        var newLine = graph.MapDownToInsertionPoint(line.Start, PointTrackingMode.Positive, PythonCoreConstants.IsPythonContent);
                        if (newLine != null) {
                            lineStart = newLine.Value.Position;
                        }
                    }
                } else if (sCookie.Snapshot != line.Snapshot) {
                    var newLine = Span.GetStartPoint(sCookie.Snapshot).GetContainingLine();
                    lineStart = newLine.Start.Position;
                }
            }

            int pos = lineStart + defIndent;

            var analysis = GetAnalysisEntry();
            var cls = analysis.GetDefinitionTreeByIndex(pos).LastOrDefault(member => member.MemberType == Interpreter.PythonMemberType.Class);
            var members = analysis.GetOverrideableByIndex(pos).ToArray();
            var completions = members
                .Select(member => PythonCompletion(glyphService,
                    member.Name,
                    MakeCompletionString(member, cls.Name),
                    member.Documentation,
                    StandardGlyphGroup.GlyphGroupOverload));

            var res = new FuzzyCompletionSet("PythonOverrides", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} classes", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}