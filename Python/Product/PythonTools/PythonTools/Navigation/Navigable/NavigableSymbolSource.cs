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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.LanguageServerClient;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Newtonsoft.Json.Linq;
using LSP = Microsoft.VisualStudio.LanguageServer.Protocol;

namespace Microsoft.PythonTools.Navigation.Navigable {
    class NavigableSymbolSource : INavigableSymbolSource {
        private readonly IServiceProvider _serviceProvider;
        private readonly ITextBuffer _buffer;
        private readonly IClassifier _classifier;
        private readonly ITextStructureNavigator _textNavigator;

        private static readonly string[] _classifications = new string[] {
            PredefinedClassificationTypeNames.Identifier,
            PredefinedClassificationTypeNames.Type,
            PythonPredefinedClassificationTypeNames.Class,
            PythonPredefinedClassificationTypeNames.Function,
            PythonPredefinedClassificationTypeNames.Module,
            PythonPredefinedClassificationTypeNames.Parameter,
        };

        public NavigableSymbolSource(IServiceProvider serviceProvider, ITextBuffer buffer, IClassifier classifier, ITextStructureNavigator textNavigator) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
            _textNavigator = textNavigator ?? throw new ArgumentNullException(nameof(textNavigator));
        }

        public void Dispose() {
        }

        public async Task<INavigableSymbol> GetNavigableSymbolAsync(SnapshotSpan triggerSpan, CancellationToken cancellationToken) {
            Debug.Assert(triggerSpan.Length == 1);

            cancellationToken.ThrowIfCancellationRequested();

            var extent = _textNavigator.GetExtentOfWord(triggerSpan.Start);
            if (!extent.IsSignificant) {
                return null;
            }

            // Check with pylance, which will give us a precise
            // result, including the source location.
            var result = await GetDefinitionLocationsAsync(extent.Span, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            if (result != null && result.Any()) {
                return result.First();
            }

            return null;
        }

        internal async Task<NavigableSymbol[]> GetDefinitionLocationsAsync(SnapshotSpan span, CancellationToken cancellationToken) {

            var service = _serviceProvider.GetService(typeof(PythonToolsService)) as PythonToolsService;
            if (service != null && service.GetLanguageClient() != null) {
                var result = await service.GetLanguageClient().InvokeTextDocumentDefinitionAsync(
                    new LSP.TextDocumentPositionParams {
                        TextDocument = new LSP.TextDocumentIdentifier {
                            Uri = new System.Uri(_buffer.GetFilePath())
                        },
                        Position = span.Start.GetPosition()
                    },
                    cancellationToken);

                if (result != null) {
                    if (result is JToken token) {
                        var array = ResultConverter.ConvertResult(token);
                        var locations = array as LSP.Location[];
                        if (locations != null) {
                            return locations.OrderBy(l => l.Range.Start.Line).Select(l => {
                                return new NavigableSymbol(_serviceProvider, span.GetText(), l, span);
                            }).ToArray();
                        }
                    }
                }
            }

            return null;
        }
    }
}
