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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class EditorFiles {
        private readonly ConcurrentDictionary<Uri, EditorFile> _files = new ConcurrentDictionary<Uri, EditorFile>();
        private readonly Server _server;
        private readonly SynchronizationContext _syncContext;

        public EditorFiles(Server server) {
            _server = server;
            _syncContext = new SingleThreadSynchronizationContext();
        }

        public EditorFile GetDocument(Uri uri) => _files.GetOrAdd(uri, _ => new EditorFile(_server, _syncContext));
        public void Remove(Uri uri) => _files.TryRemove(uri, out _);
        public void Open(Uri uri) => GetDocument(uri).Open(uri);
        public void Close(Uri uri) => GetDocument(uri).Close(uri);

        public void UpdateDiagnostics() {
            foreach (var entry in _server.ProjectFiles.All) {
                GetDocument(entry.DocumentUri).UpdateAnalysisDiagnostics(entry, -1);
            }
        }
    }

    internal sealed class EditorFile {
        private readonly Server _server;
        private readonly SynchronizationContext _syncContext;
        private readonly List<DidChangeTextDocumentParams> _pendingChanges = new List<DidChangeTextDocumentParams>();
        private readonly object _lock = new object();

        private readonly IDictionary<int, BufferVersion> _parseBufferDiagnostics = new Dictionary<int, BufferVersion>();
        private IEnumerable<PublishDiagnosticsEventArgs> _lastReportedParseDiagnostics;
        private IEnumerable<PublishDiagnosticsEventArgs> _lastReportedAnalysisDiagnostics;
        private bool _ignoreDiagnosticsVersion;

        public EditorFile(Server server, SynchronizationContext syncContext) {
            _server = server;
            _syncContext = syncContext;
        }

        public bool IsOpen { get; private set; }

        public void Open(Uri documentUri) {
            IsOpen = true;
            // Force update of the diagnostics if reporting of issues in closed files was turned off.
            _ignoreDiagnosticsVersion = true;

            if (_lastReportedAnalysisDiagnostics != null) {
                PublishDiagnostics(_lastReportedAnalysisDiagnostics ?? _lastReportedParseDiagnostics ?? Array.Empty<PublishDiagnosticsEventArgs>());
            }
        }

        public void Close(Uri documentUri) {
            IsOpen = false;
            _syncContext.Post(_ => HideDiagnostics(documentUri), null);
        }

        public void DidChangeTextDocument(DidChangeTextDocumentParams @params, bool enqueueForParsing) {
            var changes = @params.contentChanges;
            if (changes == null || changes.Length == 0) {
                return;
            }

            var uri = @params.textDocument.uri;
            var doc = _server.ProjectFiles.GetEntry(uri) as IDocument;
            if (doc == null) {
                return;
            }

            try {
                var part = _server.ProjectFiles.GetPart(uri);
                _server.TraceMessage($"Received changes for {uri}");

                var docVersion = Math.Max(doc.GetDocumentVersion(part), 0);
                var fromVersion = Math.Max(@params.textDocument._fromVersion ?? docVersion, 0);

                if (fromVersion > docVersion && @params.contentChanges?.Any(c => c.range == null) != true) {
                    // Expected from version hasn't been seen yet, and there are no resets in this
                    // change, so enqueue it for later.
                    _server.TraceMessage($"Deferring changes for {uri} until version {fromVersion} is seen");
                    lock (_pendingChanges) {
                        _pendingChanges.Add(@params);
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

                DidChangeTextDocumentParams? next = null;
                lock (_pendingChanges) {
                    var notExpired = _pendingChanges
                        .Where(p => p.textDocument.version.GetValueOrDefault() >= toVersion)
                        .OrderBy(p => p.textDocument.version.GetValueOrDefault())
                        .ToArray();

                    _pendingChanges.Clear();
                    if (notExpired.Any()) {
                        next = notExpired.First();
                        _pendingChanges.AddRange(notExpired.Skip(1));
                    }
                }
                if (next.HasValue) {
                    DidChangeTextDocument(next.Value, false);
                }
            } finally {
                if (enqueueForParsing) {
                    _server.TraceMessage($"Applied changes to {uri}");
                    _server.EnqueueItem(doc, enqueueForAnalysis: @params._enqueueForAnalysis ?? true);
                }
            }
        }

        public void UpdateParseDiagnostics(VersionCookie vc, Uri documentUri) {
            List<PublishDiagnosticsEventArgs> diags = null;

            lock (_lock) {
                var last = _parseBufferDiagnostics;

                foreach (var kv in vc.GetAllParts(documentUri)) {
                    var part = _server.ProjectFiles.GetPart(kv.Key);
                    if (!last.TryGetValue(part, out var lastVersion) || lastVersion.Version < kv.Value.Version || _ignoreDiagnosticsVersion) {
                        last[part] = kv.Value;
                        diags = diags ?? new List<PublishDiagnosticsEventArgs>();
                        diags.Add(new PublishDiagnosticsEventArgs {
                            uri = kv.Key,
                            diagnostics = kv.Value.Diagnostics,
                            _version = kv.Value.Version
                        });
                    }
                }
                _ignoreDiagnosticsVersion = false;
                _lastReportedParseDiagnostics = diags ?? _lastReportedParseDiagnostics;

                if (diags != null) {
                    PublishDiagnostics(diags);
                }
            }
        }

        public void UpdateAnalysisDiagnostics(IProjectEntry projectEntry, int version) {
            lock (_lock) {
                var diags = _server.Analyzer.GetDiagnostics(projectEntry);
                var settings = _server.Settings;

                for (var i = 0; i < diags.Count; i++) {
                    diags[i].severity = settings.analysis.GetEffectiveSeverity(diags[i].code, diags[i].severity);
                }

                var severity = settings.analysis.GetEffectiveSeverity(ErrorMessages.UnresolvedImportCode, DiagnosticSeverity.Warning);
                var pythonProjectEntry = projectEntry as IPythonProjectEntry;
                var parse = pythonProjectEntry?.GetCurrentParse();

                // TODO: move this to the normal analysis process
                if (parse != null && severity != DiagnosticSeverity.Unspecified) {
                    var walker = new ImportStatementWalker(parse.Tree, pythonProjectEntry, _server.Analyzer, severity);
                    parse.Tree.Walk(walker);
                    diags = diags.Concat(walker.Diagnostics).ToArray();
                }

                if (pythonProjectEntry is IDocument doc) {
                    if (_lastReportedParseDiagnostics != null) {
                        diags = diags.Concat(_lastReportedParseDiagnostics.SelectMany(d => d.diagnostics)).ToArray();
                    }
                }

                var lastPublishedVersion = _lastReportedAnalysisDiagnostics?.FirstOrDefault()?._version;
                version = version >= 0 ? version : (lastPublishedVersion.HasValue ? lastPublishedVersion.Value : 0);

                _lastReportedAnalysisDiagnostics = new[] { new PublishDiagnosticsEventArgs {
                    uri = pythonProjectEntry.DocumentUri,
                    diagnostics = diags,
                    _version = version
                }};

                PublishDiagnostics(_lastReportedAnalysisDiagnostics);
            }
        }

        private void PublishDiagnostics(IEnumerable<PublishDiagnosticsEventArgs> args) {
            _syncContext.Post(_ => {
                foreach (var a in args) {
                    if (!ShouldHideDiagnostics) {
                        _server.PublishDiagnostics(a);
                    } else {
                        HideDiagnostics(a.uri);
                    }
                }
            }, null);
        }

        private void HideDiagnostics(Uri documentUri) {
            _server.PublishDiagnostics(new PublishDiagnosticsEventArgs {
                uri = documentUri,
                diagnostics = Array.Empty<Diagnostic>()
            });
        }

        private bool ShouldHideDiagnostics => !IsOpen && _server.Settings.analysis.openFilesOnly;
    }
}
