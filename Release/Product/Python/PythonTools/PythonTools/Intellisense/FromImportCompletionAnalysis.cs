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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    internal class ImportKeywordCompletionAnalysis : CompletionAnalysis {
        public ImportKeywordCompletionAnalysis(ITrackingSpan span, ITextBuffer buffer, CompletionOptions options)
            : base(span, buffer, options) { }

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

        private FromImportCompletionAnalysis(string[] ns, bool includeStar, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {

            _namespace = ns;
            _includeStar = includeStar;
        }

        public static CompletionAnalysis Make(IList<ClassificationSpan> tokens, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
            Debug.Assert(tokens[0].Span.GetText() == "from");
            int beforeImportToken = tokens
                .TakeWhile(tok => !IsKeyword(tok, "import"))
                .Count();

            bool includeStar = false;
            var lastToken = tokens.LastOrDefault();
            if (lastToken != null) {
                if (lastToken.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier) &&
                    lastToken.Span.End < span.GetEndPoint(textBuffer.CurrentSnapshot).Position) {
                    // If the last token was a name, but the cursor is not on
                    // that name anymore...
                    if (beforeImportToken == tokens.Count) {
                        // "from a.b "...
                        return new ImportKeywordCompletionAnalysis(span, textBuffer, options);
                    } else {
                        // "from a.b import x "...
                        // "from a.b import x, y "...
                        return new AsKeywordCompletionAnalysis(span, textBuffer, options);
                    }
                } else if (beforeImportToken == tokens.Count) {
                    // "from "...
                    return ImportCompletionAnalysis.Make(tokens, span, textBuffer, options);
                } else if (beforeImportToken == tokens.Count - 1) {
                    // "from a.b "...
                    includeStar = true;
                }
            }

            if (tokens.Take(tokens.Count - 1).Any(tok => tok != null && tok.Span != null && tok.Span.GetText() == "*")) {
                // No completions after "*" has been imported
                return EmptyCompletionContext;
            }

            bool seenAs = false;
            foreach (var tok in tokens.Skip(beforeImportToken + 1)) {
                if (IsKeyword(tok, "as")) {
                    seenAs = true;
                } else if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma)) {
                    seenAs = false;
                }
            }
            if (seenAs) {
                // "from a.b import x as "...
                // "from a.b import x, y as "...
                // BUT NOT "from a.b import x as x1, "...
                return EmptyCompletionContext;
            }

            var ns = new List<string>();
            bool expectDot = false;
            foreach (var tok in tokens.Take(beforeImportToken).Skip(1)) {
                if (expectDot) {
                    if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Dot)) {
                        expectDot = false;
                    } else {
                        return EmptyCompletionContext;
                    }
                } else {
                    if (tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                        ns.Add(tok.Span.GetText());
                        expectDot = true;
                    } else {
                        return EmptyCompletionContext;
                    }
                }
            }

            if (!expectDot) {
                return EmptyCompletionContext;
            }

            return new FromImportCompletionAnalysis(ns.ToArray(), includeStar, span, textBuffer, options);
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
                var completion = new[] { PythonCompletion(glyphService, "*", "Import all members from the module", StandardGlyphGroup.GlyphArrow) };
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
