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
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Navigation.Peek {
    internal sealed class PythonPeekResultSource : IPeekResultSource {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly AnalysisLocation[] _locations;

        public PythonPeekResultSource(IPeekResultFactory peekResultFactory, AnalysisLocation[] locations) {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback) {
            if (resultCollection == null) {
                throw new ArgumentNullException(nameof(resultCollection));
            }

            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            foreach (var location in _locations) {
                resultCollection.Add(CreateResult(location));
            }
        }

        private IDocumentPeekResult CreateResult(AnalysisLocation location) {
            var fileName = PathUtils.GetFileOrDirectoryName(location.FilePath);

            var displayInfo = new PeekResultDisplayInfo2(
                label: string.Format("{0} - ({1}, {2})", fileName, location.Line, location.Column),
                labelTooltip: location.FilePath,
                title: fileName,
                titleTooltip: location.FilePath,
                startIndexOfTokenInLabel: 0,
                lengthOfTokenInLabel: 0
            );

            int startLine = location.Line - 1;
            int startColumn = location.Column - 1;
            int endLine = startLine;
            int endColumn = startColumn;
            if (location.DefinitionStartLine.HasValue) {
                startLine = location.DefinitionStartLine.Value - 1;
            }
            if (location.DefinitionStartColumn.HasValue) {
                startColumn = location.DefinitionStartColumn.Value - 1;
            }
            if (location.DefinitionEndLine.HasValue) {
                endLine = location.DefinitionEndLine.Value - 1;
            }
            if (location.DefinitionEndColumn.HasValue) {
                endColumn = location.DefinitionEndColumn.Value - 1;
            }

            return _peekResultFactory.Create(
                displayInfo,
                default(ImageMoniker),
                location.FilePath,
                startLine,
                startColumn,
                endLine,
                endColumn,
                location.Line - 1,
                location.Column - 1,
                location.Line - 1,
                location.Column - 1,
                isReadOnly: false,
                editorDestination: new Guid(PythonConstants.EditorFactoryGuid)
            );
        }
    }
}
