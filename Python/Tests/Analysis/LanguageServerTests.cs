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

        public Task<Server> CreateServer() {
            return CreateServer((Uri)null, Default);
        }

        public Task<Server> CreateServer(string rootPath, PythonVersion version = null, Dictionary<Uri, PublishDiagnosticsEventArgs> diagnosticEvents = null) {
            return CreateServer(string.IsNullOrEmpty(rootPath) ? null : new Uri(rootPath), version ?? Default, diagnosticEvents);
        }

        public async Task<Server> CreateServer(Uri rootUri, PythonVersion version = null, Dictionary<Uri, PublishDiagnosticsEventArgs> diagnosticEvents = null) {
            version = version ?? Default;
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
                        liveLinting = true,
                        traceLogging = true
                    }
                }
            });

            if (diagnosticEvents != null) {
                s.OnPublishDiagnostics += (sender, e) => { lock (diagnosticEvents) diagnosticEvents[e.uri] = e; };
            }

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
            uri = uri ?? new Uri($"python://./{moduleName ?? "test-module"}.py");
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
            var s = await CreateServer();

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
            var s = await CreateServer();
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
                new string [0],
                new string [0],
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
                } }
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
            var s = await CreateServer();
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
        public async Task SignatureHelp() {
            var s = await CreateServer(null);
            var mod = await AddModule(s, @"f()
def f(): pass
def f(a): pass
def f(a, b): pass
def f(a, *b): pass
def f(a, **b): pass
def f(a, *b, **c): pass

");

            await AssertSignature(s, mod, new SourceLocation(1, 3),
                new string[] { "f()", "f(a:=)", "f(a:=,b:=)", "f(a:=,*b:tuple=)", "f(a:=,**b:dict=)", "f(a:=,*b:tuple=,**c:dict=)" },
                new string[0]
            );

            if (Default.Configuration.Version.Major != 3) {
                return;
            }

            await s.UnloadFileAsync(mod);

            mod = await AddModule(s, @"f()
def f(a : int): pass
def f(a : int, b: int): pass
def f(x : str, y: str): pass
def f(a = 2, b): pass

");

            await AssertSignature(s, mod, new SourceLocation(1, 3),
                new string[] { "f(a:int=)", "f(a:int=2,b:int=)", "f(x:str=,y:str=)" },
                new string[0]
            );
        }

        [TestMethod, Priority(0)]
        public async Task FindReferences() {
            var s = await CreateServer(null);
            var mod1 = await AddModule(s, @"
def f(a):
    a.real
b = 1
f(a=b)
class C:
    real = []
    f = 2
c=C()
f(a=c)
real = None", "mod1");

            // Add 10 blank lines to ensure the line numbers do not collide
            // We only check line numbers below, and by design we only get one
            // reference per location, so we disambiguate by ensuring mod2's
            // line numbers are larger than mod1's
            var mod2 = await AddModule(s, @"import mod1
" + "\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n\r\n" + @"
class D:
    real = None
    a = 1
    b = a
mod1.f(a=D)", "mod2");

            // f
            var expected = new[] {
                "Definition;(2, 5) - (2, 6)",
                "Value;(2, 5) - (3, 11)",
                "Reference;(5, 1) - (5, 2)",
                "Reference;(10, 1) - (10, 2)",
                "Reference;(17, 6) - (17, 7)"
            };
            var unexpected = new[] {
                "Definition;(8, 5) - (8, 6)",
            };
            await AssertReferences(s, mod1, SourceLocation.MinValue, expected, unexpected, "f");
            await AssertReferences(s, mod1, new SourceLocation(2, 5), expected, unexpected);
            await AssertReferences(s, mod1, new SourceLocation(5, 2), expected, unexpected);
            await AssertReferences(s, mod1, new SourceLocation(10, 2), expected, unexpected);
            await AssertReferences(s, mod2, new SourceLocation(17, 6), expected, unexpected);

            await AssertReferences(s, mod1, new SourceLocation(8, 5), unexpected, expected);

            Assert.AreEqual(new SourceSpan(2, 1, 3, 11), (await s.FindReferences(new ReferencesParams {
                textDocument = mod1,
                position = SourceLocation.MinValue,
                _expr = "f",
                context = new ReferenceContext { includeDeclaration = true, _includeDefinitionRanges = true }
            })).First(r => r._kind == ReferenceKind.Definition)._definitionRange);

            // a
            expected = new[] {
                "Definition;(2, 7) - (2, 8)",
                "Reference;(3, 5) - (3, 6)",
                "Reference;(5, 3) - (5, 4)",
                "Reference;(10, 3) - (10, 4)",
                "Reference;(17, 8) - (17, 9)"
            };
            unexpected = new[] {
                "Definition;(15, 5) - (15, 6)",
                "Reference;(16, 9) - (16, 10)"
            };
            await AssertReferences(s, mod1, new SourceLocation(3, 8), expected, unexpected, "a");
            await AssertReferences(s, mod1, new SourceLocation(2, 8), expected, unexpected);
            await AssertReferences(s, mod1, new SourceLocation(3, 5), expected, unexpected);
            await AssertReferences(s, mod1, new SourceLocation(5, 3), expected, unexpected);
            await AssertReferences(s, mod1, new SourceLocation(10, 3), expected, unexpected);
            await AssertReferences(s, mod2, new SourceLocation(17, 8), expected, unexpected);

            await AssertReferences(s, mod2, new SourceLocation(15, 5), unexpected, expected);
            await AssertReferences(s, mod2, new SourceLocation(16, 9), unexpected, expected);

            // real (in f)
            expected = new[] {
                "Reference;(3, 7) - (3, 11)",
                "Definition;(7, 5) - (7, 9)",
                "Definition;(14, 5) - (14, 9)"
            };
            unexpected = new[] {
                "Definition;(11, 1) - (11, 5)"
            };
            await AssertReferences(s, mod1, new SourceLocation(3, 5), expected, unexpected, "a.real");
            await AssertReferences(s, mod1, new SourceLocation(3, 8), expected, unexpected);

            // C.real
            expected = new[] {
                "Reference;(3, 7) - (3, 11)",
                "Definition;(7, 5) - (7, 9)"
            };
            unexpected = new[] {
                "Definition;(11, 1) - (11, 5)",
                "Definition;(14, 5) - (14, 9)"
            };
            await AssertReferences(s, mod1, new SourceLocation(7, 8), expected, unexpected);

            // D.real
            expected = new[] {
                "Reference;(3, 7) - (3, 11)",
                "Definition;(14, 5) - (14, 9)"
            };
            unexpected = new[] {
                "Definition;(7, 5) - (7, 9)",
                "Definition;(11, 1) - (11, 5)"
            };
            await AssertReferences(s, mod2, new SourceLocation(14, 8), expected, unexpected);
        }

        [TestMethod, Priority(0)]
        public async Task MultiPartDocument() {
            var s = await CreateServer();

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
            var s = await CreateServer();

            var mod = await AddModule(s, "");

            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
            Assert.AreEqual(Tuple.Create("", 0), await ApplyChange(s, mod, 1, 0, new DocumentChange { WholeBuffer = true }));
            Assert.AreEqual(Tuple.Create("test", 1), await ApplyChange(s, mod, DocumentChange.Insert("test", SourceLocation.MinValue)));
        }

        [TestMethod, Priority(0)]
        public async Task ParseErrorDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((string)null, null, diags);
            var u = await AddModule(s, "def f(/)\n    error text\n");
            await s.WaitForCompleteAnalysisAsync();

            AssertUtil.ContainsExactly(
                GetDiagnostics(diags, u),
                "Error;unexpected token '/';Python;0;6;7",
                "Error;invalid parameter;Python;0;6;7",
                "Error;unexpected token '<newline>';Python;0;8;4",
                "Error;unexpected indent;Python;1;4;9",
                "Error;unexpected token 'text';Python;1;10;14",
                "Error;unexpected token '<dedent>';Python;1;14;0"
            );
        }

        [TestMethod, Priority(0)]
        public async Task ParseIndentationDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((string)null, null, diags);

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
                await s.DidChangeTextDocument(new DidChangeTextDocumentParams {
                    contentChanges = new[] {
                            new TextDocumentContentChangedEvent {
                                text = "def f():\r\n        pass\r\n\tpass"
                            }
                        },
                    textDocument = new VersionedTextDocumentIdentifier { uri = mod, version = 2 }
                });
                await s.WaitForCompleteAnalysisAsync();

                var messages = GetDiagnostics(diags, mod).ToArray();
                if (tc == DiagnosticSeverity.Unspecified) {
                    AssertUtil.ContainsExactly(messages);
                } else {
                    AssertUtil.ContainsExactly(messages, $"{tc};inconsistent whitespace;Python;2;0;1");
                }

                await s.UnloadFileAsync(mod);
            }
        }

        [TestMethod, Priority(0)]
        public async Task ParseAndAnalysisDiagnostics() {
            var diags = new Dictionary<Uri, PublishDiagnosticsEventArgs>();
            var s = await CreateServer((Uri)null, null, diags);

            var u = await AddModule(s, "y\nx x");
            await s.WaitForCompleteAnalysisAsync();

            AssertUtil.ContainsExactly(
                GetDiagnostics(diags, u),
                "Warning;unknown variable 'y';Python;0;0;1",
                "Warning;unknown variable 'x';Python;1;0;1",
                "Error;unexpected token 'x';Python;1;2;3"
            );
        }

        private static IEnumerable<string> GetDiagnostics(Dictionary<Uri, PublishDiagnosticsEventArgs> events, Uri uri) {
            return events[uri].diagnostics
                .OrderBy(d => (SourceLocation)d.range.start)
                .Select(d => $"{d.severity};{d.message};{d.source};{d.range.start.line};{d.range.start.character};{d.range.end.character}");
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

        public static async Task AssertSignature(Server s, TextDocumentIdentifier document, SourceLocation position, IEnumerable<string> contains, IEnumerable<string> excludes, string expr = null) {
            var sigs = (await s.SignatureHelp(new TextDocumentPositionParams {
                textDocument = document,
                position = position,
                _expr = expr
            })).signatures;

            AssertUtil.CheckCollection(
                sigs.Select(sig => $"{sig.label}({string.Join(",", sig.parameters.Select(p => $"{p.label}:{p._type}={p._defaultValue}"))})"),
                contains,
                excludes
            );
        }

        public static async Task AssertReferences(Server s, TextDocumentIdentifier document, SourceLocation position, IEnumerable<string> contains, IEnumerable<string> excludes, string expr = null, bool returnDefinition = false) {
            var refs = (await s.FindReferences(new ReferencesParams {
                textDocument = document,
                position = position,
                _expr = expr,
                context = new ReferenceContext {
                    includeDeclaration = true,
                    _includeDefinitionRanges = returnDefinition,
                    _includeValues = true
                }
            }));

            IEnumerable<string> set;
            if (returnDefinition) {
                set = refs.Select(r => $"{r._kind ?? ReferenceKind.Reference};{r._definitionRange}");
            } else {
                set = refs.Select(r => $"{r._kind ?? ReferenceKind.Reference};{r.range}");
            }

            AssertUtil.CheckCollection(
                set,
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
