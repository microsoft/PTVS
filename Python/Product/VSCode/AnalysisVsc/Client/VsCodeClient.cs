// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using LanguageServer.VsCode.Contracts.Client;
using Microsoft.DsTools.Core.Logging;
using Microsoft.DsTools.Core.Services;
using Microsoft.PythonTools.VsCode.Logging;

namespace Microsoft.PythonTools.VsCode.Client {
    /// <summary>
    /// Represents running instance of the VS Code
    /// </summary>
    internal sealed class VsCodeClient : IVsCodeClient {
        private readonly ClientProxy _client;

        public VsCodeClient(ClientProxy client, IServiceManager serviceManager) {
            _client = client;
            var output = new Output(client.Window, serviceManager.GetService<IActionLog>());
            serviceManager.AddService(output);
        }

        public IClient Client => _client.Client;
        public ITextDocument TextDocument => _client.TextDocument;
        public IWindow Window => _client.Window;
    }
}
