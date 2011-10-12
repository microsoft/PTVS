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
    public class PythonAnalyzer : IInterpreterState, IGroupableAnalysisProject {
        private readonly IPythonInterpreter _interpreter;
        private readonly ConcurrentDictionary<string, ModuleReference> _modules;
        private readonly Dictionary<string, ModuleInfo> _modulesByFilename;
        private readonly Dictionary<object, object> _itemCache;
        private BuiltinModule _builtinModule;
        private readonly Dictionary<string, XamlProjectEntry> _xamlByFilename = new Dictionary<string, XamlProjectEntry>();
        internal Namespace _propertyObj, _classmethodObj, _staticmethodObj, _typeObj, _rangeFunc, _frozensetType;
        internal ISet<Namespace> _objectSet;
        internal Namespace _functionType;
        internal BuiltinClassInfo _dictType, _listType, _tupleType, _generatorType, _intType, _stringType, _boolType, _setType, _objectType, _dictKeysType, _dictValuesType, _longType, _floatType, _unicodeType, _bytesType;
        internal ConstantInfo _noneInst;
        private readonly Deque<AnalysisUnit> _queue;
        private KnownTypes _types;
        internal readonly IModuleContext _defaultContext;
        private readonly PythonLanguageVersion _langVersion;
        internal readonly AnalysisUnit _evalUnit;   // a unit used for evaluating when we don't otherwise have a unit available
        private readonly HashSet<string> _analysisDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private int? _crossModuleLimit;
        private static object _nullKey = new object();

        public PythonAnalyzer(IPythonInterpreterFactory interpreterFactory)
            : this(interpreterFactory.CreateInterpreter(), interpreterFactory.GetLanguageVersion()) {            
        }

        public PythonAnalyzer(IPythonInterpreter pythonInterpreter, PythonLanguageVersion langVersion) {
            _langVersion = langVersion;
            _interpreter = pythonInterpreter;
            _modules = new ConcurrentDictionary<string, ModuleReference>();
            _modulesByFilename = new Dictionary<string, ModuleInfo>(StringComparer.OrdinalIgnoreCase);
            _itemCache = new Dictionary<object, object>();

            var allBuiltins = InitializeBuiltinModules();
            pythonInterpreter.ModuleNamesChanged += new EventHandler(ModuleNamesChanged);

            _queue = new Deque<AnalysisUnit>();

            LoadKnownTypes();

            FinishBuiltinsInit(allBuiltins);

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

            // cached for quick checks to see if we're a call to clr.AddReference

            SpecializeFunction("wpf", "LoadComponent", LoadComponent);
        }

        void ModuleNamesChanged(object sender, EventArgs e) {
            LoadKnownTypes();

            FinishBuiltinsInit(InitializeBuiltinModules());
        }

        #region Public API

        public PythonLanguageVersion LanguageVersion {
            get {
                return _langVersion;
            }
        }

        /// <summary>
        /// Adds a new module of code to the list of available modules and returns a ProjectEntry object.
        /// </summary>
        /// <param name="moduleName">The name of the module; used to associate with imports</param>
        /// <param name="filePath">The path to the file on disk</param>
        /// <param name="cookie">An application-specific identifier for the module</param>
        /// <returns></returns>
        public IPythonProjectEntry AddModule(string moduleName, string filePath, IAnalysisCookie cookie = null) {
            var entry = new ProjectEntry(this, moduleName, filePath, cookie);

            if (moduleName != null) {
                Modules[moduleName] = new ModuleReference(entry.MyScope);
            }
            if (filePath != null) {
                _modulesByFilename[filePath] = entry.MyScope;
            }
            return entry;
        }

        public void RemoveModule(IProjectEntry entry) {
            _modulesByFilename.Remove(entry.FilePath);
            ModuleReference modRef;
            Modules.TryRemove(PathToModuleName(entry.FilePath), out modRef);
            var projEntry2 = entry as IProjectEntry2;
            if (projEntry2 != null) {
                projEntry2.RemovedFromProject();
            }
        }

        public IXamlProjectEntry AddXamlFile(string filePath, IAnalysisCookie cookie = null) {
            var entry = new XamlProjectEntry(filePath);

            _xamlByFilename[filePath] = entry;

            return entry;
        }

        /// <summary>
        /// Gets a top-level list of all the available modules as a list of MemberResults.
        /// </summary>
        /// <returns></returns>
        public MemberResult[] GetModules(bool topLevelOnly = false) {
            var d = new Dictionary<string, HashSet<Namespace>>();
            foreach (var keyValue in Modules) {
                var modName = keyValue.Key;
                var moduleRef = keyValue.Value;

                if (topLevelOnly && modName.IndexOf('.') != -1) {
                    continue;
                }

                if (moduleRef.Module != null || moduleRef.HasEphemeralReferences) {
                    HashSet<Namespace> l;
                    if (!d.TryGetValue(modName, out l)) {
                        d[modName] = l = new HashSet<Namespace>();
                    }
                    if (moduleRef.Module != null) {
                        // The REPL shows up here with value=None
                        l.Add(moduleRef.Module);
                    }
                }
            }

            var result = new MemberResult[d.Count];
            int pos = 0;
            foreach (var kvp in d) {
                result[pos++] = new MemberResult(kvp.Key, kvp.Value);
            }
            return result;
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

                if (moduleRef.Module != null || moduleRef.HasEphemeralReferences) {
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

                if (moduleRef.Module != null || moduleRef.HasEphemeralReferences) {
                    if (moduleRef.Module != null) {
                        // then check for members within the module.
                        if (ModuleContainsMember(name, moduleRef)) {
                            yield return new ExportedMemberInfo(modName + "." + name, true);
                        } else {
                            yield return new ExportedMemberInfo(modName + "." + name, false);
                        }
                    }
                }
            }
        }

        private bool ModuleContainsMember(string name, ModuleReference moduleRef) {
            BuiltinModule builtin = moduleRef.Module as BuiltinModule;
            if (builtin != null) {
                var mem = builtin.InterpreterModule.GetMember(_defaultContext, name);
                if (mem != null) {
                    if (IsExcludedBuiltin(builtin, mem)) {
                        // if a module imports a builtin and exposes it don't report it for purposes of adding imports
                        return false;
                    }

                    IPythonMultipleMembers multiMem = mem as IPythonMultipleMembers;
                    if (multiMem != null) {
                        foreach (var innerMem in multiMem.Members) {
                            if (IsExcludedBuiltin(builtin, innerMem)) {
                                // if something non-excludable aliased w/ something excluable we probably
                                // only care about the excluable (for example a module and None - timeit.py
                                // does this in the std lib)
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }

            ModuleInfo modInfo = moduleRef.Module as ModuleInfo;
            if (modInfo != null) {
                VariableDef varDef;
                if (modInfo.Scope.Variables.TryGetValue(name, out varDef) &&
                    varDef.VariableStillExists &&
                    varDef.Types.Count > 0) {

                    foreach (var type in varDef.Types) {
                        if (type is ModuleInfo || type is BuiltinModule) {
                            // we find modules via our modules list, dont duplicate these
                            return false;
                        }

                        foreach (var location in type.Locations) {
                            if (location.ProjectEntry != modInfo.ProjectEntry) {
                                // declared in another module
                                return false;
                            }
                        }
                    }

                    return true;
                }
            }
            return false;
        }

        private static bool IsExcludedBuiltin(BuiltinModule builtin, IMember mem) {
            IPythonFunction func;
            IPythonType type;
            IPythonConstant constant;
            if (mem is IPythonModule || // modules are handled specially
                ((func = mem as IPythonFunction) != null && func.DeclaringModule != builtin.InterpreterModule) ||   // function imported into another module
                ((type = mem as IPythonType) != null && type.DeclaringModule != builtin.InterpreterModule) ||   // type imported into another module
                ((constant = mem as IPythonConstant) != null && constant.Type.TypeId == BuiltinTypeId.Object)) {    // constant which we have no real type info for.
                return true;
            }

            if (constant != null) {
                if (constant.Type.DeclaringModule.Name == "__future__" && 
                    constant.Type.Name == "_Feature" && 
                    builtin.Name != "__future__") {
                    // someone has done a from __future__ import blah, don't include import in another
                    // module in the list of places where you can import this from.
                    return true;
                }
            }
            return false;
        }

        private static bool PackageNameMatches(string name, string modName) {
            int lastDot;
            return (lastDot = modName.LastIndexOf('.')) != -1 &&
                modName.Length == lastDot + 1 + name.Length &&
                String.Compare(modName, lastDot + 1, name, 0, name.Length) == 0;
        }

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

        public void SpecializeFunction(string moduleName, string name, Action<CallExpression> dlg) {
            SpecializeFunction(moduleName, name, (call, unit, types) => { dlg(call); return null; });
        }

        public IEnumerable<string> AnalysisDirectories {
            get {
                return _analysisDirs;
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

        /// <summary>
        /// gets or sets the maximum number of files which will be used for cross module analysis.
        /// 
        /// By default this is null and cross module analysis will not be limited.  Setting the
        /// value will cause cross module analysis to be disabled after that number of files has been
        /// loaded.
        /// </summary>
        public int? CrossModulAnalysisLimit {
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
        private void SpecializeFunction(string moduleName, string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg) {
            ModuleReference module;

            if (Modules.TryGetValue(moduleName, out module)) {
                BuiltinModule builtin = module.Module as BuiltinModule;
                Debug.Assert(builtin != null);
                if (builtin != null) {
                    foreach (var v in builtin[name]) {
                        BuiltinFunctionInfo funcInfo = v as BuiltinFunctionInfo;
                        if (funcInfo != null) {
                            builtin[name] = new SpecializedBuiltinFunction(this, funcInfo.Function, dlg).SelfSet;
                            break;
                        }
                    }
                }
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
                                                new SimpleSrcLocation(type.LineNumber, type.LineOffset),
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
                                                new SimpleSrcLocation(member.LineNumber, member.LineOffset),
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

        private Dictionary<string, BuiltinModule> InitializeBuiltinModules() {
            var names = _interpreter.GetModuleNames();
            Dictionary<string, BuiltinModule> allBuiltins = new Dictionary<string, BuiltinModule>();
            foreach (string modName in names) {
                var mod = _interpreter.ImportModule(modName);
                if (mod != null) {
                    ModuleReference modRef;
                    if (Modules.TryGetValue(modName, out modRef)) {
                        var existingBuiltin = modRef.Module as BuiltinModule;
                        if (existingBuiltin != null && existingBuiltin._type == mod) {
                            // don't replace existing module which is the same
                            continue;
                        }
                    }

                    var newMod = new BuiltinModule(mod, this);
                    Modules[modName] = new ModuleReference(newMod);
                    if (modName.IndexOf('.') != -1) {
                        allBuiltins[modName] = newMod;
                    }
                }
            }

            return allBuiltins;
       }

        private void FinishBuiltinsInit(Dictionary<string, BuiltinModule> allBuiltins) {
            // now make sure children modules are listed in their parents...
            foreach (var nameModule in allBuiltins) {
                var modName = nameModule.Key;
                var module = nameModule.Value;

                int dotStart = modName.LastIndexOf('.');
                Debug.Assert(dotStart != -1);
                string parentName = modName.Substring(0, dotStart);
                do {
                    ModuleReference parentRef;
                    if (!Modules.TryGetValue(parentName, out parentRef)) {
                        Modules[parentName] = parentRef = new ModuleReference(new BuiltinModule(new EmptyBuiltinModule(parentName), this));
                    }
                    BuiltinModule parentModule = parentRef.Module as BuiltinModule;
                    if (parentModule == null) {
                        break;
                    }
                    int dotEnd;
                    string curModName;
                    if ((dotEnd = modName.IndexOf('.', dotStart + 1)) == -1) {
                        curModName = modName.Substring(dotStart + 1);
                    } else {
                        curModName = modName.Substring(dotStart + 1, dotEnd - dotStart - 1);
                    }
                    ISet<Namespace> existing;
                    if (parentModule.TryGetMember(curModName, out existing)) {
                        // module is aliased w/ a member, merge it in...
                        var newSet = new HashSet<Namespace>(existing);
                        newSet.Add(module);
                        parentModule[curModName] = newSet;
                    } else {
                        parentModule[curModName] = module;
                    }
                    module = parentModule;
                } while (dotStart != 0 && (dotStart = parentName.LastIndexOf('.', dotStart - 1)) != -1);
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

        internal IPythonType GetPythonType(BuiltinTypeId id) {
            return _interpreter.GetBuiltinType(id);
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
                return GetCached(attr, () => new BuiltinModule((IPythonModule)attr, this));
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

        internal ConcurrentDictionary<string, ModuleReference> Modules {
            get { return _modules; }
        }

        internal Dictionary<string, ModuleInfo> ModulesByFilename {
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

        public void AddAnalysisDirectory(string dir) {
            _analysisDirs.Add(dir);
        }
    }
}
