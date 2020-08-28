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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Environments {
    /// <summary>
    /// Conda locator that returns the path to the conda executable
    /// specified by the user in tools/options/python/conda page.
    /// This is the highest priority locator because it respects
    /// the user's choice.
    /// </summary>
    [Export(typeof(ICondaLocator))]
    [ExportMetadata("Priority", 100)]
    sealed class UserSpecifiedCondaLocator : ICondaLocator {
        private readonly IServiceProvider _site;

        [ImportingConstructor]
        public UserSpecifiedCondaLocator(
            [Import(typeof(SVsServiceProvider), AllowDefault = true)] IServiceProvider site = null
        ) {
            _site = site;
        }

        public string CondaExecutablePath {
            get {
                return _site?.GetUIThread().Invoke(() => 
                    _site?.GetPythonToolsService()?.CondaOptions.CustomCondaExecutablePath
                );
            }
        }
    }
}
