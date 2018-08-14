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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstScrapedPythonModule : IPythonModule
#if DEBUG
        // In debug builds we let you F12 to the scraped file
        , ILocatedMember
#endif
        {
        private readonly string _filePath;
        protected readonly Dictionary<string, IMember> _members;
        private bool _scraped;

        public AstScrapedPythonModule(string name, string filePath) {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _filePath = filePath;
            _members = new Dictionary<string, IMember>();
            _scraped = false;
        }

        public string Name { get; }

        public string Documentation {
            get {
                var m = GetMember(null, "__doc__") as AstPythonStringLiteral;
                return m != null ? m.Value : string.Empty;
            }
        }

        public PythonMemberType MemberType => PythonMemberType.Module;

        public IEnumerable<string> GetChildrenModules() => Enumerable.Empty<string>();

        public virtual IMember GetMember(IModuleContext context, string name) {
            IMember m;
            if (!_scraped) {
                Imported(context);
            }
            lock (_members) {
                _members.TryGetValue(name, out m);
            }
            if (m is ILazyMember lm) {
                m = lm.Get();
                lock (_members) {
                    _members[name] = m;
                }
            }
            return m;
        }

        public virtual IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            if (!_scraped) {
                Imported(moduleContext);
            }
            lock (_members) {
                return _members.Keys.ToArray();
            }
        }

        public IEnumerable<string> ParseErrors { get; private set; }

        internal static bool KeepAst { get; set; }
        internal PythonAst Ast { get; private set; }

#if DEBUG
        public IEnumerable<LocationInfo> Locations { get; private set; } = new LocationInfo[0];
#endif

        protected virtual List<string> GetScrapeArguments(IPythonInterpreterFactory factory) {
            var args = new List<string> { "-B", "-E" };

            ModulePath mp = AstPythonInterpreterFactory.FindModuleAsync(factory, _filePath, CancellationToken.None)
                .WaitAndUnwrapExceptions();
            if (string.IsNullOrEmpty(mp.FullName)) {
                return null;
            }

            if (!InstallPath.TryGetFile("scrape_module.py", out string sm)) {
                return null;
            }

            args.Add(sm);
            args.Add("-u8");
            args.Add(mp.ModuleName);
            args.Add(mp.LibraryPath);

            return args;
        }

        protected virtual PythonWalker PrepareWalker(IPythonInterpreter interpreter, PythonAst ast) {
#if DEBUG
            // In debug builds we let you F12 to the scraped file
            var filePath = string.IsNullOrEmpty(_filePath)
                ? null
                : ((interpreter as AstPythonInterpreter)?.Factory as AstPythonInterpreterFactory)?.GetCacheFilePath(_filePath);
            var uri = string.IsNullOrEmpty(filePath) ? null : new Uri(filePath);
            const bool includeLocations = true;
#else
            const string filePath = null;
            const Uri uri = null;
            const bool includeLocations = false;
#endif
            return new AstAnalysisWalker(interpreter, ast, this, filePath, uri, _members, includeLocations, true);
        }

        protected virtual void PostWalk(PythonWalker walker) {
            (walker as AstAnalysisWalker)?.Complete();
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
            Debugger.NotifyOfCrossThreadDependency();

            var interp = context as AstPythonInterpreter;
            var fact = interp?.Factory as AstPythonInterpreterFactory;
            if (fact == null) {
                return;
            }

            var code = LoadCachedCode(interp);
            bool needCache = code == null;

            _scraped = true;

            if (needCache) {
                if (!File.Exists(fact.Configuration.InterpreterPath)) {
                    return;
                }

                var args = GetScrapeArguments(fact);
                if (args == null) {
                    return;
                }

                var ms = new MemoryStream();
                code = ms;

                using (var sw = new StreamWriter(ms, Encoding.UTF8, 4096, true))
                using (var proc = new ProcessHelper(
                    fact.Configuration.InterpreterPath,
                    args,
                    fact.Configuration.PrefixPath
                )) {
                    proc.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    proc.OnOutputLine = sw.WriteLine;
                    proc.OnErrorLine = s => fact.Log(TraceLevel.Error, "Scrape", s);

                    fact.Log(TraceLevel.Info, "Scrape", proc.FileName, proc.Arguments);

                    proc.Start();
                    var exitCode = proc.Wait(60000);

                    if (exitCode == null) {
                        proc.Kill();
                        fact.Log(TraceLevel.Error, "ScrapeTimeout", proc.FileName, proc.Arguments);
                        return;
                    } else if (exitCode != 0) {
                        fact.Log(TraceLevel.Error, "Scrape", "ExitCode", exitCode);
                        return;
                    }
                }

                code?.Seek(0, SeekOrigin.Begin);
            }

            if (code == null) {
                return;
            }

            PythonAst ast;
            using (code) {
                var sink = new CollectingErrorSink();
                using (var sr = new StreamReader(code, Encoding.UTF8, true, 4096, true)) {
                    var parser = Parser.CreateParser(sr, fact.GetLanguageVersion(), new ParserOptions { ErrorSink = sink, StubFile = true });
                    ast = parser.ParseFile();
                }

                ParseErrors = sink.Errors.Select(e => "{0} ({1}): {2}".FormatUI(_filePath ?? "(builtins)", e.Span, e.Message)).ToArray();
                if (ParseErrors.Any()) {
                    fact.Log(TraceLevel.Error, "Parse", _filePath ?? "(builtins)");
                    foreach (var e in ParseErrors) {
                        fact.Log(TraceLevel.Error, "Parse", e);
                    }
                }

                if (needCache) {
                    // We know we created the stream, so it's safe to seek here
                    code.Seek(0, SeekOrigin.Begin);
                    SaveCachedCode(interp, code);
                }
            }

            if (KeepAst) {
                Ast = ast;
            }

#if DEBUG
            if (!string.IsNullOrEmpty(_filePath)) {
                var cachePath = fact.GetCacheFilePath(_filePath);
                if (!string.IsNullOrEmpty(cachePath)) {
                    Locations = new[] { new LocationInfo(cachePath, null, 1, 1) };
                }
            }
#endif

            var walker = PrepareWalker(interp, ast);
            lock (_members) {
                ast.Walk(walker);
                PostWalk(walker);
            }
        }
    }
}
