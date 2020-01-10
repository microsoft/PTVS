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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using StreamJsonRpc;

namespace PythonToolsTests.Mocks {
    /// <summary>
    /// Minimal version of the broker used by VS
    /// (which is internal and so we can't instantiate).
    /// Note that this requires StreamJsonRpc to run, which in turn depends on
    /// a lot of other dlls that are normally available in VS install, but that
    /// need to be referenced by the test project in order to run outside VS.
    /// </summary>
    internal class MockLanguageClientBroker : ILanguageClientBroker {
        private JsonRpc _rpc;

        public async Task LoadAsync(ILanguageClientMetadata metadata, ILanguageClient client) {
            if (client == null) {
                throw new ArgumentNullException(nameof(client));
            }

            client.StartAsync += Client_StartAsync;
            await client.OnLoadedAsync();
        }

        private async Task Client_StartAsync(object sender, EventArgs args) {
            var client = (ILanguageClient)sender;
            var connection = await client.ActivateAsync(CancellationTokens.GetToken(TimeSpan.FromSeconds(15)));
            await InitializeAsync(client, connection);
        }

        private async Task InitializeAsync(ILanguageClient client, Connection connection) {
            var messageHandler = new HeaderDelimitedMessageHandler(connection.Writer, connection.Reader);
            _rpc = new JsonRpc(messageHandler, this) {
                CancelLocallyInvokedMethodsWhenConnectionIsClosed = true
            };

            _rpc.StartListening();

            (client as ILanguageClientCustomMessage2)?.AttachForCustomMessageAsync(_rpc);

            var initParam = new InitializeParams {
                ProcessId = Process.GetCurrentProcess().Id,
                InitializationOptions = client.InitializationOptions
            };

            await _rpc.InvokeWithParameterObjectAsync(Methods.Initialize.Name, initParam, cancellationToken: CancellationToken.None);
            await _rpc.NotifyWithParameterObjectAsync(Methods.Initialized.Name, new InitializedParams());
            await client.OnServerInitializedAsync();
        }
    }
}
