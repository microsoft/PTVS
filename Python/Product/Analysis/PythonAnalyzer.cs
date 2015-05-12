/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.PyAnalysis;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;

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
        private readonly Dictionary<object, AnalysisValue> _itemCache;
        private readonly string _builtinName;
        internal BuiltinModule _builtinModule;
        private readonly ConcurrentDictionary<string, XamlProjectEntry> _xamlByFilename = new ConcurrentDictionary<string, XamlProjectEntry>();
        internal ConstantInfo _noneInst;
        private readonly Deque<AnalysisUnit> _queue;
        private Action<int> _reportQueueSize;
        private int _reportQueueInterval;
        internal readonly IModuleContext _defaultContext;
        private readonly PythonLanguageVersion _langVersion;
        internal readonly AnalysisUnit _evalUnit;   // a unit used for evaluating when we don't otherwise have a unit available
        private readonly HashSet<string> _analysisDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<SpecializationInfo>> _specializationInfo = new Dictionary<string, List<SpecializationInfo>>();  // delayed specialization information, for modules not yet loaded...
        private AnalysisLimits _limits;
        private static object _nullKey = new object();
        private readonly SemaphoreSlim _reloadLock = new SemaphoreSlim(1, 1);

        private const string AnalysisLimitsKey = @"Software\Microsoft\PythonTools\" + AssemblyVersionInfo.VSVersion +
            @"\Analysis\Project";

        /// <summary>
        /// Creates a new analyzer that is ready for use.
        /// </summary>
        public static async Task<PythonAnalyzer> CreateAsync(
            IPythonInterpreterFactory factory,
            IPythonInterpreter interpreter = null
        ) {
            var res = new PythonAnalyzer(factory, interpreter, null);
            try {
                await res.ReloadModulesAsync().ConfigureAwait(false);
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
            IPythonInterpreter interpreter = null,
            string builtinName = null
        ) {
            var res = new PythonAnalyzer(factory, interpreter, null);
            try {
                res.ReloadModulesAsync().WaitAndUnwrapExceptions();
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
            return new PythonAnalyzer(factory, interpreter, null);
        }

        [Obsolete("Use CreateAsync instead")]
        public PythonAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreter interpreter = null)
            : this(factory, interpreter, null) {
            ReloadModulesAsync().WaitAndUnwrapExceptions();
        }

        internal PythonAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreter pythonInterpreter, string builtinName) {
            _interpreterFactory = factory;
            _langVersion = factory.GetLanguageVersion();
            _disposeInterpreter = pythonInterpreter == null;
            _interpreter = pythonInterpreter ?? factory.CreateInterpreter();
            _builtinName = builtinName ?? (_langVersion.Is3x() ? SharedDatabaseState.BuiltinName3x : SharedDatabaseState.BuiltinName2x);
            _modules = new ModuleTable(this, _interpreter);
            _modulesByFilename = new ConcurrentDictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
            _modulesWithUnresolvedImports = new HashSet<ModuleInfo>();
            _itemCache = new Dictionary<object, AnalysisValue>();

            try {
                using (var key = Registry.CurrentUser.OpenSubKey(AnalysisLimitsKey)) {
                    Limits = AnalysisLimits.LoadFromStorage(key);
                }
            } catch (SecurityException) {
                Limits = new AnalysisLimits();
            } catch (UnauthorizedAccessException) {
                Limits = new AnalysisLimits();
            } catch (IOException) {
                Limits = new AnalysisLimits();
            }

            _queue = new Deque<AnalysisUnit>();

            _defaultContext = _interpreter.CreateModuleContext();

            _evalUnit = new AnalysisUnit(null, null, new ModuleInfo("$global", new ProjectEntry(this, "$global", String.Empty, null), _defaultContext).Scope, true);
            AnalysisLog.NewUnit(_evalUnit);

            LoadInitialKnownTypes();
        }

        private void LoadInitialKnownTypes() {
            if (_builtinModule != null) {
                Debug.Fail("LoadInitialKnownTypes should only be called once");
                return;
            }

            var fallbackDb = PythonTypeDatabase.CreateDefaultTypeDatabase(LanguageVersion.ToVersion());
            _builtinModule = _modules.GetBuiltinModule(fallbackDb.GetModule(SharedDatabaseState.BuiltinName2x));

            FinishLoadKnownTypes(fallbackDb);
        }

        private async Task LoadKnownTypesAsync() {
            _itemCache.Clear();

            Debug.Assert(_builtinModule != null, "LoadInitialKnownTypes was not called");
            if (_builtinModule == null) {
                LoadInitialKnownTypes();
            }

            var moduleRef = await Modules.TryImportAsync(_builtinName).ConfigureAwait(false);
            if (moduleRef != null) {
                _builtinModule = (BuiltinModule)moduleRef.Module;
            } else {
                Modules[_builtinName] = new ModuleReference(_builtinModule);
            }

            FinishLoadKnownTypes(null);

            var sysModule = await _modules.TryImportAsync("sys").ConfigureAwait(false);
            if (sysModule != null) {
                var bm = sysModule.AnalysisModule as BuiltinModule;
                if (bm != null) {
                    sysModule.Module = new SysModuleInfo(bm);
                }
            }
        }

        private void FinishLoadKnownTypes(PythonTypeDatabase db) {
            _itemCache.Clear();

            if (db == null) {
                db = PythonTypeDatabase.CreateDefaultTypeDatabase(LanguageVersion.ToVersion());
                Types = KnownTypes.Create(this, db);
            } else {
                Types = KnownTypes.CreateDefault(this, db);
            }

            ClassInfos = (IKnownClasses)Types;
            _noneInst = (ConstantInfo)GetCached(_nullKey, () => new ConstantInfo(ClassInfos[BuiltinTypeId.NoneType], (object)null));

            DoNotUnionInMro = AnalysisSet.Create(new AnalysisValue[] {
                ClassInfos[BuiltinTypeId.Object],
                ClassInfos[BuiltinTypeId.Type]
            });

            AddBuiltInSpecializations();
        }

        /// <summary>
        /// Reloads the modules from the interpreter.
        /// 
        /// This method should be called on the analysis thread and is usually invoked
        /// when the interpreter signals that it's modules have changed.
        /// </summary>
        public async Task ReloadModulesAsync() {
            if (!_reloadLock.Wait(0)) {
                // If we don't lock immediately, wait for the current reload to
                // complete and then return.
                await _reloadLock.WaitAsync().ConfigureAwait(false);
                _reloadLock.Release();
                return;
            }

            try {
                _modules.ReInit();
                await LoadKnownTypesAsync().ConfigureAwait(false);

                _interpreter.Initialize(this);

                foreach (var mod in _modulesByFilename.Values) {
                    mod.Clear();
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
        public IPythonProjectEntry AddModule(string moduleName, string filePath, IAnalysisCookie cookie = null) {
            var entry = new ProjectEntry(this, moduleName, filePath, cookie);

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

        /// <summary>
        /// Removes the specified project entry from the current analysis.
        /// 
        /// This method is thread safe.
        /// </summary>
        public void RemoveModule(IProjectEntry entry) {
            if (entry == null) {
                throw new ArgumentNullException("entry");
            }
            Contract.EndContractBlock();

            ModuleInfo removed;
            _modulesByFilename.TryRemove(entry.FilePath, out removed);

            var pyEntry = entry as IPythonProjectEntry;
            if (pyEntry != null) {
                ModuleReference modRef;
                Modules.TryRemove(pyEntry.ModuleName, out modRef);
            }
            entry.RemovedFromProject();
        }

        /// <summary>
        /// Adds a XAML file to be analyzed.  
        /// 
        /// This method is thread safe.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="cookie"></param>
        /// <returns></returns>
        public IXamlProjectEntry AddXamlFile(string filePath, IAnalysisCookie cookie = null) {
            var entry = new XamlProjectEntry(filePath);

            _xamlByFilename[filePath] = entry;

            return entry;
        }

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
            ModuleReference moduleRef;
            return ResolvePotentialModuleNames(importFrom, relativeModuleName, absoluteImports)
                .Any(m => Modules.TryImport(m, out moduleRef));
        }

        /// <summary>
        /// Returns a sequence of candidate absolute module names for the given
        /// modules.
        /// </summary>
        /// <param name="projectEntry">
        /// The project entry that is importing the module.
        /// </param>
        /// <param name="relativeModuleName">
        /// A dotted name identifying the path to the module.
        /// </param>
        /// <returns>
        /// A sequence of strings representing the absolute names of the module
        /// in order of precedence.
        /// </returns>
        internal static IEnumerable<string> ResolvePotentialModuleNames(
            IPythonProjectEntry projectEntry,
            string relativeModuleName,
            bool absoluteImports
        ) {
            string importingFrom = null;
            if (projectEntry != null) {
                importingFrom = projectEntry.ModuleName;
                if (ModulePath.IsInitPyFile(projectEntry.FilePath)) {
                    if (string.IsNullOrEmpty(importingFrom)) {
                        importingFrom = "__init__";
                    } else {
                        importingFrom += ".__init__";
                    }
                }
            }

            if (string.IsNullOrEmpty(relativeModuleName)) {
                yield break;
            }

            // Handle relative module names
            if (relativeModuleName.FirstOrDefault() == '.') {
                if (string.IsNullOrEmpty(importingFrom)) {
                    // No source to import relative to.
                    yield break;
                }

                var prefix = importingFrom.Split('.');

                if (relativeModuleName.LastOrDefault() == '.') {
                    // Last part empty means the whole name is dots, so there's
                    // nothing to concatenate.
                    yield return string.Join(".", prefix.Take(prefix.Length - relativeModuleName.Length));
                } else {
                    var suffix = relativeModuleName.Split('.');
                    var dotCount = suffix.TakeWhile(bit => string.IsNullOrEmpty(bit)).Count();
                    if (dotCount < prefix.Length) {
                        // If we have as many dots as prefix parts, the entire
                        // name will disappear. Despite what PEP 328 says, in
                        // reality this means the import will fail.
                        yield return string.Join(".", prefix.Take(prefix.Length - dotCount).Concat(suffix.Skip(dotCount)));
                    }
                }
                yield break;
            }

            // The two possible names that can be imported here are:
            // * relativeModuleName
            // * importingFrom.relativeModuleName
            // and the order they are returned depends on whether
            // absolute_import is enabled or not.

            // With absolute_import, we treat the name as complete first.
            if (absoluteImports) {
                yield return relativeModuleName;
            }

            if (!string.IsNullOrEmpty(importingFrom)) {
                var prefix = importingFrom.Split('.');

                if (prefix.Length > 1) {
                    var adjacentModuleName = string.Join(".", prefix.Take(prefix.Length - 1)) + "." + relativeModuleName;
                    yield return adjacentModuleName;
                }
            }

            // Without absolute_import, we treat the name as complete last.
            if (!absoluteImports) {
                yield return relativeModuleName;
            }
        }


        /// <summary>
        /// Looks up the specified module by name.
        /// </summary>
        public MemberResult[] GetModule(string name) {
            return GetModules(modName => modName != name);
        }

        /// <summary>
        /// Gets a top-level list of all the available modules as a list of MemberResults.
        /// </summary>
        /// <returns></returns>
        public MemberResult[] GetModules(bool topLevelOnly = false) {
            return GetModules(modName => topLevelOnly && modName.IndexOf('.') != -1);
        }

        private MemberResult[] GetModules(Func<string, bool> excludedPredicate) {
            var d = new Dictionary<string, List<ModuleLoadState>>();
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (String.IsNullOrWhiteSpace(modName) ||
                    excludedPredicate(modName)) {
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
                    yield return value.Module;
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
            // provide module names first
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;
            
                if (moduleRef.IsValid) {
                    // include modules which can be imported
                    string pkgName;
                    if (modName == name) {
                        yield return new ExportedMemberInfo(null, modName);
                    } else if (GetPackageNameIfMatch(name, modName, out pkgName)) {
                        yield return new ExportedMemberInfo(pkgName, name);
                    }
                }
            }

            // then include module members
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
            return String.Compare(fullName, lastDot + 1, name, 0, name.Length) == 0;
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
                List<MemberResult> result = new List<MemberResult>();
                if (includeMembers) {
                    foreach (var keyValue in module.GetAllMembers(moduleContext)) {
                        result.Add(new MemberResult(keyValue.Key, keyValue.Value));
                    }
                    return result.ToArray();
                } else {
                    foreach (var child in module.GetChildrenPackages(moduleContext)) {
                        result.Add(new MemberResult(child.Key, child.Key, new[] { child.Value }, PythonMemberType.Module));
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
                            result.Add(new MemberResult(keyValue.Key, keyValue.Value));
                        }
                    }
                    return result.ToArray();
                }
            }
            return new MemberResult[0];
        }

        /// <summary>
        /// Gets the list of directories which should be analyzed.
        /// 
        /// This property is thread safe.
        /// </summary>
        public IEnumerable<string> AnalysisDirectories {
            get {
                lock (_analysisDirs) {
                    return _analysisDirs.ToArray();
                }
            }
        }

        [Obsolete("Use ModulePath.FromFullPath() instead")]
        public static string PathToModuleName(string path) {
            return ModulePath.FromFullPath(path).ModuleName;
        }

        /// <summary>
        /// Converts a given absolute path name to a fully qualified Python module name by walking the directory tree.
        /// </summary>
        /// <param name="path">Path to convert.</param>
        /// <param name="fileExists">A function that is used to verify the existence of files (in particular, __init__.py)
        /// in the tree. Its signature and semantics should match that of <see cref="File.Exists"/>.</param>
        /// <returns>A fully qualified module name.</returns>
        [Obsolete("Use ModulePath.FromFullPath() instead")]
        public static string PathToModuleName(string path, Func<string, bool> fileExists) {
            return ModulePath.FromFullPath(
                path,
                isPackage: dirName => fileExists(Path.Combine(dirName, "__init__.py"))
            ).ModuleName;
        }

        public AnalysisLimits Limits {
            get { return _limits; }
            set { _limits = value; }
        }

        #endregion

        #region Internal Implementation

        internal IKnownPythonTypes Types {
            get;
            private set;
        }

        internal IKnownClasses ClassInfos {
            get;
            private set;
        }

        internal IAnalysisSet DoNotUnionInMro {
            get;
            private set;
        }

        internal Deque<AnalysisUnit> Queue {
            get {
                return _queue;
            }
        }

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

            var attrType = (attr != null) ? attr.GetType() : typeof(NoneType);
            if (attr is IPythonType) {
                return GetBuiltinType((IPythonType)attr);
            } else if (attr is IPythonFunction) {
                var bf = (IPythonFunction)attr;
                return GetCached(attr, () => new BuiltinFunctionInfo(bf, this)) ?? _noneInst;
            } else if (attr is IPythonMethodDescriptor) {
                return GetCached(attr, () => {
                    var md = (IPythonMethodDescriptor)attr;
                    if (md.IsBound) {
                        return new BuiltinFunctionInfo(md.Function, this);
                    } else {
                        return new BuiltinMethodInfo(md, this);
                    }
                }) ?? _noneInst;
            } else if (attr is IBuiltinProperty) {
                return GetCached(attr, () => new BuiltinPropertyInfo((IBuiltinProperty)attr, this)) ?? _noneInst;
            } else if (attr is IPythonModule) {
                return _modules.GetBuiltinModule((IPythonModule)attr);
            } else if (attr is IPythonEvent) {
                return GetCached(attr, () => new BuiltinEventInfo((IPythonEvent)attr, this)) ?? _noneInst;
            } else if (attr is IPythonConstant) {
                return GetConstant((IPythonConstant)attr).First();
            } else if (attrType == typeof(bool) || attrType == typeof(int) || attrType == typeof(Complex) ||
                        attrType == typeof(string) || attrType == typeof(long) || attrType == typeof(double) ||
                        attr == null) {
                return GetConstant(attr).First();
            } else if (attr is IMemberContainer) {
                return GetCached(attr, () => new ReflectedNamespace((IMemberContainer)attr, this));
            } else if (attr is IPythonMultipleMembers) {
                IPythonMultipleMembers multMembers = (IPythonMultipleMembers)attr;
                var members = multMembers.Members;
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

            return result;
        }

        internal ModuleTable Modules {
            get { return _modules; }
        }

        internal ConcurrentDictionary<string, ModuleInfo> ModulesByFilename {
            get { return _modulesByFilename; }
        }

        internal IAnalysisSet GetConstant(IPythonConstant value) {
            object key = value ?? _nullKey;
            return GetCached(key, () => new ConstantInfo(value, this)) ?? _noneInst;
        }

        internal IAnalysisSet GetConstant(object value) {
            object key = value ?? _nullKey;
            return GetCached(key, () => new ConstantInfo(value, this)) ?? _noneInst;
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

            throw new InvalidOperationException();
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
                ReloadModulesAsync().WaitAndUnwrapExceptions();
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

        /// <summary>
        /// Adds a directory to the list of directories being analyzed.
        /// 
        /// This method is thread safe.
        /// </summary>
        public void AddAnalysisDirectory(string dir) {
            var dirsChanged = AnalysisDirectoriesChanged;
            bool added;
            lock (_analysisDirs) {
                added = _analysisDirs.Add(dir);
            }
            if (added && dirsChanged != null) {
                dirsChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Removes a directory from the list of directories being analyzed.
        /// 
        /// This method is thread safe.
        /// 
        /// New in 1.1.
        /// </summary>
        public void RemoveAnalysisDirectory(string dir) {
            var dirsChanged = AnalysisDirectoriesChanged;
            bool removed;
            lock (_analysisDirs) {
                removed = _analysisDirs.Remove(dir);
            }
            if (removed && dirsChanged != null) {
                dirsChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Event fired when the analysis directories have changed.  
        /// 
        /// This event can be fired on any thread.
        /// 
        /// New in 1.1.
        /// </summary>
        public event EventHandler AnalysisDirectoriesChanged;

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
            }
        }

        #endregion
    }
}
