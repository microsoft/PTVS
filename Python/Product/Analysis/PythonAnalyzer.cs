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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Performs analysis of multiple Python code files and enables interrogation of the resulting analysis.
    /// </summary>
    public partial class PythonAnalyzer : IGroupableAnalysisProject, IDisposable {
        private readonly IPythonInterpreter _interpreter;
        private readonly bool _disposeInterpreter;
        private readonly IPythonInterpreterFactory _interpreterFactory;
        private readonly ModuleTable _modules;
        private readonly ConcurrentDictionary<string, ModuleInfo> _modulesByFilename;
        private readonly HashSet<ModuleInfo> _modulesWithUnresolvedImports;
        private readonly object _modulesWithUnresolvedImportsLock = new object();
        private IKnownPythonTypes _knownTypes;
        private readonly Dictionary<object, AnalysisValue> _itemCache;
        internal readonly string _builtinName;
        internal BuiltinModule _builtinModule;
#if DESKTOP
        private readonly ConcurrentDictionary<string, XamlProjectEntry> _xamlByFilename = new ConcurrentDictionary<string, XamlProjectEntry>();
#endif
        internal ConstantInfo _noneInst;
        private Action<int> _reportQueueSize;
        private int _reportQueueInterval;
        internal readonly IModuleContext _defaultContext;
        private readonly PythonLanguageVersion _langVersion;
        internal readonly AnalysisUnit _evalUnit;   // a unit used for evaluating when we don't otherwise have a unit available
        private readonly List<string> _searchPaths = new List<string>();
        private readonly List<string> _typeStubPaths = new List<string>();
        private readonly Dictionary<string, List<SpecializationInfo>> _specializationInfo = new Dictionary<string, List<SpecializationInfo>>();  // delayed specialization information, for modules not yet loaded...
        private AnalysisLimits _limits;
        private static object _nullKey = new object();
        private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);
        private Dictionary<IProjectEntry[], AggregateProjectEntry> _aggregates = new Dictionary<IProjectEntry[], AggregateProjectEntry>(AggregateComparer.Instance);
        private readonly Dictionary<IProjectEntry, Dictionary<Node, LanguageServer.Diagnostic>> _diagnostics = new Dictionary<IProjectEntry, Dictionary<Node, LanguageServer.Diagnostic>>();

        public const string PythonAnalysisSource = "Python (analysis)";

        /// <summary>
        /// Creates a new analyzer that is ready for use.
        /// </summary>
        public static async Task<PythonAnalyzer> CreateAsync(
            IPythonInterpreterFactory factory,
            IPythonInterpreter interpreter = null,
            CancellationToken token = default(CancellationToken)
        ) {
            var res = new PythonAnalyzer(factory, interpreter);
            try {
                await res.ReloadModulesAsync(token).ConfigureAwait(false);
                var r = res;
                res = null;
                return r;
            } finally {
                if (res != null) {
                    res.Dispose();
                }
            }
        }

        // Test helper method
        internal static PythonAnalyzer CreateSynchronously(
            IPythonInterpreterFactory factory,
            IPythonInterpreter interpreter = null
        ) {
            var res = new PythonAnalyzer(factory, interpreter);
            try {
                res.ReloadModulesAsync(CancellationToken.None).WaitAndUnwrapExceptions();
                var r = res;
                res = null;
                return r;
            } finally {
                if (res != null) {
                    res.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates a new analyzer that is not ready for use. You must call and
        /// wait for <see cref="ReloadModulesAsync"/> to complete before using.
        /// </summary>
        public static PythonAnalyzer Create(IPythonInterpreterFactory factory, IPythonInterpreter interpreter = null) {
            return new PythonAnalyzer(factory, interpreter);
        }

        internal PythonAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreter pythonInterpreter) {
            _interpreterFactory = factory;
            _langVersion = factory.GetLanguageVersion();
            _disposeInterpreter = pythonInterpreter == null;
            _interpreter = pythonInterpreter ?? factory.CreateInterpreter();
            _builtinName = BuiltinTypeId.Unknown.GetModuleName(_langVersion);
            _modules = new ModuleTable(this, _interpreter);
            _modulesByFilename = new ConcurrentDictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
            _modulesWithUnresolvedImports = new HashSet<ModuleInfo>();
            _itemCache = new Dictionary<object, AnalysisValue>();

            Limits = AnalysisLimits.GetDefaultLimits();

            Queue = new Deque<AnalysisUnit>();

            _defaultContext = _interpreter.CreateModuleContext();

            _evalUnit = new AnalysisUnit(null, null, new ModuleInfo("$global", new ProjectEntry(this, "$global", String.Empty, null, null), _defaultContext).Scope, true);
            AnalysisLog.NewUnit(_evalUnit);
        }

        private async Task LoadKnownTypesAsync(CancellationToken token) {
            _itemCache.Clear();

            var fallback = new FallbackBuiltinModule(_langVersion);

            var moduleRef = await Modules.TryImportAsync(_builtinName, token).ConfigureAwait(false);
            if (moduleRef != null) {
                _builtinModule = (BuiltinModule)moduleRef.Module;
            } else {
                _builtinModule = new BuiltinModule(fallback, this);
                Modules[_builtinName] = new ModuleReference(_builtinModule, _builtinName);
            }
            _builtinModule.InterpreterModule.Imported(_defaultContext);

            Modules.AddBuiltinModuleWrapper("sys", SysModuleInfo.Wrap);
            Modules.AddBuiltinModuleWrapper("typing", TypingModuleInfo.Wrap);

            _knownTypes = KnownTypes.Create(this, fallback);

            _noneInst = (ConstantInfo)GetCached(
                _nullKey,
                () => new ConstantInfo(ClassInfos[BuiltinTypeId.NoneType], null, PythonMemberType.Constant)
            );

            AddBuiltInSpecializations();
        }

        /// <summary>
        /// Reloads the modules from the interpreter.
        /// 
        /// This method should be called on the analysis thread and is usually invoked
        /// when the interpreter signals that it's modules have changed.
        /// </summary>
        public async Task ReloadModulesAsync(CancellationToken token = default(CancellationToken)) {
            if (!_reloadLock.Wait(0)) {
                // If we don't lock immediately, wait for the current reload to
                // complete and then return.
                await _reloadLock.WaitAsync().ConfigureAwait(false);
                _reloadLock.Release();
                return;
            }

            try {
                _interpreterFactory.NotifyImportNamesChanged();
                _modules.ReInit();
                _interpreter.Initialize(this);

                await LoadKnownTypesAsync(token);

                foreach (var mod in _modulesByFilename.Values) {
                    mod.Clear();
                    mod.EnsureModuleVariables(this);
                }
            } finally {
                _reloadLock.Release();
            }
        }

        #region Public API

        public PythonLanguageVersion LanguageVersion {
            get {
                return _langVersion;
            }
        }

        /// <summary>
        /// Adds a new module of code to the list of available modules and returns a ProjectEntry object.
        /// 
        /// This method is thread safe.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="cookie">An application-specific identifier for the module</param>
        /// <returns>The project entry for the new module.</returns>
        public IPythonProjectEntry AddModule(string moduleName, string filePath, Uri documentUri = null, IAnalysisCookie cookie = null) {
            var entry = new ProjectEntry(this, moduleName, filePath, documentUri, cookie);

            if (moduleName != null) {
                var moduleRef = Modules.GetOrAdd(moduleName);
                moduleRef.Module = entry.MyScope;

                DoDelayedSpecialization(moduleName);
            }
            if (filePath != null) {
                _modulesByFilename[filePath] = entry.MyScope;
            }
            return entry;
        }

        /// <summary>
        /// Associates an existing module with a new name.
        /// </summary>
        /// <remarks>New in 2.1</remarks>
        public void AddModuleAlias(string moduleName, string moduleAlias) {
            ModuleReference modRef;
            if (Modules.TryImport(moduleName, out modRef)) {
                Modules[moduleAlias] = modRef;
            }
        }

        public void RemoveModule(IProjectEntry entry) => RemoveModule(entry, null);

        /// <summary>
        /// Removes the specified project entry from the current analysis.
        /// 
        /// This method is thread safe.
        /// </summary>
        /// <param name="entry">The entry to remove.</param>
        /// <param name="onImporter">Action to perform on each module that
        /// had imported the one being removed.</param>
        public void RemoveModule(IProjectEntry entry, Action<IPythonProjectEntry> onImporter) {
            if (entry == null) {
                throw new ArgumentNullException(nameof(entry));
            }
            Contract.EndContractBlock();

            var pyEntry = entry as IPythonProjectEntry;
            IPythonProjectEntry[] importers = null;
            if (!string.IsNullOrEmpty(pyEntry?.ModuleName)) {
                importers = GetEntriesThatImportModule(pyEntry.ModuleName, false).ToArray();
            }

            if (!string.IsNullOrEmpty(entry.FilePath) && _modulesByFilename.TryRemove(entry.FilePath, out var moduleInfo)) {
                lock (_modulesWithUnresolvedImportsLock) {
                    _modulesWithUnresolvedImports.Remove(moduleInfo);
                }
            }

            entry.RemovedFromProject();
            ClearDiagnostics(entry);

            if (onImporter == null) {
                onImporter = e => e.Analyze(CancellationToken.None, enqueueOnly: true);
            }

            if (!string.IsNullOrEmpty(pyEntry?.ModuleName)) {
                Modules.TryRemove(pyEntry.ModuleName, out var _);
                foreach (var e in importers.MaybeEnumerate()) {
                    onImporter(e);
                }
            }
        }

#if DESKTOP
        /// <summary>
        /// Adds a XAML file to be analyzed.  
        /// 
        /// This method is thread safe.
        /// </summary>
        internal IXamlProjectEntry AddXamlFile(string filePath, IAnalysisCookie cookie = null) {
            var entry = new XamlProjectEntry(filePath);

            _xamlByFilename[filePath] = entry;

            return entry;
        }
#endif

        /// <summary>
        /// Returns a sequence of project entries that import the specified
        /// module. The sequence will be empty if the module is unknown.
        /// </summary>
        /// <param name="moduleName">
        /// The absolute name of the module. This should never end with
        /// '__init__'.
        /// </param>
        public IEnumerable<IPythonProjectEntry> GetEntriesThatImportModule(string moduleName, bool includeUnresolved) {
            ModuleReference modRef;
            var entries = new List<IPythonProjectEntry>();
            if (_modules.TryImport(moduleName, out modRef) && modRef.HasReferences) {
                entries.AddRange(modRef.References.Select(m => m.ProjectEntry).OfType<IPythonProjectEntry>());
            }

            if (includeUnresolved) {
                // Have to iterate over modules with unresolved imports to find
                // ephemeral references.
                lock (_modulesWithUnresolvedImportsLock) {
                    foreach (var module in _modulesWithUnresolvedImports) {
                        if (module.GetAllUnresolvedModules().Contains(moduleName)) {
                            entries.Add(module.ProjectEntry);
                        }
                    }
                }
            }

            return entries;
        }

        /// <summary>
        /// Returns a sequence of absolute module names that, if available,
        /// would resolve one or more unresolved references.
        /// </summary>
        internal ISet<string> GetAllUnresolvedModuleNames() {
            var set = new HashSet<string>(StringComparer.Ordinal);
            lock (_modulesWithUnresolvedImportsLock) {
                foreach (var module in _modulesWithUnresolvedImports) {
                    set.UnionWith(module.GetAllUnresolvedModules());
                }
            }
            return set;
        }

        internal void ModuleHasUnresolvedImports(ModuleInfo module, bool hasUnresolvedImports) {
            lock (_modulesWithUnresolvedImportsLock) {
                if (hasUnresolvedImports) {
                    _modulesWithUnresolvedImports.Add(module);
                } else {
                    _modulesWithUnresolvedImports.Remove(module);
                }
            }
        }

        /// <summary>
        /// Returns true if a module has been imported.
        /// </summary>
        /// <param name="importFrom">
        /// The entry of the module doing the import. If null, the module name
        /// is resolved as an absolute name.
        /// </param>
        /// <param name="relativeModuleName">
        /// The absolute or relative name of the module. If a relative name is 
        /// passed here, <paramref name="importFrom"/> must be provided.
        /// </param>
        /// <param name="absoluteImports">
        /// True if Python 2.6/3.x style imports should be used.
        /// </param>
        /// <returns>
        /// True if the module was imported during analysis; otherwise, false.
        /// </returns>
        public bool IsModuleResolved(IPythonProjectEntry importFrom, string relativeModuleName, bool absoluteImports) {
            var unresolved = importFrom.GetModuleInfo()?.GetAllUnresolvedModules();
            if (unresolved == null || unresolved.Count == 0) {
                return true;
            }
            var names = ModuleResolver.ResolvePotentialModuleNames(importFrom, relativeModuleName, absoluteImports);
            return names.All(n => !unresolved.Contains(n));
        }

        /// <summary>
        /// Gets a top-level list of all the available modules as a list of MemberResults.
        /// </summary>
        /// <returns></returns>
        public MemberResult[] GetModules() {
            var d = new Dictionary<string, List<ModuleLoadState>>();
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (string.IsNullOrWhiteSpace(modName) || modName.Contains(".")) {
                    continue;
                }

                if (moduleRef.IsValid) {
                    List<ModuleLoadState> l;
                    if (!d.TryGetValue(modName, out l)) {
                        d[modName] = l = new List<ModuleLoadState>();
                    }
                    if (moduleRef.HasModule) {
                        // The REPL shows up here with value=None
                        l.Add(moduleRef);
                    }
                }
            }

            return ModuleDictToMemberResult(d);
        }

        private static MemberResult[] ModuleDictToMemberResult(Dictionary<string, List<ModuleLoadState>> d) {
            var result = new MemberResult[d.Count];
            int pos = 0;
            foreach (var kvp in d) {
                var lazyEnumerator = new LazyModuleEnumerator(kvp.Value);
                result[pos++] = new MemberResult(
                    kvp.Key,
                    lazyEnumerator.GetLazyModules,
                    lazyEnumerator.GetModuleType
                );
            }
            return result;
        }

        class LazyModuleEnumerator {
            private readonly List<ModuleLoadState> _loaded;

            public LazyModuleEnumerator(List<ModuleLoadState> loaded) {
                _loaded = loaded;
            }

            public IEnumerable<AnalysisValue> GetLazyModules() {
                foreach (var value in _loaded) {
                    yield return new SyntheticDefinitionInfo(
                        value.Name,
                        null,
                        string.IsNullOrEmpty(value.MaybeSourceFile) ?
                            Enumerable.Empty<LocationInfo>() :
                            new[] { new LocationInfo(value.MaybeSourceFile, null, 0, 0) }
                    );
                }
            }

            public PythonMemberType GetModuleType() {
                PythonMemberType? type = null;
                foreach (var value in _loaded) {
                    if (type == null) {
                        type = value.MemberType;
                    } else if (type != value.MemberType) {
                        type = PythonMemberType.Multiple;
                        break;
                    }
                }
                return type ?? PythonMemberType.Unknown;
            }
        }

        /// <summary>
        /// Searches all modules which match the given name and searches in the modules
        /// for top-level items which match the given name.  Returns a list of all the
        /// available names fully qualified to their name.  
        /// </summary>
        /// <param name="name"></param>
        public IEnumerable<ExportedMemberInfo> FindNameInAllModules(string name) {
            string pkgName;

            if (_interpreter is ICanFindModuleMembers finder) {
                foreach (var modName in finder.GetModulesNamed(name)) {
                    int dot = modName.LastIndexOf('.');
                    if (dot < 0) {
                        yield return new ExportedMemberInfo(null, modName);
                    } else {
                        yield return new ExportedMemberInfo(modName.Remove(dot), modName.Substring(dot + 1));
                    }
                }

                foreach (var modName in finder.GetModulesContainingName(name)) {
                    yield return new ExportedMemberInfo(modName, name);
                }

                // Scan added modules directly
                foreach (var mod in _modulesByFilename.Values) {
                    if (mod.Name == name) {
                        yield return new ExportedMemberInfo(null, mod.Name);
                    } else if (GetPackageNameIfMatch(name, mod.Name, out pkgName)) {
                        yield return new ExportedMemberInfo(pkgName, name);
                    }

                    if (mod.IsMemberDefined(_defaultContext, name)) {
                        yield return new ExportedMemberInfo(mod.Name, name);
                    }
                }

                yield break;
            }

            // provide module names first
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.IsValid) {
                    // include modules which can be imported
                    if (modName == name) {
                        yield return new ExportedMemberInfo(null, modName);
                    } else if (GetPackageNameIfMatch(name, modName, out pkgName)) {
                        yield return new ExportedMemberInfo(pkgName, name);
                    }
                }
            }

            foreach (var modName in _interpreter.GetModuleNames()) {
                if (modName == name) {
                    yield return new ExportedMemberInfo(null, modName);
                } else if (GetPackageNameIfMatch(name, modName, out pkgName)) {
                    yield return new ExportedMemberInfo(pkgName, name);
                }
            }

            // then include imported module members
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.IsValid && moduleRef.ModuleContainsMember(_defaultContext, name)) {
                    yield return new ExportedMemberInfo(modName, name);
                }
            }
        }

        private static bool GetPackageNameIfMatch(string name, string fullName, out string packageName) {
            int lastDot = fullName.LastIndexOf('.');
            if (lastDot < 0) {
                packageName = null;
                return false;
            }

            packageName = fullName.Remove(lastDot);
            return String.Compare(fullName, lastDot + 1, name, 0, name.Length, StringComparison.Ordinal) == 0;
        }

        /// <summary>
        /// Returns the interpreter that the analyzer is using.
        /// 
        /// This property is thread safe.
        /// </summary>
        public IPythonInterpreter Interpreter {
            get {
                return _interpreter;
            }
        }

        /// <summary>
        /// Returns the interpreter factory that the analyzer is using.
        /// </summary>
        public IPythonInterpreterFactory InterpreterFactory {
            get {
                return _interpreterFactory;
            }
        }

        /// <summary>
        /// returns the MemberResults associated with modules in the specified
        /// list of names.  The list of names is the path through the module, for example
        /// ['System', 'Runtime']
        /// </summary>
        /// <returns></returns>
        public MemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers = false) {
            ModuleReference moduleRef;
            if (Modules.TryImport(names[0], out moduleRef)) {
                var module = moduleRef.Module as IModule;
                if (module != null) {
                    return GetModuleMembers(moduleContext, names, includeMembers, module);
                }

            }

            return new MemberResult[0];
        }

        internal static MemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers, IModule module) {
            for (int i = 1; i < names.Length && module != null; i++) {
                module = module.GetChildPackage(moduleContext, names[i]);
            }

            if (module != null) {
                var result = new Dictionary<string, List<IAnalysisSet>>();
                if (includeMembers) {
                    foreach (var keyValue in module.GetAllMembers(moduleContext)) {
                        if (!result.TryGetValue(keyValue.Key, out var results)) {
                            result[keyValue.Key] = results = new List<IAnalysisSet>();
                        }
                        results.Add(keyValue.Value);
                    }
                    return MemberDictToMemberResult(result);
                } else {
                    foreach (var child in module.GetChildrenPackages(moduleContext)) {
                        if (!result.TryGetValue(child.Key, out var results)) {
                            result[child.Key] = results = new List<IAnalysisSet>();
                        }
                        results.Add(child.Value);
                    }
                    foreach (var keyValue in module.GetAllMembers(moduleContext)) {
                        bool anyModules = false;
                        foreach (var ns in keyValue.Value.OfType<MultipleMemberInfo>()) {
                            if (ns.Members.OfType<IModule>().Any(mod => !(mod is MultipleMemberInfo))) {
                                anyModules = true;
                                break;
                            }
                        }
                        if (anyModules) {
                            if (!result.TryGetValue(keyValue.Key, out var results)) {
                                result[keyValue.Key] = results = new List<IAnalysisSet>();
                            }
                            results.Add(keyValue.Value);
                        }
                    }
                    return MemberDictToMemberResult(result);
                }
            }
            return new MemberResult[0];
        }

        private static MemberResult[] MemberDictToMemberResult(Dictionary<string, List<IAnalysisSet>> results) {
            return results.Select(r => new MemberResult(r.Key, r.Value.SelectMany())).ToArray();
        }


        /// <summary>
        /// Gets the list of directories which should be analyzed.
        /// 
        /// This property is thread safe.
        /// </summary>
        public IEnumerable<string> AnalysisDirectories => _searchPaths.AsLockedEnumerable().ToArray();

        /// <summary>
        /// Gets the list of directories which should be searched for type stubs.
        /// 
        /// This property is thread safe.
        /// </summary>
        public IEnumerable<string> TypeStubDirectories => _typeStubPaths.AsLockedEnumerable().ToArray();

        public AnalysisLimits Limits {
            get { return _limits; }
            set { _limits = value; }
        }

        public bool EnableDiagnostics { get; set; }

        public void AddDiagnostic(Node node, AnalysisUnit unit, string message, LanguageServer.DiagnosticSeverity severity, string code = null) {
            if (!EnableDiagnostics) {
                return;
            }

            lock (_diagnostics) {
                if (!_diagnostics.TryGetValue(unit.ProjectEntry, out var diags)) {
                    _diagnostics[unit.ProjectEntry] = diags = new Dictionary<Node, LanguageServer.Diagnostic>();
                }
                diags[node] = new LanguageServer.Diagnostic {
                    message = message,
                    range = node.GetSpan(unit.ProjectEntry.Tree),
                    severity = severity,
                    code = code,
                    source = PythonAnalysisSource
                };
            }
        }

        public IReadOnlyList<LanguageServer.Diagnostic> GetDiagnostics(IProjectEntry entry) {
            lock (_diagnostics) {
                if (_diagnostics.TryGetValue(entry, out var diags)) {
                    return diags.OrderBy(kv => kv.Key.StartIndex).Select(kv => kv.Value).ToArray();
                }
            }
            return Array.Empty<LanguageServer.Diagnostic>();
        }

        public IReadOnlyDictionary<IProjectEntry, IReadOnlyList<LanguageServer.Diagnostic>> GetAllDiagnostics() {
            var res = new Dictionary<IProjectEntry, IReadOnlyList<LanguageServer.Diagnostic>>();
            lock (_diagnostics) {
                foreach (var kv in _diagnostics) {
                    res[kv.Key] = kv.Value.OrderBy(d => d.Key.StartIndex).Select(d => d.Value).ToArray();
                }
            }
            return res;
        }

        public void ClearDiagnostic(Node node, AnalysisUnit unit, string code = null) {
            if (!EnableDiagnostics) {
                return;
            }

            lock (_diagnostics) {
                if (_diagnostics.TryGetValue(unit.ProjectEntry, out var diags) && diags.TryGetValue(node, out var d)) {
                    if (code == null || d.code == code) {
                        diags.Remove(node);
                    }
                }
            }
        }

        public void ClearDiagnostics(IProjectEntry entry) {
            lock (_diagnostics) {
                _diagnostics.Remove(entry);
            }
        }
        #endregion

        #region Internal Implementation

        internal IKnownPythonTypes Types {
            get {
                if (_knownTypes != null) {
                    return _knownTypes;
                }
                throw new InvalidOperationException("Analyzer has not been initialized. Call ReloadModulesAsync() first.");
            }
        }

        internal IKnownClasses ClassInfos {
            get {
                if (_knownTypes != null) {
                    return (IKnownClasses)_knownTypes;
                }
                throw new InvalidOperationException("Analyzer has not been initialized. Call ReloadModulesAsync() first.");
            }
        }

        internal Deque<AnalysisUnit> Queue { get; }

        /// <summary>
        /// Returns the cached value for the provided key, creating it with
        /// <paramref name="maker"/> if necessary. If <paramref name="maker"/>
        /// attempts to get the same value, returns <c>null</c>.
        /// </summary>
        /// <param name="key">The identifier for the cached value.</param>
        /// <param name="maker">Function to create the value.</param>
        /// <returns>The cached value or <c>null</c>.</returns>
        internal AnalysisValue GetCached(object key, Func<AnalysisValue> maker) {
            AnalysisValue result;
            if (!_itemCache.TryGetValue(key, out result)) {
                // Set the key to prevent recursion
                _itemCache[key] = null;
                _itemCache[key] = result = maker();
            }
            return result;
        }

        internal BuiltinModule BuiltinModule {
            get { return _builtinModule; }
        }

        internal BuiltinInstanceInfo GetInstance(IPythonType type) {
            return GetBuiltinType(type).Instance;
        }

        internal BuiltinClassInfo GetBuiltinType(IPythonType type) {
            return (BuiltinClassInfo)GetCached(type,
                () => MakeBuiltinType(type)
            ) ?? ClassInfos[BuiltinTypeId.Object];
        }

        private BuiltinClassInfo MakeBuiltinType(IPythonType type) {
            switch (type.TypeId) {
                case BuiltinTypeId.List: return new ListBuiltinClassInfo(type, this);
                case BuiltinTypeId.Tuple: return new TupleBuiltinClassInfo(type, this);
                case BuiltinTypeId.Object: return new ObjectBuiltinClassInfo(type, this);
                case BuiltinTypeId.Dict: return new DictBuiltinClassInfo(type, this);
                default: return new BuiltinClassInfo(type, this);
            }
        }

        internal IAnalysisSet GetAnalysisSetFromObjects(object objects) {
            var typeList = objects as IEnumerable<object>;
            if (typeList == null) {
                return AnalysisSet.Empty;
            }
            return AnalysisSet.UnionAll(typeList.Select(GetAnalysisValueFromObjects));
        }

        internal IAnalysisSet GetAnalysisSetFromObjects(IEnumerable<IPythonType> typeList) {
            if (typeList == null) {
                return AnalysisSet.Empty;
            }
            return AnalysisSet.UnionAll(typeList.Select(GetAnalysisValueFromObjects));
        }

        internal AnalysisValue GetAnalysisValueFromObjectsThrowOnNull(object attr) {
            if (attr == null) {
                throw new ArgumentNullException("attr");
            }
            return GetAnalysisValueFromObjects(attr);
        }

        internal AnalysisValue GetAnalysisValueFromObjects(object attr) {
            if (attr == null) {
                return _noneInst;
            }

            var attrType = attr.GetType();
            if (attr is IPythonType pt) {
                return GetBuiltinType(pt);
            } else if (attr is IPythonFunction pf) {
                return GetCached(attr, () => new BuiltinFunctionInfo(pf, this)) ?? _noneInst;
            } else if (attr is IPythonMethodDescriptor md) {
                return GetCached(attr, () => {
                    if (md.IsBound) {
                        return new BuiltinFunctionInfo(md.Function, this);
                    } else {
                        return new BuiltinMethodInfo(md, this);
                    }
                }) ?? _noneInst;
            } else if (attr is IPythonBoundFunction pbf) {
                return GetCached(attr, () => new BoundBuiltinMethodInfo(pbf, this)) ?? _noneInst;
            } else if (attr is IBuiltinProperty bp) {
                return GetCached(attr, () => new BuiltinPropertyInfo(bp, this)) ?? _noneInst;
            } else if (attr is IPythonModule pm) {
                return _modules.GetBuiltinModule(pm);
            } else if (attr is IPythonEvent pe) {
                return GetCached(attr, () => new BuiltinEventInfo(pe, this)) ?? _noneInst;
            } else if (attr is IPythonConstant ||
                       attrType == typeof(bool) || attrType == typeof(int) || attrType == typeof(Complex) ||
                       attrType == typeof(string) || attrType == typeof(long) || attrType == typeof(double)) {
                return GetConstant(attr).First();
            } else if (attr is IMemberContainer mc) {
                return GetCached(attr, () => new ReflectedNamespace(mc, this));
            } else if (attr is IPythonMultipleMembers mm) {
                var members = mm.Members;
                return GetCached(attr, () =>
                    MultipleMemberInfo.Create(members.Select(GetAnalysisValueFromObjects)).FirstOrDefault() ??
                        ClassInfos[BuiltinTypeId.NoneType].Instance
                );
            } else {
                var pyAttrType = GetTypeFromObject(attr);
                Debug.Assert(pyAttrType != null);
                return GetBuiltinType(pyAttrType).Instance;
            }
        }

        internal IDictionary<string, IAnalysisSet> GetAllMembers(IMemberContainer container, IModuleContext moduleContext) {
            var names = container.GetMemberNames(moduleContext);
            var result = new Dictionary<string, IAnalysisSet>();
            foreach (var name in names) {
                result[name] = GetAnalysisValueFromObjects(container.GetMember(moduleContext, name));
            }
            var children = (container as IModule)?.GetChildrenPackages(moduleContext);
            if (children?.Any() ?? false) {
                foreach (var child in children) {
                    IAnalysisSet existing;
                    if (result.TryGetValue(child.Key, out existing)) {
                        result[child.Key] = existing.Add(child.Value);
                    } else {
                        result[child.Key] = child.Value;
                    }
                }
            }

            return result;
        }

        internal ModuleTable Modules {
            get { return _modules; }
        }

        internal ConcurrentDictionary<string, ModuleInfo> ModulesByFilename {
            get { return _modulesByFilename; }
        }

        public bool TryGetProjectEntryByPath(string path, out IProjectEntry projEntry) {
            ModuleInfo modInfo;
            if (_modulesByFilename.TryGetValue(path, out modInfo)) {
                projEntry = modInfo.ProjectEntry;
                return true;
            }

            projEntry = null;
            return false;
        }

        internal IAnalysisSet GetConstant(object value) {
            object key = value ?? _nullKey;
            return GetCached(key, () => {
                var constant = value as IPythonConstant;
                var constantType = constant?.Type;
                var av = GetAnalysisValueFromObjectsThrowOnNull(constantType ?? GetTypeFromObject(value));

                if (av is ConstantInfo ci) {
                    return ci;
                }

                if (av is BuiltinClassInfo bci) {
                    if (constant == null) {
                        return new ConstantInfo(bci, value, PythonMemberType.Constant);
                    }
                    return bci.Instance;
                }
                return _noneInst;
            }) ?? _noneInst;
        }

        private static void Update<K, V>(IDictionary<K, V> dict, IDictionary<K, V> newValues) {
            foreach (var kvp in newValues) {
                dict[kvp.Key] = kvp.Value;
            }
        }

        internal IPythonType GetTypeFromObject(object value) {
            if (value == null) {
                return Types[BuiltinTypeId.NoneType];
            }

            var astConst = value as IPythonConstant;
            if (astConst != null) {
                return Types[astConst.Type?.TypeId ?? BuiltinTypeId.Object] ?? Types[BuiltinTypeId.Object];
            }

            switch (Type.GetTypeCode(value.GetType())) {
                case TypeCode.Boolean: return Types[BuiltinTypeId.Bool];
                case TypeCode.Double: return Types[BuiltinTypeId.Float];
                case TypeCode.Int32: return Types[BuiltinTypeId.Int];
                case TypeCode.String: return Types[BuiltinTypeId.Unicode];
                case TypeCode.Object:
                    if (value.GetType() == typeof(Complex)) {
                        return Types[BuiltinTypeId.Complex];
                    } else if (value.GetType() == typeof(AsciiString)) {
                        return Types[BuiltinTypeId.Bytes];
                    } else if (value.GetType() == typeof(BigInteger)) {
                        return Types[BuiltinTypeId.Long];
                    } else if (value.GetType() == typeof(Ellipsis)) {
                        return Types[BuiltinTypeId.Ellipsis];
                    }
                    break;
            }

            Debug.Fail("unsupported constant type <{0}> value '{1}'".FormatInvariant(value.GetType().FullName, value));
            return Types[BuiltinTypeId.Object];
        }

        internal BuiltinClassInfo MakeGenericType(IAdvancedPythonType clrType, params IPythonType[] clrIndexType) {
            var res = clrType.MakeGenericType(clrIndexType);

            return (BuiltinClassInfo)GetAnalysisValueFromObjects(res);
        }

        #endregion

        #region IGroupableAnalysisProject Members

        public void AnalyzeQueuedEntries(CancellationToken cancel) {
            if (cancel.IsCancellationRequested) {
                return;
            }

            if (_builtinModule == null) {
                Debug.Fail("Used analyzer without reloading modules");
                ReloadModulesAsync(cancel).WaitAndUnwrapExceptions();
            }

            var ddg = new DDG();
            ddg.Analyze(Queue, cancel, _reportQueueSize, _reportQueueInterval);
            foreach (var entry in ddg.AnalyzedEntries) {
                entry.RaiseOnNewAnalysis();
            }
        }

        #endregion

        /// <summary>
        /// Specifies a callback to invoke to provide feedback on the number of
        /// items being processed.
        /// </summary>
        public void SetQueueReporting(Action<int> reportFunction, int interval = 1) {
            _reportQueueSize = reportFunction;
            _reportQueueInterval = interval;
        }

        public IReadOnlyList<string> GetSearchPaths() => _searchPaths.AsLockedEnumerable().ToArray();

        /// <summary>
        /// Sets the search paths for this analyzer, invoking callbacks for any
        /// path added or removed.
        /// </summary>
        public void SetSearchPaths(IEnumerable<string> paths) {
            lock (_searchPaths) {
                _searchPaths.Clear();
                _searchPaths.AddRange(paths.MaybeEnumerate());
            }
            SearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        public IReadOnlyList<string> GetTypeStubPaths() => _typeStubPaths.AsLockedEnumerable().ToArray();

        /// <summary>
        /// Sets the type stub search paths for this analyzer, invoking callbacks for any
        /// path added or removed.
        /// </summary>
        public void SetTypeStubPaths(IEnumerable<string> paths) {
            lock (_typeStubPaths) {
                _typeStubPaths.Clear();
                _typeStubPaths.AddRange(paths.MaybeEnumerate());
            }
            SearchPathsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Event fired when the analysis directories have changed.  
        /// 
        /// This event can be fired on any thread.
        /// 
        /// New in 1.1.
        /// </summary>
        public event EventHandler SearchPathsChanged;

        #region IDisposable Members

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                IDisposable interpreter = _interpreter as IDisposable;
                if (_disposeInterpreter && interpreter != null) {
                    interpreter.Dispose();
                }
                // Try and acquire the lock before disposing. This helps avoid
                // some (non-fatal) exceptions.
                try {
                    _reloadLock.Wait(TimeSpan.FromSeconds(10));
                    _reloadLock.Dispose();
                } catch (ObjectDisposedException) {
                }
            }
        }

        ~PythonAnalyzer() {
            Dispose(false);
        }

        #endregion

        internal AggregateProjectEntry GetAggregate(params IProjectEntry[] aggregating) {
            Debug.Assert(new HashSet<IProjectEntry>(aggregating).Count == aggregating.Length);

            SortAggregates(aggregating);

            return GetAggregateWorker(aggregating);
        }

        private static void SortAggregates(IProjectEntry[] aggregating) {
            Array.Sort(aggregating, (x, y) => x.GetHashCode() - y.GetHashCode());
        }

        internal AggregateProjectEntry GetAggregate(HashSet<IProjectEntry> from, IProjectEntry with) {
            Debug.Assert(!from.Contains(with));

            IProjectEntry[] all = new IProjectEntry[from.Count + 1];
            from.CopyTo(all);
            all[from.Count] = with;

            SortAggregates(all);

            return GetAggregateWorker(all);
        }

        internal void ClearAggregate(AggregateProjectEntry entry) {
            var aggregating = entry._aggregating.ToArray();
            SortAggregates(aggregating);

            _aggregates.Remove(aggregating);
        }

        private AggregateProjectEntry GetAggregateWorker(IProjectEntry[] all) {
            AggregateProjectEntry agg;
            if (!_aggregates.TryGetValue(all, out agg)) {
                _aggregates[all] = agg = new AggregateProjectEntry(new HashSet<IProjectEntry>(all));

                foreach (var proj in all) {
                    IAggregateableProjectEntry aggretable = proj as IAggregateableProjectEntry;
                    if (aggretable != null) {
                        aggretable.AggregatedInto(agg);
                    }
                }
            }

            return agg;
        }

        class AggregateComparer : IEqualityComparer<IProjectEntry[]> {
            public static AggregateComparer Instance = new AggregateComparer();

            public bool Equals(IProjectEntry[] x, IProjectEntry[] y) {
                if (x.Length != y.Length) {
                    return false;
                }
                for (int i = 0; i < x.Length; i++) {
                    if (x[i] != y[i]) {
                        return false;
                    }
                }
                return true;
            }

            public int GetHashCode(IProjectEntry[] obj) {
                int res = 0;
                for (int i = 0; i < obj.Length; i++) {
                    res ^= obj[i].GetHashCode();
                }
                return res;
            }
        }

    }
}
