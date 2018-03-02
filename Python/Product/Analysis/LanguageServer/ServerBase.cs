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
using System.Threading;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public abstract class ServerBase {
        private RequestLock _lock;

        private sealed class RequestLock : IDisposable {
            public readonly ServerBase Owner;
            public readonly CancellationTokenSource CancellationTokenSource;

            public CancellationToken Token => CancellationTokenSource.Token;
            public void Cancel() => CancellationTokenSource.Cancel();

            public RequestLock(ServerBase owner, int millisecondsTimeout) {
                CancellationTokenSource = millisecondsTimeout > 0 ?
                    new CancellationTokenSource(millisecondsTimeout) :
                    new CancellationTokenSource();

                Owner = owner;
                if (Interlocked.CompareExchange(ref Owner._lock, this, null) != null) {
                    throw new InvalidOperationException("currently processing another request");
                }
            }

            public void Dispose() {
                CancellationTokenSource.Dispose();
                Interlocked.CompareExchange(ref Owner._lock, null, this);
            }
        }

        /// <summary>
        /// Should be used in a using() statement around any requests that support
        /// cancellation or timeout.
        /// </summary>
        public IDisposable AllowRequestCancellation(int millisecondsTimeout = -1) => new RequestLock(this, millisecondsTimeout);

        /// <summary>
        /// Get this token at the start of request processing and abort when it
        /// is marked as cancelled.
        /// </summary>
        protected CancellationToken CancellationToken => Volatile.Read(ref _lock)?.Token ?? CancellationToken.None;

        #region Client Requests

        [JsonRpcMethod("$/initialize")]
        public abstract Task<InitializeResult> Initialize(InitializeParams @params);

        public virtual Task Initialized(InitializedParams @params) => Task.CompletedTask;

        [JsonRpcMethod("$/shutdown")]
        public virtual Task Shutdown() => Task.CompletedTask;

        [JsonRpcMethod("$/exit")]
        public virtual Task Exit() => Task.CompletedTask;

        [JsonRpcMethod("$/cancelRequest")]
        public virtual void CancelRequest() => Volatile.Read(ref _lock)?.Cancel();

        [JsonRpcMethod("window/showMessageRequest")]
        public virtual Task<MessageActionItem?> ShowMessageRequest(ShowMessageRequestParams @params)
           => Task.FromResult((MessageActionItem?)null);

        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public virtual Task DidChangeConfiguration(DidChangeConfigurationParams @params) => Task.CompletedTask;

        [JsonRpcMethod("workspace/didChangeWatchedFiles")]
        public virtual Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) => Task.CompletedTask;

        [JsonRpcMethod("workspace/symbol")]
        public virtual Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params)
             => Task.FromResult(new SymbolInformation[0]);

        public virtual Task<object> ExecuteCommand(ExecuteCommandParams @params) => Task.FromResult((object)null);

        [JsonRpcMethod("textDocument/didOpen")]
        public virtual Task DidOpenTextDocument(DidOpenTextDocumentParams @params) => Task.CompletedTask;

        [JsonRpcMethod("textDocument/didChange")]
        public virtual Task DidChangeTextDocument(DidChangeTextDocumentParams @params) => Task.CompletedTask;

        [JsonRpcMethod("textDocument/willSave")]
        public virtual Task WillSaveTextDocument(WillSaveTextDocumentParams @params) => Task.CompletedTask;

        public virtual Task<TextEdit[]> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams @params)
            => Task.FromResult(new TextEdit[0]);

        [JsonRpcMethod("textDocument/didSave")]
        public virtual Task DidSaveTextDocument(DidSaveTextDocumentParams @params) => Task.CompletedTask;

        [JsonRpcMethod("textDocument/didClose")]
        public virtual Task DidCloseTextDocument(DidCloseTextDocumentParams @params) => Task.CompletedTask;

        [JsonRpcMethod("textDocument/completion")]
        public virtual Task<CompletionList> Completion(CompletionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("completionItem/resolve")]
        public virtual Task<CompletionItem> CompletionItemResolve(CompletionItem item) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/hover")]
        public virtual Task<Hover> Hover(TextDocumentPositionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/signatureHelp")]
        public virtual Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/definition")]
        public virtual Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/references")]
        public virtual Task<Reference[]> FindReferences(ReferencesParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/documentHighlight")]
        public virtual Task<DocumentHighlight[]> DocumentHighlight(TextDocumentPositionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/documentSymbol")]
        public virtual Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/codeAction")]
        public virtual Task<Command[]> CodeAction(CodeActionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/codeLens")]
        public virtual Task<CodeLens[]> CodeLens(TextDocumentPositionParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("codeLens/resolve")]
        public virtual Task<CodeLens> CodeLensResolve(CodeLens item) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/documentLink")]
        public virtual Task<DocumentLink[]> DocumentLink(DocumentLinkParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("documentLink/resolve")]
        public virtual Task<DocumentLink> DocumentLinkResolve(DocumentLink item) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/formatting")]
        public virtual Task<TextEdit[]> DocumentFormatting(DocumentFormattingParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/rangeFormatting")]
        public virtual Task<TextEdit[]> DocumentRangeFormatting(DocumentRangeFormattingParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public virtual Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params) => throw new NotImplementedException();

        [JsonRpcMethod("textDocument/rename")]
        public virtual Task<WorkspaceEdit> Rename(RenameParams @params) => throw new NotImplementedException();
        #endregion

        #region Server Requests

        public event EventHandler<ShowMessageEventArgs> OnShowMessage;
        protected void ShowMessage(MessageType type, string message) => OnShowMessage?.Invoke(this, new ShowMessageEventArgs { type = type, message = message });

        public event EventHandler<LogMessageEventArgs> OnLogMessage;
        protected void LogMessage(MessageType type, string message) => OnLogMessage?.Invoke(this, new LogMessageEventArgs { type = type, message = message });

        public event EventHandler<TelemetryEventArgs> OnTelemetry;
        protected void Telemetry(TelemetryEventArgs e) => OnTelemetry?.Invoke(this, e);

        public event EventHandler<RegisterCapabilityEventArgs> OnRegisterCapability;
        protected Task RegisterCapability(RegistrationParams @params) {
            var evt = OnRegisterCapability;
            if (evt == null) {
                return Task.CompletedTask;
            }
            var tcs = new TaskCompletionSource<object>();
            var e = new RegisterCapabilityEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }


        public event EventHandler<UnregisterCapabilityEventArgs> OnUnregisterCapability;
        protected Task UnregisterCapability(UnregistrationParams @params) {
            var evt = OnUnregisterCapability;
            if (evt == null) {
                return Task.CompletedTask;
            }
            var tcs = new TaskCompletionSource<object>();
            var e = new UnregisterCapabilityEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }

        public event EventHandler<ApplyWorkspaceEditEventArgs> OnApplyWorkspaceEdit;
        protected Task<ApplyWorkspaceEditResponse?> ApplyWorkspaceEdit(ApplyWorkspaceEditParams @params) {
            var evt = OnApplyWorkspaceEdit;
            if (evt == null) {
                return Task.FromResult((ApplyWorkspaceEditResponse?)null);
            }
            var tcs = new TaskCompletionSource<ApplyWorkspaceEditResponse?>();
            var e = new ApplyWorkspaceEditEventArgs(tcs) { @params = @params };
            evt(this, e);
            return tcs.Task;
        }

        public event EventHandler<PublishDiagnosticsEventArgs> OnPublishDiagnostics;
        protected void PublishDiagnostics(PublishDiagnosticsEventArgs e) => OnPublishDiagnostics?.Invoke(this, e);

        #endregion
    }
}
