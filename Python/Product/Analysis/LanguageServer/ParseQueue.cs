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
        public const string PythonParserSource = "Python (parser)";
        private const string TaskCommentSource = "Task comment";

        private readonly ConcurrentDictionary<Uri, ParseTask> _parsing;
        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
            _parsing = new ConcurrentDictionary<Uri, ParseTask>();
        }

        public int Count => _parsingInProgress.Count;

        public Task WaitForAllAsync() => _parsingInProgress.WaitForZeroAsync();

        public Task<IAnalysisCookie> Enqueue(IDocument doc, PythonLanguageVersion languageVersion) {
            if (doc == null) {
                throw new ArgumentNullException(nameof(doc));
            }

            var task = new ParseTask(this, doc, languageVersion);
            try {
                return _parsing.AddOrUpdate(doc.DocumentUri, task, (d, prev) => task.ContinueAfter(prev)).Start();
            } finally {
                task.DisposeIfNotStarted();
            }
        }

        private IPythonParse ParseWorker(IDocument doc, PythonLanguageVersion languageVersion) {
            IPythonParse result = null;

            if (doc is IExternalProjectEntry externalEntry) {
                using (var r = doc.ReadDocument(0, out var version)) {
                    if (r == null) {
                        throw new FileNotFoundException("failed to parse file", externalEntry.FilePath);
                    }
                    result = new StaticPythonParse(null, new VersionCookie(version));
                    externalEntry.ParseContent(r, result.Cookie);
                }
            } else if (doc is IPythonProjectEntry pyEntry) {
                var lastParse = pyEntry.GetCurrentParse();
                result = ParsePythonEntry(pyEntry, languageVersion, lastParse?.Cookie as VersionCookie);
            } else {
                Debug.Fail($"Don't know how to parse {doc.GetType().FullName}");
            }
            return result;
        }

        private IPythonParse ParsePythonEntry(IPythonProjectEntry entry, PythonLanguageVersion languageVersion, VersionCookie lastParseCookie) {
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
                // If the document is a real file, we should have been able to parse.
                if (entry.DocumentUri.IsFile) {
                    throw new FileNotFoundException("failed to parse file {0}".FormatInvariant(entry.DocumentUri.AbsoluteUri), entry.FilePath);
                }
                // Otherwise, it is likely just empty for now, so no need to cause a fuss
                return null;
            }

            var cookie = new VersionCookie(buffers);

            if (buffers.Count == 1) {
                tree = buffers.First().Value.Ast;
            } else {
                tree = new PythonAst(buffers.Values.Select(v => v.Ast));
            }

            return new StaticPythonParse(tree, cookie);
        }

        public Dictionary<string, DiagnosticSeverity> TaskCommentMap { get; set; }

        public DiagnosticSeverity InconsistentIndentation { get; set; }

        sealed class ParseTask {
            private readonly ParseQueue _queue;
            private readonly IDocument _document;
            private readonly PythonLanguageVersion _languageVersion;

            private readonly IPythonParse _parse;

            private readonly TaskCompletionSource<IAnalysisCookie> _tcs;
            private Task<IAnalysisCookie> _previous;

            // State transitions:
            //  UNSTARTED -> QUEUED     when Start() called
            //  QUEUED    -> STARTED    when worker starts running on worker thread
            //  STARTED   -> DISPOSED   when task completes
            //  UNSTARTED -> DISPOSED   when DisposeIfNotStarted() is called
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
                _parse = (_document as IPythonProjectEntry)?.BeginParse();

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

                _parse?.Dispose();
                _queue._parsingInProgress.Decrement();
            }

            public ParseTask ContinueAfter(ParseTask currentTask) {
                // Set our previous task
                Volatile.Write(ref _previous, currentTask?.Task);

                return this;
            }

            public Task<IAnalysisCookie> Start() {
                int actualState = Interlocked.CompareExchange(ref _state, QUEUED, UNSTARTED);
                if (actualState != UNSTARTED) {
                    if (actualState == DISPOSED) {
                        throw new ObjectDisposedException(GetType().FullName);
                    }
                    throw new InvalidOperationException("cannot start parsing");
                }

                // If we were pending, calling start ensures we are queued
                var previous = Interlocked.Exchange(ref _previous, null);
                if (previous != null) {
                    previous.ContinueWith(StartAfterTask);
                    return Task;
                }

                ThreadPool.QueueUserWorkItem(StartWork);
                return Task;
            }

            private void StartAfterTask(Task<IAnalysisCookie> previous) {
                ThreadPool.QueueUserWorkItem(StartWork);
            }

            private void StartWork(object state) {
                int actualState = Interlocked.CompareExchange(ref _state, STARTED, QUEUED);
                if (actualState != QUEUED) {
                    // Silently complete if we are not queued.
                    if (Interlocked.Exchange(ref _state, DISPOSED) != DISPOSED) {
                        DisposeWorker();
                    }
                    return;
                }

                try {
                    var r = _queue.ParseWorker(_document, _languageVersion);
                    if (r != null && _parse != null) {
                        _parse.Tree = r.Tree;
                        _parse.Cookie = r.Cookie;
                        _parse.Complete();
                    }
                    _tcs.SetResult(r?.Cookie);
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
                IndentationInconsistencySeverity = DiagnosticsErrorSink.GetSeverity(InconsistentIndentation),
                StubFile = entry.DocumentUri.AbsolutePath.EndsWithOrdinal(".pyi", ignoreCase: true)
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
