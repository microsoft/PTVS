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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreter : IPythonInterpreter {
        private readonly AstPythonInterpreterFactory _factory;
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinModule;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules;

        public AstPythonInterpreter(AstPythonInterpreterFactory factory) {
            _factory = factory;
            _builtinModule = new Dictionary<BuiltinTypeId, IPythonType>();
            _modules = new ConcurrentDictionary<string, IPythonModule>();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
        }

        public event EventHandler ModuleNamesChanged;

        public IModuleContext CreateModuleContext() {
            return null;
        }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            IPythonType res;
            lock (_builtinModule) {
                if (!_builtinModule.TryGetValue(id, out res)) {
                    _builtinModule[id] = res = new AstPythonType(id.ToString());
                }
            }
            return res;
        }

        public IList<string> GetModuleNames() {
            return _factory.GetImportableModules();
        }

        public IPythonModule ImportModule(string name) {
            IPythonModule mod;
            if (_modules.TryGetValue(name, out mod) && mod != null) {
                return mod;
            }

            var searchPaths = _factory.GetSearchPaths();
            foreach (var sp in searchPaths) {
                ModulePath mp;
                try {
                    mp = ModulePath.FromBasePathAndName(sp.Path, name);
                } catch (ArgumentException) {
                    continue;
                }

                mod = AstPythonModule.FromFile(this, mp.SourceFile, _factory.Configuration.Version.ToLanguageVersion(), mp.FullName);
                if (!_modules.TryAdd(name, mod)) {
                    mod = _modules[name];
                }
                return mod;
            }
            return null;
        }

        public void Initialize(PythonAnalyzer state) {
        }
    }
}
