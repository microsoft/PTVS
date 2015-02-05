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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    class DecoratorCompletionAnalysis : CompletionAnalysis {
        internal DecoratorCompletionAnalysis(ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(span, textBuffer, options) {
        }

        private static bool IsDecoratorType(MemberResult member) {
            switch (member.MemberType) {
                case Interpreter.PythonMemberType.Function:
                case Interpreter.PythonMemberType.Class:
                    // Classes and functions need further checking
                    break;
                case Interpreter.PythonMemberType.Module:
                case Interpreter.PythonMemberType.Namespace:
                    // Always include modules
                    return true;
                default:
                    // Never include anything else
                    return false;
            }

            // TODO: Only include objects that look like decorators
            // This is probably impossible to tell, since a decorator may be
            // called immediately or as part of creating the function. Filtering
            // down to callables that return a callable would work, but if our
            // analysis has failed then items could randomly be missing.
            return true;
        }

        public override CompletionSet GetCompletions(IGlyphService glyphService) {
            var start = _stopwatch.ElapsedMilliseconds;

            var analysis = GetAnalysisEntry();
            var index = VsProjectAnalyzer.TranslateIndex(
                Span.GetEndPoint(TextBuffer.CurrentSnapshot).Position,
                TextBuffer.CurrentSnapshot,
                analysis
            );

            var completions = analysis.GetAllAvailableMembersByIndex(index, GetMemberOptions.None)
                .Where(IsDecoratorType)
                .Select(member => PythonCompletion(glyphService, member))
                .OrderBy(completion => completion.DisplayText);


            var res = new FuzzyCompletionSet("PythonDecorators", "Python", Span, completions, _options, CompletionComparer.UnderscoresLast);

            var end = _stopwatch.ElapsedMilliseconds;

            if (/*Logging &&*/ end - start > TooMuchTime) {
                Trace.WriteLine(String.Format("{0} lookup time {1} for {2} completions", this, end - start, res.Completions.Count));
            }

            return res;
        }
    }
}
