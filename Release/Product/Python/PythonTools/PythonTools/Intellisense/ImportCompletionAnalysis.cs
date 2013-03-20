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
    /// Provides the completion context for when the user is doing an import
    /// </summary>
    internal class ImportCompletionAnalysis : CompletionAnalysis {
        private readonly string[] _namespace;
        
        internal ImportCompletionAnalysis(IList<ClassificationSpan> tokens, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {

            Debug.Assert(tokens[0].Span.GetText() == "import" || tokens[0].Span.GetText() == "from");

            int beforeLastComma = tokens
                .Reverse()
                .SkipWhile(tok => !tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Comma))
                .Count();

            if (tokens.Count >= 2 && beforeLastComma < tokens.Count) {
                int spanEnd = Span.GetEndPoint(textBuffer.CurrentSnapshot).Position;
                var nameParts = new List<string>();
                bool removeLastPart = false, lastWasError = false;
                foreach(var tok in tokens.Skip(beforeLastComma > 0 ? beforeLastComma : 1)) {
                    if (tok.ClassificationType.IsOfType(PredefinedClassificationTypeNames.Identifier)) {
                        nameParts.Add(tok.Span.GetText());
                        // Only remove the last part if the trigger point is
                        // not right at the end of it.
                        removeLastPart = (tok.Span.End.Position != spanEnd);
                    } else if (tok.ClassificationType.IsOfType(PythonPredefinedClassificationTypeNames.Dot)) {
                        removeLastPart = false;
                    } else {
                        lastWasError = true;
                        break;
                    }
                }

                if (!lastWasError) {
                    if (removeLastPart && nameParts.Count > 0) {
                        nameParts.RemoveAt(nameParts.Count - 1);
                    }
                    _namespace = nameParts.ToArray();
                }
            }
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var completions = GetModules(_namespace, true).Select(m => PythonCompletion(glyphService, m));

            var res = new FuzzyCompletionSet("PythonImports", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} imports", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}
