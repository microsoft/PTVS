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
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Intellisense {
    class CompletionSource : ICompletionSource {
        private readonly ITextBuffer _textBuffer;
        private readonly CompletionSourceProvider _provider;

        public CompletionSource(CompletionSourceProvider provider, ITextBuffer textBuffer) {
            _textBuffer = textBuffer;
            _provider = provider;
        }

        public void AugmentCompletionSession(ICompletionSession session, IList<CompletionSet> completionSets) {
            var textBuffer = _textBuffer;
            var span = session.CreateTrackingSpan(textBuffer);
            bool intersectMembers = true;
            bool hideAdvanced = false;
            if (PythonToolsPackage.Instance != null) {
                intersectMembers = PythonToolsPackage.Instance.AdvancedEditorOptionsPage.IntersectMembers;
                hideAdvanced = PythonToolsPackage.Instance.LangPrefs.HideAdvancedMembers;
            }
            var provider = textBuffer.CurrentSnapshot.GetCompletions(span, intersectMembers, hideAdvanced);

            var completions = provider.GetCompletions(_provider._glyphService);
           
            if (completions == null || completions.Completions.Count == 0) {
                if (PythonToolsPackage.Instance != null && 
                    !session.TextView.GetAnalyzer().InterpreterFactory.IsAnalysisCurrent()) {
                    // no completions, inform the user via the status bar that the analysis is not yet complete.
                    var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
                    statusBar.SetText(Resources.WarningAnalysisNotCurrent);
                }
                return;
            }

            completionSets.Add(completions);
        }

        public void Dispose() {
        }
    }
}
