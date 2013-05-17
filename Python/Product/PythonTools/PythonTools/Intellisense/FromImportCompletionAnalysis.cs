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

            if (beforeImportToken == tokens.Count) {
                if (tokens.Last().ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                    return new ImportKeywordCompletionAnalysis(span, textBuffer, options);
                } else {
                    return new ImportCompletionAnalysis(tokens, span, textBuffer, options);
                }
            }

            if (tokens.Last().ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                return EmptyCompletionContext;
            }

            if (tokens.Take(tokens.Count - 1).Any(tok => tok != null && tok.Span != null && tok.Span.GetText() == "*")) {
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
                } else if (tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                    ns.Add(tok.Span.GetText());
                    expectDot = true;
                } else {
                    return EmptyCompletionContext;
                }
            }

            if (!expectDot) {
                return EmptyCompletionContext;
            }

            bool includeStar = IsKeyword(tokens.Last(), "import");

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
