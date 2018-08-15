// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using Microsoft.Cascade.Extensibility;
using Microsoft.Cascade.LanguageServices.Common;
using Microsoft.Cascade.LanguageServices.Contracts;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft
{
    internal class PythonLanguageClient : ICollaborationService, IAsyncDisposable
    {
        private static string[] PythonContentTypes = new string[] { "python" };
        private static DocumentFilter[] PythonDocumentFilters = new DocumentFilter[]
        {
            new DocumentFilter() { Language = "python" }
        };
        private IAsyncDisposable languageServiceProviderService;
        private SVsServiceProvider serviceProvider;

        public PythonLanguageClient(SVsServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        internal async Task InitializeAsync(ILanguageServerHostService languageServerHostService)
        {
            if (languageServerHostService == null)
            {
                throw new ArgumentNullException(nameof(languageServerHostService));
            }

            var pythonLanguageServiceProviderCallback = new PythonLanguageServiceProviderCallback(this.serviceProvider);
            this.languageServiceProviderService = await languageServerHostService.CreateCustomLanguageServerProviderAsync(
                "languageServerProvider-python",
                new LanguageServerProviderMetadata
                {
                    IsLanguageClientProvider = false,
                    ContentTypes = PythonContentTypes,
                    DocumentFilters = PythonDocumentFilters
                },
                pythonLanguageServiceProviderCallback,
                null);
        }

        public async Task DisposeAsync()
        {
            await this.languageServiceProviderService?.DisposeAsync();
        }
    }
}
