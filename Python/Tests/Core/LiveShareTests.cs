extern alias pythontools;
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.LiveShare;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Threading;
using pythontools::Microsoft.PythonTools.LanguageServerClient;
using PythonToolsTests.Mocks;
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
            PrepareLanguageServer();
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_Initialize() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var client = await CreateClientAsync()) {
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

            using (var client = await CreateClientAsync()) {
                var sourcePath = TestData.GetPath("TestData", "LiveShare", "module.py");
                var uri = await OpenDocumentAsync(client, sourcePath);
                cb.SetClient(uri, client);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<CompletionParams, CompletionList>(Methods.TextDocumentCompletionName),
                    new CompletionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
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

            using (var client = await CreateClientAsync()) {
                var sourcePath = TestData.GetPath("TestData", "LiveShare", "module.py");
                var uri = await OpenDocumentAsync(client, sourcePath);
                cb.SetClient(uri, client);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Hover>(Methods.TextDocumentHoverName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
                        Position = new Position { Line = 6, Character = 10 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.AreEqual(6, res.Range.Start.Line);
                Assert.AreEqual(6, res.Range.End.Line);
                Assert.AreEqual(4, res.Range.Start.Character);
                Assert.AreEqual(11, res.Range.End.Character);

                Assert.IsInstanceOfType(res.Contents.Value, typeof(MarkupContent));
                var content = (MarkupContent)res.Contents.Value;
                Assert.AreEqual(MarkupKind.PlainText, content.Kind);
                AssertUtil.Contains(content.Value, "my_func", "a doc string");
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_Definition() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var client = await CreateClientAsync()) {
                var sourcePath = TestData.GetPath("TestData", "LiveShare", "module.py");
                var uri = await OpenDocumentAsync(client, sourcePath);
                cb.SetClient(uri, client);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Location[]>(Methods.TextDocumentDefinitionName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
                        Position = new Position { Line = 12, Character = 6 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.IsTrue(UriEqualityComparer.Default.Equals(uri, res[0].Uri));
                Assert.AreEqual(2, res[0].Range.Start.Line);

                res = await cb.RequestAsync(
                    new LS.LspRequest<TextDocumentPositionParams, Location[]>(Methods.TextDocumentDefinitionName),
                    new TextDocumentPositionParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
                        Position = new Position { Line = 13, Character = 6 }
                    },
                    null,
                    CancellationToken.None
                );

                Assert.IsTrue(UriEqualityComparer.Default.Equals(uri, res[0].Uri));
                Assert.AreEqual(3, res[0].Range.Start.Line);
            }
        }

        [TestMethod, Priority(UnitTestPriority.P1)]
        public async Task LiveShareCallback_References() {
            var cb = PythonLanguageServiceProviderCallback.CreateTestInstance();

            using (var client = await CreateClientAsync()) {
                var sourcePath = TestData.GetPath("TestData", "LiveShare", "module.py");
                var uri = await OpenDocumentAsync(client, sourcePath);
                cb.SetClient(uri, client);

                var res = await cb.RequestAsync(
                    new LS.LspRequest<ReferenceParams, Location[]>(Methods.TextDocumentReferencesName),
                    new ReferenceParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
                        Position = new Position { Line = 2, Character = 10 },
                        Context = new ReferenceContext { IncludeDeclaration = true }
                    },
                    null,
                    CancellationToken.None
                );

                var uris = new HashSet<Uri>(res.Select(r => r.Uri));
                Assert.AreEqual(1, uris.Count);
                Assert.IsTrue(UriEqualityComparer.Default.Equals(uri, uris.First()));
                AssertUtil.ContainsAtLeast(res.Select(r => r.Range.Start.Line), 2, 12);

                res = await cb.RequestAsync(
                    new LS.LspRequest<ReferenceParams, Location[]>(Methods.TextDocumentReferencesName),
                    new ReferenceParams {
                        TextDocument = new TextDocumentIdentifier { Uri = uri },
                        Position = new Position { Line = 3, Character = 10 },
                        Context = new ReferenceContext { IncludeDeclaration = true }
                    },
                    null,
                    CancellationToken.None
                );

                uris = new HashSet<Uri>(res.Select(r => r.Uri));
                Assert.AreEqual(1, uris.Count);
                Assert.IsTrue(UriEqualityComparer.Default.Equals(uri, uris.First()));
                AssertUtil.ContainsAtLeast(res.Select(r => r.Range.Start.Line), 3, 13);
            }
        }

        private static void PrepareLanguageServer() {
            var serverFolderPath = TestData.GetTempPath();
            PythonLanguageServerDotNetCore.ExtractToFolder(serverFolderPath);

            Environment.SetEnvironmentVariable("PTVS_NODE_SERVER_ENABLED", "0");
            Environment.SetEnvironmentVariable("PTVS_DOTNETCORE_SERVER_LOCATION", serverFolderPath);
        }

        private static Task<PythonLanguageClient> CreateClientAsync() =>
            CreateClientAsync(PythonPaths.Versions.LastOrDefault());

        private static async Task<PythonLanguageClient> CreateClientAsync(PythonVersion version) {
            version.AssertInstalled();

            var contentTypeName = PythonFilePathToContentTypeProvider.GetContentTypeNameForGlobalPythonFile();
            var sp = new MockServiceProvider();

            var clientContext = new PythonLanguageClientContextFixed(
                contentTypeName,
                version.Configuration,
                null,
                Enumerable.Empty<string>()
            );

            var broker = new MockLanguageClientBroker();
            await PythonLanguageClient.EnsureLanguageClientAsync(
                sp,
                new JoinableTaskContext(),
                clientContext,
                broker
            );

            return PythonLanguageClient.FindLanguageClient(contentTypeName);
        }

        private static async Task<Uri> OpenDocumentAsync(PythonLanguageClient client, string sourcePath) {
            var uri = new Uri(sourcePath, UriKind.Absolute);
            var openDocParams = new DidOpenTextDocumentParams() {
                TextDocument = new TextDocumentItem() {
                    Uri = uri,
                    Text = File.ReadAllText(sourcePath),
                    Version = 0,
                }
            };

            await client.InvokeTextDocumentDidOpenAsync(openDocParams);

            return uri;
        }
    }
}
