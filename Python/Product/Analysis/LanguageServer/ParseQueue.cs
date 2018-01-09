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

        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
        }

        public int Count => _parsingInProgress.Count;

        public Task WaitForAllAsync() => _parsingInProgress.WaitForZeroAsync();

        private sealed class ParseState {
            public IProjectEntry Entry;
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

        public Task<IAnalysisCookie> Enqueue(IProjectEntry entry, PythonLanguageVersion languageVersion) {
            var tcs = new TaskCompletionSource<IAnalysisCookie>();
            (entry as IPythonProjectEntry)?.BeginParsingTree();
            _parsingInProgress.Increment();
            bool enqueued = false;
            try {
                enqueued = ThreadPool.QueueUserWorkItem(ParseWorker, new ParseState {
                    Entry = entry,
                    LanguageVersion = languageVersion,
                    Task = tcs
                });
            } catch (Exception ex) {
                tcs.SetException(ex);
            } finally {
                if (!enqueued) {
                    AbortParsingTree(entry as IPythonProjectEntry);
                    _parsingInProgress.Decrement();
                }
            }
            return tcs.Task;
        }

        private void ParseWorker(object state) {
            var ps = state as ParseState;
            var entry = ps?.Entry;
            var task = ps?.Task;
            if (entry == null) {
                Debug.Fail("invalid type passed to ParseItemWorker");
                return;
            }

            IAnalysisCookie result = null;

            try {
                var doc = entry as IDocument;
                if (doc == null) {
                    throw new NotSupportedException($"cannot parse {entry.GetType().FullName}");
                }

                if (entry is IExternalProjectEntry externalEntry) {
                    using (var r = doc.ReadDocument(0, out var version)) {
                        if (r == null) {
                            throw new FileNotFoundException("failed to parse file", entry.FilePath);
                        }
                        result = new VersionCookie(version);
                        externalEntry.ParseContent(r, result);
                    }
                } else if (entry is IPythonProjectEntry pyEntry) {
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
                            throw new FileNotFoundException($"failed to parse file {entry.DocumentUri.AbsoluteUri}", entry.FilePath);
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
            } finally {
                _parsingInProgress.Decrement();
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

        public Dictionary<string, Severity> TaskCommentMap { get; set; }

        private void ParsePython(
            Stream stream,
            IPythonProjectEntry entry,
            PythonLanguageVersion version,
            out PythonAst tree,
            out List<Diagnostic> diagnostics
        ) {
            var opts = new ParserOptions {
                BindReferences = true
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
