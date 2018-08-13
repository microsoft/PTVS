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
using System.Threading.Tasks;
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

        private readonly ConcurrentDictionary<string, Func<BuiltinModule, BuiltinModule>> _builtinModuleType = new ConcurrentDictionary<string, Func<BuiltinModule, BuiltinModule>>();

        public ModuleTable(PythonAnalyzer analyzer, IPythonInterpreter interpreter) {
            _analyzer = analyzer;
            _interpreter = interpreter;
        }

        public bool Contains(string name) {
            return _modules.ContainsKey(name);
        }

        public void AddBuiltinModuleWrapper(string moduleName, Func<BuiltinModule, BuiltinModule> moduleWrapper) {
            if (_modules.TryGetValue(moduleName, out var modRef) &&
                modRef.Module is IPythonModule pm &&
                _builtinModuleTable.TryGetValue(pm, out var existing)) {
                _builtinModuleTable[pm] = moduleWrapper(existing);
            } else {
                _builtinModuleType[moduleName] = moduleWrapper;
            }
        }

        /// <summary>
        /// Gets a reference to a module that has already been imported. You
        /// probably want to use <see cref="TryImport"/>.
        /// </summary>
        /// <returns>
        /// True if an attempt to import the module was made during the analysis
        /// that used this module table. The reference may be null, or the
        /// module within the reference may be null, even if this function
        /// returns true.
        /// </returns>
        /// <remarks>
        /// This exists for inspecting the results of an analysis (for example,
        /// <see cref="SaveAnalysis"/>). To get access to a module while
        /// analyzing code, even (especially!) if the module may not exist,
        /// you should call <see cref="TryImport"/>.
        /// </remarks>
        internal bool TryGetImportedModule(string name, out ModuleReference res) {
            return _modules.TryGetValue(name, out res);
        }
        
        /// <summary>
        /// Gets a reference to a module.
        /// </summary>
        /// <param name="name">The full import name of the module.</param>
        /// <param name="res">The module reference object.</param>
        /// <returns>
        /// True if the module is available. This means that <c>res.Module</c>
        /// is not null. If this function returns false, <paramref name="res"/>
        /// may be valid and should not be replaced, but it is an unresolved
        /// reference.
        /// </returns>
        public async Task<ModuleReference> TryImportAsync(string name, CancellationToken token) {
            ModuleReference res;
            bool firstImport = false;

            if (!_modules.TryGetValue(name, out res) || res == null) {
                var mod = await ImportModuleAsync(name, token).ConfigureAwait(false);
                _modules[name] = res = new ModuleReference(GetBuiltinModule(mod), name);
                firstImport = true;
            }

            if (res != null && res.Module == null) {
                var mod = await ImportModuleAsync(name, token).ConfigureAwait(false);
                res.Module = GetBuiltinModule(mod);
            }
            if (firstImport && res != null && res.Module != null && _analyzer != null) {
                await Task.Run(() => _analyzer.DoDelayedSpecialization(name)).ConfigureAwait(false);
            }
            if (res != null && res.Module == null) {
                return null;
            }
            return res;
        }

        private async Task<IPythonModule> ImportModuleAsync(string name, CancellationToken token) {
            var interpreter2 = _interpreter as IPythonInterpreter2;
            IPythonModule mod;
            if (interpreter2 != null) {
                mod = await interpreter2.ImportModuleAsync(name, token).ConfigureAwait(false);
            } else {
                mod = await Task.Run(() => _interpreter.ImportModule(name)).ConfigureAwait(false);
            }
            return mod;
        }

        /// <summary>
        /// Gets a reference to a module.
        /// </summary>
        /// <param name="name">The full import name of the module.</param>
        /// <param name="res">The module reference object.</param>
        /// <returns>
        /// True if the module is available. This means that <c>res.Module</c>
        /// is not null. If this function returns false, <paramref name="res"/>
        /// may be valid and should not be replaced, but it is an unresolved
        /// reference.
        /// </returns>
        public bool TryImport(string name, out ModuleReference res) {
            var firstImport = false;
            if (!_modules.TryGetValue(name, out res) || res == null) {
                _modules[name] = res = new ModuleReference(GetBuiltinModule(_interpreter.ImportModule(name)), name);
                firstImport = true;
            }
            if (res != null && res.Module == null) {
                res.Module = GetBuiltinModule(_interpreter.ImportModule(name));
            }
            if (firstImport && res != null && res.Module != null && _analyzer != null) {
                _analyzer.DoDelayedSpecialization(name);
            }
            return res != null && res.Module != null;
        }
        public bool TryRemove(string name, out ModuleReference res) => _modules.TryRemove(name, out res);

        public ModuleReference this[string name] {
            get {
                ModuleReference res;
                if (!TryImport(name, out res)) {
                    throw new KeyNotFoundException(name);
                }
                return res;
            }
            set {
                _modules[name] = value;
            }
        }

        public ModuleReference GetOrAdd(string name) {
            return _modules.GetOrAdd(name, _ => new ModuleReference(name: name));
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
                    IPythonModule newModule = null;
                    if (newNames.Contains(name)) {
                        newModule = _interpreter.ImportModule(name);
                    }

                    if (newModule == null) {
                        // this module was unloaded
                        ModuleReference dummy;
                        _modules.TryRemove(name, out dummy);

                        BuiltinModule removedModule;
                        _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out removedModule);
                        foreach (var child in builtinModule.InterpreterModule.GetChildrenModules()) {
                            _modules.TryRemove(builtinModule.Name + "." + child, out dummy);
                        }
                    } else if (builtinModule.InterpreterModule != newModule) {
                        // this module was replaced with a new module
                        BuiltinModule removedModule;
                        _builtinModuleTable.TryRemove(builtinModule.InterpreterModule, out removedModule);
                        moduleRef.Module = GetBuiltinModule(newModule);
                        ImportChildren(newModule);
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
                Func<BuiltinModule, BuiltinModule> wrap;
                res = new BuiltinModule(attr, _analyzer);
                if (_builtinModuleType.TryGetValue(attr.Name, out wrap) && wrap != null) {
                    res = wrap(res);
                }
                _builtinModuleTable[attr] = res;
            }
            return res;
        }

        internal void ImportChildren(IPythonModule interpreterModule) {
            BuiltinModule module = null;
            foreach (var child in interpreterModule.GetChildrenModules()) {
                module = module ?? GetBuiltinModule(interpreterModule);
                ModuleReference modRef;
                var fullname = module.Name + "." + child;
                if (!_modules.TryGetValue(fullname, out modRef) || modRef == null || modRef.Module == null)  {
                    IAnalysisSet value;
                    if (module.TryGetMember(child, out value)) {
                        var mod = value as IModule;
                        if (mod != null) {
                            _modules[fullname] = new ModuleReference(mod, fullname);
                            _analyzer?.DoDelayedSpecialization(fullname);
                        }
                    }
                }
            }
        }

        #region IEnumerable<KeyValuePair<string,ModuleReference>> Members

        public IEnumerator<KeyValuePair<string, ModuleLoadState>> GetEnumerator() {
            var unloadedNames = new HashSet<string>(_interpreter.GetModuleNames(), StringComparer.Ordinal);
            var unresolvedNames = _analyzer?.GetAllUnresolvedModuleNames();

            foreach (var keyValue in _modules) {
                unresolvedNames?.Remove(keyValue.Key);

                if (keyValue.Value.Module is Interpreter.Ast.AstNestedPythonModule anpm && !anpm.IsLoaded) {
                    continue;
                }

                unloadedNames.Remove(keyValue.Key);
                yield return new KeyValuePair<string, ModuleLoadState>(keyValue.Key, new InitializedModuleLoadState(keyValue.Value));
            }

            foreach (var name in unloadedNames) {
                yield return new KeyValuePair<string, ModuleLoadState>(name, new UninitializedModuleLoadState(this, name));
            }

            if (unresolvedNames != null) {
                foreach (var name in unresolvedNames) {
                    yield return new KeyValuePair<string, ModuleLoadState>(name, new UnresolvedModuleLoadState());
                }
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

            public override string Name {
                get { return null; }
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

            public UninitializedModuleLoadState(
                ModuleTable moduleTable,
                string name
            ) {
                this._moduleTable = moduleTable;
                this._name = name;
            }

            public override AnalysisValue Module {
                get {
                    ModuleReference res;
                    if (_moduleTable.TryImport(_name, out res)) {
                        _type = res.AnalysisModule?.MemberType;
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

            public override string Name {
                get {
                    return _name;
                }
            }

            public override string MaybeSourceFile {
                get {
                    ModuleReference res;
                    if (_moduleTable.TryGetImportedModule(_name, out res)) {
                        return res.AnalysisModule?.DeclaringModule?.FilePath;
                    }
                    return null;
                }
            }

            public override PythonMemberType MemberType {
                get {
                    return _type ?? PythonMemberType.Module;
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
                if (reference == null) {
                    throw new ArgumentNullException(nameof(reference));
                }
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

            public override string Name => _reference.Name;
            public override string MaybeSourceFile => Module?.DeclaringModule?.FilePath;

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
                    if (modInfo.Scope.TryGetVariable(name, out varDef) &&
                        varDef.VariableStillExists) {
                        var types = varDef.TypesNoCopy;
                        if (types.Count > 0) {
                            foreach (var type in types) {
                                if (type is ModuleInfo || type is BuiltinModule) {
                                    // we find modules via our modules list, dont duplicate these
                                    return false;
                                }

                                foreach (var location in type.Locations) {
                                    if (location.FilePath != modInfo.ProjectEntry.FilePath) {
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

        public abstract string Name {
            get;
        }

        public virtual string MaybeSourceFile {
            get { return null; }
        }

        public abstract PythonMemberType MemberType {
            get;
        }

        internal abstract bool ModuleContainsMember(IModuleContext context, string name);
    }
}
