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
using System.IO;
using System.Linq;
using System.Numerics;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.PyAnalysis;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Performs analysis of multiple Python code files and enables interrogation of the resulting analysis.
    /// </summary>
    public class PythonAnalyzer : IInterpreterState, IGroupableAnalysisProject, IDisposable {
        private readonly IPythonInterpreter _interpreter;
        private readonly ModuleTable _modules;
        private readonly ConcurrentDictionary<string, ModuleInfo> _modulesByFilename;
        private readonly Dictionary<object, object> _itemCache;
        private BuiltinModule _builtinModule;
        private readonly ConcurrentDictionary<string, XamlProjectEntry> _xamlByFilename = new ConcurrentDictionary<string, XamlProjectEntry>();
        internal Namespace _propertyObj, _classmethodObj, _staticmethodObj, _typeObj, _rangeFunc, _frozensetType;
        internal ISet<Namespace> _objectSet;
        internal Namespace _functionType;
        internal BuiltinClassInfo _dictType, _listType, _tupleType, _generatorType, _intType, _stringType, _boolType, _setType, _objectType, _dictKeysType, _dictValuesType, _longType, _floatType, _unicodeType, _bytesType, _complexType;
        internal ConstantInfo _noneInst;
        private readonly Deque<AnalysisUnit> _queue;
        private KnownTypes _types;
        internal readonly IModuleContext _defaultContext;
        private readonly PythonLanguageVersion _langVersion;
        internal readonly AnalysisUnit _evalUnit;   // a unit used for evaluating when we don't otherwise have a unit available
        private readonly HashSet<string> _analysisDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, List<SpecializationInfo>> _specializationInfo = new Dictionary<string, List<SpecializationInfo>>();  // delayed specialization information, for modules not yet loaded...

        private int? _crossModuleLimit;
        private static object _nullKey = new object();

        public PythonAnalyzer(IPythonInterpreterFactory interpreterFactory)
            : this(interpreterFactory.CreateInterpreter(), interpreterFactory.GetLanguageVersion()) {            
        }

        public PythonAnalyzer(IPythonInterpreter pythonInterpreter, PythonLanguageVersion langVersion) {
            _langVersion = langVersion;
            _interpreter = pythonInterpreter;
            _modules = new ModuleTable(this, _interpreter, _interpreter.GetModuleNames());
            _modulesByFilename = new ConcurrentDictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
            _itemCache = new Dictionary<object, object>();


            _queue = new Deque<AnalysisUnit>();

            LoadKnownTypes();

            pythonInterpreter.Initialize(this);

            _defaultContext = pythonInterpreter.CreateModuleContext();

            _evalUnit = new AnalysisUnit(null, new InterpreterScope[] { new ModuleInfo("$global", new ProjectEntry(this, "$global", String.Empty, null), _defaultContext).Scope }, true);
        }

        private void LoadKnownTypes() {
            _types = new KnownTypes(this);
            _builtinModule = (BuiltinModule)Modules["__builtin__"].Module;
            _propertyObj = GetBuiltin("property");
            _classmethodObj = GetBuiltin("classmethod");
            _staticmethodObj = GetBuiltin("staticmethod");
            _typeObj = GetBuiltin("type");
            _intType = (BuiltinClassInfo)GetBuiltin("int");
            _complexType = (BuiltinClassInfo)GetBuiltin("complex");
            _stringType = (BuiltinClassInfo)GetBuiltin("str");
            if (_langVersion.Is2x()) {
                _unicodeType = (BuiltinClassInfo)GetBuiltin("unicode");
                _bytesType = (BuiltinClassInfo)GetBuiltin("str");
            } else {
                _unicodeType = (BuiltinClassInfo)GetBuiltin("str");
                _bytesType = (BuiltinClassInfo)GetBuiltin("bytes");
            }

            _objectType = (BuiltinClassInfo)GetBuiltin("object");
            _objectSet = _objectType.SelfSet;

            _setType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Set));
            _rangeFunc = GetBuiltin("range");
            _frozensetType = GetBuiltin("frozenset");
            _functionType = GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Function));
            _generatorType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Generator));
            _dictType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Dict));
            _boolType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Bool));
            _noneInst = (ConstantInfo)GetNamespaceFromObjects(null);
            _listType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.List));
            if (_langVersion.Is2x()) {
                _longType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Long));
            }
            _tupleType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Tuple));
            _floatType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.Float));
            _dictKeysType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.DictKeys));
            _dictValuesType = (BuiltinClassInfo)GetNamespaceFromObjects(_interpreter.GetBuiltinType(BuiltinTypeId.DictValues));

            SpecializeFunction("__builtin__", "range", (n, unit, args) => unit.DeclaringModule.GetOrMakeNodeVariable(n, (nn) => new RangeInfo(_types.List, unit.ProjectState).SelfSet));
            SpecializeFunction("__builtin__", "min", ReturnUnionOfInputs);
            SpecializeFunction("__builtin__", "max", ReturnUnionOfInputs);
            SpecializeFunction("__builtin__", "getattr", SpecialGetAttr);

            // analyzing the copy module causes an explosion in types (it gets called w/ all sorts of types to be
            // copied, and always returns the same type).  So we specialize these away so they return the type passed
            // in and don't do any analyze.  Ditto for the rest of the functions here...  
            SpecializeFunction("copy", "deepcopy", CopyFunction, analyze: false);
            SpecializeFunction("copy", "copy", CopyFunction, analyze: false);
            SpecializeFunction("pickle", "dumps", ReturnsString, analyze: false);
            SpecializeFunction("UserDict.UserDict", "update", Nop, analyze: false);
            SpecializeFunction("pprint", "pprint", Nop, analyze: false);
            SpecializeFunction("pprint", "pformat", ReturnsString, analyze: false);
            SpecializeFunction("pprint", "saferepr", ReturnsString, analyze: false);
            SpecializeFunction("pprint", "_safe_repr", ReturnsString, analyze: false);
            SpecializeFunction("pprint", "_format", ReturnsString, analyze: false);
            SpecializeFunction("pprint.PrettyPrinter", "_format", ReturnsString, analyze: false);
            SpecializeFunction("decimal.Decimal", "__new__", Nop, analyze: false);
            SpecializeFunction("StringIO.StringIO", "write", Nop, analyze: false);
            SpecializeFunction("threading.Thread", "__init__", Nop, analyze: false);
            SpecializeFunction("subprocess.Popen", "__init__", Nop, analyze: false);
            SpecializeFunction("Tkinter.Toplevel", "__init__", Nop, analyze: false);
            SpecializeFunction("weakref.WeakValueDictionary", "update", Nop, analyze: false);
            SpecializeFunction("os._Environ", "get", ReturnsString, analyze: false);
            SpecializeFunction("os._Environ", "update", Nop, analyze: false);
            SpecializeFunction("ntpath", "expandvars", ReturnsString, analyze: false);
            SpecializeFunction("idlelib.EditorWindow.EditorWindow", "__init__", Nop, analyze: false);

            // cached for quick checks to see if we're a call to clr.AddReference

            SpecializeFunction("wpf", "LoadComponent", LoadComponent);
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

            _itemCache.Clear();

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
                Modules[moduleName] = new ModuleReference(entry.MyScope);

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
            ModuleInfo removed;
            _modulesByFilename.TryRemove(entry.FilePath, out removed);
            ModuleReference modRef;
            Modules.TryRemove(PathToModuleName(entry.FilePath), out modRef);
            var projEntry2 = entry as IProjectEntry2;
            if (projEntry2 != null) {
                projEntry2.RemovedFromProject();
            }
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

                if (excludedPredicate(modName)) {
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

            public IEnumerable<Namespace> GetLazyModules() {
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
                    foreach (var keyValue in ((Namespace)module).GetAllMembers(moduleContext)) {
                        result.Add(new MemberResult(keyValue.Key, keyValue.Value));
                    }
                    return result.ToArray();
                } else {
                    foreach (var child in module.GetChildrenPackages(moduleContext)) {
                        result.Add(new MemberResult(child.Key, child.Key, new[] { child.Value }, PythonMemberType.Module));
                    }
                    return result.ToArray();
                }
            }
            return new MemberResult[0];
        }

        /// <summary>
        /// Specializes the provided function in the given module name to return an instance of the given type.
        /// 
        /// The type is a fully qualified module name (e.g. thread.LockType).  
        /// </summary>
        /// <param name="moduleName"></param>
        /// <param name="name"></param>
        /// <param name="returnType"></param>
        public void SpecializeFunction(string moduleName, string name, string returnType) {
            int lastDot;
            if ((lastDot = returnType.LastIndexOf('.')) == -1) {
                throw new ArgumentException(String.Format("Expected module.typename for return type, got '{0}'", returnType));
            }

            string retModule = returnType.Substring(0, lastDot);
            string typeName = returnType.Substring(lastDot + 1);

            SpecializeFunction(moduleName, name, (call, unit, types) => {
                ModuleReference modRef;
                if (Modules.TryGetValue(retModule, out modRef)) {
                    if (modRef.Module != null) {
                        ISet<Namespace> res = EmptySet<Namespace>.Instance;
                        bool madeSet = false;
                        foreach (var value in modRef.Module.GetMember(call, unit, typeName)) {
                            if (value is ClassInfo) {
                                res = res.Union(((ClassInfo)value).Instance.SelfSet, ref madeSet);
                            } else {
                                res = res.Union(value.SelfSet, ref madeSet);
                            }
                        }
                        return res;
                    }
                }
                return null;
            });
        }

        public void SpecializeFunction(string moduleName, string name, Action<CallExpression, CallInfo> dlg) {
            SpecializeFunction(moduleName, name, (call, unit, types) => { dlg(call, new CallInfo(types)); return null; });
        }

        public void SpecializeFunction(string moduleName, string name, Action<CallExpression> dlg) {
            SpecializeFunction(moduleName, name, (call, unit, types) => { dlg(call); return null; });
        }

        public void SpecializeFunction(string moduleName, string name, Action<PythonAnalyzer, CallExpression> dlg) {
            SpecializeFunction(moduleName, name, (call, unit, types) => { dlg(this, call); return null; });
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
                File.Exists(Path.Combine(dirName, "__init__.py"))) {
                moduleName = Path.GetFileName(dirName) + "." + moduleName;
            }

            return moduleName;
        }

        [Obsolete("This was misspelled, use CrossModuleAnalysisLimit instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public int? CrossModulAnalysisLimit {
            get {
                return CrossModuleAnalysisLimit;
            }
            set {
                CrossModuleAnalysisLimit = value;
            }
        }

        /// <summary>
        /// gets or sets the maximum number of files which will be used for cross module analysis.
        /// 
        /// By default this is null and cross module analysis will not be limited.  Setting the
        /// value will cause cross module analysis to be disabled after that number of files has been
        /// loaded.
        /// </summary>
        public int? CrossModuleAnalysisLimit {
            get {
                return _crossModuleLimit;
            }
            set {
                _crossModuleLimit = value;
            }
        }

        #endregion

        #region Internal Implementation

        internal KnownTypes Types {
            get {
                return _types;
            }
        }

        /// <summary>
        /// Replaces a built-in function (specified by module name and function name) with a customized
        /// delegate which provides specific behavior for handling when that function is called.
        /// 
        /// Currently this just provides a hook when the function is called - it could be expanded
        /// to providing the interpretation of when the function is called as well.
        /// </summary>
        private void SpecializeFunction(string moduleName, string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, bool analyze = true) {
            ModuleReference module;

            int lastDot;
            string realModName = null;
            if (Modules.TryGetValue(moduleName, out module)) {
                IModule mod = module.Module as IModule;
                Debug.Assert(mod != null);
                if (mod != null) {
                    mod.SpecializeFunction(name, dlg, analyze);
                }
            } else if ((lastDot = moduleName.LastIndexOf('.')) != -1 &&
                Modules.TryGetValue(realModName = moduleName.Substring(0, lastDot), out module)) {

                IModule mod = module.Module as IModule;
                Debug.Assert(mod != null);
                if (mod != null) {
                    mod.SpecializeFunction(moduleName.Substring(lastDot + 1, moduleName.Length - (lastDot + 1)) + "." + name, dlg, analyze);
                }
            } else {
                SaveDelayedSpecialization(moduleName, name, dlg, analyze, realModName);
            }
        }

        private ISet<Namespace> LoadComponent(CallExpression node, AnalysisUnit unit, ISet<Namespace>[] args) {
            if (args.Length == 2 && Interpreter is IDotNetPythonInterpreter) {
                var xaml = args[1];
                var self = args[0];

                foreach (var arg in xaml) {
                    string strConst = arg.GetConstantValue() as string;
                    if (strConst == null) {
                        var bytes = arg.GetConstantValue() as AsciiString;
                        if (bytes != null) {
                            strConst = bytes.String;
                        }
                    }

                    if (strConst != null) {
                        // process xaml file, add attributes to self
                        string xamlPath = Path.Combine(Path.GetDirectoryName(unit.DeclaringModule.ProjectEntry.FilePath), strConst);
                        XamlProjectEntry xamlProject;
                        if (_xamlByFilename.TryGetValue(xamlPath, out xamlProject)) {
                            // TODO: Get existing analysis if it hasn't changed.
                            var analysis = xamlProject.Analysis;

                            if (analysis == null) {
                                xamlProject.Analyze();
                                analysis = xamlProject.Analysis;
                            }

                            xamlProject.AddDependency(unit.ProjectEntry);

                            var evalUnit = unit.CopyForEval();

                            // add named objects to instance
                            foreach (var keyValue in analysis.NamedObjects) {
                                var type = keyValue.Value;
                                if (type.Type.UnderlyingType != null) {  
                                           
                                    var ns = GetNamespaceFromObjects(((IDotNetPythonInterpreter)Interpreter).GetBuiltinType(type.Type.UnderlyingType));
                                    if (ns is BuiltinClassInfo) {
                                        ns = ((BuiltinClassInfo)ns).Instance;
                                    }
                                    self.SetMember(node, evalUnit, keyValue.Key, ns.SelfSet);
                                }

                                // TODO: Better would be if SetMember took something other than a node, then we'd
                                // track references w/o this extra effort.
                                foreach (var inst in self) {
                                    InstanceInfo instInfo = inst as InstanceInfo;
                                    if (instInfo != null) {
                                        VariableDef def;
                                        if (instInfo.InstanceAttributes.TryGetValue(keyValue.Key, out def)) {
                                            def.AddAssignment(
                                                new EncodedLocation(SourceLocationResolver.Instance, new SourceLocation(1, type.LineNumber, type.LineOffset)),
                                                xamlProject
                                            );
                                        }
                                    }
                                }
                            }

                            // add references to event handlers
                            foreach (var keyValue in analysis.EventHandlers) {
                                // add reference to methods...
                                var member = keyValue.Value;

                                // TODO: Better would be if SetMember took something other than a node, then we'd
                                // track references w/o this extra effort.
                                foreach (var inst in self) {
                                    InstanceInfo instInfo = inst as InstanceInfo;
                                    if (instInfo != null) {
                                        ClassInfo ci = instInfo.ClassInfo;

                                        VariableDef def;
                                        if (ci.Scope.Variables.TryGetValue(keyValue.Key, out def)) {
                                            def.AddReference(
                                                new EncodedLocation(SourceLocationResolver.Instance, new SourceLocation(1, member.LineNumber, member.LineOffset)),
                                                xamlProject
                                            );
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // load component returns self
                return self;
            }

            return EmptySet<Namespace>.Instance;
        }

        internal Deque<AnalysisUnit> Queue {
            get {
                return _queue;
            }
        }

        internal BuiltinModule ImportBuiltinModule(string modName, bool bottom = true) {
            IPythonModule mod = null;

            if (modName.IndexOf('.') != -1) {
                string[] names = modName.Split('.');
                if (names[0].Length > 0) {
                    mod = _interpreter.ImportModule(names[0]);
                    if (bottom) {
                        int curIndex = 1;
                        while (mod != null && curIndex < names.Length) {
                            mod = mod.GetMember(_defaultContext, names[curIndex++]) as IPythonModule;
                        }
                    }
                }
                // else relative import, we're not getting a builtin module...
            } else {
                mod = _interpreter.ImportModule(modName);
            }

            if (mod != null) {
                return (BuiltinModule)GetNamespaceFromObjects(mod);
            }

            return null;
        }

        private ISet<Namespace> Nop(CallExpression call, AnalysisUnit unit, ISet<Namespace>[] args) {
            return EmptySet<Namespace>.Instance;
        }

        private ISet<Namespace> CopyFunction(CallExpression call, AnalysisUnit unit, ISet<Namespace>[] args) {
            if (args.Length > 0) {
                return args[0];
            }
            return EmptySet<Namespace>.Instance;
        }

        private ISet<Namespace> ReturnsString(CallExpression call, AnalysisUnit unit, ISet<Namespace>[] args) {
            return _stringType.Instance;
        }

        private ISet<Namespace> SpecialGetAttr(CallExpression call, AnalysisUnit unit, ISet<Namespace>[] args) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            if (args.Length >= 2) {
                if (args.Length >= 3) {
                    // getattr(foo, 'bar', baz), baz is a possible return value.
                    res = args[2];
                }

                foreach (var value in args[0]) {
                    foreach (var name in args[1]) {
                        // getattr(foo, 'bar') - attempt to do the getattr and return the proper value
                        var strValue = name.GetConstantValueAsString();
                        if (strValue != null) {
                            res = res.Union(value.GetMember(call, unit, strValue), ref madeSet);
                        }
                    }
                }
            }
            return res;
        }
        
        private ISet<Namespace> ReturnUnionOfInputs(CallExpression call, AnalysisUnit unit, ISet<Namespace>[] args) {
            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var set in args) {
                res = res.Union(set, ref madeSet);
            }
            return res;
        }

        /// <summary>
        /// Gets a builtin value
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal Namespace GetBuiltin(string name) {
            return _builtinModule[name].First();
        }

        internal T GetCached<T>(object key, Func<T> maker) where T : class {
            object result;
            if (!_itemCache.TryGetValue(key, out result)) {
                _itemCache[key] = result = maker();
            } else {
                Debug.Assert(result is T);
            }
            return (result as T);
        }        

        internal BuiltinModule BuiltinModule {
            get { return _builtinModule; }
        }

        internal BuiltinInstanceInfo GetInstance(IPythonType type) {
            return GetBuiltinType(type).Instance;
        }

        internal BuiltinClassInfo GetBuiltinType(IPythonType type) {
            return GetCached(type,
                () => MakeBuiltinType(type)
            );
        }

        private BuiltinClassInfo MakeBuiltinType(IPythonType type) {
            switch(type.TypeId) {
                case BuiltinTypeId.List:  return new ListBuiltinClassInfo(type, this);
                case BuiltinTypeId.Tuple: return new TupleBuiltinClassInfo(type, this);
                default: return new BuiltinClassInfo(type, this);
            }
        }

        internal Namespace GetNamespaceFromObjects(object attr) {
            var attrType = (attr != null) ? attr.GetType() : typeof(NoneType);
            if (attr is IPythonType) {
                return GetBuiltinType((IPythonType)attr);
            } else if (attr is IPythonFunction) {
                var bf = (IPythonFunction)attr;
                return GetCached(attr, () => new BuiltinFunctionInfo(bf, this));
            } else if (attr is IPythonMethodDescriptor) {
                return GetCached(attr, () => new BuiltinMethodInfo((IPythonMethodDescriptor)attr, this));
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
                    Namespace[] nses = new Namespace[members.Count];
                    for (int i = 0; i < members.Count; i++) {
                        nses[i] = GetNamespaceFromObjects(members[i]);
                    }
                    return new MultipleMemberInfo(nses);
                }
                );
            } else {
                var pyAattrType = GetTypeFromObject(attr);
                return GetBuiltinType(pyAattrType).Instance;
            }
        }

        internal IDictionary<string, ISet<Namespace>> GetAllMembers(IMemberContainer container, IModuleContext moduleContext) {
            var names = container.GetMemberNames(moduleContext);
            var result = new Dictionary<string, ISet<Namespace>>();
            foreach (var name in names) {
                result[name] = GetNamespaceFromObjects(container.GetMember(moduleContext, name));
            }

            return result;
        }

        internal ModuleTable Modules {
            get { return _modules; }
        }

        internal ConcurrentDictionary<string, ModuleInfo> ModulesByFilename {
            get { return _modulesByFilename; }
        }

        internal ISet<Namespace> GetConstant(IPythonConstant value) {
            object key = value ?? _nullKey;
            return GetCached<ISet<Namespace>>(key, () => new ConstantInfo(value, this).SelfSet);
        }

        internal ISet<Namespace> GetConstant(object value) {
            object key = value ?? _nullKey;
            return GetCached<ISet<Namespace>>(key, () => new ConstantInfo(value, this).SelfSet);
        }

        private static void Update<K, V>(IDictionary<K, V> dict, IDictionary<K, V> newValues) {
            foreach (var kvp in newValues) {
                dict[kvp.Key] = kvp.Value;
            }
        }

        internal IPythonType GetTypeFromObject(object value) {
            if (value == null) {
                return Types.None;
            }
            switch (Type.GetTypeCode(value.GetType())) {
                case TypeCode.Boolean: return Types.Bool;
                case TypeCode.Double: return Types.Float;
                case TypeCode.Int32: return Types.Int;
                case TypeCode.String: return Types.Str;
                case TypeCode.Object:
                    if (value.GetType() == typeof(Complex)) {
                        return Types.Complex;
                    } else if(value.GetType() == typeof(AsciiString)) {
                        return Types.Bytes;
                    } else if (value.GetType() == typeof(BigInteger)) {
                        if (LanguageVersion.Is3x()) {
                            return Types.Int;
                        } else {
                            return Types.Long;
                        }
                    } else if (value.GetType() == typeof(Ellipsis)) {
                        return Types.Ellipsis;
                    }
                    break;
            }

            throw new InvalidOperationException();            
        }

        internal BuiltinClassInfo MakeGenericType(IAdvancedPythonType clrType, params IPythonType[] clrIndexType) {
            var res = clrType.MakeGenericType(clrIndexType);

            return (BuiltinClassInfo)GetNamespaceFromObjects(res);
        }

        #endregion

        #region IGroupableAnalysisProject Members

        void IGroupableAnalysisProject.AnalyzeQueuedEntries() {
            new DDG().Analyze(Queue);
        }

        #endregion

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

        /// <summary>
        /// Processes any delayed specialization for when a module is added for the 1st time.
        /// </summary>
        /// <param name="moduleName"></param>
        private void DoDelayedSpecialization(string moduleName) {
            List<SpecializationInfo> specInfo;
            if (_specializationInfo.TryGetValue(moduleName, out specInfo)) {
                foreach (var curSpec in specInfo) {
                    SpecializeFunction(curSpec.ModuleName, curSpec.Name, curSpec.Delegate, curSpec.Analyze);
                }
            }
        }

        private void SaveDelayedSpecialization(string moduleName, string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, bool analyze, string realModName) {
            if (_specializationInfo == null) {
                _specializationInfo = new Dictionary<string, List<SpecializationInfo>>();
            }
            List<SpecializationInfo> specList;
            if (!_specializationInfo.TryGetValue(moduleName, out specList)) {
                _specializationInfo[realModName ?? moduleName] = specList = new List<SpecializationInfo>();
            }

            specList.Add(new SpecializationInfo(moduleName, name, dlg, analyze));
        }

        class SpecializationInfo {
            public readonly string Name, ModuleName;
            public readonly Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> Delegate;
            public readonly bool Analyze;

            public SpecializationInfo(string moduleName, string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, bool analyze) {
                ModuleName = moduleName;
                Name = name;
                Delegate = dlg;
                Analyze = analyze;
            }
        }
    }
}
