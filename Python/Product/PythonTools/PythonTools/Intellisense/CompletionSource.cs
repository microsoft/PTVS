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

using System.Collections.Generic;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    public static class CompletionSessionExtensions {
        public static CompletionOptions GetOptions(this ICompletionSession session) {
            var options = new CompletionOptions {
                ConvertTabsToSpaces = session.TextView.Options.IsConvertTabsToSpacesEnabled(),
                IndentSize = session.TextView.Options.GetIndentSize(),
                TabSize = session.TextView.Options.GetTabSize()
            };

            if (PythonToolsPackage.Instance != null) {
                options.IntersectMembers = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.IntersectMembers;
                options.HideAdvancedMembers = PythonToolsPackage.Instance.LangPrefs.HideAdvancedMembers;
                options.FilterCompletions = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.FilterCompletions;
                options.SearchMode = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.SearchMode;
            }
            return options;
        }
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
            var span = session.GetApplicableSpan(textBuffer);
            var triggerPoint = session.GetTriggerPoint(textBuffer);
            var options = session.GetOptions();
            var provider = textBuffer.CurrentSnapshot.GetCompletions(span, triggerPoint, options);

            var completions = provider.GetCompletions(_provider._glyphService);
           
            if (completions != null && completions.Completions.Count > 0) {
                completionSets.Add(completions);
            }
        }

        public void Dispose() {
        }
    }
}
