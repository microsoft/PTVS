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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    class DecoratorCompletionAnalysis : CompletionAnalysis {
        internal DecoratorCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
        }

        private static bool IsDecoratorType(CompletionResult member) {
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
            if (analysis == null) {
                return null;
            }

            var index = VsProjectAnalyzer.TranslateIndex(
                Span.GetEndPoint(TextBuffer.CurrentSnapshot).Position,
                TextBuffer.CurrentSnapshot,
                analysis
            );

            var completions = analysis.Analyzer.GetAllAvailableMembersAsync(analysis, index, GetMemberOptions.None).Result
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
