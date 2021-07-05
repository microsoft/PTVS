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

namespace TestUtilities.Python {

    public class PythonServiceGeneralOptionsSetter : IDisposable {
        private readonly PythonToolsService _pyService;
        private readonly bool? _updateSearchPathsWhenAddingLinkedFiles;
        private readonly bool? _unresolvedImportWarning;
        private readonly bool? _invalidEncodingWarning;
        private readonly bool? _clearGlobalPythonPath;

        public PythonServiceGeneralOptionsSetter(
            PythonToolsService pyService,
            bool? updateSearchPathsWhenAddingLinkedFiles = null,
            bool? unresolvedImportWarning = null,
            bool? invalidEncodingWarning = null,
            bool? clearGlobalPythonPath = null
        ) {
            _pyService = pyService;
            var options = _pyService.GeneralOptions;

            if (updateSearchPathsWhenAddingLinkedFiles.HasValue) {
                _updateSearchPathsWhenAddingLinkedFiles = options.UpdateSearchPathsWhenAddingLinkedFiles;
                options.UpdateSearchPathsWhenAddingLinkedFiles = updateSearchPathsWhenAddingLinkedFiles.Value;
            }

            if (unresolvedImportWarning.HasValue) {
                _unresolvedImportWarning = options.UnresolvedImportWarning;
                options.UnresolvedImportWarning = unresolvedImportWarning.Value;
            }

            if (invalidEncodingWarning.HasValue) {
                _invalidEncodingWarning = options.InvalidEncodingWarning;
                options.InvalidEncodingWarning = invalidEncodingWarning.Value;
            }

            if (clearGlobalPythonPath.HasValue) {
                _clearGlobalPythonPath = options.ClearGlobalPythonPath;
                options.ClearGlobalPythonPath = clearGlobalPythonPath.Value;
            }
        }

        public void Dispose() {
            var options = _pyService.GeneralOptions;

            if (_updateSearchPathsWhenAddingLinkedFiles.HasValue) {
                options.UpdateSearchPathsWhenAddingLinkedFiles = _updateSearchPathsWhenAddingLinkedFiles.Value;
            }

            if (_unresolvedImportWarning.HasValue) {
                options.UnresolvedImportWarning = _unresolvedImportWarning.Value;
            }

            if (_invalidEncodingWarning.HasValue) {
                options.InvalidEncodingWarning = _invalidEncodingWarning.Value;
            }

            if (_clearGlobalPythonPath.HasValue) {
                options.ClearGlobalPythonPath = _clearGlobalPythonPath.Value;
            }
        }
    }
}
