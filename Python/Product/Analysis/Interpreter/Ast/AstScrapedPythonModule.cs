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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstScrapedPythonModule : IPythonModule {
        private readonly string _filePath;
        private string _documentation;
        protected readonly Dictionary<string, IMember> _members;
        private bool _scraped;

        public AstScrapedPythonModule(string name, string filePath) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _documentation = string.Empty;
            _filePath = filePath;
            _members = new Dictionary<string, IMember>();
        }

        public string Name { get; }

        public string Documentation => _documentation;

        public PythonMemberType MemberType => PythonMemberType.Module;

        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();

        public virtual IMember GetMember(IModuleContext context, string name) {
            lock (_members) {
                IMember m;
                _members.TryGetValue(name, out m);
                return m;
            }
        }

        public virtual IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            lock (_members) {
                return _members.Keys.ToArray();
            }
        }

        public IEnumerable<string> ParseErrors { get; private set; }

        protected virtual List<string> GetScrapeArguments(IPythonInterpreterFactory factory) {
            var args = new List<string> { "-B", "-E" };

            ModulePath mp = AstPythonInterpreterFactory.FindModule(factory, _filePath);
            if (string.IsNullOrEmpty(mp.FullName)) {
                return null;
            }

            var sm = PythonToolsInstallPath.TryGetFile("scrape_module.py", GetType().Assembly);
            if (!File.Exists(sm)) {
                return null;
            }

            args.Add(sm);
            args.Add(mp.ModuleName);
            args.Add(mp.LibraryPath);

            return args;
        }

        protected virtual PythonWalker PrepareWalker(IPythonInterpreter interpreter, PythonAst ast) {
            return new AstAnalysisWalker(interpreter, ast, this, _filePath, _members, false);
        }

        protected virtual void PostWalk(PythonWalker walker) {
        }

        protected virtual Stream LoadCachedCode(AstPythonInterpreter interpreter) {
            return (interpreter.Factory as AstPythonInterpreterFactory)?.ReadCachedModule(_filePath);
        }

        protected virtual void SaveCachedCode(AstPythonInterpreter interpreter, Stream code) {
            (interpreter.Factory as AstPythonInterpreterFactory)?.WriteCachedModule(_filePath, code);
        }

        public void Imported(IModuleContext context) {
            if (_scraped) {
                return;
            }
            _scraped = true;

            var interp = context as AstPythonInterpreter;
            var fact = interp?.Factory;
            if (fact == null) {
                return;
            }

            var code = LoadCachedCode(interp);
            bool needCache = code == null;

            if (needCache) {
                if (!File.Exists(fact.Configuration.InterpreterPath)) {
                    return;
                }

                var args = GetScrapeArguments(fact);
                if (args == null) {
                    return;
                }

                using (var p = ProcessOutput.RunHiddenAndCapture(fact.Configuration.InterpreterPath, args.ToArray())) {
                    p.Wait();
                    if (p.ExitCode == 0) {
                        var ms = new MemoryStream();
                        code = ms;
                        using (var sw = new StreamWriter(ms, Encoding.UTF8, 4096, true)) {
                            foreach (var line in p.StandardOutputLines) {
                                sw.WriteLine(line);
                            }
                        }
                        code.Seek(0, SeekOrigin.Begin);
                    } else {
                        if (fact is AstPythonInterpreterFactory f) {
                            f.Log(TraceLevel.Error, "Scrape", p.Arguments);
                            foreach (var e in p.StandardErrorLines) {
                                f.Log(TraceLevel.Error, "Scrape", Name, e);
                            }
                        }

                        var err = new List<string> { $"Error scraping {Name}", p.Arguments };
                        err.AddRange(p.StandardErrorLines);
                        Debug.Fail(string.Join(Environment.NewLine, err));
                        ParseErrors = err;
                    }
                }
            }

            if (code == null) {
                return;
            }

            PythonAst ast;
            using (code) {
                var sink = new CollectingErrorSink();
                using (var sr = new StreamReader(code, Encoding.UTF8, true, 4096, true))
                using (var parser = Parser.CreateParser(sr, fact.GetLanguageVersion(), new ParserOptions { ErrorSink = sink })) {
                    ast = parser.ParseFile();
                }

                ParseErrors = sink.Errors.Select(e => $"{_filePath ?? "(builtins)"} ({e.Span}): {e.Message}").ToArray();
                if (ParseErrors.Any() && fact is AstPythonInterpreterFactory f) {
                    f.Log(TraceLevel.Error, "Parse", _filePath ?? "(builtins)");
                    foreach (var e in ParseErrors) {
                        f.Log(TraceLevel.Error, "Parse", e);
                    }
                }

                if (needCache) {
                    // We know we created the stream, so it's safe to seek here
                    code.Seek(0, SeekOrigin.Begin);
                    SaveCachedCode(interp, code);
                }
            }

            var walker = PrepareWalker(interp, ast);
            lock (_members) {
                ast.Walk(walker);
                PostWalk(walker);
            }
        }
    }
}
