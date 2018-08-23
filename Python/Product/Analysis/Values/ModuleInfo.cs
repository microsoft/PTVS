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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ModuleInfo : AnalysisValue, IReferenceableContainer, IModule {
        private readonly string _name;
        private readonly ProjectEntry _projectEntry;
        private Dictionary<Node, IAnalysisSet> _sequences;  // sequences defined in the module
        private readonly ModuleScope _scope;
        private readonly Dictionary<Node, InterpreterScope> _scopes;    // scopes from Ast node to InterpreterScope
        private readonly WeakReference _weakModule;
        private readonly IModuleContext _context;
        private Dictionary<string, WeakReference> _packageModules;
        private Dictionary<string, Tuple<CallDelegate, bool>> _specialized;
        private ModuleInfo _parentPackage;
        private DependentData _definition = new DependentData();
        private readonly HashSet<ModuleReference> _referencedModules;
        private readonly HashSet<String> _unresolvedModules;

        public ModuleInfo(string moduleName, ProjectEntry projectEntry, IModuleContext moduleContext) {
            _name = moduleName;
            _projectEntry = projectEntry;
            _sequences = new Dictionary<Node, IAnalysisSet>();
            _scope = new ModuleScope(this);
            _weakModule = new WeakReference(this);
            _context = moduleContext;
            _scopes = new Dictionary<Node, InterpreterScope>();
            _referencedModules = new HashSet<ModuleReference>();
            _unresolvedModules = new HashSet<string>(StringComparer.Ordinal);
        }

        internal void Clear() {
            _sequences.Clear();
            _scope.ClearLinkedVariables();
            _scope.ClearVariables();
            _scope.ClearNodeScopes();
            _referencedModules.Clear();
            _unresolvedModules.Clear();
        }

        internal void EnsureModuleVariables(PythonAnalyzer state) {
            var entry = ProjectEntry;

            _scope.SetModuleVariable("__builtins__", state.ClassInfos[BuiltinTypeId.Dict].Instance);
            _scope.SetModuleVariable("__file__", GetStr(state, entry.FilePath));
            _scope.SetModuleVariable("__name__", GetStr(state, Name));
            _scope.SetModuleVariable("__package__", GetStr(state, ParentPackage?.Name));
            if (state.LanguageVersion.Is3x()) {
                _scope.SetModuleVariable("__cached__", GetStr(state));
                if (ModulePath.IsInitPyFile(entry.FilePath)) {
                    _scope.SetModuleVariable("__path__", state.ClassInfos[BuiltinTypeId.List].Instance);
                }
                _scope.SetModuleVariable("__spec__", state.ClassInfos[BuiltinTypeId.Object].Instance);
            }
            ModuleDefinition.EnqueueDependents();

        }
        private static IAnalysisSet GetStr(PythonAnalyzer state, string s = null) {
            if (string.IsNullOrEmpty(s)) {
                return state.ClassInfos[BuiltinTypeId.Str].Instance;
            }
            if (state.LanguageVersion.Is2x()) {
                return state.GetConstant(new AsciiString(new UTF8Encoding(false).GetBytes(s), s));
            }
            return state.GetConstant(s);
        }

        /// <summary>
        /// Returns all the absolute module names that need to be resolved from
        /// this module.
        /// 
        /// Note that a single import statement may add multiple names to this
        /// set, and so the Count property does not accurately reflect the 
        /// actual number of imports required.
        /// </summary>
        internal ISet<string> GetAllUnresolvedModules() {
            return _unresolvedModules;
        }

        internal void AddUnresolvedModule(string relativeModuleName, bool absoluteImports) {
            _unresolvedModules.UnionWith(ModuleResolver.ResolvePotentialModuleNames(_projectEntry, relativeModuleName, absoluteImports));
            _projectEntry.ProjectState.ModuleHasUnresolvedImports(this, true);
        }

        internal void ClearUnresolvedModules() {
            _unresolvedModules.Clear();
            _projectEntry.ProjectState.ModuleHasUnresolvedImports(this, false);
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            var res = new Dictionary<string, IAnalysisSet>();
            foreach (var kvp in _scope.AllVariables) {
                if (!options.ForEval()) {
                    kvp.Value.ClearOldValues();
                }
                if (kvp.Value._dependencies.Count > 0) {
                    var types = kvp.Value.Types;
                    if (types.Count > 0) {
                        res[kvp.Key] = types;
                    }
                }
            }
            return res;
        }

        public IModuleContext InterpreterContext => _context;

        public ModuleInfo ParentPackage {
            get { return _parentPackage; }
            set { _parentPackage = value; }
        }

        public void AddChildPackage(ModuleInfo childPackage, AnalysisUnit curUnit, string realName = null) {
            realName = realName ?? childPackage.Name;
            int lastDot;
            if ((lastDot = realName.LastIndexOf('.')) != -1) {
                realName = realName.Substring(lastDot + 1);
            }

            childPackage.ParentPackage = this;
            Scope.SetVariable(childPackage.ProjectEntry.Tree, curUnit, realName, childPackage.SelfSet, false);

            if (_packageModules == null) {
                _packageModules = new Dictionary<string, WeakReference>();
            }
            _packageModules[realName] = childPackage.WeakModule;
        }

        public IEnumerable<KeyValuePair<string, AnalysisValue>> GetChildrenPackages(IModuleContext moduleContext) {
            if (_packageModules != null) {
                foreach (var keyValue in _packageModules) {
                    var res = keyValue.Value.Target as IModule;
                    if (res != null) {
                        yield return new KeyValuePair<string, AnalysisValue>(keyValue.Key, (AnalysisValue)res);
                    }
                }
            }
        }

        public IModule GetChildPackage(IModuleContext moduleContext, string name) {
            WeakReference weakMod;
            if (_packageModules != null && _packageModules.TryGetValue(name, out weakMod)) {
                var res = weakMod.Target;
                if (res != null) {
                    return (IModule)res;
                }

                _packageModules.Remove(name);
            }
            return null;
        }

        public void AddModuleReference(ModuleReference moduleRef) {
            if (moduleRef == null) {
                Debug.Fail("moduleRef should never be null");
                throw new ArgumentNullException(nameof(moduleRef));
            }
            _referencedModules.Add(moduleRef);
            moduleRef.AddReference(this);
        }

        public void RemoveModuleReference(ModuleReference moduleRef) {
            if (_referencedModules.Remove(moduleRef)) {
                moduleRef.RemoveReference(this);
            }
        }

        public IEnumerable<ModuleReference> ModuleReferences => _referencedModules;
        public void SpecializeFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            lock (this) {
                if (_specialized == null) {
                    _specialized = new Dictionary<string, Tuple<CallDelegate, bool>>();
                }
                _specialized[name] = Tuple.Create(callable, mergeOriginalAnalysis);
            }
        }

        internal void Specialize() {
            lock (this) {
                if (_specialized != null) {
                    foreach (var keyValue in _specialized) {
                        SpecializeOneFunction(keyValue.Key, keyValue.Value.Item1, keyValue.Value.Item2);
                    }
                }
            }
        }

        private void SpecializeOneFunction(string name, CallDelegate callable, bool mergeOriginalAnalysis) {
            int lastIndex;
            VariableDef def;
            if (Scope.TryGetVariable(name, out def)) {
                SpecializeVariableDef(def, callable, mergeOriginalAnalysis);
            } else if ((lastIndex = name.LastIndexOf('.')) != -1 &&
                Scope.TryGetVariable(name.Substring(0, lastIndex), out def)) {
                var methodName = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));
                foreach (var v in def.TypesNoCopy) {
                    ClassInfo ci = v as ClassInfo;
                    if (ci != null) {
                        VariableDef methodDef;
                        if (ci.Scope.TryGetVariable(methodName, out methodDef)) {
                            SpecializeVariableDef(methodDef, callable, mergeOriginalAnalysis);
                        }
                    }
                }
            }
        }

        private static void SpecializeVariableDef(VariableDef def, CallDelegate callable, bool mergeOriginalAnalysis) {
            List<AnalysisValue> items = new List<AnalysisValue>();
            foreach (var v in def.TypesNoCopy) {
                if (!(v is SpecializedNamespace) && v.DeclaringModule != null) {
                    items.Add(v);
                }
            }

            def._dependencies = default(SingleDict<IVersioned, ReferenceableDependencyInfo>);
            foreach (var item in items) {
                def.AddTypes(item.DeclaringModule, new SpecializedCallable(item, callable, mergeOriginalAnalysis).SelfSet);
            }
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.Empty;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            if (unit.ForEval) {
                VariableDef value;
                return Scope.TryGetVariable(name, out value) ? value.Types : AnalysisSet.Empty;
            } else {
                ModuleDefinition.AddDependency(unit);
                return Scope.CreateEphemeralVariable(node, unit, name).Types;
            }
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            if (variable.AddTypes(unit, value, true, ProjectEntry)) {
                ModuleDefinition.EnqueueDependents();
            }

            variable.AddAssignment(node, unit);
        }

        /// <summary>
        /// Gets a weak reference to this module
        /// </summary>
        public WeakReference WeakModule {
            get {
                return _weakModule;
            }
        }

        public DependentData ModuleDefinition {
            get {
                return _definition;
            }
        }

        public ModuleScope Scope {
            get {
                return _scope;
            }
        }

        public override string Name {
            get { return _name; }
        }

        public ProjectEntry ProjectEntry {
            get { return _projectEntry; }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Module;
            }
        }

        public override string ToString()  => $"Module {base.ToString()}";
        public override string ShortDescription => $"Python module {Name}";

        public override string Description {
            get {
                var result = new StringBuilder("Python module ");
                result.Append(Name);
                var doc = Documentation;
                if (!string.IsNullOrEmpty(doc)) {
                    result.Append("\n\n");
                    result.Append(doc);
                }
                return result.ToString();
            }
        }

        public override string Documentation {
            get {
                if (ProjectEntry.Tree != null && ProjectEntry.Tree.Body != null) {
                    return ProjectEntry.Tree.Body.Documentation.TrimDocumentation() ?? String.Empty;
                }
                return String.Empty;
            }
        }

        public override IEnumerable<LocationInfo> Locations 
             => new[] { new LocationInfo(ProjectEntry.FilePath, ProjectEntry.DocumentUri, 1, 1) };

        public override IPythonType PythonType
            => ProjectEntry.ProjectState.Types[BuiltinTypeId.Module];

        internal override BuiltinTypeId TypeId => BuiltinTypeId.Module;

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            VariableDef def;
            if (_scope.TryGetVariable(name, out def)) {
                yield return def;
            }
        }

        #endregion

        public IAnalysisSet GetModuleMember(Node node, AnalysisUnit unit, string name, bool addRef = true, InterpreterScope linkedScope = null, string linkedName = null) {
            var importedValue = Scope.CreateEphemeralVariable(node, unit, name, addRef);
            ModuleDefinition.AddDependency(unit);

            if (linkedScope != null) {
                linkedScope.AddLinkedVariable(linkedName ?? name, importedValue);
            }
            return importedValue.GetTypesNoCopy(unit, DeclaringModule);
        }


        public IEnumerable<string> GetModuleMemberNames(IModuleContext context) {
            return Scope.AllVariables.Select(kv => kv.Key);
        }

        public bool IsMemberDefined(IModuleContext context, string member) {
            if (Scope.TryGetVariable(member, out VariableDef v)) {
                return v.TypesNoCopy.Any(m => m.DeclaringModule == _projectEntry);
            }
            return false;
        }

        public void Imported(AnalysisUnit unit) {
            ModuleDefinition.AddDependency(unit);
        }
    }
}
