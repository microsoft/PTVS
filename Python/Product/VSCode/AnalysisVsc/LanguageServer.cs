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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Disposables;
using Microsoft.DsTools.Core.Services;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.VsCode.Core.Shell;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode {
    /// <summary>
    /// VS Code language server protocol implementation to use with StreamJsonRpc
    /// https://github.com/Microsoft/language-server-protocol/blob/gh-pages/specification.md
    /// https://github.com/Microsoft/vs-streamjsonrpc/blob/master/doc/index.md
    /// </summary>
    public sealed partial class LanguageServer : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(LanguageServer));
        private readonly Server _server = new Server();
        private readonly CancellationTokenSource _sessionTokenSource = new CancellationTokenSource();
        private readonly RestTextConverter _textConverter = new RestTextConverter();
        private IUIService _ui;
        private ITelemetryService _telemetry;
        private JsonRpc _rpc;

        public CancellationToken Start(IServiceContainer services, JsonRpc rpc) {
            _ui = services.GetService<IUIService>();
            _telemetry = services.GetService<ITelemetryService>();
            _rpc = rpc;

            _server.OnLogMessage += OnLogMessage;
            _server.OnShowMessage += OnShowMessage;
            _server.OnTelemetry += OnTelemetry;
            _server.OnPublishDiagnostics += OnPublishDiagnostics;
            _server.OnApplyWorkspaceEdit += OnApplyWorkspaceEdit;
            _server.OnRegisterCapability += OnRegisterCapability;
            _server.OnUnregisterCapability += OnUnregisterCapability;

            _disposables
                .Add(() => _server.OnLogMessage -= OnLogMessage)
                .Add(() => _server.OnShowMessage -= OnShowMessage)
                .Add(() => _server.OnTelemetry -= OnTelemetry)
                .Add(() => _server.OnPublishDiagnostics -= OnPublishDiagnostics)
                .Add(() => _server.OnApplyWorkspaceEdit -= OnApplyWorkspaceEdit)
                .Add(() => _server.OnRegisterCapability -= OnRegisterCapability)
                .Add(() => _server.OnUnregisterCapability -= OnUnregisterCapability);

            return _sessionTokenSource.Token;
        }

        public void Dispose() {
            _disposables.TryDispose();
            _server.Dispose();
        }

        [JsonObject]
        class PublishDiagnosticsParams {
            [JsonProperty]
            public string uri;
            [JsonProperty]
            public Diagnostic[] diagnostics;
        }

        #region Events
        private void OnTelemetry(object sender, TelemetryEventArgs e) => _telemetry.SendTelemetry(e.value);
        private void OnShowMessage(object sender, ShowMessageEventArgs e) => _ui.ShowMessage(e.message, e.type);
        private void OnLogMessage(object sender, LogMessageEventArgs e) => _ui.LogMessage(e.message, e.type);
        private void OnPublishDiagnostics(object sender, PublishDiagnosticsEventArgs e) {
            var parameters = new PublishDiagnosticsParams {
                uri = e.uri.ToString(),
                diagnostics = e.diagnostics.ToArray()
            };
            _rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
        }
        private void OnApplyWorkspaceEdit(object sender, ApplyWorkspaceEditEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("workspace/applyEdit", e.@params).DoNotWait();
        private void OnRegisterCapability(object sender, RegisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/registerCapability", e.@params).DoNotWait();
        private void OnUnregisterCapability(object sender, UnregisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/unregisterCapability", e.@params).DoNotWait();
        #endregion

        #region Cancellation
        [JsonRpcMethod("cancelRequest")]
        public void CancelRequest() => _server.CancelRequest();
        #endregion

        #region Workspace
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public Task DidChangeConfiguration(JToken token) {
            var settings = new LanguageServerSettings();

            var rootSection = token["settings"];
            var pythonSection = rootSection?["python"];
            var autoComplete = pythonSection?["autoComplete"];
            if (autoComplete != null) {
                var showAdvancedMembers = autoComplete["showAdvancedMembers"] as JValue;
                settings.SuppressAdvancedMembers = showAdvancedMembers == null || 
                    (showAdvancedMembers.Type == JTokenType.Boolean && !showAdvancedMembers.ToObject<bool>());
            }
            var p = new DidChangeConfigurationParams() { settings = settings };
            return _server.DidChangeConfiguration(p);
        }

        [JsonRpcMethod("workspace/didChangeWatchedFiles")]
        public Task DidChangeWatchedFiles(JToken token)
           => _server.DidChangeWatchedFiles(token.ToObject<DidChangeWatchedFilesParams>());

        [JsonRpcMethod("workspace/symbol")]
        public Task<SymbolInformation[]> WorkspaceSymbols(JToken token)
           => _server.WorkspaceSymbols(token.ToObject<WorkspaceSymbolParams>());
        #endregion

        #region Commands
        [JsonRpcMethod("workspace/executeCommand")]
        public Task<object> ExecuteCommand(JToken token)
           => _server.ExecuteCommand(token.ToObject<ExecuteCommandParams>());
        #endregion

        #region TextDocument
        [JsonRpcMethod("textDocument/didOpen")]
        public Task DidOpenTextDocument(JToken token)
           => _server.DidOpenTextDocument(ToObject<DidOpenTextDocumentParams>(token));

        [JsonRpcMethod("textDocument/didChange")]
        public void DidChangeTextDocument(JToken token)
           => _server.DidChangeTextDocument(ToObject<DidChangeTextDocumentParams>(token));

        [JsonRpcMethod("textDocument/willSave")]
        public Task WillSaveTextDocument(JToken token)
           => _server.WillSaveTextDocument(ToObject<WillSaveTextDocumentParams>(token));

        public Task<TextEdit[]> WillSaveWaitUntilTextDocument(JToken token)
           => _server.WillSaveWaitUntilTextDocument(ToObject<WillSaveTextDocumentParams>(token));

        [JsonRpcMethod("textDocument/didSave")]
        public Task DidSaveTextDocument(JToken token)
           => _server.DidSaveTextDocument(ToObject<DidSaveTextDocumentParams>(token));

        [JsonRpcMethod("textDocument/didClose")]
        public Task DidCloseTextDocument(JToken token)
           => _server.DidCloseTextDocument(ToObject<DidCloseTextDocumentParams>(token));
        #endregion

        #region Editor features
        [JsonRpcMethod("textDocument/completion")]
        public Task<CompletionList> Completion(JToken token)
           => _server.Completion(ToObject<CompletionParams>(token));

        [JsonRpcMethod("completionItem/resolve")]
        public Task<CompletionItem> CompletionItemResolve(JToken token)
           => _server.CompletionItemResolve(ToObject<CompletionItem>(token));

        [JsonRpcMethod("textDocument/hover")]
        public Task<Hover> Hover(JToken token)
           => _server.Hover(ToObject<TextDocumentPositionParams>(token));

        [JsonRpcMethod("textDocument/signatureHelp")]
        public Task<SignatureHelp> SignatureHelp(JToken token)
            => _server.SignatureHelp(ToObject<TextDocumentPositionParams>(token));

        [JsonRpcMethod("textDocument/definition")]
        public Task<Reference[]> GotoDefinition(JToken token)
           => _server.GotoDefinition(ToObject<TextDocumentPositionParams>(token));

        [JsonRpcMethod("textDocument/references")]
        public Task<Reference[]> FindReferences(JToken token)
           => _server.FindReferences(ToObject<ReferencesParams>(token));

        [JsonRpcMethod("textDocument/documentHighlight")]
        public Task<DocumentHighlight[]> DocumentHighlight(JToken token)
           => _server.DocumentHighlight(ToObject<TextDocumentPositionParams>(token));

        [JsonRpcMethod("textDocument/documentSymbol")]
        public Task<SymbolInformation[]> DocumentSymbol(JToken token)
           => _server.DocumentSymbol(ToObject<DocumentSymbolParams>(token));

        [JsonRpcMethod("textDocument/codeAction")]
        public Task<Command[]> CodeAction(JToken token)
           => _server.CodeAction(ToObject<CodeActionParams>(token));

        [JsonRpcMethod("textDocument/codeLens")]
        public Task<CodeLens[]> CodeLens(JToken token)
           => _server.CodeLens(ToObject<TextDocumentPositionParams>(token));

        [JsonRpcMethod("codeLens/resolve")]
        public Task<CodeLens> CodeLensResolve(JToken token)
           => _server.CodeLensResolve(ToObject<CodeLens>(token));

        [JsonRpcMethod("textDocument/documentLink")]
        public Task<DocumentLink[]> DocumentLink(JToken token)
           => _server.DocumentLink(ToObject<DocumentLinkParams>(token));

        [JsonRpcMethod("documentLink/resolve")]
        public Task<DocumentLink> DocumentLinkResolve(JToken token)
           => _server.DocumentLinkResolve(ToObject<DocumentLink>(token));

        [JsonRpcMethod("textDocument/formatting")]
        public Task<TextEdit[]> DocumentFormatting(JToken token)
           => _server.DocumentFormatting(ToObject<DocumentFormattingParams>(token));

        [JsonRpcMethod("textDocument/rangeFormatting")]
        public Task<TextEdit[]> DocumentRangeFormatting(JToken token)
           => _server.DocumentRangeFormatting(ToObject<DocumentRangeFormattingParams>(token));

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public Task<TextEdit[]> DocumentOnTypeFormatting(JToken token)
           => _server.DocumentOnTypeFormatting(ToObject<DocumentOnTypeFormattingParams>(token));

        [JsonRpcMethod("textDocument/rename")]
        public Task<WorkspaceEdit> Rename(JToken token)
            => _server.Rename(token.ToObject<RenameParams>());
        #endregion

        #region Extensions
        [JsonRpcMethod("python/loadExtension")]
        public Task LoadExtension(JToken token)
            => _server.LoadExtension(ToObject<PythonAnalysisExtensionParams>(token));

        #endregion

        private T ToObject<T>(JToken token) => token.ToObject<T>(_rpc.JsonSerializer);
    }
}
