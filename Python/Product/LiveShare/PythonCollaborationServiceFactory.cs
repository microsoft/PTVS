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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LiveShare;
using Microsoft.VisualStudio.LiveShare.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.LiveShare {
    [ExportCollaborationService(typeof(PythonCollaborationService), Scope = SessionScope.Host)]
    internal class PythonCollaborationServiceFactory : ICollaborationServiceFactory {
        private readonly IServiceProvider _serviceProvider;

        [ImportingConstructor]
        public PythonCollaborationServiceFactory([Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<ICollaborationService> CreateServiceAsync(CollaborationSession session, CancellationToken cancellationToken) {
            var languageServerHostService = session.GetService(typeof(ILanguageServerHostService)) as ILanguageServerHostService;
            if (languageServerHostService == null) {
                return null;
            }

            var pythonClient = new PythonCollaborationService(_serviceProvider);
            await pythonClient.InitializeAsync(languageServerHostService);
            return pythonClient;
        }
    }
}
