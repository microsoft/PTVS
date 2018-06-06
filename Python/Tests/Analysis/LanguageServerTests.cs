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
using Microsoft.PythonTools.Analysis.LanguageServer.Extensibility;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LanguageServerTests {
        [TestInitialize]
        public void TestInitialize() => TestEnvironmentImpl.TestInitialize();

        [TestCleanup]
        public void TestCleanup() => TestEnvironmentImpl.TestCleanup();

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
        protected virtual BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Unicode;

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
                    },
                    asyncStartup = false,
                    testEnvironment = true
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

            s.DidChangeTextDocument(new DidChangeTextDocumentParams {
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
                GetDocument(@"TestData\AstAnalysis\TopLevelCompletions.py"),
                new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in" },
                new[] { "return", "sys", "yield" }
            );

            // Completions in function body
            await AssertCompletion(
                s,
                GetDocument(@"TestData\AstAnalysis\TopLevelCompletions.py"),
                new[] { "x", "y", "z", "int", "float", "class", "def", "while", "in", "return", "yield" },
                new[] { "sys" },
                position: new Position { line = 5, character = 5 }
            );
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInFunctionDefinition() {
            var s = await CreateServer();
            var u = await AddModule(s, "def f(a, b:int, c=2, d:float=None): pass");

            await AssertNoCompletion(s, u, new SourceLocation(1, 5));
            await AssertNoCompletion(s, u, new SourceLocation(1, 7));
            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            await AssertNoCompletion(s, u, new SourceLocation(1, 10));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 14));
            await AssertNoCompletion(s, u, new SourceLocation(1, 17));
            await AssertNoCompletion(s, u, new SourceLocation(1, 19));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 29));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 34));
            await AssertNoCompletion(s, u, new SourceLocation(1, 35));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 36));

            u = await AddModule(s, "@dec\nasync   def  f(): pass");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 1));
            await AssertCompletion(s, u, new[] { "abs" }, new[] { "def" }, new SourceLocation(1, 2));
            await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 1));
            await AssertCompletion(s, u, new[] { "def" }, new string[0], new SourceLocation(2, 12));
            await AssertNoCompletion(s, u, new SourceLocation(2, 13));
            await AssertNoCompletion(s, u, new SourceLocation(2, 14));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInClassDefinition() {
            var s = await CreateServer();
            var u = await AddModule(s, "class C(object, parameter=MC): pass");

            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            if (this is LanguageServerTests_V2) {
                await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 9));
            } else {
                await AssertCompletion(s, u, new[] { "metaclass=", "object" }, new string[0], new SourceLocation(1, 9));
            }
            await AssertAnyCompletion(s, u, new SourceLocation(1, 15));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 17));
            await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 29));
            await AssertNoCompletion(s, u, new SourceLocation(1, 30));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 31));

            u = await AddModule(s, "class D(o");
            await AssertNoCompletion(s, u, new SourceLocation(1, 8));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 9));

            u = await AddModule(s, "class E(metaclass=MC,o): pass");
            await AssertCompletion(s, u, new[] { "object" }, new[] { "metaclass=" }, new SourceLocation(1, 22));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInStatements() {
            var s = await CreateServer();
            var u = await AddModule(s, "for f in l: pass\nwith x as y: pass");

            await AssertNoCompletion(s, u, new SourceLocation(1, 5));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 10));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 12));
            await AssertAnyCompletion(s, u, new SourceLocation(2, 6));
            await AssertNoCompletion(s, u, new SourceLocation(2, 11));
            await AssertAnyCompletion(s, u, new SourceLocation(2, 13));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInImport() {
            var s = await CreateServer();
            var u = await AddModule(s, "import unittest.case as C, unittest\nfrom unittest.case import TestCase as TC, TestCase");

            await AssertCompletion(s, u, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(1, 7));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 8));
            await AssertCompletion(s, u, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 17));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(1, 22));
            await AssertNoCompletion(s, u, new SourceLocation(1, 25));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(1, 28));

            await AssertCompletion(s, u, new[] { "from", "import", "abs", "dir" }, new[] { "abc" }, new SourceLocation(2, 5));
            await AssertCompletion(s, u, new[] { "abc", "unittest" }, new[] { "abs", "dir" }, new SourceLocation(2, 6));
            await AssertCompletion(s, u, new[] { "case" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 15));
            await AssertCompletion(s, u, new[] { "import" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 20));
            await AssertCompletion(s, u, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 27));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "abc", "unittest", "abs", "dir" }, new SourceLocation(2, 36));
            await AssertNoCompletion(s, u, new SourceLocation(2, 39));
            await AssertCompletion(s, u, new[] { "TestCase" }, new[] { "abs", "dir", "case" }, new SourceLocation(2, 44));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionForOverride() {
            var s = await CreateServer();
            var u = await AddModule(s, "class A(object):\n    def i(): pass\n    def \npass");

            await AssertNoCompletion(s, u, new SourceLocation(2, 9));
            await AssertCompletion(s, u, new[] { "def" }, new[] { "__init__" }, new SourceLocation(3, 8));
            await AssertCompletion(s, u, new[] { "__init__" }, new[] { "def" }, new SourceLocation(3, 9), cmpKey: ci => ci.label);
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInDecorator() {
            var s = await CreateServer();
            var u = await AddModule(s, "@dec\ndef f(): pass\n\nx = a @ b");

            await AssertCompletion(s, u, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(1, 2));
            await AssertCompletion(s, u, new[] { "f", "x", "property", "abs" }, new[] { "def" }, new SourceLocation(4, 8));

            u = await AddModule(s, "@");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 2));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInRaise() {
            var s = await CreateServer();
            var u = await AddModule(s, "raise ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));

            if (!(this is LanguageServerTests_V2)) {
                u = await AddModule(s, "raise Exception from ");
                await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(1, 7));
                await AssertCompletion(s, u, new[] { "from" }, new[] { "Exception", "def", "abs" }, new SourceLocation(1, 17));
                await AssertAnyCompletion(s, u, new SourceLocation(1, 22));
            }

            u = await AddModule(s, "raise Exception, x, y");
            await AssertAnyCompletion(s, u, new SourceLocation(1, 17));
            await AssertAnyCompletion(s, u, new SourceLocation(1, 20));
        }

        [TestMethod, Priority(0)]
        public async Task CompletionInExcept() {
            var s = await CreateServer();
            var u = await AddModule(s, "try:\n    pass\nexcept ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));

            u = await AddModule(s, "try:\n    pass\nexcept (");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 9));

            u = await AddModule(s, "try:\n    pass\nexcept Exception  as ");
            await AssertCompletion(s, u, new[] { "Exception", "ValueError" }, new[] { "def", "abs" }, new SourceLocation(3, 8));
            await AssertCompletion(s, u, new[] { "as" }, new[] { "Exception", "def", "abs" }, new SourceLocation(3, 18));
            await AssertNoCompletion(s, u, new SourceLocation(3, 22));
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
mc
";
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
            s.DidChangeTextDocument(new DidChangeTextDocumentParams {
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
                contains: new[] { "value" },
                excludes: new string[0]
            );

            await s.UnloadFileAsync(mod2);
            await s.WaitForCompleteAnalysisAsync();

            await AssertCompletion(s, mod1,
                position: new Position { line = 2, character = 5 },
                contains: new string[0],
                excludes: new[] { "value" }
            );
        }

        public class TestCompletionHook : ILanguageServerExtension {
            public TestCompletionHook() { }
            public string Name => null;
            public IReadOnlyDictionary<string, object> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties) => null;
            public void Register(Server server) => server.PostProcessCompletion += Server_PostProcessCompletion;
            private void Server_PostProcessCompletion(object sender, CompletionEventArgs e) {
                Assert.IsNotNull(e.Tree);
                Assert.IsNotNull(e.Analysis);
                for (int i = 0; i < e.CompletionList.items.Length; ++i) {
                    e.CompletionList.items[i].insertText = "*" + e.CompletionList.items[i].insertText;
                }
            }
        }

        [TestMethod, Priority(0)]
        public async Task CompletionHook() {
            var s = await CreateServer();
            var u = await AddModule(s, "x = 123\nx.");

            await AssertCompletion(s, u, new[] { "real", "imag" }, new string[0], new Position { line = 1, character = 2 });

            await s.LoadExtension(new PythonAnalysisExtensionParams {
                assembly = typeof(TestCompletionHook).Assembly.FullName,
                typeName = typeof(TestCompletionHook).FullName
            });

            await AssertCompletion(s, u, new[] { "*real", "*imag" }, new[] { "real" }, new Position { line = 1, character = 2 });
        }

        [TestMethod, Priority(0)]
        public async Task SignatureHelp() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"f()
def f(): pass
def f(a): pass
def f(a, b): pass
def f(a, *b): pass
def f(a, **b): pass
def f(a, *b, **c): pass

");

            await AssertSignature(s, mod, new SourceLocation(1, 3),
                new string[] { "f()", "f(a)", "f(a, b)", "f(a, *b: tuple)", "f(a, **b: dict)", "f(a, *b: tuple, **c: dict)" },
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
                new string[] { "f(a: int)", "f(a: int=2, b: int)", "f(x: str, y: str)" },
                new string[0]
            );
        }

        [TestMethod, Priority(0)]
        public async Task FindReferences() {
            var s = await CreateServer();
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
                "Value;(2, 1) - (3, 11)",
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
        public async Task Hover() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"123
'abc'
f()
def f(): pass

class C:
    def f(self):
        def g(self):
            pass
        return g

C.f
c = C()
c_g = c.f()

x = 123
x = 3.14
");

            await AssertHover(s, mod, new SourceLocation(1, 1), "int", new[] { "int" }, new SourceSpan(1, 1, 1, 4));
            await AssertHover(s, mod, new SourceLocation(2, 1), "str", new[] { "str" }, new SourceSpan(2, 1, 2, 6));
            await AssertHover(s, mod, new SourceLocation(3, 1), "built-in function test-module.f()", new[] { "test-module.f" }, new SourceSpan(3, 1, 3, 2));
            await AssertHover(s, mod, new SourceLocation(4, 6), "built-in function test-module.f()", new[] { "test-module.f" }, new SourceSpan(4, 5, 4, 6));

            await AssertHover(s, mod, new SourceLocation(12, 1), "class test-module.C", new[] { "test-module.C" }, new SourceSpan(12, 1, 12, 2));
            await AssertHover(s, mod, new SourceLocation(13, 1), "c: C", new[] { "test-module.C" }, new SourceSpan(13, 1, 13, 2));
            await AssertHover(s, mod, new SourceLocation(14, 7), "c: C", new[] { "test-module.C" }, new SourceSpan(14, 7, 14, 8));
            await AssertHover(s, mod, new SourceLocation(14, 9), "c.f: method f of test-module.C objects*", new[] { "test-module.C.f" }, new SourceSpan(14, 7, 14, 10));
            await AssertHover(s, mod, new SourceLocation(14, 1), $"built-in function test-module.C.f.g(self)  {Environment.NewLine}declared in C.f", new[] { "test-module.C.f.g" }, new SourceSpan(14, 1, 14, 4));

            await AssertHover(s, mod, new SourceLocation(16, 1), "x: int, float", new[] { "int", "float" }, new SourceSpan(16, 1, 16, 2));
        }

        [TestMethod, Priority(0)]
        public async Task HoverSpanCheck() {
            var s = await CreateServer();
            var mod = await AddModule(s, @"import datetime
datetime.datetime.now().day
");

            await AssertHover(s, mod, new SourceLocation(2, 1), "built-in module datetime*", new[] { "datetime" }, new SourceSpan(2, 1, 2, 9));
            if (this is LanguageServerTests_V2) {
                await AssertHover(s, mod, new SourceLocation(2, 11), "class datetime.datetime*", new[] { "datetime.datetime" }, new SourceSpan(2, 1, 2, 18));
            } else {
                await AssertHover(s, mod, new SourceLocation(2, 11), "datetime.datetime:*", new[] { "datetime", "datetime.datetime" }, new SourceSpan(2, 1, 2, 18));
            }
            await AssertHover(s, mod, new SourceLocation(2, 20), "datetime.datetime.now: bound built-in method now*", null, new SourceSpan(2, 1, 2, 22));

            if (!(this is LanguageServerTests_V2)) {
                await AssertHover(s, mod, new SourceLocation(2, 28), "datetime.datetime.now().day: int*", new[] { "int" }, new SourceSpan(2, 1, 2, 28));
            }
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
                s.DidChangeTextDocument(new DidChangeTextDocumentParams {
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

        private class GetAllExtension : ILanguageServerExtension {
            private BuiltinTypeId _typeId;
            private Server _server;

            public GetAllExtension(IReadOnlyDictionary<string, object> properties) {
                if (!Enum.TryParse((string)properties["typeid"], out _typeId)) {
                    throw new ArgumentException("typeid was not valid");
                }
            }

            public void Register(Server server) => _server = server;

            public string Name => "getall";

            public IReadOnlyDictionary<string, object> ExecuteCommand(string command, IReadOnlyDictionary<string, object> properties) {
                if (properties == null) {
                    return null;
                }

                // Very bad code, but good for testing. Copy/paste at your own risk!
                var entry = _server.GetEntry(new Uri((string)properties["uri"])) as IPythonProjectEntry;
                var location = new SourceLocation((int)properties["line"], (int)properties["column"]);

                if (command == _typeId.ToString()) {
                    var res = new List<string>();
                    foreach (var m in entry.Analysis.GetAllAvailableMembers(location)) {
                        if (m.Values.Any(v => v.MemberType == PythonMemberType.Constant && v.TypeId == _typeId)) {
                            res.Add(m.Name);
                        }
                    }
                    return new Dictionary<string, object> { ["names"] = res };
                }
                return null;
            }
        }

        [TestMethod, Priority(0)]
        public async Task ExtensionCommand() {
            var s = await CreateServer();
            var u = await AddModule(s, "x = 1\ny = 2\nz = 'abc'");

            await s.LoadExtension(new PythonAnalysisExtensionParams {
                assembly = typeof(GetAllExtension).Assembly.FullName,
                typeName = typeof(GetAllExtension).FullName,
                properties = new Dictionary<string, object> { ["typeid"] = BuiltinTypeId.Int.ToString() }
            });

            List<string> res;
            var cmd = new ExtensionCommandParams {
                extensionName = "getall",
                command = "Int",
                properties = new Dictionary<string, object> { ["uri"] = u.AbsoluteUri, ["line"] = 1, ["column"] = 1 }
            };

            res = (await s.ExtensionCommand(cmd)).properties?["names"] as List<string>;
            Assert.IsNotNull(res);
            AssertUtil.ContainsExactly(res, "x", "y");
            cmd.command = BuiltinTypeId_Str.ToString();
            res = (await s.ExtensionCommand(cmd)).properties?["names"] as List<string>;
            Assert.IsNull(res);

            await s.LoadExtension(new PythonAnalysisExtensionParams {
                assembly = typeof(GetAllExtension).Assembly.FullName,
                typeName = typeof(GetAllExtension).FullName,
                properties = new Dictionary<string, object> { ["typeid"] = BuiltinTypeId_Str.ToString() }
            });

            cmd.command = BuiltinTypeId_Str.ToString();
            res = (await s.ExtensionCommand(cmd)).properties?["names"] as List<string>;
            Assert.IsNotNull(res);
            AssertUtil.ContainsAtLeast(res, "z", "__name__", "__file__");
            cmd.command = "Int";
            res = (await s.ExtensionCommand(cmd)).properties?["names"] as List<string>;
            Assert.IsNull(res);
        }


        private static IEnumerable<string> GetDiagnostics(Dictionary<Uri, PublishDiagnosticsEventArgs> events, Uri uri) {
            return events[uri].diagnostics
                .OrderBy(d => (SourceLocation)d.range.start)
                .Select(d => $"{d.severity};{d.message};{d.source};{d.range.start.line};{d.range.start.character};{d.range.end.character}");
        }

        public static async Task AssertCompletion(Server s, TextDocumentIdentifier document, IEnumerable<string> contains, IEnumerable<string> excludes, Position? position = null, CompletionContext? context = null, Func<CompletionItem, string> cmpKey = null, string expr = null) {
            var res = await s.Completion(new CompletionParams {
                textDocument = document,
                position = position ?? new Position(),
                context = context,
                _expr = expr
            });
            DumpDetails(res);

            cmpKey = cmpKey ?? (c => c.insertText);
            AssertUtil.CheckCollection(
                res.items?.Select(cmpKey),
                contains,
                excludes
            );
        }

        private static void DumpDetails(CompletionList completions) {
            var span = ((SourceSpan?)completions._applicableSpan) ?? SourceSpan.None;
            Debug.WriteLine($"Completed {completions._expr ?? "(null)"} at {span}");
        }

        private static async Task AssertAnyCompletion(Server s, TextDocumentIdentifier document, Position position) {
            var res = await s.Completion(new CompletionParams { textDocument = document, position = position });
            DumpDetails(res);
            if (res.items == null || !res.items.Any()) {
                Assert.Fail("Completions were not returned");
            }
        }

        private static async Task AssertNoCompletion(Server s, TextDocumentIdentifier document, Position position) {
            var res = await s.Completion(new CompletionParams { textDocument = document, position = position });
            DumpDetails(res);
            if (res.items != null && res.items.Any()) {
                var msg = string.Join(", ", res.items.Select(c => c.label).Ordered());
                Assert.Fail("Completions were returned: " + msg);
            }
        }

        public static async Task AssertSignature(Server s, TextDocumentIdentifier document, SourceLocation position, IEnumerable<string> contains, IEnumerable<string> excludes, string expr = null) {
            var sigs = (await s.SignatureHelp(new TextDocumentPositionParams {
                textDocument = document,
                position = position,
                _expr = expr
            })).signatures;

            AssertUtil.CheckCollection(
                sigs.Select(sig => sig.label),
                contains,
                excludes
            );
        }

        public static async Task AssertHover(Server s, TextDocumentIdentifier document, SourceLocation position, string hoverText, IEnumerable<string> typeNames, SourceSpan? range = null, string expr = null) {
            var hover = await s.Hover(new TextDocumentPositionParams {
                textDocument = document,
                position = position,
                _expr = expr
            });

            if (hoverText.EndsWith("*")) {
                // Check prefix first, but then show usual message for mismatched value
                if (!hover.contents.value.StartsWith(hoverText.Remove(hoverText.Length - 1))) {
                    Assert.AreEqual(hoverText, hover.contents.value);
                }
            } else {
                Assert.AreEqual(hoverText, hover.contents.value);
            }
            if (typeNames != null) {
                AssertUtil.ContainsExactly(hover._typeNames, typeNames.ToArray());
            }
            if (range.HasValue) {
                Assert.AreEqual(range.Value, (SourceSpan)hover.range);
            }
        }

        public static async Task AssertReferences(Server s, TextDocumentIdentifier document, SourceLocation position, IEnumerable<string> contains, IEnumerable<string> excludes, string expr = null) {
            var refs = (await s.FindReferences(new ReferencesParams {
                textDocument = document,
                position = position,
                _expr = expr,
                context = new ReferenceContext {
                    includeDeclaration = true,
                    _includeValues = true
                }
            }));

            var set = refs.Select(r => $"{r._kind ?? ReferenceKind.Reference};{r.range}");

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
        protected override BuiltinTypeId BuiltinTypeId_Str => BuiltinTypeId.Bytes;

    }
}
