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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class Server : ServerBase, ILogger, IDisposable {
        /// <summary>
        /// Implements ability to execute module reload on the analyzer thread
        /// </summary>
        private sealed class ReloadModulesQueueItem : IAnalyzable {
            private readonly PythonAnalyzer _analyzer;
            private TaskCompletionSource<bool> _tcs = new TaskCompletionSource<bool>();
            public Task Task => _tcs.Task;

            public ReloadModulesQueueItem(PythonAnalyzer analyzer) {
                _analyzer = analyzer;
            }
            public void Analyze(CancellationToken cancel) {
                if (cancel.IsCancellationRequested) {
                    return;
                }

                var currentTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<bool>());
                var task = Task.Run(() => _analyzer.ReloadModulesAsync(), cancel);
                try {
                    task.WaitAndUnwrapExceptions();
                    currentTcs.TrySetResult(true);
                } catch (OperationCanceledException oce) {
                    currentTcs.TrySetCanceled(oce.CancellationToken);
                } catch (Exception ex) {
                    currentTcs.TrySetException(ex);
                }
            }
        }

        internal readonly AnalysisQueue _queue;
        internal readonly ParseQueue _parseQueue;
        private readonly Dictionary<IDocument, VolatileCounter> _pendingParse;
        private readonly VolatileCounter _pendingAnalysisEnqueue;

        // Uri does not consider #fragment for equality
        private readonly ProjectFiles _projectFiles = new ProjectFiles();
        private readonly OpenFiles _openFiles;
        private readonly TaskCompletionSource<bool> _analyzerCreationTcs = new TaskCompletionSource<bool>();

        internal Task _loadingFromDirectory;

        internal PythonAnalyzer _analyzer;
        internal ClientCapabilities _clientCaps;
        private InformationDisplayOptions _displayOptions;
        private LanguageServerSettings _settings = new LanguageServerSettings();
        private CompletionHandler _completionHandler;
        private SignatureHelpHandler _signatureHelpHandler;
        private FindReferencesHandler _findReferencesHandler;
        private HoverHandler _hoverHandler;
        private WorkspaceSymbolsHandler _workspaceSymbolsHandler;

        private bool _traceLogging;
        private bool _testEnvironment;
        private ReloadModulesQueueItem _reloadModulesQueueItem;

        // If null, all files must be added manually
        private string _rootDir;

        public Server() {
            _queue = new AnalysisQueue();
            _queue.UnhandledException += Analysis_UnhandledException;
            _pendingAnalysisEnqueue = new VolatileCounter();
            _parseQueue = new ParseQueue();
            _pendingParse = new Dictionary<IDocument, VolatileCounter>();
            _openFiles = new OpenFiles(_projectFiles, this);

            _displayOptions = new InformationDisplayOptions {
                trimDocumentationLines = true,
                maxDocumentationLineLength = 200,
                trimDocumentationText = true,
                maxDocumentationTextLength = 4096
            };
        }

        private void Analysis_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
            Debug.Fail(e.ExceptionObject.ToString());
            LogMessage(MessageType.Error, e.ExceptionObject.ToString());
        }

        public void Dispose() {
            _queue.Dispose();
        }

        #region ILogger
        public void TraceMessage(string message) {
            if (_traceLogging) {
                LogMessage(MessageType.Log, message.ToString());
            }
        }
        #endregion

        #region Client message handling
        public override async Task<InitializeResult> Initialize(InitializeParams @params) {
            _testEnvironment = @params.initializationOptions.testEnvironment;
            // Test environment needs predictable initialization.
            if (@params.initializationOptions.asyncStartup && !_testEnvironment) {
                CreateAnalyzer(@params.initializationOptions.interpreter).ContinueWith(t => {
                    if (t.IsFaulted) {
                        _analyzerCreationTcs.TrySetException(t.Exception);
                    } else {
                        try {
                            _analyzer = t.Result;
                            OnAnalyzerCreated(@params);
                            _analyzerCreationTcs.TrySetResult(true);
                        } catch (Exception ex) {
                            _analyzerCreationTcs.TrySetException(ex);
                            throw;
                        }
                    }
                }).DoNotWait();
            } else {
                try {
                    _analyzer = await CreateAnalyzer(@params.initializationOptions.interpreter);
                    OnAnalyzerCreated(@params);
                    _analyzerCreationTcs.TrySetResult(true);
                } catch (Exception ex) {
                    _analyzerCreationTcs.TrySetException(ex);
                    throw;
                }
            }

            return new InitializeResult {
                capabilities = new ServerCapabilities {
                    textDocumentSync = new TextDocumentSyncOptions { openClose = true, change = TextDocumentSyncKind.Incremental },
                    completionProvider = new CompletionOptions {
                        triggerCharacters = new[] { "." },
                    },
                    hoverProvider = true,
                    signatureHelpProvider = new SignatureHelpOptions { triggerCharacters = new[] { "(,)" } },
                    // https://github.com/Microsoft/PTVS/issues/3803
                    // definitionProvider = true,
                    referencesProvider = true
                }
            };
        }

        private void OnAnalyzerCreated(InitializeParams @params) {
            _clientCaps = @params.capabilities;
            _settings.SetCompletionTimeout(_clientCaps?.python?.completionsTimeout);

            _completionHandler = new CompletionHandler(_analyzer, _projectFiles, _clientCaps, this);
            _signatureHelpHandler = new SignatureHelpHandler(_analyzer,  _projectFiles, _clientCaps, this);
            _findReferencesHandler = new FindReferencesHandler(_analyzer, _projectFiles, _clientCaps, this);
            _hoverHandler = new HoverHandler(_analyzer, _projectFiles, _clientCaps, this);
            _workspaceSymbolsHandler = new WorkspaceSymbolsHandler(_projectFiles);

            _reloadModulesQueueItem = new ReloadModulesQueueItem(_analyzer);

            if (@params.initializationOptions.displayOptions != null) {
                _displayOptions = @params.initializationOptions.displayOptions;
            }

            if (string.IsNullOrEmpty(_analyzer.InterpreterFactory?.Configuration?.InterpreterPath)) {
                LogMessage(MessageType.Log, "Initializing for generic interpreter");
            } else {
                LogMessage(MessageType.Log, $"Initializing for {_analyzer.InterpreterFactory.Configuration.InterpreterPath}");
            }

            if (@params.rootUri != null) {
                _rootDir = @params.rootUri.ToAbsolutePath();
            } else if (!string.IsNullOrEmpty(@params.rootPath)) {
                _rootDir = PathUtils.NormalizePath(@params.rootPath);
            }

            SetSearchPaths(@params.initializationOptions.searchPaths);

            _traceLogging = _clientCaps?.python?.traceLogging ?? false;
            _analyzer.EnableDiagnostics = _clientCaps?.python?.liveLinting ?? false;

            if (_rootDir != null && !(_clientCaps?.python?.manualFileLoad ?? false)) {
                LogMessage(MessageType.Log, $"Loading files from {_rootDir}");
                _loadingFromDirectory = LoadFromDirectoryAsync(_rootDir);
            }
        }

        public override Task Shutdown() {
            Interlocked.Exchange(ref _analyzer, null)?.Dispose();
            _projectFiles.Clear();
            return Task.CompletedTask;
        }

        public override async Task DidOpenTextDocument(DidOpenTextDocumentParams @params) {
            TraceMessage($"Opening document {@params.textDocument.uri}");
            await _analyzerCreationTcs.Task;

            var entry = _projectFiles.GetEntry(@params.textDocument.uri, throwIfMissing: false);
            var doc = entry as IDocument;
            if (doc != null) {
                if (@params.textDocument.text != null) {
                    doc.ResetDocument(@params.textDocument.version, @params.textDocument.text);
                }
            } else if (entry == null) {
                IAnalysisCookie cookie = null;
                if (@params.textDocument.text != null) {
                    cookie = new InitialContentCookie {
                        Content = @params.textDocument.text,
                        Version = @params.textDocument.version
                    };
                }
                entry = await AddFileAsync(@params.textDocument.uri, null, cookie);
            }

            if ((doc = entry as IDocument) != null) {
                EnqueueItem(doc);
            }
        }

        public override void DidChangeTextDocument(DidChangeTextDocumentParams @params) {
            _analyzerCreationTcs.Task.Wait();
            var openedFile = _openFiles.GetDocument(@params.textDocument.uri);
            openedFile.DidChangeTextDocument(@params, _settings.AnalysisDelay, doc => EnqueueItem(doc, enqueueForAnalysis: @params._enqueueForAnalysis ?? true));
        }

        public override async Task DidChangeWatchedFiles(DidChangeWatchedFilesParams @params) {
            await _analyzerCreationTcs.Task;

            IProjectEntry entry;
            foreach (var c in @params.changes.MaybeEnumerate()) {
                switch (c.type) {
                    case FileChangeType.Created:
                        entry = await LoadFileAsync(c.uri);
                        if (entry != null) {
                            TraceMessage($"Saw {c.uri} created and loaded new entry");
                        } else {
                            LogMessage(MessageType.Warning, $"Failed to load {c.uri}");
                        }
                        break;
                    case FileChangeType.Deleted:
                        await UnloadFileAsync(c.uri);
                        break;
                    case FileChangeType.Changed:
                        if ((entry = _projectFiles.GetEntry(c.uri, false)) is IDocument doc) {
                            // If document version is >=0, it is loaded in memory.
                            if (doc.GetDocumentVersion(0) < 0) {
                                EnqueueItem(doc, AnalysisPriority.Low);
                            }
                        }
                        break;
                }
            }
        }

        public override async Task DidCloseTextDocument(DidCloseTextDocumentParams @params) {
            await _analyzerCreationTcs.Task;
            var doc = _projectFiles.GetEntry(@params.textDocument.uri) as IDocument;

            if (doc != null) {
                // No need to keep in-memory buffers now
                doc.ResetDocument(-1, null);

                // Pick up any changes on disk that we didn't know about
                EnqueueItem(doc, AnalysisPriority.Low);
            }
        }


        public override async Task DidChangeConfiguration(DidChangeConfigurationParams @params) {
            await _analyzerCreationTcs.Task;
            if (_analyzer == null) {
                LogMessage(MessageType.Error, "change configuration notification sent to uninitialized server");
                return;
            }

            if (@params.settings != null) {
                if (@params.settings is LanguageServerSettings settings) {
                    _settings = settings;
                } else {
                    LogMessage(MessageType.Error, "change configuration notification sent unsupported settings");
                    return;
                }
            }

            // Make sure reload modules is executed on the analyzer thread.
            var task = _reloadModulesQueueItem.Task;
            _queue.Enqueue(_reloadModulesQueueItem, AnalysisPriority.Normal);
            await task;

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in _analyzer.ModulesByFilename) {
                _queue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
        }

        public override async Task<CompletionList> Completion(CompletionParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();

            var uri = @params.textDocument.uri;
            // Make sure document is enqueued for processing
            var openFile = _openFiles.GetDocument(uri);
            openFile.WaitForChangeProcessingComplete(CancellationToken);

            var items = _completionHandler.GetCompletions(@params, _settings, CancellationToken);
            var res = new CompletionList { items = items };

            LogMessage(MessageType.Info, $"Found {res.items.Length} completions for {uri} at {@params.position} after filtering");
            return res;
        }

        public override Task<CompletionItem> CompletionItemResolve(CompletionItem item) {
            // TODO: Fill out missing values in item
            return Task.FromResult(item);
        }

        public override async Task<SignatureHelp> SignatureHelp(TextDocumentPositionParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();
            return _signatureHelpHandler.GetSignatureHelp(@params);
        }

        public override async Task<Reference[]> FindReferences(ReferencesParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();
            return _findReferencesHandler.FindReferences(@params, CancellationToken);
        }

        public override async Task<Hover> Hover(TextDocumentPositionParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();
            return _hoverHandler.GetHover(@params, _displayOptions, CancellationToken);

        }

        public override async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();
            return _workspaceSymbolsHandler.GetWorkspaceSymbols(@params);
        }

        #endregion

        #region Private Helpers

        private IProjectEntry RemoveEntry(Uri documentUri) {
            var entry = _projectFiles.RemoveEntry(documentUri);
            _openFiles.Remove(documentUri);
            return entry;
        }

        private async Task<PythonAnalyzer> CreateAnalyzer(PythonInitializationOptions.Interpreter interpreter) {
            IPythonInterpreterFactory factory = null;
            if (!string.IsNullOrEmpty(interpreter.assembly) && !string.IsNullOrEmpty(interpreter.typeName)) {
                try {
                    var assembly = File.Exists(interpreter.assembly) ? AssemblyName.GetAssemblyName(interpreter.assembly) : new AssemblyName(interpreter.assembly);
                    var type = Assembly.Load(assembly).GetType(interpreter.typeName, true);

                    factory = (IPythonInterpreterFactory)Activator.CreateInstance(
                        type,
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new object[] { interpreter.properties },
                        CultureInfo.CurrentCulture
                    );
                } catch (Exception ex) {
                    LogMessage(MessageType.Warning, ex.ToString());
                }
            } else {
                factory = new AstPythonInterpreterFactory(interpreter.properties);
            }

            if (factory == null) {
                Version v;
                if (!Version.TryParse(interpreter.version ?? "0.0", out v)) {
                    v = new Version();
                }
                factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(v);
            }

            var interp = factory.CreateInterpreter();
            if (interp == null) {
                throw new InvalidOperationException("Failed to create interpreter");
            }

            LogMessage(MessageType.Info, $"Created {interp.GetType().FullName} instance from {factory.GetType().FullName}");

            return await PythonAnalyzer.CreateAsync(factory, interp);
        }

        private IEnumerable<ModulePath> GetImportNames(Uri document) {
            var filePath = GetLocalPath(document);

            if (!string.IsNullOrEmpty(filePath)) {
                if (!string.IsNullOrEmpty(_rootDir) && ModulePath.FromBasePathAndFile_NoThrow(_rootDir, filePath, out var mp)) {
                    yield return mp;
                }

                foreach (var sp in _analyzer.GetSearchPaths()) {
                    if (ModulePath.FromBasePathAndFile_NoThrow(sp, filePath, out mp)) {
                        yield return mp;
                    }
                }
            }

            if (document.Scheme == "python") {
                var path = Path.Combine(document.Host, document.AbsolutePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar));
                if (ModulePath.FromBasePathAndFile_NoThrow("", path, p => true, out var mp, out _, out _)) {
                    yield return mp;
                }
            }
        }


        #endregion

        #region Non-LSP public API
        public IProjectEntry GetEntry(TextDocumentIdentifier document) => _projectFiles.GetEntry(document.uri);
        public IProjectEntry GetEntry(Uri documentUri, bool throwIfMissing = true) => _projectFiles.GetEntry(documentUri, throwIfMissing);

        public int GetPart(TextDocumentIdentifier document) => _projectFiles.GetPart(document.uri);
        public IEnumerable<string> GetLoadedFiles() => _projectFiles.GetLoadedFiles();

        public Task<IProjectEntry> LoadFileAsync(Uri documentUri) => AddFileAsync(documentUri, null);
        public Task<IProjectEntry> LoadFileAsync(Uri documentUri, Uri fromSearchPath) => AddFileAsync(documentUri, fromSearchPath);

        public Task<bool> UnloadFileAsync(Uri documentUri) {
            var entry = RemoveEntry(documentUri);
            if (entry != null) {
                if (entry is IPythonProjectEntry pyEntry) {
                    foreach (var e in _analyzer.GetEntriesThatImportModule(pyEntry.ModuleName, false)) {
                        _queue.Enqueue(e, AnalysisPriority.Normal);
                    }
                }
                _analyzer.RemoveModule(entry);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task WaitForDirectoryScanAsync() => _loadingFromDirectory ?? Task.CompletedTask;

        public async Task WaitForCompleteAnalysisAsync() {
            // Wait for all current parsing to complete
            TraceMessage($"Waiting for parsing to complete");
            await _parseQueue.WaitForAllAsync();
            TraceMessage($"Parsing complete. Waiting for analysis entries to enqueue");
            await _pendingAnalysisEnqueue.WaitForZeroAsync();
            TraceMessage($"Enqueue complete. Waiting for analysis to complete");
            await _queue.WaitForCompleteAsync();
            TraceMessage($"Analysis complete.");
        }

        public int EstimateRemainingWork() {
            return _parseQueue.Count + _queue.Count;
        }

        public event EventHandler<ParseCompleteEventArgs> OnParseComplete;
        private void ParseComplete(Uri uri, int version) {
            TraceMessage($"Parse complete for {uri} at version {version}");
            OnParseComplete?.Invoke(this, new ParseCompleteEventArgs { uri = uri, version = version });
        }

        public event EventHandler<AnalysisCompleteEventArgs> OnAnalysisComplete;
        private void AnalysisComplete(Uri uri, int version) {
            TraceMessage($"Analysis complete for {uri} at version {version}");
            OnAnalysisComplete?.Invoke(this, new AnalysisCompleteEventArgs { uri = uri, version = version });
        }

        public void SetSearchPaths(IEnumerable<string> searchPaths) => _analyzer.SetSearchPaths(searchPaths.MaybeEnumerate());

        public event EventHandler<FileFoundEventArgs> OnFileFound;
        private void FileFound(Uri uri) {
            TraceMessage($"Found file to analyze {uri}");
            OnFileFound?.Invoke(this, new FileFoundEventArgs { uri = uri });
        }

        #endregion

        private string GetLocalPath(Uri uri) {
            if (uri == null) {
                return null;
            }
            if (uri.IsFile) {
                return uri.LocalPath;
            }
            var scheme = uri.Scheme;
            var path = uri.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var bits = new List<string>(path.Length + 2);
            bits.Add("_:"); // Non-existent root
            bits.Add(scheme.ToUpperInvariant());
            bits.AddRange(path);
            return string.Join(Path.DirectorySeparatorChar.ToString(), bits);
        }

        private Task<IProjectEntry> AddFileAsync(Uri documentUri, Uri fromSearchPath, IAnalysisCookie cookie = null) {
            var item = _projectFiles.GetEntry(documentUri, throwIfMissing: false);

            if (item != null) {
                return Task.FromResult(item);
            }

            IEnumerable<string> aliases = null;
            var path = GetLocalPath(documentUri);
            if (fromSearchPath != null) {
                if (ModulePath.FromBasePathAndFile_NoThrow(GetLocalPath(fromSearchPath), path, out var mp)) {
                    aliases = new[] { mp.ModuleName };
                }
            } else {
                aliases = GetImportNames(documentUri).Select(mp => mp.ModuleName).ToArray();
            }

            if (!(aliases?.Any() ?? false)) {
                aliases = new[] { Path.GetFileNameWithoutExtension(path) };
            }

            var reanalyzeEntries = aliases.SelectMany(a => _analyzer.GetEntriesThatImportModule(a, true)).ToArray();
            var first = aliases.FirstOrDefault();

            var pyItem = _analyzer.AddModule(first, path, documentUri, cookie);
            item = pyItem;
            foreach (var a in aliases.Skip(1)) {
                _analyzer.AddModuleAlias(first, a);
            }

            var actualItem = _projectFiles.GetOrAddEntry(documentUri, item);
            if (actualItem != item) {
                return Task.FromResult(actualItem);
            }

            if (_clientCaps?.python?.analysisUpdates ?? false) {
                pyItem.OnNewAnalysis += ProjectEntry_OnNewAnalysis;
            }

            if (item is IDocument doc) {
                EnqueueItem(doc);
            }

            if (reanalyzeEntries != null) {
                foreach (var entryRef in reanalyzeEntries) {
                    _queue.Enqueue(entryRef, AnalysisPriority.Low);
                }
            }

            return Task.FromResult(item);
        }

        private void RemoveDocumentParseCounter(Task t, IDocument doc, VolatileCounter counter) {
            if (t.IsCompleted) {
                lock (_pendingParse) {
                    if (counter.IsZero) {
                        if (_pendingParse.TryGetValue(doc, out var existing) && existing == counter) {
                            _pendingParse.Remove(doc);
                        }
                        return;
                    }
                }
            }
            counter.WaitForChangeToZeroAsync().ContinueWith(t2 => RemoveDocumentParseCounter(t, doc, counter));
        }

        private IDisposable GetDocumentParseCounter(IDocument doc, out int count) {
            VolatileCounter counter;
            lock (_pendingParse) {
                if (!_pendingParse.TryGetValue(doc, out counter)) {
                    _pendingParse[doc] = counter = new VolatileCounter();
                    // Automatically remove counter from the dictionary when it reaches zero.
                    counter.WaitForChangeToZeroAsync().ContinueWith(t => RemoveDocumentParseCounter(t, doc, counter));
                }
                var res = counter.Incremented();
                count = counter.Count;
                return res;
            }
        }

        private void EnqueueItem(IDocument doc, AnalysisPriority priority = AnalysisPriority.Normal, bool enqueueForAnalysis = true) {
            var pending = _pendingAnalysisEnqueue.Incremented();
            try {
                Task<IAnalysisCookie> cookieTask;
                using (GetDocumentParseCounter(doc, out var count)) {
                    if (count > 3) {
                        // Rough check to prevent unbounded queueing. If we have
                        // multiple parses in queue, we will get the latest doc
                        // version in one of the ones to come.
                        return;
                    }
                    TraceMessage($"Parsing document {doc.DocumentUri}");
                    cookieTask = _parseQueue.Enqueue(doc, _analyzer.LanguageVersion);
                }

                // The call must be fire and forget, but should not be yielding.
                // It is called from DidChangeTextDocument which must fully finish
                // since otherwise Complete() may come before the change is enqueued
                // for processing and the completion list will be driven off the stale data.
                var p = pending;
                cookieTask.ContinueWith(t => {
                    if (t.IsFaulted) {
                        // Happens when file got deleted before processing
                        p.Dispose();
                        LogMessage(MessageType.Error, t.Exception.Message);
                        return;
                    }
                    OnDocumentChangeProcessingComplete(doc, t.Result as VersionCookie, enqueueForAnalysis, priority, p);
                }).DoNotWait();
                pending = null;
            } finally {
                pending?.Dispose();
            }
        }

        private void OnDocumentChangeProcessingComplete(IDocument doc, VersionCookie vc, bool enqueueForAnalysis, AnalysisPriority priority, IDisposable disposeWhenEnqueued) {
            try {
                if (vc != null) {
                    foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                        ParseComplete(kv.Key, kv.Value.Version);
                    }
                } else {
                    ParseComplete(doc.DocumentUri, 0);
                }

                if (doc is IAnalyzable analyzable && enqueueForAnalysis) {
                    TraceMessage($"Enqueing document {doc.DocumentUri} for analysis");
                    _queue.Enqueue(analyzable, priority);
                }

                disposeWhenEnqueued?.Dispose();
                disposeWhenEnqueued = null;
                var openedFile = _openFiles.GetDocument(doc.DocumentUri);
                if (vc != null) {
                    var reported = openedFile.LastReportedDiagnostics;
                    lock (reported) {
                        foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                            var part = _projectFiles.GetPart(kv.Key);
                            if (!reported.TryGetValue(part, out var lastVersion) || lastVersion.Version < kv.Value.Version) {
                                reported[part] = kv.Value;
                                PublishDiagnostics(new PublishDiagnosticsEventArgs {
                                    uri = kv.Key,
                                    diagnostics = kv.Value.Diagnostics,
                                    _version = kv.Value.Version
                                });
                            }
                        }
                    }
                }
            } catch (BadSourceException) {
            } catch (OperationCanceledException ex) {
                LogMessage(MessageType.Warning, $"Parsing {doc.DocumentUri} cancelled");
                TraceMessage($"{ex}");
            } catch (Exception ex) {
                LogMessage(MessageType.Error, ex.ToString());
            } finally {
                disposeWhenEnqueued?.Dispose();
            }
        }

        private void ProjectEntry_OnNewAnalysis(object sender, EventArgs e) {
            if (sender is IPythonProjectEntry entry) {
                TraceMessage($"Received new analysis for {entry.DocumentUri}");
                var version = 0;
                var parse = entry.GetCurrentParse();
                if (parse?.Cookie is VersionCookie vc && vc.Versions.Count > 0) {
                    foreach (var kv in vc.GetAllParts(entry.DocumentUri)) {
                        AnalysisComplete(kv.Key, kv.Value.Version);
                        if (kv.Value.Version > version) {
                            version = kv.Value.Version;
                        }
                    }
                } else {
                    AnalysisComplete(entry.DocumentUri, 0);
                }

                var diags = _analyzer.GetDiagnostics(entry);
                if (!diags.Any()) {
                    return;
                }

                if (entry is IDocument doc) {
                    var reported = _openFiles.GetDocument(doc.DocumentUri).LastReportedDiagnostics;
                    lock (reported) {
                        if (reported.TryGetValue(0, out var lastVersion)) {
                            diags = diags.Concat(lastVersion.Diagnostics).ToArray();
                        }
                    }
                }

                PublishDiagnostics(new PublishDiagnosticsEventArgs {
                    diagnostics = diags,
                    uri = entry.DocumentUri,
                    _version = version
                });
            }
        }

        private async Task LoadFromDirectoryAsync(string rootDir) {
            foreach (var file in PathUtils.EnumerateFiles(rootDir, recurse: false, fullPaths: true)) {
                if (!ModulePath.IsPythonSourceFile(file)) {
                    if (ModulePath.IsPythonFile(file, true, true, true)) {
                        // TODO: Deal with scrapable files (if we need to do anything?)
                    }
                    continue;
                }

                var entry = await LoadFileAsync(new Uri(PathUtils.NormalizePath(file)));
                if (entry != null) {
                    FileFound(entry.DocumentUri);
                }
            }
            foreach (var dir in PathUtils.EnumerateDirectories(rootDir, recurse: false, fullPaths: true)) {
                // TODO: figure out correct loading sequence and the name resolution 
                // so files in different folders don't replace each other.
                // See https://github.com/Microsoft/vscode-python/issues/1063

                //if (!ModulePath.PythonVersionRequiresInitPyFiles(_analyzer.LanguageVersion.ToVersion()) ||
                //    !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(dir))) {

                // Skip over virtual environments.
                // TODO: handle pyenv that may have more compilcated structure
                if (!Directory.Exists(Path.Combine(dir, "lib", "site-packages"))) {
                    await LoadFromDirectoryAsync(dir);
                }
                //}
            }
        }

        private void IfTestWaitForAnalysisComplete() {
            if (_testEnvironment) {
                WaitForDirectoryScanAsync().Wait();
                WaitForCompleteAnalysisAsync().Wait();
            }
        }
    }
}
