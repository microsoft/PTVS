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
using System.Diagnostics;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is doing an import
    /// </summary>
    internal class ImportCompletionAnalysis : CompletionAnalysis {
        internal ImportCompletionAnalysis(string text, int pos, ITrackingSpan span, ITextBuffer textBuffer)
            : base(text, pos, span, textBuffer) {
        }

        public static CompletionAnalysis Make(ClassificationSpan start, ClassificationSpan end, Span loc,
                ITextSnapshot snapshot, ITrackingSpan span, ITextBuffer buffer, bool isSpace) {
            if (start == end) {
                return new ImportCompletionAnalysis(String.Empty, loc.Start, span, buffer);
            } else if (!isSpace) {
                int nsLen = end.Span.End - start.Span.End - 1;
                var nsSpan = new SnapshotSpan(snapshot, start.Span.End + 1, nsLen);
                var text = nsSpan.GetText().Trim();
                return new ImportCompletionAnalysis(text, loc.Start, span, buffer);
            } else {
                return EmptyCompletionContext;
            }
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var completions = GetModules(glyphService, Text);
            var res = new PythonCompletionSet(Text, Text, Span, completions, new Completion[0]);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} imports", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}
