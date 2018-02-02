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
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LanguageServerTests {
        public static PythonVersion DefaultV3 {
            get {
                var ver = PythonPaths.Python36_x64 ?? PythonPaths.Python36 ??
                    PythonPaths.Python35_x64 ?? PythonPaths.Python35;
                ver.AssertInstalled();
                return ver;
            }
        }

        public static PythonVersion DefaultV2 {
            get {
                var ver = PythonPaths.Python27_x64 ?? PythonPaths.Python27;
                ver.AssertInstalled();
                return ver;
            }
        }

        protected virtual PythonVersion Default => DefaultV3;

        public Task<Server> CreateServer(string rootPath, PythonVersion version = null) {
            return CreateServer(string.IsNullOrEmpty(rootPath) ? null : new Uri(rootPath), version ?? Default);
        }

        public async Task<Server> CreateServer(Uri rootUri, PythonVersion version) {
            version.AssertInstalled();
            var s = new Server();
            s.OnLogMessage += Server_OnLogMessage;
            var properties = new InterpreterFactoryCreationOptions {
                TraceLevel = System.Diagnostics.TraceLevel.Verbose,
                DatabasePath = TestData.GetTempPath($"AstAnalysisCache{version.Version}")
            }.ToDictionary();
            version.Configuration.WriteToDictionary(properties);

            await s.Initialize(new InitializeParams {
                rootUri = rootUri,
                initializationOptions = new PythonInitializationOptions {
                    interpreter = new PythonInitializationOptions.Interpreter {
                        assembly = typeof(AstPythonInterpreterFactory).Assembly.Location,
                        typeName = typeof(AstPythonInterpreterFactory).FullName,
                        properties = properties
                    }
                },
                capabilities = new ClientCapabilities {
                    python = new PythonClientCapabilities {
                        analysisUpdates = true,
                        traceLogging = true
                    }
                }
            });
            if (rootUri != null) {
                await s.WaitForDirectoryScanAsync().ConfigureAwait(false);
                await s.WaitForCompleteAnalysisAsync().ConfigureAwait(false);
            }

            return s;
        }

        private void Server_OnLogMessage(object sender, LogMessageEventArgs e) {
            switch (e.type) {
                case MessageType.Error: Trace.TraceError(e.message); break;
                case MessageType.Warning: Trace.TraceWarning(e.message); break;
                case MessageType.Info: Trace.TraceInformation(e.message); break;
                case MessageType.Log: Trace.TraceInformation("LOG: " + e.message); break;
            }
        }

        private TextDocumentIdentifier GetDocument(string file) {
            if (!Path.IsPathRooted(file)) {
                file = TestData.GetPath(file);
            }
            return new TextDocumentIdentifier { uri = new Uri(file) };
        }

        private static async Task<Uri> AddModule(Server s, string content, string moduleName = null, Uri uri = null, string language = null) {
            uri = uri ?? new Uri($"python://test/{moduleName ?? "test-module"}.py");
            await s.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri,
                    text = content,
                    languageId = language ?? "python"
                }
            }).ConfigureAwait(false);
            await s.WaitForCompleteAnalysisAsync().ConfigureAwait(false);
            return uri;
        }

        [TestMethod, Priority(0)]
        public async Task Initialize() {
            var s = await CreateServer(TestData.GetPath(@"TestData\HelloWorld"));

            var u = GetDocument(@"TestData\HelloWorld\Program.py").uri.AbsoluteUri;
            AssertUtil.ContainsExactly(s.GetLoadedFiles(), u);
        }

        [TestMethod, Priority(0)]
        public async Task OpenFile() {
            var s = await CreateServer(TestData.GetPath(@"TestData\HelloWorld"));

            var u = await AddModule(s, "a = 1", "mod");
            AssertUtil.ContainsAtLeast(s.GetLoadedFiles(), u.AbsoluteUri);

            Assert.IsTrue(await s.UnloadFileAsync(u));
            AssertUtil.DoesntContain(s.GetLoadedFiles(), u.AbsoluteUri);
        }

        [TestMethod, Priority(0)]
        public async Task ApplyChanges() {
            var s = await CreateServer(null);

            var m = await AddModule(s, "", "mod");
            Assert.AreEqual(Tuple.Create("x", 1), await ApplyChange(s, m, DocumentChange.Insert("x", new SourceLocation(1, 1))));
            Assert.AreEqual(Tuple.Create("", 2), await ApplyChange(s, m, DocumentChange.Delete(new SourceLocation(1, 1), new SourceLocation(1, 2))));
            Assert.AreEqual(Tuple.Create("y", 3), await ApplyChange(s, m, DocumentChange.Insert("y", new SourceLocation(1, 1))));
        }

        private static Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            params DocumentChange[] e
        ) {
            var initialVersion = Math.Max((s.GetEntry(document) as IDocument)?.GetDocumentVersion(s.GetPart(document)) ?? 0, 0);
            return ApplyChange(s, document, initialVersion, initialVersion + 1, e);
        }

        private static async Task<Tuple<string, int>> ApplyChange(
            Server s,
            Uri document,
            int initialVersion,
            int finalVersion,
            params DocumentChange[] e
        ) {
            var parseStart = new TaskCompletionSource<object>();
            EventHandler<ParseCompleteEventArgs> handler = null;
            handler = (sender, ev) => {
                if (ev.uri == document) {
                    parseStart.TrySetResult(null);
                    ((Server)sender).OnParseComplete -= handler;
                }
            };
            s.OnParseComplete += handler;

            await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier {
                    uri = document,
                    version = finalVersion
                },
                contentChanges = e.Select(c => new TextDocumentContentChangedEvent {
                    range = c.WholeBuffer ? null : (Range?)c.ReplacedSpan,
                    text = c.InsertedText
                }).ToArray()
            });
            await parseStart.Task;

            int newVersion = -1;
            var code = (s.GetEntry(document) as IDocument)?.ReadDocument(s.GetPart(document), out newVersion).ReadToEnd();
            return Tuple.Create(code, newVersion);
        }

        [TestMethod, Priority(0)]
        public async Task TopLevelCompletions() {
            var s = await CreateServer(TestData.GetPath(@"TestData\AstAnalysis"));

            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\Values.py"),
                context: new CompletionContext {
                    _statementKeywords = false,
                    _expressionKeywords = false
                },
                contains: new[] { "x", "y", "z", "pi", "int", "float" },
                excludes: new[] { "sys", "class", "def", "while", "in" }
            );

            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\Values.py"),
                context: new CompletionContext {
                    _statementKeywords = true,
                    _expressionKeywords = false
                },
                contains: new[] { "x", "y", "z", "pi", "int", "float", "class", "def", "while" },
                excludes: new[] { "sys", "in" }
            );

            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\Values.py"),
                new[] { "x", "y", "z", "pi", "int", "float", "class", "def", "while", "in" },
                new[] { "sys" }
            );
        }

        [TestMethod, Priority(0)]
        public async Task CompletionWithNewDot() {
            // LSP assumes that the text buffer is up to date with typing,
            // which means the language server must know about dot for a
            // dot completion.
            // To do this, we have to support using a newer tree than the
            // current analysis, so that we can quickly parse the new text
            // with the dot but not block on reanalysis.
            var s = await CreateServer(null);
            var code = @"
class MyClass:
    def f(self): pass

mc = MyClass()
mc";
            int testLine = 5;
            int testChar = 2;

            var mod = await AddModule(s, code);

            // Completion after "mc " should normally be blank
            await AssertCompletion(s, mod,
                new string[0],
                new string[0],
                position: new Position { line = testLine, character = testChar + 1 }
            );

            // While we're here, test with the special override field
            await AssertCompletion(s, mod,
                new[] { "f" },
                new[] { "abs", "bin", "int", "mc" },
                position: new Position { line = testLine, character = testChar + 1 },
                expr: "mc"
            );

            // Send the document update.
            await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 1 },
                contentChanges = new[] { new TextDocumentContentChangedEvent {
                    text = ".",
                    range = new Range {
                        start = new Position { line = testLine, character = testChar },
                        end = new Position { line = testLine, character = testChar }
                    }
                } },
                // Suppress reanalysis to avoid a race
                _enqueueForAnalysis = false
            });

            // Now with the "." event sent, we should see this as a dot completion
            await AssertCompletion(s, mod,
                new[] { "f" },
                new[] { "abs", "bin", "int", "mc" },
                position: new Position { line = testLine, character = testChar + 1 }
            );
        }

        [TestMethod, Priority(0)]
        public async Task CompletionAfterLoad() {
            var s = await CreateServer(null);
            var mod1 = await AddModule(s, "import mod2\n\nmod2.", "mod1");

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );

            var mod2 = await AddModule(s, "value = 123", "mod2");

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );

            await s.UnloadFileAsync(mod2);

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );
        }

        [TestMethod, Priority(0)]
        public async Task MultiPartDocument() {
            var s = await CreateServer(null);

            var mod = await AddModule(s, "x = 1", "mod");
            var modP2 = new Uri(mod, "#2");
            var modP3 = new Uri(mod, "#3");

            await AssertCompletion(s, mod, new[] { "x" }, Enumerable.Empty<string>());

            Assert.AreEqual(Tuple.Create("y = 2", 1), await ApplyChange(s, modP2, DocumentChange.Insert("y = 2", SourceLocation.MinValue)));
            await s.WaitForCompleteAnalysisAsync();

            await AssertCompletion(s, modP2, new[] { "x", "y" }, Enumerable.Empty<string>());

            Assert.AreEqual(Tuple.Create("z = 3", 1), await ApplyChange(s, modP3, DocumentChange.Insert("z = 3", SourceLocation.MinValue)));
            await s.WaitForCompleteAnalysisAsync();

            await AssertCompletion(s, modP3, new[] { "x", "y", "z" }, Enumerable.Empty<string>());
            await AssertCompletion(s, mod, new[] { "x", "y", "z" }, Enumerable.Empty<string>());

            await ApplyChange(s, mod, DocumentChange.Delete(SourceLocation.MinValue, SourceLocation.MinValue.AddColumns(5)));
            await s.WaitForCompleteAnalysisAsync();
            await AssertCompletion(s, modP2, new[] { "y", "z" }, new[] { "x" });
            await AssertCompletion(s, modP3, new[] { "y", "z" }, new[] { "x" });
        }

        [TestMethod, Priority(0)]
        public async Task UpdateDocumentBuffer() {
            var s = await CreateServer(null);

            var mod = await AddModule(s, "");

            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
            Assert.AreEqual(Tuple.Create("", 0), await ApplyChange(s, mod, 1, 0, new DocumentChange { WholeBuffer = true }));
            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
        }

        private static async Task<PublishDiagnosticsEventArgs> WaitForDiagnostics(Server s, int minimumVersion, Func<Task> action, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<PublishDiagnosticsEventArgs>();

            if (cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => tcs.TrySetCanceled());
            }

            EventHandler<PublishDiagnosticsEventArgs> handler = null;
            handler = (sender, pdea) => {
                if (pdea._version >= minimumVersion) {
                    tcs.TrySetResult(pdea);
                    s.OnPublishDiagnostics -= handler;
                }
            };
            s.OnPublishDiagnostics += handler;

            await action().ConfigureAwait(false);
            return await tcs.Task.ConfigureAwait(false);
        }

        [TestMethod, Priority(0)]
        public async Task ParseErrorDiagnostics() {
            var s = await CreateServer(null);

            var e = await WaitForDiagnostics(
                s,
                0,
                () => AddModule(s, "def f(/)\n    error text here\n"),
                CancellationTokens.After5s
            );

            AssertUtil.ContainsExactly(
                e.diagnostics.Select(d => $"{d.message};{d.source};{d.range.start.line};{d.range.start.character};{d.range.end.character}"),
                "unexpected token '/';Python;0;6;7",
                "invalid parameter;Python;0;6;7",
                "unexpected token '<newline>';Python;0;8;4",
                "unexpected indent;Python;1;4;9",
                "unexpected token 'text';Python;1;10;14",
                "unexpected token '<dedent>';Python;1;19;0"
            );
        }

        [TestMethod, Priority(0)]
        public async Task ParseIndentationDiagnostics() {
            var s = await CreateServer(null);

            var evts = new List<PublishDiagnosticsEventArgs>();
            s.OnPublishDiagnostics += (sender, pdea) => evts.Add(pdea);

            foreach (var tc in new[] {
                DiagnosticSeverity.Error,
                DiagnosticSeverity.Warning,
                DiagnosticSeverity.Information,
                DiagnosticSeverity.Unspecified
            }) {
                // For now, these options have to be configured directly
                s._parseQueue.InconsistentIndentation = tc;

                Trace.TraceInformation("Testing {0}", tc);

                var mod = await AddModule(s, "");
                var e = await WaitForDiagnostics(
                    s,
                    2,
                    async () => await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                        contentChanges = new[] {
                            new TextDocumentContentChangedEvent {
                                text = "def f():\r\n        pass\r\n\tpass"
                            }
                        },
                        textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 2 }
                    }),
                    CancellationTokens.After5s
                );

                Assert.AreEqual(mod, e.uri);
                var messages = e.diagnostics.Select(d => $"{d.severity};{d.message};{d.source};{d.range.start.line};{d.range.start.character};{d.range.end.character}").ToArray();
                if (tc == DiagnosticSeverity.Unspecified) {
                    AssertUtil.ContainsExactly(messages);
                } else {
                    AssertUtil.ContainsExactly(messages, $"{tc};inconsistent whitespace;Python;2;0;1");
                }

                await s.UnloadFileAsync(mod);
            }
        }

        public static async Task AssertCompletion(Server s, TextDocumentIdentifier document, IEnumerable<string> contains, IEnumerable<string> excludes, Position? position = null, CompletionContext? context = null, Func<CompletionItem, string> cmpKey = null, string expr = null) {
            cmpKey = cmpKey ?? (c => c.insertText);
            AssertUtil.CheckCollection(
                (await s.Completion(new CompletionParams {
                    textDocument = document,
                    position = position ?? new Position(),
                    context = context,
                    _expr = expr
                })).items?.Select(cmpKey),
                contains,
                excludes
            );
        }
    }

    [TestClass]
    public class LanguageServerTests_V2 : LanguageServerTests {
        protected override PythonVersion Default => DefaultV2;
    }
}
