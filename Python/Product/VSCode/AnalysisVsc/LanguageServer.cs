// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DsTools.Core.Disposables;
using Microsoft.DsTools.Core.Services;
using Microsoft.DsTools.Core.Services.Shell;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.VsCode.Core.Shell;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.PythonTools.VsCode {
    /// <summary>
    /// VS Code language server protocol implementation to use with StreamJsonRpc
    /// https://github.com/Microsoft/language-server-protocol/blob/gh-pages/specification.md
    /// https://github.com/Microsoft/vs-streamjsonrpc/blob/master/doc/index.md
    /// </summary>
    public sealed class LanguageServer : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(LanguageServer));
        private readonly Server _server = new Server();
        private readonly CancellationTokenSource _sessionTokenSource = new CancellationTokenSource();
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

        struct PublishDiagnosticsParams {
            public Uri uri;
            public Diagnostic[] diagnostics;
        }

        #region Events
        private void OnTelemetry(object sender, TelemetryEventArgs e) => _telemetry.SendTelemetry(e.value);
        private void OnShowMessage(object sender, ShowMessageEventArgs e) => _ui.ShowMessage(e.message, e.type);
        private void OnLogMessage(object sender, LogMessageEventArgs e) => _ui.LogMessage(e.message, e.type);
        private void OnPublishDiagnostics(object sender, PublishDiagnosticsEventArgs e) {
            var parameters = new PublishDiagnosticsParams {
                uri = e.uri,
                diagnostics = e.diagnostics.ToArray()
            };
            _rpc.InvokeAsync("textDocument/publishDiagnostics", parameters);
        }
        private void OnApplyWorkspaceEdit(object sender, ApplyWorkspaceEditEventArgs e) => _rpc.InvokeAsync("workspace/applyEdit", e.@params);
        private void OnRegisterCapability(object sender, RegisterCapabilityEventArgs e) => _rpc.InvokeAsync("client/registerCapability", e.@params);
        private void OnUnregisterCapability(object sender, UnregisterCapabilityEventArgs e) => _rpc.InvokeAsync("client/unregisterCapability", e.@params);
        #endregion

        #region Lifetime
        [JsonRpcMethod("initialize")]
        public Task<InitializeResult> Initialize(JToken token) {
            var p = token.ToObject<InitializeParams>();
            // Monitor parent process
            if (p.processId.HasValue) {
                Process.GetProcessById(p.processId.Value).Exited += (s, e) => {
                    _sessionTokenSource.Cancel();
                };
            }
            return _server.Initialize(p);
        }

        [JsonRpcMethod("shutdown")]
        public Task Shutdown() => _server.Shutdown();

        [JsonRpcMethod("exit")]
        public Task Exit() => _server.Exit();
        #endregion

        #region Cancellation
        [JsonRpcMethod("cancelRequest")]
        public void CancelRequest() => _server.CancelRequest();
        #endregion

        #region Window
        [JsonRpcMethod("window/showMessageRequest")]
        public Task<MessageActionItem?> ShowMessageRequest(JToken token)
           => _server.ShowMessageRequest(token.ToObject<ShowMessageRequestParams>());
        #endregion

        #region Workspace
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public Task DidChangeConfiguration(JToken token)
           => _server.DidChangeConfiguration(token.ToObject<DidChangeConfigurationParams>());

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
           => _server.DidOpenTextDocument(token.ToObject<DidOpenTextDocumentParams>());

        [JsonRpcMethod("textDocument/didChange")]
        public Task DidChangeTextDocument(JToken token)
           => _server.DidChangeTextDocument(token.ToObject<DidChangeTextDocumentParams>());

        [JsonRpcMethod("textDocument/willSave")]
        public Task WillSaveTextDocument(JToken token)
           => _server.WillSaveTextDocument(token.ToObject<WillSaveTextDocumentParams>());

        public Task<TextEdit[]> WillSaveWaitUntilTextDocument(JToken token)
           => _server.WillSaveWaitUntilTextDocument(token.ToObject<WillSaveTextDocumentParams>());

        [JsonRpcMethod("textDocument/didSave")]
        public Task DidSaveTextDocument(JToken token)
           => _server.DidSaveTextDocument(token.ToObject<DidSaveTextDocumentParams>());

        [JsonRpcMethod("textDocument/didClose")]
        public Task DidCloseTextDocument(JToken token)
           => _server.DidCloseTextDocument(token.ToObject<DidCloseTextDocumentParams>());
        #endregion

        #region Editor features
        [JsonRpcMethod("textDocument/completion")]
        public Task<CompletionList> Completion(JToken token)
           => _server.Completion(token.ToObject<CompletionParams>());

        [JsonRpcMethod("completionItem/resolve")]
        public Task<CompletionItem> CompletionItemResolve(JToken token)
           => _server.CompletionItemResolve(token.ToObject<CompletionItem>());

        [JsonRpcMethod("textDocument/hover")]
        public Task<Hover> Hover(JToken token)
           => _server.Hover(token.ToObject<TextDocumentPositionParams>());

        [JsonRpcMethod("textDocument/signatureHelp")]
        public Task<SignatureHelp> SignatureHelp(JToken token)
           => _server.SignatureHelp(token.ToObject<TextDocumentPositionParams>());

        [JsonRpcMethod("textDocument/definition")]
        public Task<Reference[]> GotoDefinition(JToken token)
           => _server.GotoDefinition(token.ToObject<TextDocumentPositionParams>());

        [JsonRpcMethod("textDocument/references")]
        public Task<Reference[]> FindReferences(JToken token)
           => _server.FindReferences(token.ToObject<ReferencesParams>());

        [JsonRpcMethod("textDocument/documentHighlight")]
        public Task<DocumentHighlight[]> DocumentHighlight(JToken token)
           => _server.DocumentHighlight(token.ToObject<TextDocumentPositionParams>());

        [JsonRpcMethod("textDocument/documentSymbol")]
        public Task<SymbolInformation[]> DocumentSymbol(JToken token)
           => _server.DocumentSymbol(token.ToObject<DocumentSymbolParams>());

        [JsonRpcMethod("textDocument/codeAction")]
        public Task<Command[]> CodeAction(JToken token)
           => _server.CodeAction(token.ToObject<CodeActionParams>());

        [JsonRpcMethod("textDocument/codeLens")]
        public Task<CodeLens[]> CodeLens(JToken token)
           => _server.CodeLens(token.ToObject<TextDocumentPositionParams>());

        [JsonRpcMethod("codeLens/resolve")]
        public Task<CodeLens> CodeLensResolve(JToken token)
           => _server.CodeLensResolve(token.ToObject<CodeLens>());

        [JsonRpcMethod("textDocument/documentLink")]
        public Task<DocumentLink[]> DocumentLink(JToken token)
           => _server.DocumentLink(token.ToObject<DocumentLinkParams>());

        [JsonRpcMethod("documentLink/resolve")]
        public Task<DocumentLink> DocumentLinkResolve(JToken token)
           => _server.DocumentLinkResolve(token.ToObject<DocumentLink>());

        [JsonRpcMethod("textDocument/formatting")]
        public Task<TextEdit[]> DocumentFormatting(JToken token)
           => _server.DocumentFormatting(token.ToObject<DocumentFormattingParams>());

        [JsonRpcMethod("textDocument/rangeFormatting")]
        public Task<TextEdit[]> DocumentRangeFormatting(JToken token)
           => _server.DocumentRangeFormatting(token.ToObject<DocumentRangeFormattingParams>());

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public Task<TextEdit[]> DocumentOnTypeFormatting(JToken token)
           => _server.DocumentOnTypeFormatting(token.ToObject<DocumentOnTypeFormattingParams>());

        [JsonRpcMethod("textDocument/rename")]
        public Task<WorkspaceEdit> Rename(JToken token)
            => _server.Rename(token.ToObject<RenameParams>());
        #endregion
    }
}
