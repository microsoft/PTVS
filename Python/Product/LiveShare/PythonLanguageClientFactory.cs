// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Cascade.Client;
using Microsoft.Cascade.Extensibility;
using Microsoft.Cascade.LanguageServices.Common;
using Microsoft.VisualStudio.Shell;

namespace Microsoft
{
    [ExportCollaborationService(
        typeof(PythonLanguageClient),
        Scope = SessionScope.Host,
        Features = WellKnownFeatures.LspServices)]
    internal class PythonLanguageClientFactory : ICollaborationServiceFactory
    {
        private readonly SVsServiceProvider serviceProvider;

        [ImportingConstructor]
        public PythonLanguageClientFactory(SVsServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<ICollaborationService> CreateServiceAsync(SessionContext sessionContext, CancellationToken cancellationToken)
        {
            var languageServerHostService = sessionContext.ServiceProvider.GetService<ILanguageServerHostService>();
            if (languageServerHostService == null)
            {
                return null;
            }

            var pythonClient = new PythonLanguageClient(this.serviceProvider);
            await pythonClient.InitializeAsync(languageServerHostService);
            return pythonClient;
        }
    }
}
