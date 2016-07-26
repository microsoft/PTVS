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
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Language.NavigateTo.Interfaces;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Navigation.NavigateTo {
    [Export(typeof(INavigateToItemProviderFactory))]
    internal class PythonNavigateToItemProviderFactory : INavigateToItemProviderFactory {
        private readonly IGlyphService _glyphService;

        [ImportingConstructor]
        public PythonNavigateToItemProviderFactory(IGlyphService glyphService) {
            _glyphService = glyphService;
        }

        public bool TryCreateNavigateToItemProvider(IServiceProvider serviceProvider, out INavigateToItemProvider provider) {
            var shell = serviceProvider.GetShell();
            var guid = GuidList.guidPythonToolsPackage;
            IVsPackage pkg;
            if (shell.IsPackageLoaded(ref guid, out pkg) == VSConstants.S_OK && pkg != null) {
                provider = serviceProvider.GetUIThread().Invoke(() => {
                    return new PythonNavigateToItemProvider(serviceProvider, _glyphService);
                });
                return true;
            }

            // Not loaded, so nothing to provide
            provider = null;
            return false;
        }
    }
}
