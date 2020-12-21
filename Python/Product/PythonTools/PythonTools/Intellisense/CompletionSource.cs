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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;

namespace Microsoft.PythonTools.Intellisense {
    static class CompletionSessionExtensions {
        private const string CompleteWord = nameof(CompleteWord);
        private const string TriggerChar = nameof(TriggerChar);

        public static CompletionOptions GetOptions(this ICompletionSession session, IServiceProvider serviceProvider) {
            var pyService = serviceProvider.GetPythonToolsService();

            var options = new CompletionOptions {
                ConvertTabsToSpaces = session.TextView.Options.IsConvertTabsToSpacesEnabled(),
                IndentSize = session.TextView.Options.GetIndentSize(),
                TabSize = session.TextView.Options.GetTabSize(),
                IntersectMembers = pyService.AdvancedOptions.IntersectMembers,
                HideAdvancedMembers = pyService.LangPrefs.HideAdvancedMembers,
                FilterCompletions = pyService.AdvancedOptions.FilterCompletions,
            };

            return options;
        }

        public static void SetCompleteWordMode(this IIntellisenseSession session)
            => session.Properties[CompleteWord] = true;

        public static void ClearCompleteWordMode(this IIntellisenseSession session)
            => session.Properties.RemoveProperty(CompleteWord);

        public static bool IsCompleteWordMode(this IIntellisenseSession session)
            => session.Properties.TryGetProperty(CompleteWord, out bool prop) && prop;

        public static void SetTriggerCharacter(this IIntellisenseSession session, char triggerChar)
            => session.Properties[TriggerChar] = triggerChar;

        public static char GetTriggerCharacter(this IIntellisenseSession session)
            => session.Properties.TryGetProperty(TriggerChar, out char c) ? c : '\0';
    }

    class CompletionSource : ICompletionSource {
        private readonly ITextBuffer _textBuffer;
        private readonly CompletionSourceProvider _provider;

        public CompletionSource(CompletionSourceProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            var textBuffer = _textBuffer;
            var triggerPoint = session.GetTriggerPoint(textBuffer);
            var provider = _provider._pyService.GetCompletions(
                session,
                session.TextView,
                textBuffer.CurrentSnapshot,
                triggerPoint
            );

            var completions = provider.GetCompletions(_provider._glyphService);

            if (completions != null && completions.Completions.Count > 0) {
                completionSets.Add(completions);
            }
        }

        public void Dispose() {
        }
    }
}
