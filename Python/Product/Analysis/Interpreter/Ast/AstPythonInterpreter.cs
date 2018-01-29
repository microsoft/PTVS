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

// Setting this variable will enable the typeshed package to override
// imports. However, this generally makes completions worse, so it's
// turned off for now.
//#define USE_TYPESHED

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;

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

#if USE_TYPESHED
        private readonly object _typeShedPathsLock = new object();
        private IReadOnlyList<string> _typeShedPaths;
#endif

        public AstPythonInterpreter(AstPythonInterpreterFactory factory, AnalysisLogWriter log = null) {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _log = log;
            _factory.ImportableModulesChanged += Factory_ImportableModulesChanged;
            _modules = new ConcurrentDictionary<string, IPythonModule>();
            _builtinTypes = new Dictionary<BuiltinTypeId, IPythonType>();
            BuiltinModuleName = BuiltinTypeId.Unknown.GetModuleName(_factory.LanguageVersion);
            _noneType = new AstPythonBuiltinType("NoneType", BuiltinTypeId.NoneType);
            _builtinTypes[BuiltinTypeId.NoneType] = _noneType;
            _builtinTypes[BuiltinTypeId.Unknown] = new AstPythonBuiltinType("Unknown", BuiltinTypeId.Unknown);
        }

        public void Dispose() {
            _factory.ImportableModulesChanged -= Factory_ImportableModulesChanged;
        }

        private void Factory_ImportableModulesChanged(object sender, EventArgs e) {
            _modules.Clear();
#if USE_TYPESHED
            lock (_typeShedPathsLock) {
                _typeShedPaths = null;
            }
#endif
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
            if (id < 0 || id > BuiltinTypeIdExtensions.LastTypeId) {
                throw new KeyNotFoundException("(BuiltinTypeId)({0})".FormatInvariant((int)id));
            }

            IPythonType res;
            lock (_builtinTypes) {
                if (!_builtinTypes.TryGetValue(id, out res)) {
                    var bm = ImportModule(BuiltinModuleName) as AstBuiltinsPythonModule;
                    res = bm?.GetAnyMember("__{0}__".FormatInvariant(id)) as IPythonType;
                    if (res == null) {
                        var name = id.GetTypeName(_factory.Configuration.Version);
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

        private async Task<IReadOnlyDictionary<string, string>> GetUserSearchPathPackagesAsync() {
            var ussp = _userSearchPathPackages;
            if (ussp == null) {
                IReadOnlyList<string> usp;
                lock (_userSearchPathsLock) {
                    usp = _userSearchPaths;
                    ussp = _userSearchPathPackages;
                }
                if (ussp != null || usp == null || !usp.Any()) {
                    return ussp;
                }

                ussp = await AstPythonInterpreterFactory.GetImportableModulesAsync(usp);
                lock (_userSearchPathsLock) {
                    if (_userSearchPathPackages == null) {
                        _userSearchPathPackages = ussp;
                    } else {
                        ussp = _userSearchPathPackages;
                    }
                }
            }
            return ussp;
        }

        public IList<string> GetModuleNames() {
            var ussp = GetUserSearchPathPackagesAsync().WaitAndUnwrapExceptions();
            var ssp = _factory.GetImportableModulesAsync().WaitAndUnwrapExceptions();
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
            if (searchPaths == null) {
                return null;
            }

            int i = name.IndexOf('.');
            var firstBit = i < 0 ? name : name.Remove(i);
            string searchPath;

            ModulePath mp;

            if (packages != null && packages.TryGetValue(firstBit, out searchPath) && !string.IsNullOrEmpty(searchPath)) {
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
                    var bmn = ((AstBuiltinsPythonModule)_builtinModule).GetAnyMember("__builtin_module_names__") as AstPythonStringLiteral;
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
            if (!string.IsNullOrEmpty(Factory.Configuration?.InterpreterPath)) {
                mod = ImportFromSearchPathsAsync(name).WaitAndUnwrapExceptions() ?? ImportFromBuiltins(name);
            }
            if (mod == null) {
                mod = ImportFromCache(name);
            }

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

        private IPythonModule ImportFromCache(string name) {
            if (string.IsNullOrEmpty(_factory.CreationOptions.DatabasePath)) {
                return null;
            }

            if (File.Exists(_factory.GetCacheFilePath("python.{0}.pyi".FormatInvariant(name)))) {
                return new AstCachedPythonModule(name, "python.{0}".FormatInvariant(name));
            } else if (File.Exists(_factory.GetCacheFilePath("{0}.pyi".FormatInvariant(name)))) {
                return new AstCachedPythonModule(name, name);
            }

            return null;
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

        private async Task<IPythonModule> ImportFromSearchPathsAsync(string name) {
            var mmp = FindModuleInSearchPath(
                _userSearchPaths,
                await GetUserSearchPathPackagesAsync(),
                name
            );

            if (mmp.HasValue) {
                lock (_userSearchPathsLock) {
                    if (_userSearchPathImported == null) {
                        _userSearchPathImported = new HashSet<string>();
                    }
                    _userSearchPathImported.Add(name);
                }
            } else {
                mmp = FindModuleInSearchPath(
                    await _factory.GetSearchPathsAsync(),
                    await _factory.GetImportableModulesAsync(),
                    name
                );
            }

            if (!mmp.HasValue) {
                return null;
            }

            var mp = mmp.Value;

#if USE_TYPESHED
            lock (_typeShedPathsLock) {
                if (_typeShedPaths == null) {
                    var typeshed = FindModuleInSearchPath(_factory.GetSearchPaths(), _factory.GetImportableModules(), "typeshed");
                    if (typeshed.HasValue) {
                        _typeShedPaths = GetTypeShedPaths(PathUtils.GetParent(typeshed.Value.SourceFile)).ToArray();
                    } else {
                        _typeShedPaths = Array.Empty<string>();
                    }
                }
                if (_typeShedPaths.Any()) {
                    var mtsp = FindModuleInSearchPath(_typeShedPaths, null, mp.FullName);
                    if (mtsp.HasValue) {
                        mp = mtsp.Value;
                    }
                }
            }
#endif

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
            var usp = GetUserSearchPathPackagesAsync().WaitAndUnwrapExceptions();
            var ssp = _factory.GetImportableModulesAsync().WaitAndUnwrapExceptions();

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

            return res.Where(m => m == name || m.EndsWith(dotName, StringComparison.Ordinal));
        }

        public IEnumerable<string> GetModulesContainingName(string name) {
            // TODO: Some efficient way of searching every module

            yield break;
        }

        private IEnumerable<string> GetTypeShedPaths(string path) {
            var version = _factory.Configuration.Version;
            var stdlib = Path.Combine(path, "stdlib");
            var thirdParty = Path.Combine(path, "third_party");

            foreach (var subdir in new[] { version.ToString(), version.Major.ToString(), "2and3" }) {
                var candidate = Path.Combine(stdlib, subdir);
                if (Directory.Exists(candidate)) {
                    yield return candidate;
                }
            }

            foreach (var subdir in new[] { version.ToString(), version.Major.ToString(), "2and3" }) {
                var candidate = Path.Combine(thirdParty, subdir);
                if (Directory.Exists(candidate)) {
                    yield return candidate;
                }
            }
        }

    }
}
