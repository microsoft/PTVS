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
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    internal class ImportKeywordCompletionAnalysis : CompletionAnalysis {
        public ImportKeywordCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer buffer, CompletionOptions options)
            : base(services, session, view, span, buffer, options) { }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var completion = new[] { PythonCompletion(glyphService, "import", null, StandardGlyphGroup.GlyphKeyword) };
            return new FuzzyCompletionSet("PythonImportKeyword", "Python", Span, completion, _options, CompletionComparer.UnderscoresLast);
        }
    }

    /// <summary>
    /// Provides the completion context for when the user is doing an "import from"
    /// </summary>
    internal class FromImportCompletionAnalysis : CompletionAnalysis {
        private readonly string[] _namespace;
        private readonly bool _includeStar;

        private FromImportCompletionAnalysis(PythonEditorServices services, string[] ns, bool includeStar, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {

            _namespace = ns;
            _includeStar = includeStar;
        }

        public static CompletionAnalysis Make(PythonEditorServices services, IList<ClassificationSpan> tokens, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
            Debug.Assert(tokens[0].Span.GetText() == "from");

            var ns = new List<string>();
            bool nsComplete = false;
            bool seenImport = false;
            bool seenName = false;
            bool seenAs = false;
            bool seenAlias = false;
            bool includeStar = true;
            foreach (var token in tokens.Skip(1)) {
                if (token == null || token.Span.End > span.GetEndPoint(textBuffer.CurrentSnapshot).Position) {
                    break;
                }

                if (!seenImport) {
                    if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                        ns.Add(token.Span.GetText());
                        nsComplete = true;
                    } else if (token.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Dot)) {
                        nsComplete = false;
                    }
                    seenImport = IsKeyword(token, "import");
                } else if (token.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma)) {
                    seenName = false;
                    seenAs = false;
                    seenAlias = false;
                    includeStar = false;
                } else if (token.Span.GetText() == "*") {
                    // Nothing comes after a star
                    return EmptyCompletionContext;
                } else if (IsKeyword(token, "as")) {
                    seenAs = true;
                } else if (token.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                    if (seenAlias) {
                        return EmptyCompletionContext;
                    } else if (seenAs) {
                        seenAlias = true;
                    } else if (seenName) {
                        return EmptyCompletionContext;
                    } else {
                        seenName = true;
                    }
                } else {
                    includeStar = false;
                }
            }
            if (!seenImport) {
                if (nsComplete) {
                    return new ImportKeywordCompletionAnalysis(services, session, view, span, textBuffer, options);
                } else {
                    return ImportCompletionAnalysis.Make(services, tokens, session, view, span, textBuffer, options);
                }
            }

            if (!nsComplete || seenAlias || seenAs) {
                return EmptyCompletionContext;
            }

            if (seenName) {
                return new AsKeywordCompletionAnalysis(services, session, view, span, textBuffer, options);
            }

            return new FromImportCompletionAnalysis(services, ns.ToArray(), includeStar, session, view, span, textBuffer, options);
        }

        private static string GetText(ITextSnapshot snapshot, ClassificationSpan start, ClassificationSpan target, bool includeEnd) {
            var nsLen = (includeEnd ? target.Span.End : target.Span.Start) - start.Span.End - 1;
            var nsSpan = new SnapshotSpan(snapshot, start.Span.End + 1, nsLen);
            var nsText = nsSpan.GetText().Trim();
            return nsText;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;
            var completions = GetModules(_namespace, false).Select(m => PythonCompletion(glyphService, m));

            if (_includeStar) {
                var completion = new[] { PythonCompletion(glyphService, "*", Strings.FromImportCompletionImportAllMembersFromModuleTooltip, StandardGlyphGroup.GlyphArrow) };
                completions = completions.Concat(completion);
            }

            var res = new FuzzyCompletionSet("PythonFromImports", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} imports", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}
