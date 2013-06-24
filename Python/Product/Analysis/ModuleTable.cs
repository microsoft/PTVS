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
using System.Linq;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Maintains the list of modules loaded into the PythonAnalyzer.
    /// 
    /// This keeps track of the builtin modules as well as the user defined modules.  It's wraps
    /// up various elements we need to keep track of such as thread safety and lazy loading of built-in
    /// modules.
    /// </summary>
    class ModuleTable : IEnumerable<KeyValuePair<string, ModuleLoadState>> {
        private readonly IPythonInterpreter _interpreter;
        private readonly PythonAnalyzer _analyzer;
        private readonly ConcurrentDictionary<IPythonModule, BuiltinModule> _builtinModuleTable = new ConcurrentDictionary<IPythonModule, BuiltinModule>();
        private readonly ConcurrentDictionary<string, ModuleReference> _modules = new ConcurrentDictionary<string, ModuleReference>(StringComparer.Ordinal);
        private string[] _builtinNames;
#if DEBUG
        internal static Stopwatch _timer = new Stopwatch();
#endif

        public ModuleTable(PythonAnalyzer analyzer, IPythonInterpreter interpreter, IEnumerable<string> builtinNames) {
            _analyzer = analyzer;
            _interpreter = interpreter;
            SaveNames(builtinNames);
        }

        private void SaveNames(IEnumerable<string> builtinNames) {
            var names = builtinNames.ToArray();
            Array.Sort(names, StringComparer.Ordinal);
            _builtinNames = names;
        }
        
        public bool TryGetValue(string name, out ModuleReference res) {
            if (_modules.TryGetValue(name, out res)) {
                return true;
            } else if (Array.BinarySearch(_builtinNames, name, StringComparer.Ordinal) >= 0) {
                res = LoadModule(name);
                return res != null;
            }

            return false;
        }

        private ModuleReference LoadModule(string name) {
#if DEBUG
            var start = _timer.ElapsedMilliseconds;
            _timer.Start();
            try {
#endif
                return LoadModuleWorker(name);
#if DEBUG
            } finally {
                _timer.Stop();
                Debug.WriteLine(string.Format("ModuleLoadTime: {0} {1}ms Total: {2}ms", name, _timer.ElapsedMilliseconds - start, _timer.ElapsedMilliseconds));
            }
#endif
        }

        private ModuleReference LoadModuleWorker(string name) {
            var mod = _interpreter.ImportModule(name);
            if (mod != null) {
                // get or create our BuiltinModule object
                var newMod = GetBuiltinModule(mod);
                var res = _modules[name] = new ModuleReference(newMod);
                
                // load any of our children (recursively)
                LoadChildrenModules(name);

                // and then make sure we're published in our parent module, recursively
                // loading our parent if necessary.
                int dotStart;
                if ((dotStart = name.IndexOf('.')) != -1) {
                    string parentName = name.Substring(0, dotStart);
                    do {
                        ModuleReference parentRef;
                        if (!_modules.TryGetValue(parentName, out parentRef)) {
                            parentRef = LoadModuleWorker(parentName);
                            if (parentRef == null) {
                                parentRef = new ModuleReference(new BuiltinModule(new EmptyBuiltinModule(parentName), _analyzer));
                            }
                            _modules[parentName] = parentRef;
                        }

                        BuiltinModule parentModule = parentRef.Module as BuiltinModule;
                        if (parentModule == null) {
                            break;
                        }

                        int dotEnd;
                        string curModName;
                        if ((dotEnd = name.IndexOf('.', dotStart + 1)) == -1) {
                            curModName = name.Substring(dotStart + 1);
                        } else {
                            curModName = name.Substring(dotStart + 1, dotEnd - dotStart - 1);
                        }

                        IAnalysisSet existing;
                        if (parentModule.TryGetMember(curModName, out existing)) {
                            if (existing == newMod) {
                                // we hit somewhere within the hierarchy where we're already
                                // published in our parent module. Therefore the rest of the
                                // hierarchy had to be published as well.
                                return res;
                            } else if (ShouldMergeModules(newMod, existing)) {
                                // module is aliased w/ a member, merge it in...
                                parentModule[curModName] = existing.Add(newMod, canMutate: false);
                            }
                        } else {
                            parentModule[curModName] = newMod;
                        }
                        newMod = parentModule;
                    } while (dotStart != 0 && (dotStart = parentName.LastIndexOf('.', dotStart - 1)) != -1);
                }
                return res;
            }
            return null;
        }

        private void LoadChildrenModules(string name) {
            var builtinNames = _builtinNames;
            int ourIndex = Array.BinarySearch(builtinNames, name, StringComparer.Ordinal);
            ModuleReference parentRef;

            if(ourIndex >= 0 &&
                _modules.TryGetValue(name, out parentRef) && 
                parentRef.Module is BuiltinModule)  {
                var parentModule = parentRef.Module as BuiltinModule;
                string baseName = name + ".";
                for (int i = ourIndex + 1; i < builtinNames.Length; i++) {
                    var newName = builtinNames[i];
                    if (newName.StartsWith(baseName, StringComparison.Ordinal)) {
                        if (newName.IndexOf('.', baseName.Length) != -1) {
                            // sub-sub module
                            LoadChildrenModules(newName);
                        } else {
                            ModuleReference newRef;
                            if (!_modules.TryGetValue(newName, out newRef)) {
                                var newNewMod = _interpreter.ImportModule(newName);
                                if (newNewMod != null) {
                                    var builtinMod = GetBuiltinModule(newNewMod);
                                    if (builtinMod != null) {
                                        _modules[newName] = new ModuleReference(builtinMod);

                                        parentModule[newName.Substring(baseName.Length)] = builtinMod;
                                    }
                                }
                            }
                        }
                    } else {
                        break;
                    }
                }
            }
        }

        public bool TryRemove(string name, out ModuleReference res) {
            return _modules.TryRemove(name, out res);
        }

        public ModuleReference this[string name] {
            get {
                ModuleReference res;
                if (!TryGetValue(name, out res)) {
                    throw new KeyNotFoundException(name);
                }
                return res;
            }
            set {
                _modules[name] = value;
            }
        }

        private bool ShouldMergeModules(BuiltinModule module, IAnalysisSet existing) {
            if (existing.Count == 1) {
                var existingValue = existing.First();
                if (existingValue is ConstantInfo && existingValue.PythonType == _analyzer.Types[BuiltinTypeId.Object]) {
                    // something's typed to object and won't provide useful completions, don't
                    // merge the types with that so we get better completions.
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Reloads the modules when the interpreter says they've changed.
        /// </summary>
        public void ReInit() {
            var newNames = new HashSet<string>(_interpreter.GetModuleNames());

            foreach (var keyValue in _modules) {
                var name = keyValue.Key;
                var moduleRef = keyValue.Value;

                var builtinModule = moduleRef.Module as BuiltinModule;
                if (builtinModule != null) {
                    if (!newNames.Contains(name)) {
                        // this module was unloaded
                        ModuleReference dummy;
                        _modules.TryRemove(name, out dummy);

                        BuiltinModule removedModule;
                        _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out removedModule);
                    } else {
                        // this module was replaced with a new module
                        var newModule = _interpreter.ImportModule(name);
                        if (builtinModule.InterpreterModule != newModule) {
                            BuiltinModule removedModule;
                            _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out removedModule);
                            moduleRef.Module = GetBuiltinModule(newModule);
                        }
                    }
                }
            }

            SaveNames(newNames);
        }

        internal BuiltinModule GetBuiltinModule(IPythonModule attr) {
            BuiltinModule res;
            if (!_builtinModuleTable.TryGetValue(attr, out res)) {
                _builtinModuleTable[attr] = res = new BuiltinModule(attr, _analyzer);
            }
            return res;
        }

        #region IEnumerable<KeyValuePair<string,ModuleReference>> Members

        public IEnumerator<KeyValuePair<string, ModuleLoadState>> GetEnumerator() {
            foreach (var keyValue in _modules) {
                yield return new KeyValuePair<string, ModuleLoadState>(keyValue.Key, new InitializedModuleLoadState(keyValue.Value));
            }

            foreach (var name in _builtinNames) {
                if (!_modules.ContainsKey(name)) {
                    yield return new KeyValuePair<string, ModuleLoadState>(name, new UninitializedModuleLoadState(this, name));
                }
            }
        }

        class UninitializedModuleLoadState : ModuleLoadState {
            private readonly ModuleTable _moduleTable;
            private readonly string _name;
            private PythonMemberType? _type;

            public UninitializedModuleLoadState(ModuleTable moduleTable, string name) {
                this._moduleTable = moduleTable;
                this._name = name;
            }

            public override AnalysisValue Module {
                get {
                    var res = _moduleTable.LoadModule(_name);
                    if (res != null) {
                        return res.AnalysisModule;
                    }
                    return null;
                }
            }

            public override bool IsValid {
                get {
                    return true;
                }
            }

            public override bool HasEphemeralReferences {
                get {
                    return false;
                }
            }

            public override bool HasModule {
                get {
                    return true;
                }
            }

            public override PythonMemberType MemberType {
                get {
                    if (_type == null) {
                        var mod = _moduleTable._interpreter.ImportModule(_name);
                        if (mod != null) {
                            _type = mod.MemberType;
                        } else {
                            _type = PythonMemberType.Module;
                        }
                    }
                    return _type.Value;
                }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                var mod = _moduleTable._interpreter.ImportModule(_name);
                if (mod != null) {
                    return BuiltinModuleContainsMember(context, name, mod);
                }
                return false;
            }

        }

        class InitializedModuleLoadState : ModuleLoadState {
            private readonly ModuleReference _reference;

            public InitializedModuleLoadState(ModuleReference reference) {
                _reference = reference;
            }

            public override AnalysisValue Module {
                get {
                    return _reference.AnalysisModule;
                }
            }

            public override bool HasEphemeralReferences {
                get {
                    return _reference.HasEphemeralReferences;
                }
            }

            public override bool IsValid {
                get {
                    return Module != null || HasEphemeralReferences;
                }
            }

            public override bool HasModule {
                get {
                    return Module != null;
                }
            }

            public override PythonMemberType MemberType {
                get {
                    if (Module != null) {
                        return Module.MemberType;
                    }
                    return PythonMemberType.Module;
                }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                BuiltinModule builtin = Module as BuiltinModule;
                if (builtin != null) {
                    return BuiltinModuleContainsMember(context, name, builtin.InterpreterModule);
                }

                ModuleInfo modInfo = Module as ModuleInfo;
                if (modInfo != null) {
                    VariableDef varDef;                    
                    if (modInfo.Scope.Variables.TryGetValue(name, out varDef) &&
                        varDef.VariableStillExists) {
                        var types = varDef.TypesNoCopy;
                        if (types.Count > 0) {
                            foreach (var type in types) {
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
                        }

                        return true;
                    }
                }
                return false;
            }
        }

        private static bool BuiltinModuleContainsMember(IModuleContext context, string name, IPythonModule interpModule) {
            var mem = interpModule.GetMember(context, name);
            if (mem != null) {
                if (IsExcludedBuiltin(interpModule, mem)) {
                    // if a module imports a builtin and exposes it don't report it for purposes of adding imports
                    return false;
                }

                IPythonMultipleMembers multiMem = mem as IPythonMultipleMembers;
                if (multiMem != null) {
                    foreach (var innerMem in multiMem.Members) {
                        if (IsExcludedBuiltin(interpModule, innerMem)) {
                            // if something non-excludable aliased w/ something excluable we probably
                            // only care about the excluable (for example a module and None - timeit.py
                            // does this in the std lib)
                            return false;
                        }
                    }
                }

                return true;
            }
            return false;
        }

        private static bool IsExcludedBuiltin(IPythonModule builtin, IMember mem) {
            IPythonFunction func;
            IPythonType type;
            IPythonConstant constant;
            if (mem is IPythonModule || // modules are handled specially
                ((func = mem as IPythonFunction) != null && func.DeclaringModule != builtin) ||   // function imported into another module
                ((type = mem as IPythonType) != null && type.DeclaringModule != builtin) ||   // type imported into another module
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

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            throw new System.NotImplementedException();
        }

        #endregion
    }

    abstract class ModuleLoadState {
        public abstract AnalysisValue Module {
            get;
        }

        public abstract bool HasModule {
            get;
        }

        public abstract bool HasEphemeralReferences {
            get;
        }

        public abstract bool IsValid {
            get;
        }

        public abstract PythonMemberType MemberType {
            get;
        }

        internal abstract bool ModuleContainsMember(IModuleContext context, string name);
    }
}
