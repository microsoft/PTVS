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
using Microsoft.VisualStudio.LiveShare;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LiveShare {
    internal class PythonCollaborationService : ICollaborationService, IAsyncDisposable {
        private static string[] PythonContentTypes = new string[] { PythonCoreConstants.ContentType };
        private static DocumentFilter[] PythonDocumentFilters = new[] {
            new DocumentFilter() { Language = "python" }
        };

        private IAsyncDisposable _languageServiceProviderService;
        private readonly IServiceProvider _serviceProvider;

        public PythonCollaborationService(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        internal async Task InitializeAsync(ILanguageServerHostService languageServerHostService) {
            if (languageServerHostService == null) {
                throw new ArgumentNullException(nameof(languageServerHostService));
            }

            var pythonLanguageServiceProviderCallback = new PythonLanguageServiceProviderCallback(_serviceProvider);
            _languageServiceProviderService = await languageServerHostService.CreateCustomLanguageServerProviderAsync(
                "languageServerProvider-python",
                new LanguageServerProviderMetadata {
                    IsLanguageClientProvider = false,
                    ContentTypes = PythonContentTypes,
                    DocumentFilters = PythonDocumentFilters
                },
                pythonLanguageServiceProviderCallback,
                null);
        }

        public async Task DisposeAsync() {
            if (_languageServiceProviderService != null) {
                await _languageServiceProviderService.DisposeAsync();
            }
        }
    }
}
