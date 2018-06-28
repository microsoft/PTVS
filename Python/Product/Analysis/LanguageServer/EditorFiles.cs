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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class EditorFiles {
        private readonly ConcurrentDictionary<Uri, EditorFile> _files = new ConcurrentDictionary<Uri, EditorFile>();
        private readonly Server _server;

        public EditorFiles(Server server) {
            _server = server;
        }

        public EditorFile GetDocument(Uri uri) => _files.GetOrAdd(uri, _ => new EditorFile(_server));
        public void Remove(Uri uri) => _files.TryRemove(uri, out _);
        public void Open(Uri uri) => GetDocument(uri).Open(uri);
        public void Close(Uri uri) => GetDocument(uri).Close(uri);

        public void UpdateDiagnostics() {
            foreach (var entry in _server.ProjectFiles.All) {
                GetDocument(entry.DocumentUri).UpdateAnalysisDiagnostics(entry);
            }
        }
    }

    internal sealed class EditorFile {
        private readonly Server _server;
        private readonly List<DidChangeTextDocumentParams> _pendingChanges = new List<DidChangeTextDocumentParams>();
        private readonly object _lock = new object();

        private IDictionary<int, BufferVersion> _parseBufferDiagnostics = new Dictionary<int, BufferVersion>();
        private IDictionary<Uri, PublishDiagnosticsEventArgs> _parseDiagnostics = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
        private PublishDiagnosticsEventArgs _analysisDiagnostics;
        private PublishDiagnosticsEventArgs[] _lastPublishedDiagnostics;
        private bool _ignoreDiagnosticsVersion;


        public EditorFile(Server server) {
            _server = server;
        }

        public bool IsOpen { get; private set; }
        public void Open(Uri documentUri) {
            IsOpen = true;
            _ignoreDiagnosticsVersion = true;
            PublishDiagnostics(documentUri);
        }

        public void Close(Uri documentUri) {
            IsOpen = false;
            HideDiagnostics(documentUri);
        }

        public void DidChangeTextDocument(DidChangeTextDocumentParams @params, Action<IDocument> enqueueAction) {
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
                var fromVersion = Math.Max(@params.textDocument.version - 1 ?? docVersion, 0);

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
                    DidChangeTextDocument(next.Value, null);
                }
            } finally {
                if (enqueueAction != null) {
                    _server.TraceMessage($"Applied changes to {uri}");
                    enqueueAction(doc);
                }
            }
        }

        public void UpdateParseDiagnostics(VersionCookie vc, Uri documentUri) {
            lock (_lock) {
                var last = _parseBufferDiagnostics;
                Dictionary<Uri, PublishDiagnosticsEventArgs> newDiags = null;

                foreach (var kv in vc.GetAllParts(documentUri)) {
                    var part = _server.ProjectFiles.GetPart(kv.Key);
                    if (!last.TryGetValue(part, out var lastVersion) || lastVersion.Version < kv.Value.Version || _ignoreDiagnosticsVersion) {
                        last[part] = kv.Value;

                        newDiags = newDiags ?? new Dictionary<Uri, PublishDiagnosticsEventArgs>();
                        newDiags[kv.Key] = new PublishDiagnosticsEventArgs {
                            uri = kv.Key,
                            diagnostics = kv.Value.Diagnostics,
                            _version = kv.Value.Version
                        };
                    }
                }

                if (newDiags != null) {
                    _parseDiagnostics = newDiags;
                }
                _ignoreDiagnosticsVersion = false;

                PublishDiagnostics(documentUri);
            }
        }

        public void UpdateAnalysisDiagnostics(IProjectEntry projectEntry) {
            var diags = _server.Analyzer.GetDiagnostics(projectEntry);
            for (var i = 0; i < diags.Count; i++) {
                diags[i].severity = _server.Settings.analysis.GetEffectiveSeverity(diags[i].code, diags[i].severity);
            }

            var severity = _server.Settings.analysis.GetEffectiveSeverity(ErrorMessages.UnresolvedImportCode, DiagnosticSeverity.Warning);
            var pythonProjectEntry = projectEntry as IPythonProjectEntry;
            var parse = pythonProjectEntry?.GetCurrentParse();

            if (parse != null && _server.Settings.analysis.Show(severity)) {
                var walker = new ImportStatementWalker(parse.Tree, pythonProjectEntry, _server.Analyzer, severity);
                parse.Tree.Walk(walker);
                diags = diags.Concat(walker.Diagnostics).ToArray();
            }

            lock (_lock) {
                _analysisDiagnostics = new PublishDiagnosticsEventArgs {
                    uri = pythonProjectEntry.DocumentUri,
                    diagnostics = diags
                };
                PublishDiagnostics(projectEntry.DocumentUri);
            }
        }

        private void PublishDiagnostics(Uri documentUri) {
            if (HideDiagnostics(documentUri)) {
                return;
            }

            PublishDiagnosticsEventArgs[] diags;

            if (_analysisDiagnostics == null) {
                // No analysis diagnostics, report one from the parser
                diags = _parseDiagnostics.Values.ToArray();
            } else {
                // Both parser and analysis diagnostics exist, merge them
                // so they don't overwrite each other for the document.
                var uri = _analysisDiagnostics.uri;
                if (_parseDiagnostics.TryGetValue(uri, out var documentParseDiags)) {
                    var combined = new PublishDiagnosticsEventArgs {
                        uri = uri,
                        diagnostics = documentParseDiags.diagnostics.Concat(_analysisDiagnostics.diagnostics).ToList()
                    };
                    diags = _parseDiagnostics.Values.Except(new[] { documentParseDiags }).Concat(new[] { combined }).ToArray();
                } else {
                    diags = _parseDiagnostics.Values.Concat(new[] { _analysisDiagnostics }).ToArray();
                }
            }

            if (_lastPublishedDiagnostics == null || !_lastPublishedDiagnostics.SequenceEqual(diags, new PublishDiagComparer())) {
                _lastPublishedDiagnostics = diags;
                foreach (var d in diags) {
                    _server.PublishDiagnostics(d);
                }
            }
        }

        private void ClearDiagnostics(Uri documentUri) {
            _server.PublishDiagnostics(new PublishDiagnosticsEventArgs {
                uri = documentUri,
                diagnostics = Array.Empty<Diagnostic>()
            });
        }
        private bool HideDiagnostics(Uri documentUri) {
            if (ShouldHideDiagnostics) {
                ClearDiagnostics(documentUri);
                return true;
            }
            return false;
        }

        private bool ShouldHideDiagnostics => !IsOpen && _server.Settings.analysis.openFilesOnly;

        class PublishDiagComparer : IEqualityComparer<PublishDiagnosticsEventArgs> {
            public bool Equals(PublishDiagnosticsEventArgs x, PublishDiagnosticsEventArgs y) 
                => x.uri == y.uri && x._version == y._version && x.diagnostics.SequenceEqual(y.diagnostics, new DiagnosticsComparer());
            public int GetHashCode(PublishDiagnosticsEventArgs obj) => obj.GetHashCode();
        }
        class DiagnosticsComparer : IEqualityComparer<Diagnostic> {
            public bool Equals(Diagnostic x, Diagnostic y)
                => x.range.start.line == y.range.start.line
                && x.range.start.character == y.range.start.character
                && x.range.end.line == y.range.end.line
                && x.range.end.character == y.range.end.character
                && x.code == y.code 
                && x.message == y.message 
                && x.severity == y.severity;
            public int GetHashCode(Diagnostic obj) => obj.GetHashCode();
        }
    }
}
