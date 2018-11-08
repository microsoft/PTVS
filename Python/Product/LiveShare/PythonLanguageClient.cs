// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using Microsoft.VisualStudio.LiveShare;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.LiveShare {
    internal class PythonLanguageClient : ICollaborationService, IAsyncDisposable {
        private static string[] PythonContentTypes = new string[] { PythonCoreConstants.ContentType };
        private static DocumentFilter[] PythonDocumentFilters = new[] {
            new DocumentFilter() { Language = "python" }
        };

        private IAsyncDisposable _languageServiceProviderService;
        private readonly IServiceProvider _serviceProvider;

        public PythonLanguageClient(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        internal async Task InitializeAsync(ILanguageServerHostService languageServerHostService) {
            if (languageServerHostService == null) {
                throw new ArgumentNullException(nameof(languageServerHostService));
            }

            var pythonLanguageServiceProviderCallback = new PythonLanguageServiceProviderCallback(this._serviceProvider);
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
