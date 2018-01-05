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
        private readonly VolatileCounter _parsingInProgress;

        public ParseQueue() {
            _parsingInProgress = new VolatileCounter();
        }

        public int Count => _parsingInProgress.Count;

        public Task WaitForAllAsync() => _parsingInProgress.WaitForZeroAsync();

        public sealed class ParseResult {
            public PythonAst Tree;
            public PublishDiagnosticsEventArgs Diagnostics;
        }

        private sealed class ParseState {
            public IProjectEntry Entry;
            public PythonLanguageVersion LanguageVersion;
            public TaskCompletionSource<ParseResult> Task;
        }

        public Task<ParseResult> Enqueue(IProjectEntry entry, PythonLanguageVersion languageVersion) {
            var tcs = new TaskCompletionSource<ParseResult>();
            (entry as IPythonProjectEntry)?.BeginParsingTree();
            _parsingInProgress.Increment();
            bool enqueued = false;
            try {
                enqueued = ThreadPool.QueueUserWorkItem(ParseFromDiskWorker, new ParseState {
                    Entry = entry,
                    LanguageVersion = languageVersion,
                    Task = tcs
                });
            } catch (Exception ex) {
                tcs.SetException(ex);
            } finally {
                if (!enqueued) {
                    _parsingInProgress.Decrement();
                }
            }
            return tcs.Task;
        }

        private void ParseFromDiskWorker(object state) {
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
                        externalEntry.ParseContent(r, new VersionCookie(version));
                    }
                    result = new ParseResult();
                } else if (entry is IPythonProjectEntry pyEntry) {
                    using (var r = OpenStream(entry, out var versions)) {
                        if (r == null) {
                            throw new FileNotFoundException("failed to parse file", entry.FilePath);
                        }
                        result = ParsePython(r, pyEntry, ps.LanguageVersion);
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

        private ParseResult ParsePython(Stream stream, IPythonProjectEntry entry, PythonLanguageVersion version) {
            var diags = new List<Diagnostic>();
            var opts = new ParserOptions {
                BindReferences = true,
                ErrorSink = new DiagnosticsErrorSink("Python parser", diags)
            };
            opts.ProcessComment += new DiagnosticsErrorSink("Task comment", diags).ProcessTaskComment;

            var parser = Parser.CreateParser(stream, version, opts);
            var diagnostics = diags;

            var result = new ParseResult();
            var tree = parser.ParseFile();
            entry.UpdateTree(tree, null);
            result.Tree = tree;
            if (diagnostics?.Any() ?? false) {
                result.Diagnostics = new PublishDiagnosticsEventArgs {
                    diagnostics = diagnostics,
                    uri = new Uri(entry.FilePath)
                };
            }
            return result;
        }
    }
}
