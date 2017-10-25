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
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    internal class AsKeywordCompletionAnalysis : CompletionAnalysis {
        public AsKeywordCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer buffer, CompletionOptions options)
            : base(services, session, view, span, buffer, options) { }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var completion = new[] { PythonCompletion(glyphService, "as", null, StandardGlyphGroup.GlyphKeyword) };
            return new FuzzyCompletionSet("PythonAsKeyword", "Python", Span, completion, _options, CompletionComparer.UnderscoresLast);
        }
    }

    /// <summary>
    /// Provides the completion context for when the user is doing an import
    /// </summary>
    internal class ImportCompletionAnalysis : CompletionAnalysis {
        private static readonly Regex _validNameRegex = new Regex("^[a-zA-Z_][a-zA-Z0-9_]*$");
        private readonly string[] _namespace;

        private ImportCompletionAnalysis(PythonEditorServices services, string[] ns, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
            _namespace = ns;
        }

        public static CompletionAnalysis Make(PythonEditorServices services, IList<ClassificationSpan> tokens, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
            Debug.Assert(tokens[0].Span.GetText() == "import" || tokens[0].Span.GetText() == "from");

            if (tokens.Count >= 2) {
                var ns = new List<string>();
                bool expectDot = false, skipToComma = false;
                foreach (var tok in tokens.SkipWhile(tok => !tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier))) {
                    if (skipToComma) {
                        if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma)) {
                            expectDot = false;
                            skipToComma = false;
                            ns.Clear();
                        }
                    } else if (expectDot) {
                        if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Dot)) {
                            expectDot = false;
                        } else if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma)) {
                            expectDot = false;
                            ns.Clear();
                        } else {
                            skipToComma = true;
                        }
                    } else {
                        if (tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                            ns.Add(tok.Span.GetText());
                            expectDot = true;
                        } else {
                            skipToComma = true;
                        }
                    }
                }

                if (skipToComma) {
                    return EmptyCompletionContext;
                }
                if (expectDot) {
                    return new AsKeywordCompletionAnalysis(services, session, view, span, textBuffer, options);
                }
                return new ImportCompletionAnalysis(services, ns.ToArray(), session, view, span, textBuffer, options);
            }

            return new ImportCompletionAnalysis(services, new string[0], session, view, span, textBuffer, options);
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var completions = GetModules(_namespace, true)
                .Where(m => _validNameRegex.IsMatch(m.Name))
                .Select(m => PythonCompletion(glyphService, m));

            var res = new FuzzyCompletionSet("PythonImports", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} imports", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}
