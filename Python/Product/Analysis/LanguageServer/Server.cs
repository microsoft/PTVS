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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed class Server : ServerBase, IDisposable {
        internal PythonAnalyzer _analyzer;
        internal readonly AnalysisQueue _analysisQueue;
        internal readonly ProjectEntryMap _projectFiles;

        public Server() {
            _analysisQueue = new AnalysisQueue();
            _projectFiles = new ProjectEntryMap();
        }

        public void Dispose() {
            _analysisQueue.Dispose();
        }

        #region Client message handling

        public async override Task<InitializeResult> Initialize(InitializeParams @params) {
            _analyzer = await CreateAnalyzer(@params.initializationOptions.interpreter);

            return new InitializeResult {
                capabilities = new ServerCapabilities {
                    completionProvider = new CompletionOptions { resolveProvider = true },
                    textDocumentSync = new TextDocumentSyncOptions { openClose = true, change = TextDocumentSyncKind.Incremental }
                }
            };
        }

        public override Task Shutdown() {
            Interlocked.Exchange(ref _analyzer, null)?.Dispose();
            _projectFiles.Clear();
            return Task.CompletedTask;
        }

        public override async Task DidOpenTextDocument(DidOpenTextDocumentParams @params) {
            
        }

        public override async Task DidCloseTextDocument(DidCloseTextDocumentParams @params) {
            
        }

        public override async Task<CompletionList> Completion(CompletionParams @params) {
            // TODO: Get completion list items
            return new CompletionList {
            };
        }

        public override async Task<CompletionItem> CompletionItemResolve(CompletionItem item) {
            // TODO: Fill out missing values in item
            return item;
        }

        #endregion

        private async Task<PythonAnalyzer> CreateAnalyzer(PythonInitializationOptions.Interpreter interpreter) {
            IPythonInterpreterFactory factory = null;
            if (!string.IsNullOrEmpty(interpreter.assembly) && !string.IsNullOrEmpty(interpreter.typeName)) {
                try {
                    var assembly = File.Exists(interpreter.assembly) ? AssemblyName.GetAssemblyName(interpreter.assembly) : new AssemblyName(interpreter.assembly);
                    var type = Assembly.Load(assembly).GetType(interpreter.typeName, true);

                    factory = (IPythonInterpreterFactory)Activator.CreateInstance(
                        type,
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        new object[] { interpreter.properties },
                        CultureInfo.CurrentCulture
                    );
                } catch (Exception ex) {
                    LogMessage(new LogMessageEventArgs { type = MessageType.Warning, message = ex.ToString() });
                }
            }

            if (factory == null) {
                Version v;
                if (!Version.TryParse(interpreter.version ?? "0.0", out v)) {
                    v = new Version();
                }
                factory = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(v);
            }

            var interp = factory.CreateInterpreter();
            if (interp == null) {
                throw new InvalidOperationException("Failed to create interpreter");
            }

            return await PythonAnalyzer.CreateAsync(factory, interp);
        }
    }
}
