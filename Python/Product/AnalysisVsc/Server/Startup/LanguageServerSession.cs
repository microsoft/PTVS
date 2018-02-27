// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

using System.Threading;
using JsonRpc.Standard.Client;
using JsonRpc.Standard.Contracts;
using LanguageServer.VsCode.Contracts.Client;
using Microsoft.Common.Core.Diagnostics;

namespace Microsoft.R.LanguageServer.Server {
    public class LanguageServerSession {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public LanguageServerSession(JsonRpcClient rpcClient, IJsonRpcContractResolver contractResolver) {
            Check.ArgumentNull(nameof(rpcClient), rpcClient);

            RpcClient = rpcClient;
            var builder = new JsonRpcProxyBuilder { ContractResolver = contractResolver };
            Client = new ClientProxy(builder, rpcClient);
         }

        public CancellationToken CancellationToken => cts.Token;
        public JsonRpcClient RpcClient { get; }
        public ClientProxy Client { get; }
        public void StopServer() => cts.Cancel();
    }
}