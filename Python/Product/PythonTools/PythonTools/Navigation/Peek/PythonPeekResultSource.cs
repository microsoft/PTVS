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
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Navigation.Peek {
    internal sealed class PythonPeekResultSource : IPeekResultSource {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly AnalysisLocation _location;

        public PythonPeekResultSource(IPeekResultFactory peekResultFactory, AnalysisLocation location) {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _location = location ?? throw new ArgumentNullException(nameof(location));
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback) {
            if (resultCollection == null) {
                throw new ArgumentNullException(nameof(resultCollection));
            }

            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var fileName = PathUtils.GetFileOrDirectoryName(_location.FilePath);

            var displayInfo = new PeekResultDisplayInfo(
                label: string.Format("{0} - ({1}, {2})", fileName, _location.Line, _location.Column),
                labelTooltip: _location.FilePath,
                title: fileName,
                titleTooltip: _location.FilePath
            );

            int startLine = _location.Line - 1;
            int startColumn = _location.Column - 1;
            int endLine = startLine;
            int endColumn = startColumn;
            if (_location.DefinitionStartLine.HasValue) {
                startLine = _location.DefinitionStartLine.Value - 1;
            }
            if (_location.DefinitionStartColumn.HasValue) {
                startColumn = _location.DefinitionStartColumn.Value - 1;
            }
            if (_location.DefinitionEndLine.HasValue) {
                endLine = _location.DefinitionEndLine.Value - 1;
            }
            if (_location.DefinitionEndColumn.HasValue) {
                endColumn = _location.DefinitionEndColumn.Value - 1;
            }

            var result = _peekResultFactory.Create(
                displayInfo,
                _location.FilePath,
                startLine,
                startColumn,
                endLine,
                endColumn,
                _location.Line - 1,
                _location.Column - 1,
                isReadOnly: false);

            resultCollection.Add(result);
        }
    }
}
