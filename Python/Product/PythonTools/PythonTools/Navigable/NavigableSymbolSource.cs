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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Classification;

namespace Microsoft.PythonTools.Navigable {
    class NavigableSymbolSource : INavigableSymbolSource {
        private readonly ITextView _view;
        private readonly ITextBuffer _buffer;
        private readonly IClassifier _classifier;
        private readonly ITextStructureNavigator _textNavigator;

        public NavigableSymbolSource(ITextView textView, ITextBuffer buffer, IClassifier classifier, ITextStructureNavigator textNavigator) {
            _view = textView ?? throw new ArgumentNullException(nameof(textView));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _textNavigator = textNavigator ?? throw new ArgumentNullException(nameof(textNavigator));
        }

        public void Dispose() {
        }

        public async Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken token) {
            Debug.Assert(triggerSpan.Length == 1);

            token.ThrowIfCancellationRequested();

            var extent = _textNavigator.GetExtentOfWord(triggerSpan.Start);
            if (!extent.IsSignificant) {
                return null;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var classification in _classifier.GetClassificationSpans(extent.Span)) {
                token.ThrowIfCancellationRequested();

                var classificationName = classification.ClassificationType.Classification.ToLower();
                if (classificationName.Contains("identifier") || classificationName.Contains("user types")) {
                    return new NavigableSymbol(classification.Span);
                }
            }

            return null;
        }
    }
}
