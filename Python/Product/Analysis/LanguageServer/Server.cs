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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class Server : ServerBase, IDisposable {
        internal readonly AnalysisQueue _queue;
        internal readonly ParseQueue _parseQueue;
        // Uri does not consider #fragment for equality
        private readonly ConcurrentDictionary<Uri, IProjectEntry> _projectFiles;

        private readonly ConcurrentDictionary<Uri, Dictionary<int, int>> _lastReportedDiagnostics;

        private readonly ConcurrentDictionary<Uri, List<DidChangeTextDocumentParams>> _pendingChanges;

        internal Task _loadingFromDirectory;

        internal PythonAnalyzer _analyzer;
        internal ClientCapabilities? _clientCaps;

        // If null, all files must be added manually
        private Uri _rootDir;

        public Server() {
            _queue = new AnalysisQueue();
            _parseQueue = new ParseQueue();
            _projectFiles = new ConcurrentDictionary<Uri, IProjectEntry>();
            _pendingChanges = new ConcurrentDictionary<Uri, List<DidChangeTextDocumentParams>>(UriEqualityComparer.IncludeFragment);
            _lastReportedDiagnostics = new ConcurrentDictionary<Uri, Dictionary<int, int>>();
        }

        public void Dispose() {
            _queue.Dispose();
        }

        #region Client message handling

        public async override Task<InitializeResult> Initialize(InitializeParams @params) {
            _analyzer = await CreateAnalyzer(@params.initializationOptions.interpreter);

            _clientCaps = @params.capabilities;
            var searchPaths = @params.initializationOptions.searchPaths;
            if (searchPaths != null) {
                _analyzer.SetSearchPaths(searchPaths);
            }

            if (@params.rootUri != null) {
                _rootDir = @params.rootUri;
            } else if (!string.IsNullOrEmpty(@params.rootPath)) {
                _rootDir = new Uri(PathUtils.NormalizePath(@params.rootPath));
            }

            if (_rootDir != null) {
                _loadingFromDirectory = LoadFromDirectoryAsync(_rootDir);
            }

            return new InitializeResult {
                capabilities = new ServerCapabilities {
                    completionProvider = new CompletionOptions { resolveProvider = true },
                    textDocumentSync = new TextDocumentSyncOptions { openClose = true, change = TextDocumentSyncKind.Incremental }
                }
            };
        }

        public override Task Shutdown() {
            Interlocked.Exchange(ref _analyzer, null)?.Dispose();
            _projectFiles.Clear();
            return Task.CompletedTask;
        }

        public override async Task DidOpenTextDocument(DidOpenTextDocumentParams @params) {
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

        public override async Task DidChangeTextDocument(DidChangeTextDocumentParams @params) {
            var changes = @params.contentChanges;
            if (changes == null) {
                return;
            }

            var uri = @params.textDocument.uri;
            var entry = GetEntry(uri);
            int part = GetPart(uri);
            if (entry is IDocument doc) {
                int docVersion = Math.Max(doc.GetDocumentVersion(part), 0);
                int fromVersion = Math.Max(@params.textDocument.version - 1 ?? docVersion, 0);

                List<DidChangeTextDocumentParams> pending;
                if (fromVersion > docVersion && @params.contentChanges?.Any(c => c.range == null) != true) {
                    // Expected from version hasn't been seen yet, and there are no resets in this
                    // change, so enqueue it for later.
                    pending = _pendingChanges.GetOrAdd(uri, _ => new List<DidChangeTextDocumentParams>());
                    lock (pending) {
                        pending.Add(@params);
                    }
                    return;
                }

                int toVersion = @params.textDocument.version ?? (fromVersion + changes.Length);

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
                        await DidChangeTextDocument(next.Value);
                        return;
                    }
                }

                EnqueueItem(doc);
            }

        }

        public override async Task DidCloseTextDocument(DidCloseTextDocumentParams @params) {
            var doc = GetEntry(@params.textDocument.uri) as IDocument;

            if (doc != null) {
                // No need to keep in-memory buffers now
                doc.ResetDocument(-1, null);

                // Pick up any changes on disk that we didn't know about
                EnqueueItem(doc, AnalysisPriority.Low);
            }
        }

        public override async Task DidChangeConfiguration(DidChangeConfigurationParams @params) {
            if (_analyzer == null) {
                LogMessage(new LogMessageEventArgs { type = MessageType.Error, message = "change configuration notification sent to uninitialized server" });
                return;
            }

            await _analyzer.ReloadModulesAsync();

            // re-analyze all of the modules when we get a new set of modules loaded...
            foreach (var entry in _analyzer.ModulesByFilename) {
                _queue.Enqueue(entry.Value.ProjectEntry, AnalysisPriority.Normal);
            }
        }

        public override async Task<CompletionList> Completion(CompletionParams @params) {
            GetAnalysis(@params.textDocument, @params.position, @params._version, out var entry, out var tree);
            var analysis = entry?.Analysis;
            if (analysis == null) {
                return new CompletionList { };
            }

            var opts = (GetMemberOptions)0;
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

            var latestTree = entry.WaitForCurrentTree(_clientCaps?.python?.completionsTimeout ?? -1);
            tree = latestTree ?? tree;

            IEnumerable<MemberResult> members = null;
            Expression expr = null;
            if (!string.IsNullOrEmpty(@params._expr)) {
                members = entry.Analysis.GetMembers(@params._expr, @params.position, opts);
            } else {
                var finder = new ExpressionFinder(tree, GetExpressionOptions.EvaluateMembers);
                expr = finder.GetExpression(@params.position) as Expression;
                if (expr != null) {
                    members = analysis.GetMembers(expr, @params.position, opts, null);
                } else {
                    members = entry.Analysis.GetAllAvailableMembers(@params.position, opts);
                }
            }


            if (@params.context?._includeAllModules ?? false) {
                var mods = _analyzer.GetModules();
                members = members?.Concat(mods) ?? mods;
            }

            if (members == null) {
                return new CompletionList { };
            }

            var filtered = members.Select(m => ToCompletionItem(m, opts));
            var filterKind = @params.context?._filterKind;
            if (filterKind.HasValue && filterKind != CompletionItemKind.None) {
                filtered = filtered.Where(m => m.kind == filterKind.Value);
            }

            return new CompletionList {
                items = filtered.ToArray()
            };
        }

        public override async Task<CompletionItem> CompletionItemResolve(CompletionItem item) {
            // TODO: Fill out missing values in item
            return item;
        }

        public override async Task<SymbolInformation[]> WorkplaceSymbols(WorkplaceSymbolParams @params) {
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

        internal IProjectEntry GetEntry(TextDocumentIdentifier document) => GetEntry(document.uri);

        internal IProjectEntry GetEntry(Uri documentUri, bool throwIfMissing = true) {
            if (!_projectFiles.TryGetValue(documentUri, out IProjectEntry entry) && throwIfMissing) {
                throw new LanguageServerException(LanguageServerException.UnknownDocument, "unknown document");
            }
            return entry;
        }

        internal int GetPart(Uri documentUri) {
            var f = documentUri.Fragment;
            int i;
            if (string.IsNullOrEmpty(f) || !f.StartsWith("#") || !int.TryParse(f.Substring(1), out i)) {
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
            entry.GetTreeAndCookie(out tree, out var cookie);
            if (expectedVersion.HasValue && cookie is VersionCookie vc) {
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
                    LogMessage(new LogMessageEventArgs { type = MessageType.Warning, message = ex.ToString() });
                }
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

            return await PythonAnalyzer.CreateAsync(factory, interp);
        }

        private IEnumerable<ModulePath> GetImportNames(Uri document) {
            var localRoot = GetLocalPath(_rootDir);
            var filePath = GetLocalPath(document);
            ModulePath mp;

            if (!string.IsNullOrEmpty(localRoot) && !string.IsNullOrEmpty(filePath)) {
                if (ModulePath.FromBasePathAndFile_NoThrow(localRoot, filePath, out mp)) {
                    yield return mp;
                }

                foreach (var sp in _analyzer.GetSearchPaths()) {
                    if (ModulePath.FromBasePathAndFile_NoThrow(sp, filePath, out mp)) {
                        yield return mp;
                    }
                }
            }

            if (document.Scheme == "python") {
                var path = Path.Combine(document.Host, document.AbsolutePath);
                yield return new ModulePath(Path.ChangeExtension(path, null), path, null);
            }
        }

        #endregion

        #region Non-LSP public API

        public Task<IProjectEntry> LoadFileAsync(Uri documentUri) {
            return AddFileAsync(documentUri, null);
        }

        public Task<IProjectEntry> LoadFileAsync(Uri documentUri, Uri fromSearchPath) {
            return AddFileAsync(documentUri, fromSearchPath);
        }

        public Task<bool> UnloadFileAsync(Uri documentUri) {
            var entry = RemoveEntry(documentUri);
            if (entry != null) {
                _analyzer.RemoveModule(entry);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task WaitForDirectoryScanAsync() {
            var task = _loadingFromDirectory;
            if (task == null) {
                return Task.CompletedTask;
            }
            return task;
        }

        public async Task WaitForCompleteAnalysisAsync() {
            // Wait for all current parsing to complete
            await _parseQueue.WaitForAllAsync();
            await _queue.WaitForCompleteAsync();
        }

        public int EstimateRemainingWork() {
            return _parseQueue.Count + _queue.Count;
        }

        public event EventHandler<ParseCompleteEventArgs> OnParseComplete;
        private void ParseComplete(Uri uri, int version) => OnParseComplete?.Invoke(this, new ParseCompleteEventArgs { uri = uri, version = version });

        public event EventHandler<AnalysisCompleteEventArgs> OnAnalysisComplete;
        private void AnalysisComplete(Uri uri, int version) => OnAnalysisComplete?.Invoke(this, new AnalysisCompleteEventArgs { uri = uri, version = version });

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

            int seen = 0;
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

        private async void EnqueueItem(IDocument doc, AnalysisPriority priority = AnalysisPriority.Normal) {
            try {
                // Avoid re-parsing the same version
                while (_parseQueue.TryGetExistingParseAsync(doc, out var task)) {
                    await task;
                }

                var cookie = await _parseQueue.Enqueue(doc, _analyzer.LanguageVersion).ConfigureAwait(false);
                var vc = cookie as VersionCookie;
                if (vc != null) {
                    foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                        ParseComplete(kv.Key, kv.Value.Version);
                    }
                } else {
                    ParseComplete(doc.DocumentUri, 0);
                }

                if (doc is IAnalyzable analyzable) {
                    _queue.Enqueue(analyzable, priority);
                }

                // Allow the caller to complete before publishing diagnostics
                await Task.Yield();

                if (vc != null) {
                    var reported = _lastReportedDiagnostics.GetOrAdd(doc.DocumentUri, _ => new Dictionary<int, int>());
                    lock (reported) {
                        foreach (var kv in vc.GetAllParts(doc.DocumentUri)) {
                            int part = GetPart(kv.Key), lastVersion;
                            if (!reported.TryGetValue(part, out lastVersion) || lastVersion < kv.Value.Version) {
                                reported[part] = kv.Value.Version;
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
            } catch (OperationCanceledException) {
            } catch (Exception ex) {
                LogMessage(new LogMessageEventArgs {
                    type = MessageType.Error,
                    message = ex.ToString()
                });
            }
        }

        private void ProjectEntry_OnNewAnalysis(object sender, EventArgs e) {
            if (sender is IPythonProjectEntry entry) {
                entry.GetTreeAndCookie(out _, out var cookie);
                if (cookie is VersionCookie vc) {
                    foreach (var kv in vc.GetAllParts(entry.DocumentUri)) {
                        AnalysisComplete(kv.Key, kv.Value.Version);
                    }
                } else {
                    AnalysisComplete(entry.DocumentUri, 0);
                }
            }
        }

        private async Task LoadFromDirectoryAsync(Uri rootDir) {
            foreach (var file in PathUtils.EnumerateFiles(GetLocalPath(rootDir), recurse: false, fullPaths: true)) {
                if (!ModulePath.IsPythonSourceFile(file)) {
                    if (ModulePath.IsPythonFile(file, true, true, true)) {
                        // TODO: Deal with scrapable files (if we need to do anything?)
                    }
                    continue;
                }

                await LoadFileAsync(new Uri(PathUtils.NormalizePath(file)));
            }
            foreach (var dir in PathUtils.EnumerateDirectories(GetLocalPath(rootDir), recurse: false, fullPaths: true)) {
                if (!ModulePath.PythonVersionRequiresInitPyFiles(_analyzer.LanguageVersion.ToVersion()) ||
                    !string.IsNullOrEmpty(ModulePath.GetPackageInitPy(dir))) {
                    await LoadFromDirectoryAsync(new Uri(dir));
                }
            }
        }


        private CompletionItem ToCompletionItem(MemberResult m, GetMemberOptions opts) {
            var res = new CompletionItem {
                label = m.Name,
                insertText = m.Completion,
                documentation = m.Documentation,
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

            var loc = m.Locations.FirstOrDefault();
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
                    if (string.IsNullOrEmpty(prefix) || m.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
                        yield return m;
                    }
                }
            }
        }


    }
}
