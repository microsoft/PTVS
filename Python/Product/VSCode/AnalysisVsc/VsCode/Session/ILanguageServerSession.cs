// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using JsonRpc.Standard.Client;
using LanguageServer.VsCode.Contracts.Client;
using Microsoft.PythonTools.Analysis.LanguageServer;

namespace Microsoft.PythonTools.VsCode.Session {
    public interface ILanguageServerSession {
        JsonRpcClient RpcClient { get; }
        ClientProxy Client { get; }
        Server AnalysisServer { get; }
        void Stop();
    }
}
