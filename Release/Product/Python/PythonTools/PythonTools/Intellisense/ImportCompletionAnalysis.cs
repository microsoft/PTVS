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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    internal class AsKeywordCompletionAnalysis : CompletionAnalysis {
        public AsKeywordCompletionAnalysis(ITrackingSpan span, ITextBuffer buffer, CompletionOptions options)
            : base(span, buffer, options) { }

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

        private ImportCompletionAnalysis(string[] ns, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
            _namespace = ns;
        }

        public static CompletionAnalysis Make(IList<ClassificationSpan> tokens, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options) {
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
                    return new AsKeywordCompletionAnalysis(span, textBuffer, options);
                }
                return new ImportCompletionAnalysis(ns.ToArray(), span, textBuffer, options);
            }

            return new ImportCompletionAnalysis(new string[0], span, textBuffer, options);
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
