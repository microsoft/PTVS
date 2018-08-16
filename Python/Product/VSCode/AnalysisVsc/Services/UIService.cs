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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis.LanguageServer;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Services {
    public sealed class UIService : IUIService, ILogger {
        private readonly JsonRpc _rpc;
        private MessageType _logLevel = MessageType.Error;

        public UIService(JsonRpc rpc) {
            _rpc = rpc;
        }
        public Task ShowMessage(string message, MessageType messageType) {
            var parameters = new ShowMessageRequestParams {
                type = messageType,
                message = message
            };
            return _rpc.NotifyWithParameterObjectAsync("window/showMessage", parameters);
        }

        public Task<MessageActionItem?> ShowMessage(string message, MessageActionItem[] actions, MessageType messageType) {
            var parameters = new ShowMessageRequestParams {
                type = messageType,
                message = message,
                actions = actions
            };
            return _rpc.InvokeWithParameterObjectAsync<MessageActionItem?>("window/showMessageRequest", parameters);
        }

        [Serializable]
        class LogMessageParams {
            public MessageType type;
            public string message;
        }

        public Task LogMessage(string message, MessageType messageType) {
            if(messageType > _logLevel) {
                return Task.CompletedTask;
            }
            var parameters = new LogMessageParams {
                type = messageType,
                message = message
            };
            return _rpc.NotifyWithParameterObjectAsync("window/logMessage", parameters);
        }

        public Task SetStatusBarMessage(string message) 
            => _rpc.NotifyWithParameterObjectAsync("window/setStatusBarMessage", message);

        public void TraceMessage(IFormattable message) => LogMessage(message.ToString(), MessageType.Info);

        public void SetLogLevel(MessageType logLevel) => _logLevel = logLevel;
    }
}
