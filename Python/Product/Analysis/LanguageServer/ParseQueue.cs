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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    class ParseQueue {
        public const string PythonParserSource = "Python";
        private const string TaskCommentSource = "Task comment";

        private readonly ConcurrentDictionary<IDocument, TaskCompletionSource<IAnalysisCookie>> _parsing;

        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
            _parsing = new ConcurrentDictionary<IDocument, TaskCompletionSource<IAnalysisCookie>>();
        }

        public int Count => _parsingInProgress.Count;

        public Task WaitForAllAsync() => _parsingInProgress.WaitForZeroAsync();

        private sealed class ParseState {
            public IDocument Document;
            public PythonLanguageVersion LanguageVersion;
            public TaskCompletionSource<IAnalysisCookie> Task;
        }

        private static void AbortParsingTree(IPythonProjectEntry entry) {
            if (entry == null) {
                return;
            }

            entry.GetTreeAndCookie(out var tree, out var cookie);
            entry.UpdateTree(tree, cookie);
        }

        internal bool TryGetExistingParseAsync(IDocument doc, out Task<IAnalysisCookie> task) {
            if (_parsing.TryGetValue(doc, out var tcs)) {
                task = tcs.Task;
                return true;
            }
            task = null;
            return false;
        }

        public async Task<IAnalysisCookie> Enqueue(IDocument doc, PythonLanguageVersion languageVersion) {
            TaskCompletionSource<IAnalysisCookie> created = null;
            var tcs = _parsing.GetOrAdd(doc, e => { return created = new TaskCompletionSource<IAnalysisCookie>(); });
            if (created == null) {
                // Do not start parsing until the previous one has completed
                await tcs.Task;
                tcs = new TaskCompletionSource<IAnalysisCookie>();
            }

            try {
                (doc as IPythonProjectEntry)?.BeginParsingTree();
                _parsingInProgress.Increment();
                bool enqueued = false;
                try {
                    enqueued = ThreadPool.QueueUserWorkItem(ParseWorker, new ParseState {
                        Document = doc,
                        LanguageVersion = languageVersion,
                        Task = tcs
                    });
                } finally {
                    if (!enqueued) {
                        AbortParsingTree(doc as IPythonProjectEntry);
                    }
                }
                return await tcs.Task;
            } finally {
                _parsing.TryRemove(doc, out _);
                _parsingInProgress.Decrement();
            }
        }

        private void ParseWorker(object state) {
            var ps = state as ParseState;
            var doc = ps?.Document;
            var task = ps?.Task;
            if (doc == null || task == null) {
                Debug.Fail("invalid type passed to ParseItemWorker");
                return;
            }

            IAnalysisCookie result = null;

            try {
                if (doc == null) {
                    throw new NotSupportedException($"cannot parse {doc.GetType().FullName}");
                }

                if (doc is IExternalProjectEntry externalEntry) {
                    using (var r = doc.ReadDocument(0, out var version)) {
                        if (r == null) {
                            throw new FileNotFoundException("failed to parse file", externalEntry.FilePath);
                        }
                        result = new VersionCookie(version);
                        externalEntry.ParseContent(r, result);
                    }
                } else if (doc is IPythonProjectEntry pyEntry) {
                    bool complete = false;
                    try {
                        var buffers = new SortedDictionary<int, BufferVersion>();
                        foreach (var part in doc.DocumentParts) {
                            using (var r = doc.ReadDocumentBytes(part, out var version)) {
                                if (r == null) {
                                    continue;
                                }
                                ParsePython(r, pyEntry, ps.LanguageVersion, out var tree, out List<Diagnostic> diags);
                                buffers[part] = new BufferVersion(
                                    version,
                                    tree,
                                    diags.MaybeEnumerate()
                                );
                            }
                        }
                        if (!buffers.Any()) {
                            throw new FileNotFoundException($"failed to parse file {pyEntry.DocumentUri.AbsoluteUri}", pyEntry.FilePath);
                        }
                        complete = true;
                        result = UpdateTree(pyEntry, buffers);
                    } finally {
                        if (!complete) {
                            AbortParsingTree(pyEntry);
                        }
                    }
                } else {
                }
                ps.Task.TrySetResult(result);
            } catch (Exception ex) {
                task.TrySetException(ex);
            }
        }

        private IAnalysisCookie UpdateTree(IPythonProjectEntry entry, SortedDictionary<int, BufferVersion> buffers) {
            var cookie = new VersionCookie(buffers);

            if (buffers.Count == 1) {
                entry.UpdateTree(buffers.First().Value.Ast, cookie);
                return cookie;
            }

            var tree = new PythonAst(buffers.Values.Select(v => v.Ast));
            entry.UpdateTree(tree, cookie);
            return cookie;
        }

        public Dictionary<string, DiagnosticSeverity> TaskCommentMap { get; set; }

        public DiagnosticSeverity InconsistentIndentation { get; set; }

        private void ParsePython(
            Stream stream,
            IPythonProjectEntry entry,
            PythonLanguageVersion version,
            out PythonAst tree,
            out List<Diagnostic> diagnostics
        ) {
            var opts = new ParserOptions {
                BindReferences = true,
                IndentationInconsistencySeverity = DiagnosticsErrorSink.GetSeverity(InconsistentIndentation)
            };

            var u = entry.DocumentUri;
            if (u != null) {
                diagnostics = new List<Diagnostic>();
                opts.ErrorSink = new DiagnosticsErrorSink(PythonParserSource, diagnostics);
                var tcm = TaskCommentMap;
                if (tcm != null && tcm.Any()) {
                    opts.ProcessComment += new DiagnosticsErrorSink(TaskCommentSource, diagnostics, tcm).ProcessTaskComment;
                }
            } else {
                diagnostics = null;
            }

            var parser = Parser.CreateParser(stream, version, opts);

            tree = parser.ParseFile();
        }
    }
}
