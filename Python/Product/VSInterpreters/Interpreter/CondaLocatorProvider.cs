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
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter {

    [Export(typeof(ICondaLocatorProvider))]
    sealed class CondaLocatorProvider : ICondaLocatorProvider {
        private readonly Lazy<ICondaLocator, ICondaLocatorMetadata>[] _condaLocators;

        [ImportingConstructor]
        public CondaLocatorProvider(
            [ImportMany] Lazy<ICondaLocator, ICondaLocatorMetadata>[] condaLocators
        ) {
            _condaLocators = condaLocators;
        }

        public ICondaLocator FindLocator() {
            // Order first to avoid creating the lower priority locators unnecessarily
            return _condaLocators
                .OrderBy(l => l.Metadata.Priority)
                .Where(l => File.Exists(l.Value.CondaExecutablePath))
                .FirstOrDefault()?.Value;
        }
    }
}
