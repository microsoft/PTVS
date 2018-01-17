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

        private readonly ConcurrentDictionary<IDocument, Task<IAnalysisCookie>> _parsing;

        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
            _parsing = new ConcurrentDictionary<IDocument, Task<IAnalysisCookie>>();
        }

        public int Count => _parsingInProgress.Count;

        public Task WaitForAllAsync() => _parsingInProgress.WaitForZeroAsync();

        private static void AbortParsingTree(IPythonProjectEntry entry) {
            if (entry == null) {
                return;
            }

            entry.GetTreeAndCookie(out var tree, out var cookie);
            entry.UpdateTree(tree, cookie);
        }

        public Task<IAnalysisCookie> Enqueue(IDocument doc, PythonLanguageVersion languageVersion) {
            if (doc == null) {
                throw new ArgumentNullException(nameof(doc));
            }

            (doc as IPythonProjectEntry)?.BeginParsingTree();
            _parsingInProgress.Increment();

            Task<IAnalysisCookie> result = null;
            try {
                result = _parsing.AddOrUpdate(doc,
                    _ => Parse(doc, languageVersion, null),
                    (_, prev) => Parse(doc, languageVersion, prev)
                );
                return result;
            } finally {
                if (result == null) {
                    AbortParsingTree(doc as IPythonProjectEntry);
                    _parsingInProgress.Decrement();
                }
            }
        }

        private Task<IAnalysisCookie> Parse(IDocument doc, PythonLanguageVersion languageVersion, Task<IAnalysisCookie> previousTask) {
            if (previousTask != null && previousTask.Status == TaskStatus.Running) {
                var callingThread = Thread.CurrentThread;
                var res = previousTask.ContinueWith(t => {
                    // We need to cancel immediately if we're running on the same thread
                    // This can happen if the task completes before we call ContinueWith,
                    // though the RunContinuationsAsynchronously should protect against this too
                    if (Thread.CurrentThread == callingThread) {
                        throw new OperationCanceledException();
                    }
                    return ParseWorker(doc, languageVersion);
                });
                if (!res.IsCanceled) {
                    return res;
                }
            }

            return Task.Factory.StartNew(() => ParseWorker(doc, languageVersion), TaskCreationOptions.RunContinuationsAsynchronously);
        }

        private IAnalysisCookie ParseWorker(IDocument doc, PythonLanguageVersion languageVersion) {
            IAnalysisCookie result = null;

            try {
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
                    pyEntry.GetTreeAndCookie(out _, out var lastCookie);
                    var lastVc = lastCookie as VersionCookie;
                    try {
                        var buffers = new SortedDictionary<int, BufferVersion>();
                        foreach (var part in doc.DocumentParts.Reverse()) {
                            using (var r = doc.ReadDocumentBytes(part, out var version)) {
                                if (r == null) {
                                    continue;
                                }
                                if (version >= 0 && lastVc != null && lastVc.Versions.TryGetValue(part, out var lastParse) && lastParse.Version >= version) {
                                    buffers[part] = lastParse;
                                    continue;
                                }
                                ParsePython(r, pyEntry, languageVersion, out var tree, out List<Diagnostic> diags);
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
                    Debug.Fail($"Don't know how to parse {doc.GetType().FullName}");
                }
            } finally {
                _parsingInProgress.Decrement();
            }
            return result;
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
                var diags = new List<Diagnostic>();
                diagnostics = diags;
                opts.ErrorSink = new DiagnosticsErrorSink(PythonParserSource, d => { lock (diags) diags.Add(d); });
                var tcm = TaskCommentMap;
                if (tcm != null && tcm.Any()) {
                    opts.ProcessComment += new DiagnosticsErrorSink(TaskCommentSource, d => { lock (diags) diags.Add(d); }, tcm).ProcessTaskComment;
                }
            } else {
                diagnostics = null;
            }

            var parser = Parser.CreateParser(stream, version, opts);

            tree = parser.ParseFile();
        }
    }
}
