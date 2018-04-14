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
using System.Threading;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal abstract class HandlerBase {
        protected PythonAnalyzer Analyzer { get; }
        protected ProjectFiles ProjectFiles { get; }
        protected ClientCapabilities ClientCaps { get; }
        protected ILogger Log { get; }

        public HandlerBase(PythonAnalyzer analyzer, ProjectFiles projectFiles, ClientCapabilities clientCaps, ILogger log) {
            Analyzer = analyzer;
            ProjectFiles = projectFiles;
            Log = log;
            ClientCaps = clientCaps;
        }

        protected PythonAst GetParseTree(IPythonProjectEntry entry, Uri documentUri, CancellationToken token, out int? version) {
            version = null;
            PythonAst tree = null;
            var parse = entry.WaitForCurrentParse(ClientCaps.python?.completionsTimeout ?? Timeout.Infinite, token);
            if (parse != null) {
                tree = parse.Tree ?? tree;
                if (parse.Cookie is VersionCookie vc) {
                    if (vc.Versions.TryGetValue(ProjectFiles.GetPart(documentUri), out var bv)) {
                        tree = bv.Ast ?? tree;
                        if (bv.Version >= 0) {
                            version = bv.Version;
                        }
                    }
                }
            }
            return tree;
        }
    }
}
