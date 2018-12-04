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
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Navigation.Navigable;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Navigation.Peek {
    internal sealed class PythonPeekableItemSource : IPeekableItemSource {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly ITextBuffer _textBuffer;
        private readonly PythonEditorServices _editorServices;

        public PythonPeekableItemSource(IServiceProvider serviceProvider, IPeekResultFactory peekResultFactory, ITextBuffer textBuffer) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
            _editorServices = serviceProvider.GetEditorServices();
        }

        public void AugmentPeekSession(IPeekSession session, IList<IPeekableItem> peekableItems) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            if (peekableItems == null) {
                throw new ArgumentNullException(nameof(peekableItems));
            }

            if (!string.Equals(session.RelationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var triggerPoint = session.GetTriggerPoint(_textBuffer.CurrentSnapshot);
            if (!triggerPoint.HasValue) {
                return;
            }

            ThreadHelper.JoinableTaskFactory.Run(async () => {
                var item = await GetPeekableItemAsync(_peekResultFactory, _textBuffer, triggerPoint.Value);
                if (item != null) {
                    peekableItems.Add(item);
                }
            });
        }

        private async Task<IPeekableItem> GetPeekableItemAsync(IPeekResultFactory peekResultFactory, ITextBuffer buffer, SnapshotPoint pt) {
            var entry = buffer.TryGetAnalysisEntry();
            if (entry == null) {
                return null;
            }

            var result = await NavigableSymbolSource.GetDefinitionLocationsAsync(entry, pt).ConfigureAwait(false);
            if (result.Length > 0) {
                return new PythonPeekableItem(peekResultFactory, result);
            }

            return null;
        }

        public void Dispose() {
        }
    }
}
