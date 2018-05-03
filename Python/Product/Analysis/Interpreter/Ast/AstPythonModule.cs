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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    sealed class AstPythonModule : IPythonModule, IProjectEntry, ILocatedMember {
        private readonly IPythonInterpreter _interpreter;
        private readonly Dictionary<object, object> _properties;
        private readonly List<string> _childModules;
        private readonly Dictionary<string, IMember> _members;
        private bool _foundChildModules;
        private string _documentation = string.Empty;

        public static IPythonModule FromFile(
            IPythonInterpreter interpreter,
            string sourceFile,
            PythonLanguageVersion langVersion
        ) => FromFile(interpreter, sourceFile, langVersion, null);

        public static IPythonModule FromFile(
            IPythonInterpreter interpreter,
            string sourceFile,
            PythonLanguageVersion langVersion,
            string moduleFullName
        ) {
            using (var stream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                return FromStream(interpreter, stream, sourceFile, langVersion, moduleFullName);
            }
        }

        // Avoid hitting the filesystem, but exclude non-importable
        // paths. Ideally, we'd stop at the first path that's a known
        // search path, except we don't know search paths here.
        private static bool IsPackageCheck(string path) {
            return ModulePath.IsImportable(PathUtils.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
        }

        public static IPythonModule FromStream(
            IPythonInterpreter interpreter,
            Stream sourceFile,
            string fileName,
            PythonLanguageVersion langVersion
        ) => FromStream(interpreter, sourceFile, fileName, langVersion, null);

        public static IPythonModule FromStream(
            IPythonInterpreter interpreter,
            Stream sourceFile,
            string fileName,
            PythonLanguageVersion langVersion,
            string moduleFullName
        ) {
            PythonAst ast;
            var parser = Parser.CreateParser(sourceFile, langVersion, new ParserOptions {
                StubFile = fileName.EndsWithOrdinal(".pyi", ignoreCase: true)
            });
            ast = parser.ParseFile();

            return new AstPythonModule(
                moduleFullName ?? ModulePath.FromFullPath(fileName, isPackage: IsPackageCheck).FullName,
                interpreter,
                ast,
                fileName
            );
        }

        internal AstPythonModule() {
            Name = string.Empty;
            FilePath = string.Empty;
            _properties = new Dictionary<object, object>();
            _childModules = new List<string>();
            _foundChildModules = true;
            _members = new Dictionary<string, IMember>();
        }

        internal AstPythonModule(string moduleName, IPythonInterpreter interpreter, PythonAst ast, string filePath) {
            Name = moduleName;
            _documentation = ast.Documentation;
            FilePath = filePath;
            DocumentUri = ProjectEntry.MakeDocumentUri(FilePath);
            Locations = new[] { new LocationInfo(filePath, DocumentUri, 1, 1) };
            _interpreter = interpreter;

            _properties = new Dictionary<object, object>();
            _childModules = new List<string>();
            _members = new Dictionary<string, IMember>();

            // Do not allow children of named modules
            if (!ModulePath.IsInitPyFile(FilePath)) {
                _foundChildModules = true;
            }

            var walker = new AstAnalysisWalker(interpreter, ast, this, filePath, DocumentUri, _members, true, true);
            ast.Walk(walker);
            walker.Complete();
        }

        internal void AddChildModule(string name, IPythonModule module) {
            lock (_childModules) {
                _childModules.Add(name);
            }
            lock (_members) {
                _members[name] = module;
            }
        }

        public string Name { get; }
        public string Documentation {
            get {
                if (_documentation == null) {
                    _members.TryGetValue("__doc__", out var m);
                    _documentation = (m as AstPythonStringLiteral)?.Value ?? string.Empty;
                    if (string.IsNullOrEmpty(_documentation)) {
                        _members.TryGetValue($"_{Name}", out m);
                        _documentation = (m as AstNestedPythonModule)?.Documentation;
                        if (string.IsNullOrEmpty(_documentation)) {
                            _documentation = TryGetDocFromModuleInitFile(FilePath);
                        }
                    }
                }
                return _documentation;
            }
        }
        public string FilePath { get; }
        public Uri DocumentUri { get; }
        public PythonMemberType MemberType => PythonMemberType.Module;
        public Dictionary<object, object> Properties => _properties;
        public IEnumerable<LocationInfo> Locations { get; }

        public int AnalysisVersion => 1;
        public IModuleContext AnalysisContext => null;
        public bool IsAnalyzed => true;
        public void Analyze(CancellationToken cancel) { }

        private static IEnumerable<string> GetChildModules(string filePath, string prefix, AstPythonInterpreter interpreter) {
            if (interpreter == null || string.IsNullOrEmpty(filePath)) {
                yield break;
            }
            var searchPath = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(searchPath)) {
                yield break;
            }

            foreach (var n in ModulePath.GetModulesInPath(
                searchPath,
                recurse: false,
                includePackages: true
            ).Select(mp => mp.ModuleName).Where(n => !string.IsNullOrEmpty(n))) {
                yield return n;
            }
        }

        public IEnumerable<string> GetChildrenModules() {
            lock (_childModules) {
                if (!_foundChildModules) {
                    // We've already checked whether this module may have children
                    // so don't worry about checking again here.
                    _foundChildModules = true;
                    foreach (var m in GetChildModules(FilePath, Name, _interpreter as AstPythonInterpreter)) {
                        _members[m] = new AstNestedPythonModule(_interpreter, m, new[] { Name + "." + m });
                        _childModules.Add(m);
                    }
                }
                return _childModules.ToArray();
            }
        }

        public IMember GetMember(IModuleContext context, string name) {
            IMember member = null;
            lock (_members) {
                _members.TryGetValue(name, out member);
            }
            if (member is ILazyMember lm) {
                member = lm.Get();
                lock (_members) {
                    _members[name] = member;
                }
            }
            return member;
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            lock (_members) {
                return _members.Keys.ToArray();
            }
        }

        public void Imported(IModuleContext context) { }
        public void RemovedFromProject() { }

        private static string TryGetDocFromModuleInitFile(string filePath) {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) {
                return string.Empty;
            }

            try {
                using (var sr = new StreamReader(filePath)) {
                    string quote = null;
                    while (true) {
                        var line = sr.ReadLine();
                        if (line == null) {
                            break;
                        }
                        if (line.Length == 0 || line.StartsWithOrdinal("#")) {
                            continue;
                        }
                        if (line.StartsWithOrdinal("\"\"\"") || line.StartsWithOrdinal("r\"\"\"")) {
                            quote = "\"\"\"";
                        } else if (line.StartsWithOrdinal("'''") || line.StartsWithOrdinal("r'''")) {
                            quote = "'''";
                        }
                        break;
                    }

                    if (quote != null) {
                        var sb = new StringBuilder();
                        while (true) {
                            var line = sr.ReadLine();
                            if (line == null || line.EndsWithOrdinal(quote)) {
                                break;
                            }
                            sb.AppendLine(line);
                        }
                        return sb.ToString();
                    }
                }
            } catch (IOException) { } catch(UnauthorizedAccessException) { }
            return string.Empty;
        }
    }
}
