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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor.OptionsExtensionMethods;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Intellisense {
    public static class CompletionSessionExtensions {
        [Obsolete("A IServiceProvider should be passed in")]
        public static CompletionOptions GetOptions(this ICompletionSession session) {
            return GetOptions(session, PythonToolsPackage.Instance);
        }

        public static CompletionOptions GetOptions(this ICompletionSession session, IServiceProvider serviceProvider) {
            var pyService = serviceProvider.GetPythonToolsService();

            var options = new CompletionOptions {
                ConvertTabsToSpaces = session.TextView.Options.IsConvertTabsToSpacesEnabled(),
                IndentSize = session.TextView.Options.GetIndentSize(),
                TabSize = session.TextView.Options.GetTabSize()
            };

            options.IntersectMembers = pyService.AdvancedOptions.IntersectMembers;
            options.HideAdvancedMembers = pyService.LangPrefs.HideAdvancedMembers;
            options.FilterCompletions = pyService.AdvancedOptions.FilterCompletions;
            options.SearchMode = pyService.AdvancedOptions.SearchMode;
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
            var options = session.GetOptions(_provider._serviceProvider);
            var provider = textBuffer.CurrentSnapshot.GetCompletions(_provider._serviceProvider, span, triggerPoint, options);

            var completions = provider.GetCompletions(_provider._glyphService);
           
            if (completions != null && completions.Completions.Count > 0) {
                completionSets.Add(completions);
            }
        }

        public void Dispose() {
        }
    }
}
