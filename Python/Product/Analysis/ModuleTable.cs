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

        public ModuleTable(PythonAnalyzer analyzer, IPythonInterpreter interpreter) {
            _analyzer = analyzer;
            _interpreter = interpreter;
        }
        
        public bool TryGetValue(string name, out ModuleReference res) {
            if (!_modules.TryGetValue(name, out res) || res == null) {
                res = new ModuleReference(GetBuiltinModule(_interpreter.ImportModule(name)));
            }
            return res != null && res.Module != null;
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

        public ModuleReference GetOrAdd(string name) {
            return _modules.GetOrAdd(name, _ => new ModuleReference());
        }

        /// <summary>
        /// Reloads the modules when the interpreter says they've changed.
        /// Modules that are already in the table as builtins are replaced or
        /// removed, but no new modules are added.
        /// </summary>
        public void ReInit() {
            var newNames = new HashSet<string>(_interpreter.GetModuleNames(), StringComparer.Ordinal);

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
        }

        internal BuiltinModule GetBuiltinModule(IPythonModule attr) {
            if (attr == null) {
                return null;
            }
            BuiltinModule res;
            if (!_builtinModuleTable.TryGetValue(attr, out res)) {
                _builtinModuleTable[attr] = res = new BuiltinModule(attr, _analyzer);
            }
            return res;
        }

        #region IEnumerable<KeyValuePair<string,ModuleReference>> Members

        public IEnumerator<KeyValuePair<string, ModuleLoadState>> GetEnumerator() {
            var unloadedNames = new HashSet<string>(_interpreter.GetModuleNames(), StringComparer.Ordinal);
            var unresolvedNames = _analyzer.GetAllUnresolvedModuleNames();

            foreach (var keyValue in _modules) {
                unloadedNames.Remove(keyValue.Key);
                unresolvedNames.Remove(keyValue.Key);
                yield return new KeyValuePair<string, ModuleLoadState>(keyValue.Key, new InitializedModuleLoadState(keyValue.Value));
            }

            foreach (var name in unloadedNames) {
                yield return new KeyValuePair<string, ModuleLoadState>(name, new UninitializedModuleLoadState(this, name));
            }

            foreach (var name in unresolvedNames) {
                yield return new KeyValuePair<string, ModuleLoadState>(name, new UnresolvedModuleLoadState());
            }
        }

        class UnresolvedModuleLoadState : ModuleLoadState {
            public override AnalysisValue Module {
                get { return null; }
            }

            public override bool HasModule {
                get { return false; }
            }

            public override bool HasReferences {
                get { return false; }
            }

            public override bool IsValid {
                get { return true; }
            }

            public override PythonMemberType MemberType {
                get { return PythonMemberType.Unknown; }
            }

            internal override bool ModuleContainsMember(IModuleContext context, string name) {
                return false;
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
                    ModuleReference res;
                    if (_moduleTable.TryGetValue(_name, out res)) {
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

            public override bool HasReferences {
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

            public override bool HasReferences {
                get {
                    return _reference.HasReferences;
                }
            }

            public override bool IsValid {
                get {
                    return Module != null || HasReferences;
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
            return GetEnumerator();
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

        public abstract bool HasReferences {
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
