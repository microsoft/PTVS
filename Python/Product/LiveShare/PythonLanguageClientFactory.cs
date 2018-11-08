// <copyright>
// Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>

using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LiveShare;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.LiveShare {
    [ExportCollaborationService(typeof(PythonLanguageClient), Scope = SessionScope.Host)]
    internal class PythonLanguageClientFactory : ICollaborationServiceFactory {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonLanguageClientFactory([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<ICollaborationService> CreateServiceAsync(CollaborationSession session, CancellationToken cancellationToken) {
            var languageServerHostService = session.GetService(typeof(ILanguageServerHostService)) as ILanguageServerHostService;
            if (languageServerHostService == null) {
                return null;
            }

            var pythonClient = new PythonLanguageClient(_serviceProvider);
            await pythonClient.InitializeAsync(languageServerHostService);
            return pythonClient;
        }
    }
}
