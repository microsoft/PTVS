// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Based on https://github.com/CXuesong/LanguageServer.NET

using System;
using JsonRpc.Standard;
using JsonRpc.Standard.Contracts;
using LanguageServer.VsCode.Contracts;
using Microsoft.PythonTools.VsCode.Commands;
using Newtonsoft.Json.Linq;

namespace Microsoft.PythonTools.VsCode.Server {
    public sealed class InitializaionService : LanguageServiceBase {
        private const string TriggerCharacters = "`:$@_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        [JsonRpcMethod(AllowExtensionData = true)]
        public InitializeResult Initialize(
            int processId,
            ClientCapabilities capabilities,
            JToken initializationOptions = null,
            Uri rootUri = null,
            string trace = null) {

            return new InitializeResult(new ServerCapabilities {
                HoverProvider = true,
                SignatureHelpProvider = new SignatureHelpOptions("(,)"),
                CompletionProvider = new CompletionOptions(true, TriggerCharacters),
                TextDocumentSync = new TextDocumentSyncOptions {
                    OpenClose = true,
                    WillSave = true,
                    Change = TextDocumentSyncKind.Incremental
                },
                DocumentFormattingProvider = true,
                DocumentRangeFormattingProvider = true,
                DocumentOnTypeFormattingProvider = new DocumentOnTypeFormattingOptions {
                    FirstTriggerCharacter = ';',
                    MoreTriggerCharacter = new[] { '}', '\n' }
                },
                DocumentSymbolProvider = true,
                ExecuteCommandProvider = new ExecuteCommandOptions {
                    Commands = Controller.Commands
                }
            });
        }

        [JsonRpcMethod(IsNotification = true)]
        public void Initialized() { }

        [JsonRpcMethod]
        public void Shutdown() { }

        [JsonRpcMethod(IsNotification = true)]
        public void Exit() => LanguageServerSession.StopServer();

        [JsonRpcMethod("$/cancelRequest", IsNotification = true)]
        public void CancelRequest(MessageId id) { }
    }
}
