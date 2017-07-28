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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreter : IPythonInterpreter, IModuleContext {
        private readonly AstPythonInterpreterFactory _factory;
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes;
        private IBuiltinPythonModule _builtinModule;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules;

        public AstPythonInterpreter(AstPythonInterpreterFactory factory) {
            _factory = factory;
            _modules = new ConcurrentDictionary<string, IPythonModule>();
            _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
        }

        public event EventHandler ModuleNamesChanged;

        public IModuleContext CreateModuleContext() => this;
        public IPythonInterpreterFactory Factory => _factory;

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    _builtinTypes[id] = res =
                        _builtinModule?.GetAnyMember(SharedDatabaseState.GetBuiltinTypeName(id, _factory.Configuration.Version)) as IPythonType ??
                        new AstPythonType(id.ToString());
                }
            }
            return res;
        }

        public IList<string> GetModuleNames() {
            return _factory.GetImportableModules().Keys.ToArray();
        }

        public IPythonModule ImportModule(string name) {
            IPythonModule mod;
            if (_modules.TryGetValue(name, out mod) && mod != null) {
                if (mod is EmptyModule) {
                    Trace.TraceWarning($"Recursively importing {name}");
                }
                return mod;
            }
            var sentinalValue = new EmptyModule();
            if (!_modules.TryAdd(name, sentinalValue)) {
                return _modules[name];
            }

            var packages = _factory.GetImportableModules();
            int i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            ModulePath mp = default(ModulePath);
            string searchPath = null;

            if (packages?.TryGetValue(firstBit, out searchPath) ?? false &&
                !string.IsNullOrEmpty(searchPath)) {
                try {
                    mp = ModulePath.FromBasePathAndName(searchPath, name);
                } catch (ArgumentException) {
                }
            }

            if (string.IsNullOrEmpty(mp.SourceFile)) {
                foreach (var sp in _factory.GetSearchPaths().MaybeEnumerate()) {
                    try {
                        mp = ModulePath.FromBasePathAndName(sp.Path, name);
                        break;
                    } catch (ArgumentException) {
                    }
                }
            }

            if (!string.IsNullOrEmpty(mp.SourceFile)) {
                if (mp.IsCompiled) {
                    mod = new AstScrapedPythonModule(mp.FullName, mp.SourceFile);
                } else {
                    mod = AstPythonModule.FromFile(this, mp.SourceFile, _factory.LanguageVersion, mp.FullName);
                }
            }

            if (!_modules.TryUpdate(name, mod, sentinalValue)) {
                mod = _modules[name];
            }

            return mod;
        }

        public void Initialize(PythonAnalyzer state) {
            _builtinModule = state.BuiltinModule.InterpreterModule as IBuiltinPythonModule;
            _modules[state.BuiltinModule.Name] = state.BuiltinModule.InterpreterModule;
        }
    }
}
