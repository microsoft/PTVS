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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonInterpreter : IPythonInterpreter, IModuleContext, ICanFindModuleMembers {
        private readonly AstPythonInterpreterFactory _factory;
        private readonly Dictionary<BuiltinTypeId, IPythonType> _builtinTypes;
        private PythonAnalyzer _analyzer;
        private AstScrapedPythonModule _builtinModule;
        private IReadOnlyList<string> _builtinModuleNames;
        private readonly ConcurrentDictionary<string, IPythonModule> _modules;
        private readonly AstPythonBuiltinType _noneType;

        internal readonly AnalysisLogWriter _log;

        private readonly object _userSearchPathsLock = new object();
        private IReadOnlyList<string> _userSearchPaths;
        private IReadOnlyDictionary<string, string> _userSearchPathPackages;
        private HashSet<string> _userSearchPathImported;

        public AstPythonInterpreter(AstPythonInterpreterFactory factory, AnalysisLogWriter log = null) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _log = log;
            _factory.ImportableModulesChanged += Factory_ImportableModulesChanged;
            _modules = new ConcurrentDictionary<string, IPythonModule>();
            _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>();
            BuiltinModuleName = _factory.LanguageVersion.Is3x() ? SharedDatabaseState.BuiltinName3x : SharedDatabaseState.BuiltinName2x;
            _noneType = new AstPythonBuiltinType("NoneType", BuiltinTypeId.NoneType);
            _builtinTypes[BuiltinTypeId.NoneType] = _noneType;
            _builtinTypes[BuiltinTypeId.Unknown] = new AstPythonBuiltinType("Unknown", BuiltinTypeId.Unknown);
        }

        public void Dispose() {
            _factory.ImportableModulesChanged -= Factory_ImportableModulesChanged;
        }

        private void Factory_ImportableModulesChanged(object sender, EventArgs e) {
            _modules.Clear();
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddUnimportableModule(string moduleName) {
            _modules[moduleName] = new SentinelModule(moduleName, false);
        }

        public event EventHandler ModuleNamesChanged;

        public IModuleContext CreateModuleContext() => this;
        public IPythonInterpreterFactory Factory => _factory;
        public string BuiltinModuleName { get; }

        public IPythonType GetBuiltinType(BuiltinTypeId id) {
            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    var bm = ImportModule(BuiltinModuleName) as AstBuiltinsPythonModule;
                    res = bm?.GetAnyMember($"__{id}") as IPythonType;
                    if (res == null) {
                        var name = SharedDatabaseState.GetBuiltinTypeName(id, _factory.Configuration.Version);
                        if (string.IsNullOrEmpty(name)) {
                            Debug.Assert(id == BuiltinTypeId.Unknown, $"no name for {id}");
                            if (!_builtinTypes.TryGetValue(BuiltinTypeId.Unknown, out res)) {
                                _builtinTypes[BuiltinTypeId.Unknown] = res = new AstPythonType("<unknown>");
                            }
                        } else {
                            res = new AstPythonType(name);
                        }
                    }
                    _builtinTypes[id] = res;
                }
            }
            return res;
        }

        private IReadOnlyDictionary<string, string> GetUserSearchPathPackages() {
            var ussp = _userSearchPathPackages;
            if (ussp == null) {
                lock (_userSearchPathsLock) {
                    if (ussp == null && _userSearchPaths != null && _userSearchPaths.Any()) {
                        ussp = AstPythonInterpreterFactory.GetImportableModules(_userSearchPaths);
                    }
                }
            }
            return ussp;
        }

        public IList<string> GetModuleNames() {
            var ussp = GetUserSearchPathPackages();
            var ssp = _factory.GetImportableModules();
            var bmn = _builtinModuleNames;

            IEnumerable<string> names = null;
            if (ussp != null) {
                names = ussp.Keys;
            }
            if (ssp != null) {
                names = names?.Union(ssp.Keys) ?? ssp.Keys;
            }
            if (bmn != null) {
                names = names?.Union(bmn) ?? bmn;
            }

            return names.MaybeEnumerate().ToArray();
        }

        private ModulePath? FindModuleInSearchPath(
            IEnumerable<string> searchPaths,
            IReadOnlyDictionary<string, string> packages,
            string name
        ) {
            return FindModuleInSearchPath(
                searchPaths?.Select(s => new PythonLibraryPath(s, false, null)),
                packages,
                name
            );
        }

        private ModulePath? FindModuleInSearchPath(
            IEnumerable<PythonLibraryPath> searchPaths,
            IReadOnlyDictionary<string, string> packages,
            string name
        ) {
            if (searchPaths == null || packages == null) {
                return null;
            }

            int i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;

            if (packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
                if (ModulePath.FromBasePathAndName_NoThrow(searchPath, name, out mp)) {
                    return mp;
                }
            }

            foreach (var sp in searchPaths.MaybeEnumerate()) {
                if (ModulePath.FromBasePathAndName_NoThrow(sp.Path, name, out mp)) {
                    return mp;
                }
            }

            return null;
        }

        public IPythonModule ImportModule(string name) {
            IPythonModule module = null;
            bool needRetry = true;
            for (int retries = 5; retries > 0 && needRetry; --retries) {
                module = ImportModuleOrRetry(name, out needRetry);
            }
            if (needRetry) {
                // Never succeeded, so just log the error and fail
                _log?.Log(TraceLevel.Error, "RetryImport", name);
                return null;
            }
            return module;
        }

        private IPythonModule ImportModuleOrRetry(string name, out bool retry) {
            retry = false;
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            Debug.Assert(!name.EndsWith("."), $"{name} should not end with '.'");

            // Handle builtins explicitly
            if (name == BuiltinModuleName) {
                if (_builtinModule == null) {
                    _modules[BuiltinModuleName] = _builtinModule = new AstBuiltinsPythonModule(_factory.LanguageVersion);
                    _builtinModuleNames = null;
                    _builtinModule.Imported(this);
                    var bmn = ((AstBuiltinsPythonModule)_builtinModule).GetAnyMember("__builtin_module_names") as AstPythonStringLiteral;
                    _builtinModuleNames = bmn?.Value?.Split(',') ?? Array.Empty<string>();
                }
                return _builtinModule;
            }

            IPythonModule mod;
            // Return any existing module
            if (_modules.TryGetValue(name, out mod) && mod != null) {
                if (mod is SentinelModule smod) {
                    // If we are importing this module on another thread, allow
                    // time for it to complete. This does not block if we are
                    // importing on the current thread or the module is not
                    // really being imported.
                    var newMod = smod.WaitForImport(5000);
                    if (newMod is SentinelModule) {
                        _log?.Log(TraceLevel.Warning, "RecursiveImport", name);
                        mod = newMod;
                    } else if (newMod == null) {
                        _log?.Log(TraceLevel.Warning, "ImportTimeout", name);
                    } else {
                        mod = newMod;
                    }
                }
                return mod;
            }

            // Set up a sentinel so we can detect recursive imports
            var sentinalValue = new SentinelModule(name, true);
            if (!_modules.TryAdd(name, sentinalValue)) {
                // Try to get the new module, in case we raced with a .Clear()
                if (_modules.TryGetValue(name, out mod) && !(mod is SentinelModule)) {
                    return mod;
                }
                // If we reach here, the race is too complicated to recover
                // from. Signal the caller to try importing again.
                _log?.Log(TraceLevel.Warning, "RetryImport", name);
                retry = true;
                return null;
            }

            // Do normal searches
            mod = ImportFromSearchPaths(name) ?? ImportFromBuiltins(name);

            // Replace our sentinel, or if we raced, get the current
            // value and abandon the one we just created.
            if (!_modules.TryUpdate(name, mod, sentinalValue)) {
                // Try to get the new module, in case we raced
                if (_modules.TryGetValue(name, out mod) && !(mod is SentinelModule)) {
                    return mod;
                }
                // If we reach here, the race is too complicated to recover
                // from. Signal the caller to try importing again.
                _log?.Log(TraceLevel.Warning, "RetryImport", name);
                retry = true;
                return null;
            }

            sentinalValue.Complete(mod);
            sentinalValue.Dispose();
            return mod;
        }

        private IPythonModule ImportFromBuiltins(string name) {
            if (_builtinModuleNames == null || !_builtinModuleNames.Contains(name)) {
                return null;
            }

            _log?.Log(TraceLevel.Info, "ImportBuiltins", name, _factory.FastRelativePath(Factory.Configuration.InterpreterPath));

            try {
                return new AstBuiltinPythonModule(name, Factory.Configuration.InterpreterPath);
            } catch (ArgumentNullException) {
                Debug.Fail("No factory means cannot import builtin modules");
                return null;
            }
        }

        private IPythonModule ImportFromSearchPaths(string name) {
            var mmp = FindModuleInSearchPath(_userSearchPaths, GetUserSearchPathPackages(), name);

            if (mmp.HasValue) {
                lock (_userSearchPathsLock) {
                    if (_userSearchPathImported == null) {
                        _userSearchPathImported = new HashSet<string>();
                    }
                    _userSearchPathImported.Add(name);
                }
            } else {
                mmp = FindModuleInSearchPath(_factory.GetSearchPaths(), _factory.GetImportableModules(), name);
            }

            if (!mmp.HasValue) {
                return null;
            }

            var mp = mmp.Value;

            if (mp.IsCompiled) {
                _log?.Log(TraceLevel.Verbose, "ImportScraped", mp.FullName, _factory.FastRelativePath(mp.SourceFile));
                return new AstScrapedPythonModule(mp.FullName, mp.SourceFile);
            }

            _log?.Log(TraceLevel.Verbose, "Import", mp.FullName, _factory.FastRelativePath(mp.SourceFile));
            return AstPythonModule.FromFile(this, mp.SourceFile, _factory.LanguageVersion, mp.FullName);
        }

        public void Initialize(PythonAnalyzer state) {
            if (_analyzer != null) {
                _analyzer.SearchPathsChanged -= Analyzer_SearchPathsChanged;
            }

            _analyzer = state;

            if (state != null) {
                lock (_userSearchPathsLock) {
                    _userSearchPaths = state.GetSearchPaths();
                }
                state.SearchPathsChanged += Analyzer_SearchPathsChanged;
                var bm = state.BuiltinModule;
                if (!string.IsNullOrEmpty(bm?.Name)) {
                    _modules[state.BuiltinModule.Name] = state.BuiltinModule.InterpreterModule;
                }
            }
        }

        private void Analyzer_SearchPathsChanged(object sender, EventArgs e) {
            lock (_userSearchPathsLock) {
                // Remove imported modules from search paths so we will
                // import them again.
                foreach (var name in _userSearchPathImported.MaybeEnumerate()) {
                    IPythonModule mod;
                    _modules.TryRemove(name, out mod);
                }
                _userSearchPathImported = null;
                _userSearchPathPackages = null;
                _userSearchPaths = _analyzer.GetSearchPaths();
            }
            ModuleNamesChanged?.Invoke(this, EventArgs.Empty);
        }

        public IEnumerable<string> GetModulesNamed(string name) {
            var usp = GetUserSearchPathPackages();
            var ssp = _factory.GetImportableModules();

            var dotName = "." + name;

            IEnumerable<string> res;
            if (usp == null) {
                if (ssp == null) {
                    res = Enumerable.Empty<string>();
                } else {
                    res = ssp.Keys;
                }
            } else if (ssp == null) {
                res = usp.Keys;
            } else {
                res = usp.Keys.Union(ssp.Keys);
            }

            return res.Where(m => m == name || m.EndsWith(dotName));
        }

        public IEnumerable<string> GetModulesContainingName(string name) {
            // TODO: Some efficient way of searching every module

            yield break;
        }
    }
}
