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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.LiveShare;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;
using LS = Microsoft.VisualStudio.LiveShare.LanguageServices;

namespace PythonToolsTests {
    [TestClass]
    public class LiveShareTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        private static Task<VsProjectAnalyzer> CreateAnalyzerAsync() =>
            CreateAnalyzerAsync(PythonPaths.Versions.LastOrDefault());

        private static async Task<VsProjectAnalyzer> CreateAnalyzerAsync(PythonVersion version) {
            version.AssertInstalled();
            var factory = new MockPythonInterpreterFactory(version.Configuration);
            var sp = new MockServiceProvider();
            var services = new Microsoft.PythonTools.Editor.PythonEditorServices(sp);
            var interpreters = new MockInterpreterOptionsService();
            interpreters.AddProvider(new MockPythonInterpreterFactoryProvider("Test Provider", factory));
            services.InterpreterRegistryService = interpreters;
            services.InterpreterOptionsService = interpreters;
            return await VsProjectAnalyzer.CreateForTestsAsync(
                services,
                factory
            );
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_Initialize() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var analyzer = await CreateAnalyzerAsync()) {
                var res = await cb.RequestAsync(
                    new LS.LspRequest<InitializeParams, InitializeResult>(Methods.InitializeName),
                    null,
                    null,
                    CancellationToken.None
                );

                Assert.IsNotNull(res.Capabilities.CompletionProvider);
                Assert.IsNotNull(res.Capabilities.SignatureHelpProvider);
                Assert.IsTrue(res.Capabilities.HoverProvider);
                Assert.IsTrue(res.Capabilities.DefinitionProvider);
                Assert.IsTrue(res.Capabilities.ReferencesProvider);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P0)]
        public async Task LiveShareCallback_Completion() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var analyzer = await CreateAnalyzerAsync()) {
                var t = analyzer.WaitForNextCompleteAnalysis();
                var entry = await analyzer.AnalyzeFileAsync(TestData.GetPath("TestData", "LiveShare", "module.py"));
                await t;

                cb.SetAnalyzer(entry.DocumentUri, analyzer);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<CompletionParams, CompletionList>(Methods.TextDocumentCompletionName),
                    new CompletionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                        Position = new Position { Line = 0, Character = 0 },
                        Context = new CompletionContext {
                            TriggerCharacter = "",
                            TriggerKind = CompletionTriggerKind.Invoked
                        }
                    },
                    null,
                    CancellationToken.None
                );

                AssertUtil.CheckCollection(
                    res.Items.Select(c => c.InsertText),
                    new[] { "MyClass", "my_func", "my_var", "path" },
                    new[] { "os" }
                );
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_Hover() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var analyzer = await CreateAnalyzerAsync()) {
                var t = analyzer.WaitForNextCompleteAnalysis();
                var entry = await analyzer.AnalyzeFileAsync(TestData.GetPath("TestData", "LiveShare", "module.py"));
                await t;

                cb.SetAnalyzer(entry.DocumentUri, analyzer);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Hover>(Methods.TextDocumentHoverName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                        Position = new Position { Line = 6, Character = 10 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.AreEqual(6, res.Range.Start.Line);
                Assert.AreEqual(6, res.Range.End.Line);
                Assert.AreEqual(4, res.Range.Start.Character);
                Assert.AreEqual(11, res.Range.End.Character);

                Assert.IsInstanceOfType(res.Contents, typeof(MarkupContent));
                var content = (MarkupContent)res.Contents;
                Assert.AreEqual(MarkupKind.PlainText, content.Kind);
                AssertUtil.Contains(content.Value, "my_func", "a doc string");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_Definition() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var analyzer = await CreateAnalyzerAsync()) {
                var t = analyzer.WaitForNextCompleteAnalysis();
                var entry = await analyzer.AnalyzeFileAsync(TestData.GetPath("TestData", "LiveShare", "module.py"));
                await t;

                cb.SetAnalyzer(entry.DocumentUri, analyzer);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Location[]>(Methods.TextDocumentDefinitionName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                        Position = new Position { Line = 12, Character = 6 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.AreEqual(entry.DocumentUri, res[0].Uri);
                Assert.AreEqual(2, res[0].Range.Start.Line);

                res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Location[]>(Methods.TextDocumentDefinitionName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                        Position = new Position { Line = 13, Character = 6 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.AreEqual(entry.DocumentUri, res[0].Uri);
                Assert.AreEqual(3, res[0].Range.Start.Line);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_References() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var analyzer = await CreateAnalyzerAsync()) {
                var t = analyzer.WaitForNextCompleteAnalysis();
                var entry = await analyzer.AnalyzeFileAsync(TestData.GetPath("TestData", "LiveShare", "module.py"));
                await t;

                cb.SetAnalyzer(entry.DocumentUri, analyzer);

                Location[] res = Array.Empty<Location>();
                // References sometimes need extra warmup. This needs to be fixed on the language
                // server side though, not here.
                for (int retries = 5; retries > 0; --retries) {
                    res = await cb.RequestAsync(
                        new LS.LspRequest<ReferenceParams, Location[]>(Methods.TextDocumentReferencesName),
                        new ReferenceParams {
                            TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                            Position = new Position { Line = 2, Character = 10 },
                            Context = new ReferenceContext { IncludeDeclaration = true }
                        },
                        null,
                        CancellationToken.None
                    );
                    if (res != null && res.Length > 0) {
                        break;
                    }
                    Thread.Sleep(100);
                }

                AssertUtil.ContainsExactly(res.Select(r => r.Uri), entry.DocumentUri);
                AssertUtil.ContainsAtLeast(res.Select(r => r.Range.Start.Line), 2, 12);

                res = await cb.RequestAsync(
                    new LS.LspRequest<ReferenceParams, Location[]>(Methods.TextDocumentReferencesName),
                    new ReferenceParams {
                        TextDocument = new TextDocumentIdentifier { Uri = entry.DocumentUri },
                        Position = new Position { Line = 3, Character = 10 },
                        Context = new ReferenceContext { IncludeDeclaration = true }
                    },
                    null,
                    CancellationToken.None
                );

                AssertUtil.ContainsExactly(res.Select(r => r.Uri), entry.DocumentUri);
                AssertUtil.ContainsAtLeast(res.Select(r => r.Range.Start.Line), 3, 13);
            }
        }
    }
}
