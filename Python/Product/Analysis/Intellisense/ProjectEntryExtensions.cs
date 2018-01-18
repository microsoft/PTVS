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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    static class ProjectEntryExtensions {
        /// <summary>
        /// Gets the verbatim AST for the current code and returns the current version.
        /// </summary>
        public static PythonAst GetVerbatimAst(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, out int version) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            var code = ((ProjectEntry)projectFile).ReadDocumentBytes(0, out version);
            if (code != null) {
                var parser = Parser.CreateParser(code, langVersion, options);
                return parser.ParseFile();
            }
            return null;
        }

        /// <summary>
        /// Gets the current AST and the code string for the project entry and returns the current code.
        /// </summary>
        public static PythonAst GetVerbatimAstAndCode(this IPythonProjectEntry projectFile, PythonLanguageVersion langVersion, out int version, out string code) {
            ParserOptions options = new ParserOptions { BindReferences = true, Verbatim = true };

            var codeReader = ((ProjectEntry)projectFile).ReadDocument(0, out version);
            if (codeReader != null) {
                code = codeReader.ReadToEnd();
                var parser = Parser.CreateParser(new StringReader(code), langVersion, options);
                return parser.ParseFile();
            }

            code = null;
            return null;
        }
    }
}
