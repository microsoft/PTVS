// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis.LanguageServer;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode.Services {
    public sealed class UIService : IUIService {
        private readonly JsonRpc _rpc;
        public UIService(JsonRpc rpc) {
            _rpc = rpc;
        }
        public Task ShowMessage(string message, MessageType messageType) {
            var parameters = new ShowMessageRequestParams {
                type = messageType,
                message = message
            };
            return _rpc.InvokeAsync("window/showMessage", parameters);
        }

        public Task<MessageActionItem?> ShowMessage(string message, MessageActionItem[] actions, MessageType messageType) {
            var parameters = new ShowMessageRequestParams {
                type = messageType,
                message = message,
                actions = actions
            };
            return _rpc.InvokeAsync<MessageActionItem?>("window/showMessageRequest", parameters);
        }

        public Task LogMessage(string message, MessageType messageType) {
            var parameters = new LogMessageParams {
                type = messageType,
                message = message
            };
            return _rpc.InvokeAsync<MessageActionItem?>("window/logMessage", parameters);
        }
    }
}
