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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstScrapedPythonModule : IPythonModule {
        private readonly string _filePath;
        private string _documentation;
        private readonly Dictionary<string, IMember> _members;
        private bool _scraped;

        public AstScrapedPythonModule(string name, string filePath) {
            _documentation = string.Empty;
            _filePath = filePath;
            _members = new Dictionary<string, IMember>();
        }

        public string Name { get; }

        public string Documentation => _documentation;

        public PythonMemberType MemberType => PythonMemberType.Module;

        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();

        public IMember GetMember(IModuleContext context, string name) {
            lock (_members) {
                IMember m;
                _members.TryGetValue(name, out m);
                return m;
            }
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            lock (_members) {
                return _members.Keys.ToArray();
            }
        }

        public void Imported(IModuleContext context) {
            if (_scraped) {
                return;
            }
            _scraped = true;

            var interp = context as AstPythonInterpreter;
            var fact = interp?.Factory;
            if (fact == null || !File.Exists(fact.Configuration.InterpreterPath)) {
                return;
            }

            ModulePath mp = AstPythonInterpreterFactory.FindModule(fact, _filePath);
            if (string.IsNullOrEmpty(mp.FullName)) {
                return;
            }

            var sm = PythonToolsInstallPath.TryGetFile("scrape_module.py", GetType().Assembly);
            if (!File.Exists(sm)) {
                return;
            }

            Stream code = null;

            using (var p = ProcessOutput.RunHiddenAndCapture(
                fact.Configuration.InterpreterPath, "-E", sm, mp.LibraryPath, mp.ModuleName
            )) {
                p.Wait();
                if (p.ExitCode == 0) {
                    var ms = new MemoryStream();
                    code = ms;
                    using (var sw = new StreamWriter(ms, Encoding.UTF8, 4096, true)) {
                        foreach (var line in p.StandardOutputLines) {
                            sw.WriteLine(line);
                        }
                    }
                }
            }

            if (code == null) {
                return;
            }

            PythonAst ast;
            code.Seek(0, SeekOrigin.Begin);
            using (var sr = new StreamReader(code, Encoding.UTF8))
            using (var parser = Parser.CreateParser(sr, fact.GetLanguageVersion())) {
                ast = parser.ParseFile();
            }

            lock (_members) {
                var walker = new AstAnalysisWalker(interp, ast, this, _filePath, _members);
                ast.Walk(walker);
            }
        }
    }
}
