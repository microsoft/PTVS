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

        public abstract Task<InitializeResult> Initialize(InitializeParams @params);

        public virtual async Task Initialized(InitializedParams @params) { }

        public virtual async Task Shutdown() { }

        public virtual async Task Exit() { }

        public virtual void CancelRequest() {
            Volatile.Read(ref _lock)?.Cancel();
        }

        public virtual async Task<MessageActionItem?> ShowMessageRequest(ShowMessageRequestParams @params) {
            return null;
        }

        public virtual async Task DidChangeConfiguration(DidChangeConfigurationParams @params) { }

        public virtual async Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) { }

        public virtual async Task<SymbolInformation[]> WorkplaceSymbols(WorkplaceSymbolParams @params) {
            return null;
        }

        public virtual async Task<object> ExecuteCommand(ExecuteCommandParams @params) {
            return null;
        }


        public virtual async Task DidOpenTextDocument(DidOpenTextDocumentParams @params) { }

        public virtual async Task DidChangeTextDocument(DidChangeTextDocumentParams @params) { }

        public virtual async Task WillSaveTextDocument(WillSaveTextDocumentParams @params) { }

        public virtual async Task<TextEdit[]> WillSaveWaitUntilTextDocument(WillSaveTextDocumentParams @params) {
            return null;
        }

        public virtual async Task DidSaveTextDocument(DidSaveTextDocumentParams @params) { }

        public virtual async Task DidCloseTextDocument(DidCloseTextDocumentParams @params) { }

        public virtual async Task<CompletionList> Completion(CompletionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<CompletionItem> CompletionItemResolve(CompletionItem item) {
            throw new NotImplementedException();
        }

        public virtual async Task<Hover> Hover(TextDocumentPositionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<Reference[]> GotoDefinition(TextDocumentPositionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<Reference[]> FindReferences(ReferencesParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<DocumentHighlight[]> DocumentHighlight(TextDocumentPositionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<SymbolInformation[]> DocumentSymbol(DocumentSymbolParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<Command[]> CodeAction(CodeActionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<CodeLens[]> CodeLens(TextDocumentPositionParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<CodeLens> CodeLensResolve(CodeLens item) {
            throw new NotImplementedException();
        }

        public virtual async Task<DocumentLink[]> DocumentLink(DocumentLinkParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<DocumentLink> DocumentLinkResolve(DocumentLink item) {
            throw new NotImplementedException();
        }

        public virtual async Task<TextEdit[]> DocumentFormatting(DocumentFormattingParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<TextEdit[]> DocumentRangeFormatting(DocumentRangeFormattingParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<TextEdit[]> DocumentOnTypeFormatting(DocumentOnTypeFormattingParams @params) {
            throw new NotImplementedException();
        }

        public virtual async Task<WorkspaceEdit> Rename(RenameParams @params) {
            throw new NotImplementedException();
        }

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
