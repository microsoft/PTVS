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
using System.Collections.Generic;
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

namespace Microsoft.Python.LanguageServer.Implementation {
    /// <summary>
    /// VS Code language server protocol implementation to use with StreamJsonRpc
    /// https://github.com/Microsoft/language-server-protocol/blob/gh-pages/specification.md
    /// https://github.com/Microsoft/vs-streamjsonrpc/blob/master/doc/index.md
    /// </summary>
    public sealed partial class LanguageServer : IDisposable {
        private readonly DisposableBag _disposables = new DisposableBag(nameof(LanguageServer));
        private readonly PythonTools.Analysis.LanguageServer.Server _server = new PythonTools.Analysis.LanguageServer.Server();
        private readonly CancellationTokenSource _sessionTokenSource = new CancellationTokenSource();
        private readonly RestTextConverter _textConverter = new RestTextConverter();
        private readonly Dictionary<Uri, Diagnostic[]> _pendingDiagnostic = new Dictionary<Uri, Diagnostic[]>();
        private readonly object _lock = new object();
        private readonly Prioritizer _prioritizer = new Prioritizer();

        private IUIService _ui;
        private ITelemetryService _telemetry;
        private IProgressService _progress;

        private JsonRpc _rpc;
        private bool _filesLoaded;
        private Task _progressReportingTask;
        private PathsWatcher _pathsWatcher;
        private IdleTimeTracker _idleTimeTracker;

        public CancellationToken Start(IServiceContainer services, JsonRpc rpc) {
            _ui = services.GetService<IUIService>();
            _telemetry = services.GetService<ITelemetryService>();
            _progress = services.GetService<IProgressService>();

            _rpc = rpc;

            _server.OnLogMessage += OnLogMessage;
            _server.OnShowMessage += OnShowMessage;
            _server.OnTelemetry += OnTelemetry;
            _server.OnPublishDiagnostics += OnPublishDiagnostics;
            _server.OnApplyWorkspaceEdit += OnApplyWorkspaceEdit;
            _server.OnRegisterCapability += OnRegisterCapability;
            _server.OnUnregisterCapability += OnUnregisterCapability;
            _server.OnAnalysisQueued += OnAnalysisQueued;
            _server.OnAnalysisComplete += OnAnalysisComplete;

            _disposables
                .Add(() => _server.OnLogMessage -= OnLogMessage)
                .Add(() => _server.OnShowMessage -= OnShowMessage)
                .Add(() => _server.OnTelemetry -= OnTelemetry)
                .Add(() => _server.OnPublishDiagnostics -= OnPublishDiagnostics)
                .Add(() => _server.OnApplyWorkspaceEdit -= OnApplyWorkspaceEdit)
                .Add(() => _server.OnRegisterCapability -= OnRegisterCapability)
                .Add(() => _server.OnUnregisterCapability -= OnUnregisterCapability)
                .Add(() => _server.OnAnalysisQueued -= OnAnalysisQueued)
                .Add(() => _server.OnAnalysisComplete -= OnAnalysisComplete)
                .Add(_prioritizer);

            return _sessionTokenSource.Token;
        }

        private void OnAnalysisQueued(object sender, AnalysisQueuedEventArgs e) => HandleAnalysisQueueEvent();
        private void OnAnalysisComplete(object sender, AnalysisCompleteEventArgs e) => HandleAnalysisQueueEvent();

        private void HandleAnalysisQueueEvent()
            => _progressReportingTask = _progressReportingTask ?? ProgressWorker();

        private async Task ProgressWorker() {
            await Task.Delay(1000);

            var remaining = _server.EstimateRemainingWork();
            if (remaining > 0) {
                using (var p = _progress.BeginProgress()) {
                    while (remaining > 0) {
                        var items = remaining > 1 ? "items" : "item";
                        // TODO: in localization this needs to be two different messages 
                        // since not all languages allow sentence construction.
                        await p.Report($"Analyzing workspace, {remaining} {items} remaining...");
                        await Task.Delay(100);
                        remaining = _server.EstimateRemainingWork();
                    }
                }
            }
            _progressReportingTask = null;
        }

        public void Dispose() {
            _pathsWatcher?.Dispose();
            _disposables.TryDispose();
            _server.Dispose();
        }

        [JsonObject]
        private class PublishDiagnosticsParams {
            [JsonProperty]
            public Uri uri;
            [JsonProperty]
            public Diagnostic[] diagnostics;
        }

        #region Events
        private void OnTelemetry(object sender, TelemetryEventArgs e) => _telemetry.SendTelemetry(e.value);
        private void OnShowMessage(object sender, ShowMessageEventArgs e) => _ui.ShowMessage(e.message, e.type);
        private void OnLogMessage(object sender, LogMessageEventArgs e) => _ui.LogMessage(e.message, e.type);

        private void OnPublishDiagnostics(object sender, PublishDiagnosticsEventArgs e) {
            lock (_lock) {
                // If list is empty (errors got fixed), publish immediately,
                // otherwise throttle so user does not get spurious squiggles
                // while typing normally.
                var diags = e.diagnostics.ToArray();
                _pendingDiagnostic[e.uri] = diags;
                if (diags.Length == 0) {
                    PublishPendingDiagnostics();
                }
            }
        }

        private void OnApplyWorkspaceEdit(object sender, ApplyWorkspaceEditEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("workspace/applyEdit", e.@params).DoNotWait();
        private void OnRegisterCapability(object sender, RegisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/registerCapability", e.@params).DoNotWait();
        private void OnUnregisterCapability(object sender, UnregisterCapabilityEventArgs e)
            => _rpc.NotifyWithParameterObjectAsync("client/unregisterCapability", e.@params).DoNotWait();
        #endregion

        #region Workspace
        [JsonRpcMethod("workspace/didChangeConfiguration")]
        public async Task DidChangeConfiguration(JToken token, CancellationToken cancellationToken) {
            using (await _prioritizer.ConfigurationPriorityAsync(cancellationToken)) {
                var settings = new LanguageServerSettings();

                var rootSection = token["settings"];
                var pythonSection = rootSection?["python"];
                if (pythonSection == null) {
                    return;
                }

                var autoComplete = pythonSection["autoComplete"];
                settings.completion.showAdvancedMembers = GetSetting(autoComplete, "showAdvancedMembers", true);

                var analysis = pythonSection["analysis"];
                settings.analysis.openFilesOnly = GetSetting(analysis, "openFilesOnly", false);
                settings.diagnosticPublishDelay = GetSetting(analysis, "diagnosticPublishDelay", 1000);
                settings.symbolsHierarchyDepthLimit = GetSetting(analysis, "symbolsHierarchyDepthLimit", 10);

                _ui.SetLogLevel(GetLogLevel(analysis));

                _idleTimeTracker?.Dispose();
                _idleTimeTracker = new IdleTimeTracker(settings.diagnosticPublishDelay, PublishPendingDiagnostics);

                _pathsWatcher?.Dispose();
                var watchSearchPaths = GetSetting(analysis, "watchSearchPaths", true);
                if (watchSearchPaths) {
                    _pathsWatcher = new PathsWatcher(
                        _initParams.initializationOptions.searchPaths,
                        () => _server.ReloadModulesAsync(CancellationToken.None).DoNotWait(),
                        _server
                     );
                }

                var errors = GetSetting(analysis, "errors", Array.Empty<string>());
                var warnings = GetSetting(analysis, "warnings", Array.Empty<string>());
                var information = GetSetting(analysis, "information", Array.Empty<string>());
                var disabled = GetSetting(analysis, "disabled", Array.Empty<string>());
                settings.analysis.SetErrorSeverityOptions(errors, warnings, information, disabled);

                await _server.DidChangeConfiguration(new DidChangeConfigurationParams { settings = settings }, cancellationToken);

                if (!_filesLoaded) {
                    await LoadDirectoryFiles();
                }
                _filesLoaded = true;
            }
        }

        [JsonRpcMethod("workspace/didChangeWatchedFiles")]
        public async Task DidChangeWatchedFiles(JToken token) {
            using (await _prioritizer.DocumentChangePriorityAsync()) {
                await _server.DidChangeWatchedFiles(token.ToObject<DidChangeWatchedFilesParams>());
            }
        }

        [JsonRpcMethod("workspace/symbol")]
        public async Task<SymbolInformation[]> WorkspaceSymbols(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync();
            await WaitForCompleteAnalysisAsync(cancellationToken);
            return await _server.WorkspaceSymbols(token.ToObject<WorkspaceSymbolParams>());
        }

        #endregion

        #region Commands
        [JsonRpcMethod("workspace/executeCommand")]
        public Task<object> ExecuteCommand(JToken token)
           => _server.ExecuteCommand(token.ToObject<ExecuteCommandParams>());
        #endregion

        #region TextDocument
        [JsonRpcMethod("textDocument/didOpen")]
        public async Task DidOpenTextDocument(JToken token, CancellationToken cancellationToken) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync(cancellationToken)) {
                await _server.DidOpenTextDocument(ToObject<DidOpenTextDocumentParams>(token));
            }
        }

        [JsonRpcMethod("textDocument/didChange")]
        public async Task DidChangeTextDocument(JToken token) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync()) {
                var @params = ToObject<DidChangeTextDocumentParams>(token);
                var version = @params.textDocument.version;
                if (version == null || @params.contentChanges.IsNullOrEmpty()) {
                    _server.DidChangeTextDocument(@params);
                    return;
                }

                // _server.DidChangeTextDocument can handle change buckets with decreasing version and without overlaping 
                // Split change into buckets that will be properly handled
                var changes = SplitDidChangeTextDocumentParams(@params, version.Value);

                foreach (var change in changes) {
                    _server.DidChangeTextDocument(change);
                }
            }
        }

        private static IEnumerable<DidChangeTextDocumentParams> SplitDidChangeTextDocumentParams(DidChangeTextDocumentParams @params, int version) {
            var changes = new Stack<DidChangeTextDocumentParams>();
            var contentChanges = new Stack<TextDocumentContentChangedEvent>();
            var previousRange = new Range();

            for (var i = @params.contentChanges.Length - 1; i >= 0; i--) {
                var contentChange = @params.contentChanges[i];
                var range = contentChange.range.GetValueOrDefault();
                if (previousRange.end > range.start) {
                    changes.Push(CreateDidChangeTextDocumentParams(@params, version, contentChanges));
                    contentChanges = new Stack<TextDocumentContentChangedEvent>();
                    version--;
                }

                contentChanges.Push(contentChange);
                previousRange = range;
            }

            if (contentChanges.Count > 0) {
                changes.Push(CreateDidChangeTextDocumentParams(@params, version, contentChanges));
            }

            return changes;
        }

        private static DidChangeTextDocumentParams CreateDidChangeTextDocumentParams(DidChangeTextDocumentParams @params, int version, Stack<TextDocumentContentChangedEvent> contentChanges)
            => new DidChangeTextDocumentParams {
                _enqueueForAnalysis = @params._enqueueForAnalysis,
                contentChanges = contentChanges.ToArray(),
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = @params.textDocument.uri,
                    version = version
                }
            };

        [JsonRpcMethod("textDocument/willSave")]
        public Task WillSaveTextDocument(JToken token)
           => _server.WillSaveTextDocument(ToObject<WillSaveTextDocumentParams>(token));

        public Task<TextEdit[]> WillSaveWaitUntilTextDocument(JToken token)
           => _server.WillSaveWaitUntilTextDocument(ToObject<WillSaveTextDocumentParams>(token));

        [JsonRpcMethod("textDocument/didSave")]
        public async Task DidSaveTextDocument(JToken token) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync()) {
                await _server.DidSaveTextDocument(ToObject<DidSaveTextDocumentParams>(token));
            }
        }

        [JsonRpcMethod("textDocument/didClose")]
        public async Task DidCloseTextDocument(JToken token) {
            _idleTimeTracker?.NotifyUserActivity();
            using (await _prioritizer.DocumentChangePriorityAsync()) {
                await _server.DidCloseTextDocument(ToObject<DidCloseTextDocumentParams>(token));
            }
        }
        #endregion

        #region Editor features
        [JsonRpcMethod("textDocument/completion")]
        public async Task<CompletionList> Completion(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Completion(ToObject<CompletionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("completionItem/resolve")]
        public Task<CompletionItem> CompletionItemResolve(JToken token)
           => _server.CompletionItemResolve(ToObject<CompletionItem>(token));

        [JsonRpcMethod("textDocument/hover")]
        public async Task<Hover> Hover(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Hover(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/signatureHelp")]
        public async Task<SignatureHelp> SignatureHelp(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.SignatureHelp(ToObject<TextDocumentPositionParams>(token));
        }

        [JsonRpcMethod("textDocument/definition")]
        public async Task<Reference[]> GotoDefinition(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.GotoDefinition(ToObject<TextDocumentPositionParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/references")]
        public async Task<Reference[]> FindReferences(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.FindReferences(ToObject<ReferencesParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/documentHighlight")]
        public async Task<DocumentHighlight[]> DocumentHighlight(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentHighlight(ToObject<TextDocumentPositionParams>(token));
        }

        [JsonRpcMethod("textDocument/documentSymbol")]
        public async Task<DocumentSymbol[]> DocumentSymbol(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            // This call is also used by VSC document outline and it needs correct information
            await WaitForCompleteAnalysisAsync(cancellationToken);
            return await _server.HierarchicalDocumentSymbol(ToObject<DocumentSymbolParams>(token), cancellationToken);
        }

        [JsonRpcMethod("textDocument/codeAction")]
        public async Task<Command[]> CodeAction(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.CodeAction(ToObject<CodeActionParams>(token));
        }

        [JsonRpcMethod("textDocument/codeLens")]
        public async Task<CodeLens[]> CodeLens(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.CodeLens(ToObject<TextDocumentPositionParams>(token));
        }

        [JsonRpcMethod("codeLens/resolve")]
        public Task<CodeLens> CodeLensResolve(JToken token)
           => _server.CodeLensResolve(ToObject<CodeLens>(token));

        [JsonRpcMethod("textDocument/documentLink")]
        public async Task<DocumentLink[]> DocumentLink(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentLink(ToObject<DocumentLinkParams>(token));
        }

        [JsonRpcMethod("documentLink/resolve")]
        public Task<DocumentLink> DocumentLinkResolve(JToken token)
           => _server.DocumentLinkResolve(ToObject<DocumentLink>(token));

        [JsonRpcMethod("textDocument/formatting")]
        public async Task<TextEdit[]> DocumentFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentFormatting(ToObject<DocumentFormattingParams>(token));
        }

        [JsonRpcMethod("textDocument/rangeFormatting")]
        public async Task<TextEdit[]> DocumentRangeFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentRangeFormatting(ToObject<DocumentRangeFormattingParams>(token));
        }

        [JsonRpcMethod("textDocument/onTypeFormatting")]
        public async Task<TextEdit[]> DocumentOnTypeFormatting(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.DocumentOnTypeFormatting(ToObject<DocumentOnTypeFormattingParams>(token));
        }

        [JsonRpcMethod("textDocument/rename")]
        public async Task<WorkspaceEdit> Rename(JToken token, CancellationToken cancellationToken) {
            await _prioritizer.DefaultPriorityAsync(cancellationToken);
            return await _server.Rename(token.ToObject<RenameParams>());
        }
        #endregion

        #region Extensions
        [JsonRpcMethod("python/loadExtension")]
        public Task LoadExtension(JToken token, CancellationToken cancellationToken)
            => _server.LoadExtension(ToObject<PythonAnalysisExtensionParams>(token), cancellationToken);

        #endregion

        private T ToObject<T>(JToken token) => token.ToObject<T>(_rpc.JsonSerializer);

        private T GetSetting<T>(JToken section, string settingName, T defaultValue) {
            var value = section?[settingName];
            try {
                return value != null ? value.ToObject<T>() : defaultValue;
            } catch (JsonException ex) {
                _server.LogMessage(MessageType.Warning, $"Exception retrieving setting '{settingName}': {ex.Message}");
            }
            return defaultValue;
        }

        private MessageType GetLogLevel(JToken analysisKey) {
            var s = GetSetting(analysisKey, "logLevel", "Error");
            if (s.EqualsIgnoreCase("Warning")) {
                return MessageType.Warning;
            }
            if (s.EqualsIgnoreCase("Info") || s.EqualsIgnoreCase("Information")) {
                return MessageType.Info;
            }
            if (s.EqualsIgnoreCase("Trace")) {
                return MessageType.Log;
            }
            return MessageType.Error;
        }

        private void PublishPendingDiagnostics() {
            List<KeyValuePair<Uri, Diagnostic[]>> list;

            lock (_lock) {
                list = _pendingDiagnostic.ToList();
                _pendingDiagnostic.Clear();
            }

            foreach (var kvp in list) {
                var parameters = new PublishDiagnosticsParams {
                    uri = kvp.Key,
                    diagnostics = kvp.Value
                };
                _rpc.NotifyWithParameterObjectAsync("textDocument/publishDiagnostics", parameters).DoNotWait();
            }
        }

        private sealed class IdleTimeTracker : IDisposable {
            private readonly int _delay;
            private readonly Action _action;
            private Timer _timer;
            private DateTime _lastActivityTime;

            public IdleTimeTracker(int msDelay, Action action) {
                _delay = msDelay;
                _action = action;
                _timer = new Timer(OnTimer, this, 50, 50);
                NotifyUserActivity();
            }

            public void NotifyUserActivity() => _lastActivityTime = DateTime.Now;

            public void Dispose() {
                _timer?.Dispose();
                _timer = null;
            }

            private void OnTimer(object state) {
                if ((DateTime.Now - _lastActivityTime).TotalMilliseconds >= _delay && _timer != null) {
                    _action();
                }
            }
        }

        private Task WaitForCompleteAnalysisAsync(CancellationToken token) {
            var tcs = new TaskCompletionSource<object>();
            var t = _server.WaitForCompleteAnalysisAsync();
            Task.Run(async () => {
                try {
                    while (!t.IsCompleted) {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(100);
                    }
                    tcs.TrySetResult(null);
                } catch (OperationCanceledException) {
                    tcs.TrySetCanceled();
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        private class Prioritizer : IDisposable {
            private const int InitializePriority = 0;
            private const int ConfigurationPriority = 1;
            private const int DocumentChangePriority = 2;
            private const int DefaultPriority = 3;
            private readonly PriorityProducerConsumer<QueueItem> _ppc;

            public Prioritizer() {
                _ppc = new PriorityProducerConsumer<QueueItem>(4);
                Task.Run(ConsumerLoop);
            }

            private async Task ConsumerLoop() {
                while (!_ppc.IsDisposed) {
                    try {
                        var item = await _ppc.ConsumeAsync();
                        if (item.IsAwaitable) {
                            var disposable = new PrioritizerDisposable(_ppc.CancellationToken);
                            item.SetResult(disposable);
                            await disposable.Task;
                        } else {
                            item.SetResult(EmptyDisposable.Instance);
                        }
                    } catch (OperationCanceledException) when (_ppc.IsDisposed) {
                        return;
                    }
                }
            }

            public Task<IDisposable> InitializePriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(InitializePriority, true, cancellationToken);

            public Task<IDisposable> ConfigurationPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(ConfigurationPriority, true, cancellationToken);

            public Task<IDisposable> DocumentChangePriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DocumentChangePriority, true, cancellationToken);

            public Task DefaultPriorityAsync(CancellationToken cancellationToken = default(CancellationToken))
                => Enqueue(DefaultPriority, false, cancellationToken);

            private Task<IDisposable> Enqueue(int priority, bool isAwaitable, CancellationToken cancellationToken = default(CancellationToken)) {
                var item = new QueueItem(isAwaitable, cancellationToken);
                _ppc.Produce(item, priority);
                return item.Task;
            }

            private struct QueueItem {
                private readonly TaskCompletionSource<IDisposable> _tcs;
                public Task<IDisposable> Task => _tcs.Task;
                public bool IsAwaitable { get; }

                public QueueItem(bool isAwaitable, CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<IDisposable>();
                    IsAwaitable = isAwaitable;
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public void SetResult(IDisposable disposable) => _tcs.TrySetResultOnThreadPool(disposable);
            }

            private class PrioritizerDisposable : IDisposable {
                private readonly TaskCompletionSource<int> _tcs;

                public PrioritizerDisposable(CancellationToken cancellationToken) {
                    _tcs = new TaskCompletionSource<int>();
                    _tcs.RegisterForCancellation(cancellationToken).UnregisterOnCompletion(_tcs.Task);
                }

                public Task Task => _tcs.Task;
                public void Dispose() => _tcs.TrySetResult(0);
            }

            public void Dispose() => _ppc.Dispose();
        }
    }
}
