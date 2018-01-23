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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Microsoft.PythonTools.Navigation.Peek {
    internal sealed class PythonPeekResultSource : IPeekResultSource {
        private readonly IPeekResultFactory _peekResultFactory;
        private readonly IAnalysisVariable[] _variables;

        public PythonPeekResultSource(IPeekResultFactory peekResultFactory, IAnalysisVariable[] variables) {
            _peekResultFactory = peekResultFactory ?? throw new ArgumentNullException(nameof(peekResultFactory));
            _variables = variables ?? throw new ArgumentNullException(nameof(variables));
        }

        public void FindResults(string relationshipName, IPeekResultCollection resultCollection, CancellationToken cancellationToken, IFindPeekResultsCallback callback) {
            if (resultCollection == null) {
                throw new ArgumentNullException(nameof(resultCollection));
            }

            if (!string.Equals(relationshipName, PredefinedPeekRelationships.Definitions.Name, StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            foreach (var variable in _variables.Where(v => !string.IsNullOrEmpty(v.Location.FilePath))) {
                resultCollection.Add(CreateResult(variable));
            }
        }

        private IDocumentPeekResult CreateResult(IAnalysisVariable variable) {
            var fileName = PathUtils.GetFileOrDirectoryName(variable.Location.FilePath);

            var displayInfo = new PeekResultDisplayInfo2(
                label: string.Format("{0} - ({1}, {2})", fileName, variable.Location.StartLine, variable.Location.StartColumn),
                labelTooltip: variable.Location.FilePath,
                title: fileName,
                titleTooltip: variable.Location.FilePath,
                startIndexOfTokenInLabel: 0,
                lengthOfTokenInLabel: 0
            );

            return _peekResultFactory.Create(
                displayInfo,
                default(ImageMoniker),
                variable.Location.FilePath,
                variable.DefinitionLocation.StartLine - 1,
                variable.DefinitionLocation.StartColumn - 1,
                (variable.DefinitionLocation.EndLine ?? variable.DefinitionLocation.StartLine) - 1,
                (variable.DefinitionLocation.EndColumn ?? variable.DefinitionLocation.StartColumn) - 1,
                variable.Location.StartLine - 1,
                variable.Location.StartColumn - 1,
                (variable.Location.EndLine ?? variable.Location.StartLine) - 1,
                (variable.Location.EndColumn ?? variable.Location.StartColumn) - 1,
                isReadOnly: false
            );
        }
    }
}
