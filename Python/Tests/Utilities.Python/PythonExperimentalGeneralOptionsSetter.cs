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
using Microsoft.PythonTools;

namespace TestUtilities.Python {

    public class PythonExperimentalGeneralOptionsSetter : IDisposable {
        private readonly PythonToolsService _pyService;
        private readonly bool? _autoDetectCondaEnvironments;
        private readonly bool? _useCondaPackageManager;

        public PythonExperimentalGeneralOptionsSetter(
            PythonToolsService pyService,
            bool? autoDetectCondaEnvironments = null,
            bool? useCondaPackageManager = null
        ) {
            _pyService = pyService;
            var options = _pyService.ExperimentalOptions;

            if (autoDetectCondaEnvironments.HasValue) {
                _autoDetectCondaEnvironments = options.AutoDetectCondaEnvironments;
                options.AutoDetectCondaEnvironments = autoDetectCondaEnvironments.Value;
            }

            if (useCondaPackageManager.HasValue) {
                _useCondaPackageManager = options.UseCondaPackageManager;
                options.UseCondaPackageManager = useCondaPackageManager.Value;
            }
        }

        public void Dispose() {
            var options = _pyService.ExperimentalOptions;

            if (_autoDetectCondaEnvironments.HasValue) {
                options.AutoDetectCondaEnvironments = _autoDetectCondaEnvironments.Value;
            }

            if (_useCondaPackageManager.HasValue) {
                options.UseCondaPackageManager = _useCondaPackageManager.Value;
            }
        }
    }
}
