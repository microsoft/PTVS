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

        public sealed class ParseResult {
            public PythonAst Tree;
            public int DocumentVersion;
            public PublishDiagnosticsEventArgs Diagnostics;
        }

        private sealed class ParseState {
            public IProjectEntry Entry;
            public PythonLanguageVersion LanguageVersion;
            public TaskCompletionSource<ParseResult> Task;
        }

        private static void AbortParsingTree(IPythonProjectEntry entry) {
            if (entry == null) {
                return;
            }

            entry.GetTreeAndCookie(out var tree, out var cookie);
            entry.UpdateTree(tree, cookie);
        }

        public Task<ParseResult> Enqueue(IProjectEntry entry, PythonLanguageVersion languageVersion) {
            var tcs = new TaskCompletionSource<ParseResult>();
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

            try {
                ParseResult result = null;
                if (entry is IExternalProjectEntry externalEntry) {
                    using (var r = OpenTextReader(entry, out var version)) {
                        if (r == null) {
                            throw new FileNotFoundException("failed to parse file", entry.FilePath);
                        }
                        result = new ParseResult { DocumentVersion = version };
                        externalEntry.ParseContent(r, new VersionCookie(version));
                    }
                    result = new ParseResult();
                } else if (entry is IPythonProjectEntry pyEntry) {
                    using (var r = OpenStream(entry, out var version)) {
                        try {
                            if (r == null) {
                                throw new FileNotFoundException("failed to parse file", entry.FilePath);
                            }
                            result = ParsePython(r, pyEntry, ps.LanguageVersion);
                            result.DocumentVersion = version;
                            pyEntry.UpdateTree(result.Tree, new VersionCookie(version));
                        } finally {
                            if (result == null) {
                                AbortParsingTree(pyEntry);
                            }
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

        private TextReader OpenTextReader(IProjectEntry entry, out int version) {
            if (entry is IDocument doc) {
                var r = doc.ReadDocument(out version);
            }

            if (!File.Exists(entry.FilePath)) {
                version = -1;
                return null;
            }

            var stream = PathUtils.OpenWithRetry(entry.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            version = 0;
            return new StreamReader(stream);
        }

        private Stream OpenStream(IProjectEntry entry, out int version) {
            if (entry is IDocument doc) {
                return doc.ReadDocumentBytes(out version);
            }

            if (!File.Exists(entry.FilePath)) {
                version = -1;
                return null;
            }

            version = 0;
            return PathUtils.OpenWithRetry(entry.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private static Uri GetOrCreateUri(IProjectEntry entry) {
            if (entry.Properties != null && entry.Properties.TryGetValue(typeof(Uri), out object o) && o is Uri uri) {
                return uri;
            }
            try {
                return new Uri(entry.FilePath);
            } catch (UriFormatException) {
                return null;
            }
        }

        public Dictionary<string, Severity> TaskCommentMap { get; set; }

        private ParseResult ParsePython(Stream stream, IPythonProjectEntry entry, PythonLanguageVersion version) {
            var opts = new ParserOptions {
                BindReferences = true
            };

            List<Diagnostic> diags = null;
            var u = GetOrCreateUri(entry);
            if (u != null) {
                diags = new List<Diagnostic>();
                opts.ErrorSink = new DiagnosticsErrorSink(PythonParserSource, diags);
                var tcm = TaskCommentMap;
                if (tcm != null && tcm.Any()) {
                    opts.ProcessComment += new DiagnosticsErrorSink(TaskCommentSource, diags, tcm).ProcessTaskComment;
                }
            }

            var parser = Parser.CreateParser(stream, version, opts);
            var diagnostics = diags;

            var result = new ParseResult();
            var tree = parser.ParseFile();
            result.Tree = tree;
            if (diagnostics?.Any() ?? false) {
                result.Diagnostics = new PublishDiagnosticsEventArgs {
                    diagnostics = diagnostics,
                    uri = u
                };
            }
            return result;
        }
    }
}
