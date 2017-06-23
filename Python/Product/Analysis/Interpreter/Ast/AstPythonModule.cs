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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    public sealed class AstPythonModule : IPythonModule, IProjectEntry, ILocatedMember {
        private readonly Dictionary<object, object> _properties;
        private readonly List<string> _childModules;
        private readonly Dictionary<string, IMember> _members;

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
            return ModulePath.IsImportable(PathUtils.GetFileOrDirectoryName(path));
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
            using (var parser = Parser.CreateParser(sourceFile, langVersion)) {
                ast = parser.ParseFile();
            }

            return new AstPythonModule(
                moduleFullName ?? ModulePath.FromFullPath(fileName, isPackage: IsPackageCheck).FullName,
                interpreter,
                ast,
                fileName
            );
        }

        internal AstPythonModule() {
            Name = string.Empty;
            Documentation = string.Empty;
            FilePath = string.Empty;
            _properties = new Dictionary<object, object>();
            _childModules = new List<string>();
            _members = new Dictionary<string, IMember>();
        }

        internal AstPythonModule(string moduleName, IPythonInterpreter interpreter, PythonAst ast, string filePath) {
            Name = moduleName;
            Documentation = ast.Documentation;
            FilePath = filePath;
            Locations = new[] { new LocationInfo(filePath, 1, 1) };

            _properties = new Dictionary<object, object>();
            _childModules = new List<string>();
            _members = new Dictionary<string, IMember>();

            var walker = new AstAnalysisWalker(interpreter, ast, this, filePath, _members);
            ast.Walk(walker);
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
        public string Documentation { get; }
        public string FilePath { get; }
        public PythonMemberType MemberType => PythonMemberType.Module;
        public Dictionary<object, object> Properties => _properties;
        public IEnumerable<LocationInfo> Locations { get; }

        public int AnalysisVersion => 1;
        public IModuleContext AnalysisContext => null;
        public bool IsAnalyzed => true;
        public void Analyze(CancellationToken cancel) { }

        public IEnumerable<string> GetChildrenModules() {
            lock (_childModules) {
                return _childModules.ToArray();
            }
        }

        public IMember GetMember(IModuleContext context, string name) {
            IMember member = null;
            lock (_members) {
                _members.TryGetValue(name, out member);
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
    }
}
