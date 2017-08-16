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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is overriding a function.
    /// </summary>
    internal class OverrideCompletionAnalysis : CompletionAnalysis {
        internal OverrideCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var line = Span.GetStartPoint(TextBuffer.CurrentSnapshot).GetContainingLine();
            var startPos = line.Start.Position + line.GetText().IndexOf("def");

            var analysis = GetAnalysisEntry();
            if (analysis == null) {
                return null;
            }

            var span = Span.GetSpan(TextBuffer.CurrentSnapshot);
            int defIndent = span.Start.GetContainingLine().GetText().IndexOf("def");

            string indentation;
            if (_options.ConvertTabsToSpaces) {
                indentation = new string(' ', defIndent + _options.IndentSize);
            } else {
                indentation = new string('\t', defIndent / 8 + 1);
            }

            var snapshot = TextBuffer.CurrentSnapshot;
            var pos = VsProjectAnalyzer.TranslateIndex(startPos, snapshot, analysis);

            var completions = analysis.Analyzer.WaitForRequest(analysis.Analyzer.GetOverrideCompletionsAsync(
                analysis,
                TextBuffer,
                pos,
                indentation
            ), "OverrideCompletionAnalysis.GetOverrideCompletions")?.overrides;

            CompletionSet res;
            if (completions != null && completions.Any()) {
                var set = new FuzzyCompletionSet(
                    "PythonOverrides",
                    "Python",
                    Span,
                    completions.Select(
                        x => PythonCompletion(
                            glyphService,
                            x.name,
                            x.completion,
                            x.doc,
                            StandardGlyphGroup.GlyphGroupOverload
                        )
                    ).ToArray(),
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