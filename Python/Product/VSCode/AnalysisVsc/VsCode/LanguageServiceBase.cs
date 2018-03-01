// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using JsonRpc.Standard.Server;
using LanguageServer.VsCode.Contracts.Client;
using Microsoft.DsTools.Core.Services;
using Microsoft.PythonTools.VsCode.Services;
using Microsoft.PythonTools.VsCode.Session;

namespace Microsoft.PythonTools.VsCode {
    public abstract class LanguageServiceBase : JsonRpcService {
        protected LanguageServerSession LanguageServerSession => RequestContext.Features.Get<LanguageServerSession>();

        protected ClientProxy Client => LanguageServerSession.Client;
        protected IServiceContainer Services => CoreShell.Current.Services;
    }
}
