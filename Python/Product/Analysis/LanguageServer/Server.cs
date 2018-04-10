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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class Server : ServerBase, IDisposable {
        internal readonly AnalysisQueue _queue;
        internal readonly ParseQueue _parseQueue;
        private readonly Dictionary<IDocument, VolatileCounter> _pendingParse;
        private readonly VolatileCounter _pendingAnalysisEnqueue;
        private readonly DisplayTextBuilder _displayTextBuilder = new DisplayTextBuilder();

        // Uri does not consider #fragment for equality
        private readonly ConcurrentDictionary<Uri, IProjectEntry> _projectFiles;
        private readonly ConcurrentDictionary<Uri, Dictionary<int, BufferVersion>> _lastReportedDiagnostics;

        // For pending changes, we use alternate comparer that checks #fragment
        private readonly ConcurrentDictionary<Uri, List<DidChangeTextDocumentParams>> _pendingChanges;
        private readonly ManualResetEventSlim _documentChangeProcessingComplete = new ManualResetEventSlim(true);
        private readonly TaskCompletionSource<bool> _analyzerCreationTcs = new TaskCompletionSource<bool>();

        internal Task _loadingFromDirectory;

        internal PythonAnalyzer _analyzer;
        internal ClientCapabilities _clientCaps;
        private InformationDisplayOptions _displayOptions;
        private bool _traceLogging;
        private bool _testEnvironment;

        // If null, all files must be added manually
        private string _rootDir;

        public Server() {
            _queue = new AnalysisQueue();
            _queue.UnhandledException += Analysis_UnhandledException;
            _pendingAnalysisEnqueue = new VolatileCounter();
            _parseQueue = new ParseQueue();
            _pendingParse = new Dictionary<IDocument, VolatileCounter>();
            _projectFiles = new ConcurrentDictionary<Uri, IProjectEntry>();
            _pendingChanges = new ConcurrentDictionary<Uri, List<DidChangeTextDocumentParams>>(UriEqualityComparer.IncludeFragment);
            _lastReportedDiagnostics = new ConcurrentDictionary<Uri, Dictionary<int, BufferVersion>>();
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

        private void TraceMessage(IFormattable message) {
            if (_traceLogging) {
                LogMessage(MessageType.Log, message.ToString());
            }
        }

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
                        resolveProvider = true
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
            if (@params.initializationOptions.displayOptions != null) {
                _displayOptions = @params.initializationOptions.displayOptions;
            }

            if (string.IsNullOrEmpty(_analyzer.InterpreterFactory?.Configuration?.InterpreterPath)) {
                LogMessage(MessageType.Log, "Initializing for generic interpreter");
            } else {
                LogMessage(MessageType.Log, $"Initializing for {_analyzer.InterpreterFactory.Configuration.InterpreterPath}");
            }

            _clientCaps = @params.capabilities;

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

            var entry = GetEntry(@params.textDocument.uri, throwIfMissing: false);
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

            var changes = @params.contentChanges;
            if (changes == null) {
                return;
            }

            _documentChangeProcessingComplete.Reset();
            try {
                var uri = @params.textDocument.uri;
                var entry = GetEntry(uri);
                var part = GetPart(uri);

                TraceMessage($"Received changes for {uri}");

                if (entry is IDocument doc) {
                    var docVersion = Math.Max(doc.GetDocumentVersion(part), 0);
                    var fromVersion = Math.Max(@params.textDocument.version - 1 ?? docVersion, 0);

                    List<DidChangeTextDocumentParams> pending;
                    if (fromVersion > docVersion && @params.contentChanges?.Any(c => c.range == null) != true) {
                        // Expected from version hasn't been seen yet, and there are no resets in this
                        // change, so enqueue it for later.
                        LogMessage(MessageType.Log, $"Deferring changes for {uri} until version {fromVersion} is seen");
                        pending = _pendingChanges.GetOrAdd(uri, _ => new List<DidChangeTextDocumentParams>());
                        lock (pending) {
                            pending.Add(@params);
                        }
                        return;
                    }

                    var toVersion = @params.textDocument.version ?? (fromVersion + changes.Length);

                    doc.UpdateDocument(part, new DocumentChangeSet(
                        fromVersion,
                        toVersion,
                        changes.Select(c => new DocumentChange {
                            ReplacedSpan = c.range.GetValueOrDefault(),
                            WholeBuffer = !c.range.HasValue,
                            InsertedText = c.text
                        })
                    ));

                    if (_pendingChanges.TryGetValue(uri, out pending) && pending != null) {
                        DidChangeTextDocumentParams? next = null;
                        lock (pending) {
                            var notExpired = pending.Where(p => p.textDocument.version.GetValueOrDefault() >= toVersion).OrderBy(p => p.textDocument.version.GetValueOrDefault()).ToArray();
                            if (notExpired.Any()) {
                                pending.Clear();
                                next = notExpired.First();
                                pending.AddRange(notExpired.Skip(1));
                            } else {
                                _pendingChanges.TryRemove(uri, out _);
                            }
                        }
                        if (next.HasValue) {
                            DidChangeTextDocument(next.Value);
                            return;
                        }
                    }

                    TraceMessage($"Applied changes to {uri}");
                    EnqueueItem(doc, enqueueForAnalysis: @params._enqueueForAnalysis ?? true);
                }
            } finally {
                _documentChangeProcessingComplete.Set();
            }
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
                        if ((entry = GetEntry(c.uri, false)) is IDocument doc) {
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
            var doc = GetEntry(@params.textDocument.uri) as IDocument;

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

            _queue.Enqueue(new AnalysisQueueWorkItem(() => _analyzer.ReloadModulesAsync().WaitAndUnwrapExceptions()), AnalysisPriority.Normal);

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in _analyzer.ModulesByFilename) {
                _queue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
        }

        public override async Task<CompletionList> Completion(CompletionParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();
            // Make sure document is enqueued for processing
            _documentChangeProcessingComplete.Wait(200, CancellationToken);

            var uri = @params.textDocument.uri;
            GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Completions in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return new CompletionList();
            }

            var opts = GetMemberOptions.None;
            if (@params.context.HasValue) {
                var c = @params.context.Value;
                if (c._intersection) {
                    opts |= GetMemberOptions.IntersectMultipleResults;
                }
                if (c._statementKeywords ?? true) {
                    opts |= GetMemberOptions.IncludeStatementKeywords;
                }
                if (c._expressionKeywords ?? true) {
                    opts |= GetMemberOptions.IncludeExpressionKeywords;
                }
            } else {
                opts = GetMemberOptions.IncludeStatementKeywords | GetMemberOptions.IncludeExpressionKeywords;
            }

            var parse = entry.WaitForCurrentParse(_clientCaps?.python?.completionsTimeout ?? Timeout.Infinite, CancellationToken);
            if (_traceLogging) {
                if (parse == null) {
                    LogMessage(MessageType.Error, $"Timed out waiting for AST for {uri}");
                } else if (parse.Cookie is VersionCookie vc && vc.Versions.TryGetValue(GetPart(uri), out var bv)) {
                    LogMessage(MessageType.Log, $"Got AST for {uri} at version {bv.Version}");
                } else {
                    LogMessage(MessageType.Log, $"Got AST for {uri}");
                }
            }
            tree = parse?.Tree ?? tree;

            IEnumerable<MemberResult> members = null;
            Expression expr = null;
            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Completing expression {@params._expr}");

                if (@params.context?._filterKind == CompletionItemKind.Module) {
                    // HACK: Special case for child modules until #3798 is completed
                    members = entry.Analysis.ProjectState.GetModuleMembers(entry.Analysis.InterpreterContext, @params._expr.Split('.'));
                } else {
                    members = entry.Analysis.GetMembers(@params._expr, @params.position, opts);
                }
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.EvaluateMembers);
                expr = finder.GetExpression(@params.position) as Expression;
                if (expr != null) {
                    TraceMessage($"Completing expression {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    members = analysis.GetMembers(expr, @params.position, opts, null);
                } else {
                    TraceMessage($"Completing all names");
                    members = entry.Analysis.GetAllAvailableMembers(@params.position, opts);
                }
            }


            if (@params.context?._includeAllModules ?? false) {
                var mods = _analyzer.GetModules();
                TraceMessage($"Including {mods?.Length ?? 0} modules");
                members = members?.Concat(mods) ?? mods;
            }

            if (@params.context?._includeArgumentNames ?? false) {
                var finder = new ExpressionFinder(tree, new GetExpressionOptions { Calls = true });
                var index = tree.LocationToIndex(@params.position);
                if (finder.GetExpression(@params.position) is CallExpression callExpr &&
                    callExpr.GetArgumentAtIndex(tree, index, out _)) {
                    var argNames = analysis.GetSignatures(callExpr.Target, @params.position)
                        .SelectMany(o => o.Parameters).Select(p => p?.Name)
                        .Where(n => !string.IsNullOrEmpty(n))
                        .Distinct()
                        .Except(callExpr.Args.MaybeEnumerate().Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)))
                        .Select(n => new MemberResult($"{n}=", PythonMemberType.NamedArgument));

                    if (_traceLogging) {
                        argNames = argNames.MaybeEnumerate().ToArray();
                        LogMessage(MessageType.Log, $"Including {argNames.Count()} named arguments");
                    }
                    members = members?.Concat(argNames) ?? argNames;
                }
            }

            if (members == null) {
                TraceMessage($"No members found in document {uri}");
                return new CompletionList();
            }

            var filtered = members.Select(m => ToCompletionItem(m, opts));
            var filterKind = @params.context?._filterKind;
            if (filterKind.HasValue && filterKind != CompletionItemKind.None) {
                TraceMessage($"Only returning {filterKind.Value} items");
                filtered = filtered.Where(m => m.kind == filterKind.Value);
            }

            var res = new CompletionList { items = filtered.ToArray() };
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

            var uri = @params.textDocument.uri;
            GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Signatures in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return new SignatureHelp();
            }

            IEnumerable<IOverloadResult> overloads;
            int activeSignature = -1, activeParameter = -1;
            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting signatures for {@params._expr}");
                overloads = analysis.GetSignatures(@params._expr, @params.position);
            } else {
                var finder = new ExpressionFinder(tree, new GetExpressionOptions { Calls = true });
                var index = tree.LocationToIndex(@params.position);
                if (finder.GetExpression(@params.position) is CallExpression callExpr) {
                    TraceMessage($"Getting signatures for {callExpr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    overloads = analysis.GetSignatures(callExpr.Target, @params.position);
                    activeParameter = -1;
                    if (callExpr.GetArgumentAtIndex(tree, index, out activeParameter) && activeParameter < 0) {
                        // Returned 'true' and activeParameter == -1 means that we are after 
                        // the trailing comma, so assume partially typed expression such as 'pow(x, y, |)
                        activeParameter = callExpr.Args.Count;
                    }
                } else {
                    LogMessage(MessageType.Info, $"No signatures found in {uri} at {@params.position}");
                    return new SignatureHelp();
                }
            }

            var sigs = overloads.Select(ToSignatureInformation).ToArray();
            if (activeParameter >= 0 && activeSignature < 0) {
                // TODO: Better selection of active signature
                activeSignature = sigs
                    .Select((s, i) => Tuple.Create(s, i))
                    .OrderBy(t => t.Item1.parameters.Length)
                    .FirstOrDefault(t => t.Item1.parameters.Length > activeParameter)
                    ?.Item2 ?? -1;
            }

            activeSignature = activeSignature >= 0
                ? activeSignature
                : (sigs.Length > 0 ? 0 : -1);

            var sh = new SignatureHelp {
                signatures = sigs,
                activeSignature = activeSignature,
                activeParameter = activeParameter
            };
            return sh;
        }

        public override async Task<Reference[]> FindReferences(ReferencesParams @params) {
            await _analyzerCreationTcs.Task;

            var uri = @params.textDocument.uri;
            GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"References in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return Array.Empty<Reference>();
            }

            int? version = null;
            var parse = entry.WaitForCurrentParse(_clientCaps?.python?.completionsTimeout ?? Timeout.Infinite, CancellationToken);
            if (parse != null) {
                tree = parse.Tree ?? tree;
                if (parse.Cookie is VersionCookie vc) {
                    if (vc.Versions.TryGetValue(GetPart(uri), out var bv)) {
                        tree = bv.Ast ?? tree;
                        if (bv.Version >= 0) {
                            version = bv.Version;
                        }
                    }
                }
            }

            var extras = new List<Reference>();

            if (@params.context?.includeDeclaration ?? false) {
                var index = tree.LocationToIndex(@params.position);
                var w = new ImportedModuleNameWalker(entry.ModuleName, index);
                tree.Walk(w);
                ModuleReference modRef;
                if (!string.IsNullOrEmpty(w.ImportedName) &&
                    _analyzer.Modules.TryImport(w.ImportedName, out modRef)) {

                    // Return a module reference
                    extras.AddRange(modRef.AnalysisModule.Locations
                        .Select(l => new Reference {
                            uri = l.DocumentUri,
                            range = l.Span,
                            _version = version,
                            _kind = ReferenceKind.Definition
                        })
                        .ToArray()
                    );
                }
            }

            IEnumerable<IAnalysisVariable> result;
            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting references for {@params._expr}");
                result = analysis.GetVariables(@params._expr, @params.position);
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.FindDefinition);
                if (finder.GetExpression(@params.position) is Expression expr) {
                    TraceMessage($"Getting references for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
                    result = analysis.GetVariables(expr, @params.position);
                } else {
                    LogMessage(MessageType.Info, $"No references found in {uri} at {@params.position}");
                    return Array.Empty<Reference>();
                }
            }

            var filtered = result.Where(v => v.Type != VariableType.None);
            if (!(@params.context?.includeDeclaration ?? false)) {
                filtered = filtered.Where(v => v.Type != VariableType.Definition);
            }
            if (!(@params.context?._includeValues ?? false)) {
                filtered = filtered.Where(v => v.Type != VariableType.Value);
            }

            var res = filtered.Select(v => new Reference {
                uri = v.Location.DocumentUri,
                range = v.Location.Span,
                _kind = ToReferenceKind(v.Type),
                _version = version
            })
                .Concat(extras)
                .GroupBy(r => r, ReferenceComparer.Instance)
                .Select(g => g.OrderByDescending(r => (SourceLocation)r.range.end).ThenBy(r => (int?)r._kind ?? int.MaxValue).First())
                .ToArray();
            return res;
        }

        public override async Task<Hover> Hover(TextDocumentPositionParams @params) {
            await _analyzerCreationTcs.Task;
            IfTestWaitForAnalysisComplete();

            var uri = @params.textDocument.uri;
            GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);

            TraceMessage($"Hover in {uri} at {@params.position}");

            var analysis = entry?.Analysis;
            if (analysis == null) {
                TraceMessage($"No analysis found for {uri}");
                return default(Hover);
            }

            tree = GetParseTree(entry, uri, _clientCaps?.python?.completionsTimeout ?? Timeout.Infinite, out var version) ?? tree;

            var index = tree.LocationToIndex(@params.position);
            var w = new ImportedModuleNameWalker(entry.ModuleName, index);
            tree.Walk(w);
            if (!string.IsNullOrEmpty(w.ImportedName) &&
                _analyzer.Modules.TryImport(w.ImportedName, out var modRef)) {
                var contents = _displayTextBuilder.MakeModuleHoverText(modRef);
                if (contents != null) {
                    return new Hover { contents = contents };
                }
            }

            Expression expr;
            SourceSpan? exprSpan;
            Analyzer.InterpreterScope scope = null;

            if (!string.IsNullOrEmpty(@params._expr)) {
                TraceMessage($"Getting hover for {@params._expr}");
                expr = analysis.GetExpressionForText(@params._expr, @params.position, out scope, out var exprTree);
                // This span will not be valid within the document, but it will at least
                // have the correct length. If we have passed "_expr" then we are likely
                // planning to ignore the returned span anyway.
                exprSpan = expr?.GetSpan(exprTree);
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.Hover);
                expr = finder.GetExpression(@params.position) as Expression;
                exprSpan = expr?.GetSpan(tree);
            }
            if (expr == null) {
                LogMessage(MessageType.Info, $"No hover info found in {uri} at {@params.position}");
                return default(Hover);
            }

            TraceMessage($"Getting hover for {expr.ToCodeString(tree, CodeFormattingOptions.Traditional)}");
            var values = analysis.GetValues(expr, @params.position, scope).ToList();

            string originalExpr;
            if (expr is ConstantExpression || expr is ErrorExpression) {
                originalExpr = null;
            } else {
                originalExpr = @params._expr?.Trim();
                if (string.IsNullOrEmpty(originalExpr)) {
                    originalExpr = expr.ToCodeString(tree, CodeFormattingOptions.Traditional);
                }
            }

            var names = values.Select(GetFullTypeName).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();

            var res = new Hover {
                contents = new MarkupContent {
                    kind = MarkupKind.Markdown,
                    value = _displayTextBuilder.MakeHoverText(values, originalExpr, _displayOptions)
                },
                range = exprSpan,
                _version = version,
                _typeNames = names
            };
            return res;
        }

        public override async Task<SymbolInformation[]> WorkspaceSymbols(WorkspaceSymbolParams @params) {
            await _analyzerCreationTcs.Task;
            var members = Enumerable.Empty<MemberResult>();
            var opts = GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DeclaredOnly;

            foreach (var entry in _projectFiles) {
                members = members.Concat(
                    GetModuleVariables(entry.Value as IPythonProjectEntry, opts, @params.query)
                );
            }

            members = members.GroupBy(mr => mr.Name).Select(g => g.First());
            return members.Select(m => ToSymbolInformation(m)).ToArray();
        }

        #endregion

        #region Private Helpers

        internal IProjectEntry GetOrAddEntry(Uri documentUri, IProjectEntry entry) {
            return _projectFiles.GetOrAdd(documentUri, entry);
        }

        internal IProjectEntry GetEntry(Uri documentUri, bool throwIfMissing = true) {
            IProjectEntry entry = null;
            if ((documentUri == null || !_projectFiles.TryGetValue(documentUri, out entry)) && throwIfMissing) {
                throw new LanguageServerException(LanguageServerException.UnknownDocument, "unknown document");
            }
            return entry;
        }

        internal int GetPart(Uri documentUri) {
            var f = documentUri.Fragment;
            int i;
            if (string.IsNullOrEmpty(f) ||
                !f.StartsWithOrdinal("#") ||
                !int.TryParse(f.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) {
                i = 0;
            }
            return i;
        }

        private IProjectEntry RemoveEntry(Uri documentUri) {
            _projectFiles.TryRemove(documentUri, out var entry);
            _lastReportedDiagnostics.TryRemove(documentUri, out _);
            return entry;
        }

        internal IEnumerable<string> GetLoadedFiles() => _projectFiles.Keys.Select(k => k.AbsoluteUri);


        private void GetAnalysis(TextDocumentIdentifier document, Position position, int? expectedVersion, out IPythonProjectEntry entry, out PythonAst tree) {
            entry = GetEntry(document) as IPythonProjectEntry;
            if (entry == null) {
                throw new LanguageServerException(LanguageServerException.UnsupportedDocumentType, "unsupported document");
            }
            var parse = entry.GetCurrentParse();
            tree = parse?.Tree;
            if (expectedVersion.HasValue && parse?.Cookie is VersionCookie vc) {
                if (vc.Versions.TryGetValue(GetPart(document.uri), out var bv)) {
                    if (bv.Version != expectedVersion.Value) {
                        throw new LanguageServerException(LanguageServerException.MismatchedVersion, $"document is at version {bv.Version}; expected {expectedVersion.Value}");
                    }
                    tree = bv.Ast;
                }
            }
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

        private string MakeHoverText(IEnumerable<AnalysisValue> values, string originalExpression, bool limitLines = true) {
            string firstLongDescription = null;
            var multiline = false;
            var result = new StringBuilder();
            var descriptions = new HashSet<string>();

            foreach (var v in values) {
                if (string.IsNullOrEmpty(firstLongDescription)) {
                    firstLongDescription = limitLines ? LimitLines(v.Description) : v.Description;
                }

                var description = LimitLines(v.ShortDescription ?? "");
                if (string.IsNullOrEmpty(description)) {
                    continue;
                }

                if (descriptions.Add(description)) {
                    if (descriptions.Count > 1) {
                        if (result.Length == 0) {
                            // Nop
                        } else if (result[result.Length - 1] != '\n') {
                            result.Append(", ");
                        } else {
                            multiline = true;
                        }
                    }
                    result.Append(description);
                }
            }

            if (descriptions.Count == 1 && !string.IsNullOrEmpty(firstLongDescription)) {
                result.Clear();
                result.Append(firstLongDescription);
            }

            if (!string.IsNullOrEmpty(originalExpression)) {
                if (originalExpression.Length > 4096) {
                    originalExpression = originalExpression.Substring(0, 4093) + "...";
                }
                if (multiline) {
                    result.Insert(0, originalExpression + ": " + Environment.NewLine);
                } else if (result.Length > 0) {
                    result.Insert(0, originalExpression + ": ");
                } else {
                    result.Append(originalExpression);
                    result.Append(": ");
                    result.Append("<unknown type>");
                }
            }

            return result.ToString().Trim();
        }

        internal static string LimitLines(
            string str,
            int maxLines = 30,
            int charsPerLine = 200,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            var lineCount = 0;
            var prettyPrinted = new StringBuilder();
            var wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < maxLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = maxLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / charsPerLine) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= maxLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }

        private static string GetFullTypeName(AnalysisValue value) {
            if (value is IHasQualifiedName qualName) {
                return qualName.FullyQualifiedName;
            }

            if (value is Values.InstanceInfo ii) {
                return GetFullTypeName(ii.ClassInfo);
            }

            if (value is Values.BuiltinInstanceInfo bii) {
                return GetFullTypeName(bii.ClassInfo);
            }

            return value?.Name;
        }

        #endregion

        #region Non-LSP public API

        public IProjectEntry GetEntry(TextDocumentIdentifier document) => GetEntry(document.uri);

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
            var item = GetEntry(documentUri, throwIfMissing: false);

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

            var actualItem = GetOrAddEntry(documentUri, item);
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

        private static bool IsDocumentChanged(IDocument doc, VersionCookie previousResult) {
            if (previousResult == null) {
                return true;
            }

            var seen = 0;
            foreach (var part in doc.DocumentParts) {
                if (!previousResult.Versions.TryGetValue(part, out var bv)) {
                    return true;
                }
                if (doc.GetDocumentVersion(part) > bv.Version) {
                    return true;
                }
                seen += 1;
            }
            if (seen != doc.DocumentParts.Count()) {
                return true;
            }
            return false;
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

                if (vc != null) {
                    var reported = _lastReportedDiagnostics.GetOrAdd(doc.DocumentUri, _ => new Dictionary<int, BufferVersion>());
                    lock (reported) {
                        foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                            var part = GetPart(kv.Key);
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

                if (entry is IDocument doc && _lastReportedDiagnostics.TryGetValue(doc.DocumentUri, out var reported)) {
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


        private CompletionItem ToCompletionItem(MemberResult m, GetMemberOptions opts) {
            var res = new CompletionItem {
                label = m.Name,
                insertText = m.Completion,
                documentation = m.Documentation,
                // Place regular items first, advanced entries last
                sortText = Char.IsLetter(m.Completion[0]) ? "1" : "2",
                kind = ToCompletionItemKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            return res;
        }

        private CompletionItemKind ToCompletionItemKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return CompletionItemKind.None;
                case PythonMemberType.Class: return CompletionItemKind.Class;
                case PythonMemberType.Instance: return CompletionItemKind.Value;
                case PythonMemberType.Delegate: return CompletionItemKind.Class;
                case PythonMemberType.DelegateInstance: return CompletionItemKind.Function;
                case PythonMemberType.Enum: return CompletionItemKind.Enum;
                case PythonMemberType.EnumInstance: return CompletionItemKind.EnumMember;
                case PythonMemberType.Function: return CompletionItemKind.Function;
                case PythonMemberType.Method: return CompletionItemKind.Method;
                case PythonMemberType.Module: return CompletionItemKind.Module;
                case PythonMemberType.Namespace: return CompletionItemKind.Module;
                case PythonMemberType.Constant: return CompletionItemKind.Constant;
                case PythonMemberType.Event: return CompletionItemKind.Event;
                case PythonMemberType.Field: return CompletionItemKind.Field;
                case PythonMemberType.Property: return CompletionItemKind.Property;
                case PythonMemberType.Multiple: return CompletionItemKind.Value;
                case PythonMemberType.Keyword: return CompletionItemKind.Keyword;
                case PythonMemberType.CodeSnippet: return CompletionItemKind.Snippet;
                case PythonMemberType.NamedArgument: return CompletionItemKind.Variable;
                default:
                    return CompletionItemKind.None;
            }
        }

        private SymbolInformation ToSymbolInformation(MemberResult m) {
            var res = new SymbolInformation {
                name = m.Name,
                kind = ToSymbolKind(m.MemberType),
                _kind = m.MemberType.ToString().ToLowerInvariant()
            };

            var loc = m.Locations.FirstOrDefault(l => !string.IsNullOrEmpty(l.FilePath));
            if (loc != null) {
                res.location = new Location {
                    uri = new Uri(PathUtils.NormalizePath(loc.FilePath), UriKind.RelativeOrAbsolute),
                    range = new SourceSpan(
                        new SourceLocation(loc.StartLine, loc.StartColumn),
                        new SourceLocation(loc.EndLine ?? loc.StartLine, loc.EndColumn ?? loc.StartColumn)
                    )
                };
            }

            return res;
        }

        private SymbolKind ToSymbolKind(PythonMemberType memberType) {
            switch (memberType) {
                case PythonMemberType.Unknown: return SymbolKind.None;
                case PythonMemberType.Class: return SymbolKind.Class;
                case PythonMemberType.Instance: return SymbolKind.Object;
                case PythonMemberType.Delegate: return SymbolKind.Function;
                case PythonMemberType.DelegateInstance: return SymbolKind.Function;
                case PythonMemberType.Enum: return SymbolKind.Enum;
                case PythonMemberType.EnumInstance: return SymbolKind.EnumMember;
                case PythonMemberType.Function: return SymbolKind.Function;
                case PythonMemberType.Method: return SymbolKind.Method;
                case PythonMemberType.Module: return SymbolKind.Module;
                case PythonMemberType.Namespace: return SymbolKind.Namespace;
                case PythonMemberType.Constant: return SymbolKind.Constant;
                case PythonMemberType.Event: return SymbolKind.Event;
                case PythonMemberType.Field: return SymbolKind.Field;
                case PythonMemberType.Property: return SymbolKind.Property;
                case PythonMemberType.Multiple: return SymbolKind.Object;
                case PythonMemberType.Keyword: return SymbolKind.None;
                case PythonMemberType.CodeSnippet: return SymbolKind.None;
                case PythonMemberType.NamedArgument: return SymbolKind.None;
                default: return SymbolKind.None;
            }
        }

        private ReferenceKind ToReferenceKind(VariableType type) {
            switch (type) {
                case VariableType.None: return ReferenceKind.Value;
                case VariableType.Definition: return ReferenceKind.Definition;
                case VariableType.Reference: return ReferenceKind.Reference;
                case VariableType.Value: return ReferenceKind.Value;
                default: return ReferenceKind.Value;
            }
        }

        private MarkupKind SelectBestMarkup(IEnumerable<MarkupKind> requested, params MarkupKind[] supported) {
            if (requested == null) {
                return supported.Last();
            }
            foreach (var k in requested) {
                if (supported.Contains(k)) {
                    return k;
                }
            }
            return MarkupKind.PlainText;
        }

        private string FormatParameter(ParameterResult p) {
            var res = new StringBuilder(p.Name);
            if (!string.IsNullOrEmpty(p.Type)) {
                res.Append(": ");
                res.Append(p.Type);
            }
            if (!string.IsNullOrEmpty(p.DefaultValue)) {
                res.Append('=');
                res.Append(p.DefaultValue);
            }
            return res.ToString();
        }

        private SignatureInformation ToSignatureInformation(IOverloadResult overload) {
            var si = new SignatureInformation();

            if (_clientCaps?.textDocument?.signatureHelp?.signatureInformation?._shortLabel ?? false) {
                si.label = overload.Name;
            } else {
                si.label = "{0}({1})".FormatInvariant(
                    overload.Name,
                    string.Join(", ", overload.Parameters.Select(FormatParameter))
                );
            }

            si.documentation = string.IsNullOrEmpty(overload.Documentation) ? null : overload.Documentation;
            si.parameters = overload.Parameters.MaybeEnumerate().Select(p => new ParameterInformation {
                label = p.Name,
                documentation = string.IsNullOrEmpty(p.Documentation) ? null : p.Documentation,
                _type = p.Type,
                _defaultValue = p.DefaultValue
            }).ToArray();

            switch (SelectBestMarkup(_clientCaps.textDocument?.signatureHelp?.signatureInformation?.documentationFormat, MarkupKind.Markdown, MarkupKind.PlainText)) {
                case MarkupKind.Markdown:
                    var converter = new RestTextConverter();
                    if (!string.IsNullOrEmpty(si.documentation.value)) {
                        si.documentation.kind = MarkupKind.Markdown;
                        si.documentation.value = converter.ToMarkdown(si.documentation.value);
                    }
                    foreach (var p in si.parameters) {
                        if (!string.IsNullOrEmpty(p.documentation.value)) {
                            p.documentation.kind = MarkupKind.Markdown;
                            p.documentation.value = converter.ToMarkdown(p.documentation.value);
                        }
                    }
                    break;
            }

            si._returnTypes = (overload as IOverloadResult2)?.ReturnType.OrderBy(k => k).ToArray();

            return si;
        }

        private static IEnumerable<MemberResult> GetModuleVariables(
            IPythonProjectEntry entry,
            GetMemberOptions opts,
            string prefix
        ) {
            var analysis = entry?.Analysis;
            if (analysis == null) {
                yield break;
            }

            foreach (var m in analysis.GetAllAvailableMembers(SourceLocation.None, opts)) {
                if (m.Values.Any(v => v.DeclaringModule == entry)) {
                    if (string.IsNullOrEmpty(prefix) || m.Name.StartsWithOrdinal(prefix, ignoreCase: true)) {
                        yield return m;
                    }
                }
            }
        }

        private void IfTestWaitForAnalysisComplete() {
            if (_testEnvironment) {
                WaitForDirectoryScanAsync().Wait();
                WaitForCompleteAnalysisAsync().Wait();
            }
        }

        private PythonAst GetParseTree(IPythonProjectEntry entry, Uri documentUri, int msTimeout, out int? version) {
            version = null;
            PythonAst tree = null;
            var parse = entry.WaitForCurrentParse(msTimeout, CancellationToken);
            if (parse != null) {
                tree = parse.Tree ?? tree;
                if (parse.Cookie is VersionCookie vc) {
                    if (vc.Versions.TryGetValue(GetPart(documentUri), out var bv)) {
                        tree = bv.Ast ?? tree;
                        if (bv.Version >= 0) {
                            version = bv.Version;
                        }
                    }
                }
            }
            return tree;
        }

        private sealed class ReferenceComparer : IEqualityComparer<Reference> {
            public static readonly IEqualityComparer<Reference> Instance = new ReferenceComparer();

            private ReferenceComparer() { }

            public bool Equals(Reference x, Reference y) {
                return x.uri == y.uri && (SourceLocation)x.range.start == y.range.start;
            }

            public int GetHashCode(Reference obj) {
                return new { u = obj.uri, l = obj.range.start.line, c = obj.range.start.character }.GetHashCode();
            }
        }
    }
}
