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

        private readonly ConcurrentDictionary<IDocument, ParseTask> _parsing;
        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
            _parsing = new ConcurrentDictionary<IDocument, ParseTask>();
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

            var task = new ParseTask(this, doc, languageVersion);
            try {
                var c = _parsing.AddOrUpdate(doc, task, (d, prev) => prev?.ContinueWith(task) ?? task);
                if (c == task) {
                    task.Start();
                }
                return task.Task;
            } finally {
                task.DisposeIfNotStarted();
            }
        }

        private IAnalysisCookie ParseWorker(IDocument doc, PythonLanguageVersion languageVersion) {
            IAnalysisCookie result = null;

            if (doc is IExternalProjectEntry externalEntry) {
                using (var r = doc.ReadDocument(0, out var version)) {
                    if (r == null) {
                        throw new FileNotFoundException("failed to parse file", externalEntry.FilePath);
                    }
                    result = new VersionCookie(version);
                    externalEntry.ParseContent(r, result);
                }
            } else if (doc is IPythonProjectEntry pyEntry) {
                pyEntry.GetTreeAndCookie(out _, out var lastCookie);
                var lastVc = lastCookie as VersionCookie;
                result = ParsePythonEntry(pyEntry, languageVersion, lastCookie as VersionCookie);
            } else {
                Debug.Fail($"Don't know how to parse {doc.GetType().FullName}");
            }
            return result;
        }

        private IAnalysisCookie ParsePythonEntry(IPythonProjectEntry entry, PythonLanguageVersion languageVersion, VersionCookie lastParseCookie) {
            PythonAst tree;
            var doc = (IDocument)entry;
            var buffers = new SortedDictionary<int, BufferVersion>();
            foreach (var part in doc.DocumentParts.Reverse()) {
                using (var r = doc.ReadDocumentBytes(part, out int version)) {
                    if (r == null) {
                        continue;
                    }

                    if (version >= 0 && lastParseCookie != null && lastParseCookie.Versions.TryGetValue(part, out var lastParse) && lastParse.Version >= version) {
                        buffers[part] = lastParse;
                        continue;
                    }

                    buffers[part] = ParsePython(r, entry, languageVersion, version);
                }
            }

            if (!buffers.Any()) {
                throw new FileNotFoundException($"failed to parse file {entry.DocumentUri.AbsoluteUri}", entry.FilePath);
            }

            var cookie = new VersionCookie(buffers);

            if (buffers.Count == 1) {
                tree = buffers.First().Value.Ast;
            } else {
                tree = new PythonAst(buffers.Values.Select(v => v.Ast));
            }

            entry.UpdateTree(tree, cookie);
            return cookie;
        }

        public Dictionary<string, DiagnosticSeverity> TaskCommentMap { get; set; }

        public DiagnosticSeverity InconsistentIndentation { get; set; }

        sealed class ParseTask {
            private readonly ParseQueue _queue;
            private readonly IDocument _document;
            private readonly PythonLanguageVersion _languageVersion;

            private readonly TaskCompletionSource<IAnalysisCookie> _tcs;
            private ParseTask _next;

            // State transitions:
            //  UNSTARTED -> QUEUED     when passed to ContinueWith
            //  UNSTARTED -> DISPOSED   when DisposeIfNotStarted() is called
            //  UNSTARTED -> STARTED    when Start() is called
            //  QUEUED    -> STARTED    when Start(QUEUED) is called
            //  STARTED   -> DISPOSED   when task completes
            // Note that calling Dispose() only has an effect on a task that
            // has not been started or queued.
            private const int UNSTARTED = 0;
            private const int QUEUED = 1;
            private const int STARTED = 2;
            private const int DISPOSED = 3;
            private int _state = UNSTARTED;

            public ParseTask(ParseQueue queue, IDocument document, PythonLanguageVersion languageVersion) {
                _queue = queue;
                _document = document;
                _languageVersion = languageVersion;

                _queue._parsingInProgress.Increment();
                (_document as IPythonProjectEntry)?.BeginParsingTree();

                _tcs = new TaskCompletionSource<IAnalysisCookie>();
            }

            public Task<IAnalysisCookie> Task => _tcs.Task;

            public void DisposeIfNotStarted() {
                if (Interlocked.CompareExchange(ref _state, DISPOSED, UNSTARTED) == UNSTARTED) {
                    DisposeWorker();
                }
            }

            private void DisposeWorker() {
                Debug.Assert(Volatile.Read(ref _state) == DISPOSED);

                var next = Interlocked.Exchange(ref _next, null);
                _queue._parsingInProgress.Decrement();
                _queue._parsing.TryUpdate(_document, next, this);
                next?.Start(QUEUED);
            }

            public ParseTask ContinueWith(ParseTask nextTask) {
                // Set our subsequent task
                var actualNext = Interlocked.CompareExchange(ref _next, nextTask, null);
                if (actualNext != null) {
                    // Already set, so pass the new task along
                    return actualNext.ContinueWith(nextTask);
                }

                // Set the subsequent to QUEUED
                if (Interlocked.CompareExchange(ref nextTask._state, QUEUED, UNSTARTED) != UNSTARTED) {
                    Interlocked.Exchange(ref _next, null);
                    throw new InvalidOperationException("cannot queue task that has been started");
                }

                // We were complete, or completed while adding the task,
                // so make sure it runs
                if (Volatile.Read(ref _state) == DISPOSED) {
                    actualNext = Interlocked.Exchange(ref _next, null);
                    if (actualNext != null) {
                        return actualNext;
                    }
                }
                return this;
            }

            public void Start() => Start(UNSTARTED);

            private void Start(int expectedState) {
                if (Interlocked.CompareExchange(ref _state, STARTED, expectedState) != expectedState) {
                    throw new InvalidOperationException("cannot start parsing");
                }

                try {
                    _tcs.SetResult(_queue.ParseWorker(_document, _languageVersion));
                } catch (Exception ex) {
                    _tcs.SetException(ex);
                } finally {
                    if (Interlocked.CompareExchange(ref _state, DISPOSED, STARTED) == STARTED) {
                        DisposeWorker();
                    }
                }
            }
        }


        private BufferVersion ParsePython(
            Stream stream,
            IPythonProjectEntry entry,
            PythonLanguageVersion languageVersion,
            int version
        ) {
            var opts = new ParserOptions {
                BindReferences = true,
                IndentationInconsistencySeverity = DiagnosticsErrorSink.GetSeverity(InconsistentIndentation)
            };

            List<Diagnostic> diags = null;

            if (entry.DocumentUri != null) {
                diags = new List<Diagnostic>();
                opts.ErrorSink = new DiagnosticsErrorSink(PythonParserSource, d => { lock (diags) diags.Add(d); });
                var tcm = TaskCommentMap;
                if (tcm != null && tcm.Any()) {
                    opts.ProcessComment += new DiagnosticsErrorSink(TaskCommentSource, d => { lock (diags) diags.Add(d); }, tcm).ProcessTaskComment;
                }
            }

            var parser = Parser.CreateParser(stream, languageVersion, opts);
            var tree = parser.ParseFile();

            return new BufferVersion(version, tree, diags.MaybeEnumerate());
        }
    }
}
