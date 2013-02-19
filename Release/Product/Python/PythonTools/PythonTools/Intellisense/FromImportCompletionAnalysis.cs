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
    /// <summary>
    /// Provides the completion context for when the user is doing an "import from"
    /// </summary>
    internal class FromImportCompletionAnalysis : CompletionAnalysis {
        private readonly string[] _namespace;
        private readonly bool _importKeywordOnly;

        public FromImportCompletionAnalysis(IList<ClassificationSpan> tokens, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
            
            Debug.Assert(tokens[0].Span.GetText() == "from");

            int beforeImportToken = tokens
                .TakeWhile(tok => !(tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Keyword) && tok.Span.GetText() == "import"))
                .Count();

            if (beforeImportToken >= 2) {
                // If there are at least two tokens ('from' <name>) before the
                // 'import' token, use completions from that package.
                if (beforeImportToken < tokens.Count || tokens.Last().ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Dot)) {
                    _namespace = tokens
                        .Take(beforeImportToken)
                        .Where(tok => tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier))
                        .Select(tok => tok.Span.GetText())
                        .ToArray();
                } else {
                    _importKeywordOnly = true;
                }
            }
        }

        private static string GetText(ITextSnapshot snapshot, ClassificationSpan start, ClassificationSpan target, bool includeEnd) {
            var nsLen = (includeEnd ? target.Span.End : target.Span.Start) - start.Span.End - 1;
            var nsSpan = new SnapshotSpan(snapshot, start.Span.End + 1, nsLen);
            var nsText = nsSpan.GetText().Trim();
            return nsText;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            if (_importKeywordOnly) {
                var completion = new[] { PythonCompletion(glyphService, "import", null, StandardGlyphGroup.GlyphKeyword) };
                return new FuzzyCompletionSet("PythonImportKeyword", "Python", Span, completion, _options, CompletionComparer.UnderscoresLast);
            }

            var start = _stopwatch.ElapsedMilliseconds;

            var modules = GetModules(_namespace, false);

            var completions = modules.Select(m => PythonCompletion(glyphService, m));
            if (_namespace != null && _namespace.Length > 0) {
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
        
        private IEnumerable<DynamicallyVisibleCompletion> FromCompletions(IGlyphService glyphService, IEnumerable<DynamicallyVisibleCompletion> inputs) {
            foreach (var input in inputs) {
                if (input.InsertionText.IndexOf('.') == -1) {
                    yield return input;
                }
            }

            yield return PythonCompletion(glyphService, "*", "Import all members from the module", StandardGlyphGroup.GlyphArrow);
        }
    }
}
