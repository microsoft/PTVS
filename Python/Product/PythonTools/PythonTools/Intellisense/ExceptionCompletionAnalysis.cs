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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides the completion context for when the user is doing an import
    /// </summary>
    internal class ExceptionCompletionAnalysis : CompletionAnalysis {
        internal ExceptionCompletionAnalysis(PythonEditorServices services, ICompletionSession session, ITextView view, ITrackingSpan span, ITextBuffer textBuffer, CompletionOptions options)
            : base(services, session, view, span, textBuffer, options) {
        }

        private static readonly string[] KnownExceptions = new[] { "GeneratorExit", "KeyboardInterrupt", 
            "StopIteration", "SystemExit" };

        private static bool IsExceptionType(CompletionResult member) {
            switch (member.MemberType) {
                case Interpreter.PythonMemberType.Class:
                    // Classes need further checking
                    break;
                case Interpreter.PythonMemberType.Module:
                case Interpreter.PythonMemberType.Namespace:
                    // Always include modules
                    return true;
                default:
                    // Never include anything else
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
            if (analysis == null) {
                return null;
            }

            var index = VsProjectAnalyzer.TranslateIndex(
                Span.GetEndPoint(TextBuffer.CurrentSnapshot).Position,
                TextBuffer.CurrentSnapshot,
                analysis
            );

            var analyzer = analysis.Analyzer;
            var completions = analyzer.WaitForRequest(analyzer.GetAllAvailableMembersAsync(analysis, index, GetMemberOptions.None), "GetCompletions")
                .MaybeEnumerate()
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