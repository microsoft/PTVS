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
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is doing an import
    /// </summary>
    internal class ExceptionCompletionAnalysis : CompletionAnalysis {
        internal ExceptionCompletionAnalysis(ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
        }

        private static readonly string[] KnownExceptions = new[] { "GeneratorExit", "KeyboardInterrupt", 
            "StopIteration", "SystemExit" };

        private static bool IsExceptionType(MemberResult member) {
            if (member.MemberType != Interpreter.PythonMemberType.Class) {
                return false;
            }

            if (KnownExceptions.Contains(member.Name)) {
                return true;
            }

            if (member.Name.IndexOf("Exception", StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                member.Name.IndexOf("Error", StringComparison.CurrentCultureIgnoreCase) >= 0) {
                return true;
            }

            return false;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var analysis = GetAnalysisEntry();
            var index = VsProjectAnalyzer.TranslateIndex(Span.GetEndPoint(TextBuffer.CurrentSnapshot).Position, TextBuffer.CurrentSnapshot, analysis);
            var completions = analysis.GetAllAvailableMembersByIndex(index, GetMemberOptions.None)
                .Where(IsExceptionType)
                .Select(member => PythonCompletion(glyphService, member))
                .OrderBy(completion => completion.DisplayText);


            var res = new FuzzyCompletionSet("PythonExceptions", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} classes", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}