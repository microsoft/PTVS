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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.PyAnalysis;
using Microsoft.Win32;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Performs analysis of multiple Python code files and enables interrogation of the resulting analysis.
    /// </summary>
    public partial class PythonAnalyzer : IGroupableAnalysisProject, IDisposable {
        private readonly IPythonInterpreter _interpreter;
        private readonly ModuleTable _modules;
        private readonly ConcurrentDictionary<string, ModuleInfo> _modulesByFilename;
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

        private const string AnalysisLimitsKey = @"Software\Microsoft\PythonTools\" + AssemblyVersionInfo.VSVersion +
            @"\Analysis\Project";

        public PythonAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreter interpreter = null)
            : this(factory, interpreter ?? factory.CreateInterpreter(), null) {
        }

        internal PythonAnalyzer(IPythonInterpreterFactory factory, IPythonInterpreter pythonInterpreter, string builtinName) {
            if (pythonInterpreter == null) {
                throw new ArgumentNullException("pythonInterpreter");
            }
            _langVersion = factory.GetLanguageVersion();
            _interpreter = pythonInterpreter;
            _builtinName = builtinName ?? (_langVersion.Is3x() ? SharedDatabaseState.BuiltinName3x : SharedDatabaseState.BuiltinName2x);
            _modules = new ModuleTable(this, _interpreter, _interpreter.GetModuleNames());
            _modulesByFilename = new ConcurrentDictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
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

            LoadKnownTypes();

            pythonInterpreter.Initialize(this);

            _defaultContext = _interpreter.CreateModuleContext();

            _evalUnit = new AnalysisUnit(null, null, new ModuleInfo("$global", new ProjectEntry(this, "$global", String.Empty, null), _defaultContext).Scope, true);
            AnalysisLog.NewUnit(_evalUnit);
        }

        private void LoadKnownTypes() {
            ModuleReference moduleRef;
            if (Modules.TryGetValue(_builtinName, out moduleRef)) {
                _builtinModule = (BuiltinModule)moduleRef.Module;
            } else {
                var fallbackDb = PythonTypeDatabase.CreateDefaultTypeDatabase(LanguageVersion.ToVersion());
                _builtinModule = _modules.GetBuiltinModule(fallbackDb.GetModule(SharedDatabaseState.BuiltinName2x));
                Modules[_builtinName] = new ModuleReference(_builtinModule);
            }

            Types = new KnownTypes(this);
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
        public void ReloadModules() {
            _modules.ReInit();
            LoadKnownTypes();

            _interpreter.Initialize(this);

            foreach (var mod in _modulesByFilename.Values) {
                mod.Clear();
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
        /// <returns></returns>
        public IPythonProjectEntry AddModule(string moduleName, string filePath, IAnalysisCookie cookie = null) {
            var entry = new ProjectEntry(this, moduleName, filePath, cookie);

            if (moduleName != null) {
                ModuleReference moduleRef;
                if (Modules.TryGetValue(moduleName, out moduleRef)) {
                    moduleRef.Module = entry.MyScope;
                } else {
                    Modules[moduleName] = new ModuleReference(entry.MyScope);
                }

                DoDelayedSpecialization(moduleName);
            }
            if (filePath != null) {
                _modulesByFilename[filePath] = entry.MyScope;
            }
            return entry;
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
                if (Modules.TryGetValue(pyEntry.ModuleName, out modRef)) {
                    if (modRef.HasReferences) {
                        modRef.Module = null;
                    } else {
                        Modules.TryRemove(pyEntry.ModuleName, out modRef);
                    }
                }
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
        public IEnumerable<IPythonProjectEntry> GetEntriesThatImportModule(string moduleName) {
            ModuleReference moduleRef;
            if (!Modules.TryGetValue(moduleName, out moduleRef)) {
                return Enumerable.Empty<IPythonProjectEntry>();
            }

            return moduleRef.References.Select(mi => mi.ProjectEntry).OfType<IPythonProjectEntry>();
        }

        /// <summary>
        /// Returns true if a module has been imported.
        /// </summary>
        /// <param name="relativeModuleName">
        /// The absolute or relative name of the module. If a relative name is 
        /// passed here, <paramref name="importedFrom"/> must be provided.
        /// </param>
        /// <param name="importedFrom">
        /// The full name of the module doing the import.
        /// </param>
        /// <returns>
        /// True if the module was imported during analysis; otherwise, false.
        /// </returns>
        public bool IsModuleResolved(string relativeModuleName, string importedFrom) {
            if (string.IsNullOrEmpty(importedFrom) || relativeModuleName.FirstOrDefault() != '.') {
                // Module name is absolute or importedFrom is not specified.
                return IsModuleResolved(relativeModuleName);
            }

            var suffix = relativeModuleName.Split('.').ToList();
            var dotCount = suffix.TakeWhile(bit => string.IsNullOrEmpty(bit)).Count();

            var prefix = importedFrom.Split('.').ToList();
            var moduleName = string.Join(".", prefix.Take(prefix.Count - dotCount).Concat(suffix.Skip(dotCount)));

            return IsModuleResolved(moduleName);
        }

        /// <summary>
        /// Returns true if a module has been imported.
        /// </summary>
        /// <param name="moduleName">
        /// The absolute name of the module.
        /// </param>
        /// <returns>
        /// True if the module was imported during analysis; otherwise, false.
        /// </returns>
        public bool IsModuleResolved(string moduleName) {
            ModuleReference moduleRef;
            return Modules.TryGetValue(moduleName, out moduleRef) &&
                moduleRef != null &&
                moduleRef.Module != null;
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
                    if (modName == name || PackageNameMatches(name, modName)) {
                        yield return new ExportedMemberInfo(modName, true);
                    }
                }
            }

            // then include module members
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (moduleRef.IsValid) {
                    // then check for members within the module.
                    if (moduleRef.ModuleContainsMember(_defaultContext, name)) {
                        yield return new ExportedMemberInfo(modName + "." + name, true);
                    } else {
                        yield return new ExportedMemberInfo(modName + "." + name, false);
                    }
                }
            }
        }

        private static bool PackageNameMatches(string name, string modName) {
            int lastDot;
            return (lastDot = modName.LastIndexOf('.')) != -1 &&
                modName.Length == lastDot + 1 + name.Length &&
                String.Compare(modName, lastDot + 1, name, 0, name.Length) == 0;
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
        /// returns the MemberResults associated with modules in the specified
        /// list of names.  The list of names is the path through the module, for example
        /// ['System', 'Runtime']
        /// </summary>
        /// <returns></returns>
        public MemberResult[] GetModuleMembers(IModuleContext moduleContext, string[] names, bool includeMembers = false) {
            ModuleReference moduleRef;
            if (Modules.TryGetValue(names[0], out moduleRef) && moduleRef.Module != null) {
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
                        foreach(var ns in keyValue.Value.OfType<MultipleMemberInfo>()) {
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

        public static string PathToModuleName(string path) {
            return PathToModuleName(path, fileName => File.Exists(fileName));
        }

        /// <summary>
        /// Converts a given absolute path name to a fully qualified Python module name by walking the directory tree.
        /// </summary>
        /// <param name="path">Path to convert.</param>
        /// <param name="fileExists">A function that is used to verify the existence of files (in particular, __init__.py)
        /// in the tree. Its signature and semantics should match that of <see cref="File.Exists"/>.</param>
        /// <returns>A fully qualified module name.</returns>
        public static string PathToModuleName(string path, Func<string, bool> fileExists) {
            string moduleName;
            string dirName;

            if (path == null) {
                return String.Empty;
            } else if (path.EndsWith("__init__.py")) {
                moduleName = Path.GetFileName(Path.GetDirectoryName(path));
                dirName = Path.GetDirectoryName(path);
            } else {
                moduleName = Path.GetFileNameWithoutExtension(path);
                dirName = path;
            }

            while (dirName.Length != 0 && (dirName = Path.GetDirectoryName(dirName)).Length != 0 &&
                fileExists(Path.Combine(dirName, "__init__.py"))) {
                moduleName = Path.GetFileName(dirName) + "." + moduleName;
            }

            return moduleName;
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

        private IModule ImportFromIMember(IMember member, string[] names, int curIndex) {
            if (member == null) {
                return null;
            }
            if (curIndex >= names.Length) {
                return GetAnalysisValueFromObjects(member) as IModule;
            }

            var ipm = member as IPythonModule;
            if (ipm != null) {
                return ImportFromIPythonModule(ipm, names, curIndex);
            }

            var im = member as IModule;
            if (im != null) {
                return ImportFromIModule(im, names, curIndex);
            }

            var ipmm = member as IPythonMultipleMembers;
            if (ipmm != null) {
                return ImportFromIPythonMultipleMembers(ipmm, names, curIndex);
            }

            return null;
        }

        private IModule ImportFromIPythonMultipleMembers(IPythonMultipleMembers mod, string[] names, int curIndex) {
            if (mod == null) {
                return null;
            }
            var modules = new List<IModule>();
            foreach (var member in mod.Members) {
                modules.Add(ImportFromIMember(member, names, curIndex));
            }
            var mods = modules.OfType<AnalysisValue>().ToArray();
            if (mods.Length == 0) {
                return null;
            } else if (mods.Length == 1) {
                return (IModule)mods[0];
            } else {
                return new MultipleMemberInfo(mods);
            }
        }

        private IModule ImportFromIModule(IModule mod, string[] names, int curIndex) {
            for (; mod != null && curIndex < names.Length; ++curIndex) {
                mod = mod.GetChildPackage(_defaultContext, names[curIndex]);
            }
            return mod;
        }

        private IModule ImportFromIPythonModule(IPythonModule mod, string[] names, int curIndex) {
            if (mod == null) {
                return null;
            }
            var member = mod.GetMember(_defaultContext, names[curIndex]);
            return ImportFromIMember(member, names, curIndex + 1);
        }

        internal IModule ImportBuiltinModule(string modName, bool bottom = true) {
            IPythonModule mod = null;

            if (modName.IndexOf('.') != -1) {
                string[] names = modName.Split('.');
                if (names[0].Length > 0) {
                    mod = _interpreter.ImportModule(names[0]);
                    if (bottom && names.Length > 1) {
                        var mod2 = ImportFromIPythonModule(mod, names, 1);
                        if (mod2 != null) {
                            return mod2;
                        }
                    }
                }
                // else relative import, we're not getting a builtin module...
            } else {
                mod = _interpreter.ImportModule(modName);
            }

            if (mod != null) {
                return (BuiltinModule)GetAnalysisValueFromObjects(mod);
            }

            return null;
        }

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
            );
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
                return GetCached(attr, () => new BuiltinFunctionInfo(bf, this));
            } else if (attr is IPythonMethodDescriptor) {
                return GetCached(attr, () => {
                    var md = (IPythonMethodDescriptor)attr;
                    if (md.IsBound) {
                        return new BuiltinFunctionInfo(md.Function, this);
                    } else {
                        return new BuiltinMethodInfo(md, this);
                    }
                });
            } else if (attr is IBuiltinProperty) {
                return GetCached(attr, () => new BuiltinPropertyInfo((IBuiltinProperty)attr, this));
            } else if (attr is IPythonModule) {
                return _modules.GetBuiltinModule((IPythonModule)attr);
            } else if (attr is IPythonEvent) {
                return GetCached(attr, () => new BuiltinEventInfo((IPythonEvent)attr, this));
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
                return GetCached(attr, () => {
                    AnalysisValue[] nses = new AnalysisValue[members.Count];
                    for (int i = 0; i < members.Count; i++) {
                        nses[i] = GetAnalysisValueFromObjects(members[i]);
                    }
                    return new MultipleMemberInfo(nses);
                }
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
            return GetCached(key, () => new ConstantInfo(value, this)).SelfSet;
        }

        internal IAnalysisSet GetConstant(object value) {
            object key = value ?? _nullKey;
            return GetCached(key, () => new ConstantInfo(value, this)).SelfSet;
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
            new DDG().Analyze(Queue, cancel, _reportQueueSize, _reportQueueInterval);
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

        void IDisposable.Dispose() {
            IDisposable interpreter = _interpreter as IDisposable;
            if (interpreter != null) {
                interpreter.Dispose();
            }
        }

        #endregion
    }
}
