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
        private readonly AnalysisVariable[] _variables;

        public PythonPeekResultSource(IPeekResultFactory peekResultFactory, AnalysisVariable[] variables) {
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

            var grouped = _variables.Where(v => !string.IsNullOrEmpty(v.Location.FilePath))
                .OrderByDescending(v => v.Location.EndLine - v.Location.StartLine)
                .ThenBy(v => v.Location.StartLine)
                .GroupBy(v => v.Location.FilePath);

            var valueAndDefs = new List<IDocumentPeekResult>();
            var defOnly = new List<IDocumentPeekResult>();

            foreach (var g in grouped) {
                bool anyValues = false;
                foreach (var value in g.Where(v => v.Type == VariableType.Value)) {
                    var def = g.FirstOrDefault(v => v.Type == VariableType.Value && LocationContains(value.Location, v.Location))?.Location ??
                        new LocationInfo(value.Location.FilePath, value.Location.DocumentUri, value.Location.StartLine, value.Location.StartColumn);
                    valueAndDefs.Add(CreateResult(g.Key, def, value.Location));
                    anyValues = true;
                }
                if (!anyValues) {
                    foreach (var def in g.Where(v => v.Type == VariableType.Definition)) {
                        defOnly.Add(CreateResult(g.Key, def.Location));
                    }
                }
            }

            foreach (var v in valueAndDefs.Concat(defOnly)) {
                resultCollection.Add(v);
            }
        }

        private static bool LocationContains(LocationInfo outer, LocationInfo inner) {
            if (inner.StartLine < outer.StartLine || inner.EndLine > outer.EndLine) {
                return false;
            }
            if (inner.StartLine == outer.StartLine && inner.StartColumn < outer.StartColumn) {
                return false;
            }
            if (inner.EndLine == outer.EndLine && inner.EndColumn > outer.EndColumn) {
                return false;
            }
            return true;
        }

        private IDocumentPeekResult CreateResult(string filePath, LocationInfo definition, LocationInfo value = null) {
            var fileName = PathUtils.GetFileOrDirectoryName(filePath);

            var displayInfo = new PeekResultDisplayInfo2(
                label: string.Format("{0} - ({1}, {2})", fileName, definition.StartLine, definition.StartColumn),
                labelTooltip: filePath,
                title: fileName,
                titleTooltip: filePath,
                startIndexOfTokenInLabel: 0,
                lengthOfTokenInLabel: 0
            );

            value = value ?? definition;
            return _peekResultFactory.Create(
                displayInfo,
                default(ImageMoniker),
                filePath,
                value.StartLine - 1,
                value.StartColumn - 1,
                (value.EndLine ?? value.StartLine) - 1,
                (value.EndColumn ?? value.StartColumn) - 1,
                definition.StartLine - 1,
                definition.StartColumn - 1,
                (definition.EndLine ?? definition.StartLine) - 1,
                (definition.EndColumn ?? definition.StartColumn) - 1,
                isReadOnly: false
            );
        }
    }
}
