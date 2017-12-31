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
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;

namespace AnalysisTests {
    [TestClass]
    public class LanguageServerTests {
        public Task<Server> CreateServer(string rootPath, PythonVersion version) {
            return CreateServer(new Uri(rootPath), version);
        }

        public async Task<Server> CreateServer(Uri rootUri, PythonVersion version) {
            version.AssertInstalled();
            var s = new Server();
            s.OnLogMessage += Server_OnLogMessage;
            await s.Initialize(new InitializeParams {
                rootUri = rootUri,
                initializationOptions = new PythonInitializationOptions {
                    interpreter = new PythonInitializationOptions.Interpreter {
                        assembly = typeof(AstPythonInterpreterFactory).Assembly.Location,
                        typeName = typeof(AstPythonInterpreterFactory).FullName,
                        properties = version.Configuration.ToDictionary()
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
                case MessageType.Log: Trace.WriteLine(e.message); break;
            }
        }

        private TextDocumentIdentifier GetDocument(string file) {
            if (!Path.IsPathRooted(file)) {
                file = TestData.GetPath(file);
            }
            return new TextDocumentIdentifier { uri = new Uri(file) };
        }

        private static async Task AddModule(Server s, string content, Uri uri = null, string language = null) {
            await s.DidOpenTextDocument(new DidOpenTextDocumentParams {
                textDocument = new TextDocumentItem {
                    uri = uri ?? new Uri("test://test-document.py"),
                    text = content,
                    languageId = language ?? "python"
                }
            }).ConfigureAwait(false);
            await s.WaitForCompleteAnalysisAsync().ConfigureAwait(false);
        }

        [TestMethod, Priority(0)]
        public async Task LangServerInitialize() {
            var s = await CreateServer(TestData.GetPath(@"TestData\HelloWorld"), PythonPaths.Versions.LastOrDefault());

            var u = GetDocument(@"TestData\HelloWorld\Program.py").uri.AbsoluteUri;
            AssertUtil.ContainsExactly(s._projectFiles.Keys, u);
        }

        [TestMethod, Priority(0)]
        public async Task LangServerCompletions() {
            var s = await CreateServer(TestData.GetPath(@"TestData\AstAnalysis"), PythonPaths.Python36_x64 ?? PythonPaths.Python36);

            AssertUtil.CheckCollection(
                (await s.Completion(new CompletionParams {
                    textDocument = GetDocument(@"TestData\AstAnalysis\Values.py"),
                    position = new Position { line = 0, character = 0 },
                    context = new CompletionContext {
                        _statementKeywords = false,
                        _expressionKeywords = false
                    }
                })).items?.Select(c => c.insertText),
                new[] { "x", "y", "z", "pi", "int", "float" },
                new[] { "sys", "class", "def", "async", "in" }
            );

            AssertUtil.CheckCollection(
                (await s.Completion(new CompletionParams {
                    textDocument = GetDocument(@"TestData\AstAnalysis\Values.py"),
                    position = new Position { line = 0, character = 0 },
                    context = new CompletionContext {
                        _statementKeywords = true,
                        _expressionKeywords = false
                    }
                })).items?.Select(c => c.insertText),
                new[] { "x", "y", "z", "pi", "int", "float", "class", "def", "async" },
                new[] { "sys", "in" }
            );

            AssertUtil.CheckCollection(
                (await s.Completion(new CompletionParams {
                    textDocument = GetDocument(@"TestData\AstAnalysis\Values.py"),
                    position = new Position { line = 0, character = 0 }
                })).items?.Select(c => c.insertText),
                new[] { "x", "y", "z", "pi", "int", "float", "class", "def", "async", "in" },
                new[] { "sys" }
            );
        }
    }
}
