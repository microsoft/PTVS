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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ModuleInfo : Namespace, IReferenceableContainer, IModule {
        private readonly string _name;
        private readonly ProjectEntry _projectEntry;
        private readonly Dictionary<Node, ISet<Namespace>> _sequences;  // sequences defined in the module
        private readonly ModuleScope _scope;
        private readonly Dictionary<Node, InterpreterScope> _scopes;    // scopes from Ast node to InterpreterScope
        private readonly WeakReference _weakModule;
        private readonly IModuleContext _context;
        private Dictionary<string, WeakReference> _packageModules;
        private Dictionary<string, Tuple<Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>>, bool>> _specialized;
        private ModuleInfo _parentPackage;
        private DependentData _definition = new DependentData();

        public ModuleInfo(string moduleName, ProjectEntry projectEntry, IModuleContext moduleContext) {
            _name = moduleName;
            _projectEntry = projectEntry;
            _sequences = new Dictionary<Node, ISet<Namespace>>();
            _scope = new ModuleScope(this);
            _weakModule = new WeakReference(this);
            _context = moduleContext;
            _scopes = new Dictionary<Node, InterpreterScope>();
        }

        internal void Clear() {
            _sequences.Clear();
            _scope.ClearLinkedVariables();
            _scope.Variables.Clear();
            _scopes.Clear();
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            var res = new Dictionary<string, ISet<Namespace>>();
            foreach (var kvp in _scope.Variables) {
                kvp.Value.ClearOldValues();
                if (kvp.Value._dependencies.Count > 0 || kvp.Value.Types.Count > 0) {
                    res[kvp.Key] = kvp.Value.Types;
                }
            }
            return res;
        }

        public IModuleContext InterpreterContext {
            get {
                return _context;
            }
        }

        public ModuleInfo ParentPackage {
            get { return _parentPackage; }
            set { _parentPackage = value; }
        }

        public void AddChildPackage(ModuleInfo childPackage, AnalysisUnit curUnit) {
            string realName = childPackage.Name;
            int lastDot;
            if ((lastDot = childPackage.Name.LastIndexOf('.')) != -1) {
                realName = childPackage.Name.Substring(lastDot + 1);
            }

            childPackage.ParentPackage = this;            
            Scope.SetVariable(childPackage.ProjectEntry.Tree, curUnit, realName, childPackage.SelfSet, false);

            if (_packageModules == null) {
                _packageModules = new Dictionary<string, WeakReference>();
            }
            _packageModules[realName] = childPackage.WeakModule;
        }

        public IEnumerable<KeyValuePair<string, Namespace>> GetChildrenPackages(IModuleContext moduleContext) {
            if (_packageModules != null) {
                foreach (var keyValue in _packageModules) {
                    var res = (ModuleInfo)keyValue.Value.Target;
                    if (res != null) {
                        yield return new KeyValuePair<string, Namespace>(keyValue.Key, res);
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

        public void SpecializeFunction(string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, bool analyze) {
            if (_specialized == null) {
                _specialized = new Dictionary<string, Tuple<Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>>, bool>>();
            }
            _specialized[name] = new Tuple<Func<CallExpression,AnalysisUnit,ISet<Namespace>[],ISet<Namespace>>,bool>(dlg, analyze);
        }

        internal void Specialize() {
            if (_specialized != null) {
                foreach (var keyValue in _specialized) {
                    SpecializeOneFunction(keyValue.Key, keyValue.Value.Item1, keyValue.Value.Item2);
                }
            }
        }

        private void SpecializeOneFunction(string name, Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, bool analyze) {
            int lastIndex;
            VariableDef def;
            if (Scope.Variables.TryGetValue(name, out def)) {
                SpecializeVariableDef(dlg, def, analyze);
            } else if ((lastIndex = name.LastIndexOf('.')) != -1 && 
                Scope.Variables.TryGetValue(name.Substring(0, lastIndex), out def)) {
                    var methodName = name.Substring(lastIndex + 1, name.Length - (lastIndex + 1));
                    foreach (var v in def.Types) {
                        ClassInfo ci = v as ClassInfo;
                        if (ci != null) {
                            VariableDef methodDef;
                            if (ci.Scope.Variables.TryGetValue(methodName, out methodDef)) {
                                SpecializeVariableDef(dlg, methodDef, analyze);
                            }
                        }
                    }
            }
        }

        private static void SpecializeVariableDef(Func<CallExpression, AnalysisUnit, ISet<Namespace>[], ISet<Namespace>> dlg, VariableDef def, bool analyze) {
            List<Namespace> items = new List<Namespace>();
            foreach (var v in def.Types) {
                if (!(v is SpecializedNamespace) && v.DeclaringModule != null) {
                    items.Add(v);
                }
            }

            def._dependencies = default(SingleDict<IProjectEntry, TypedDependencyInfo<Namespace>>);
            foreach (var item in items) {
                def.AddTypes(item.DeclaringModule, SpecializedCallable.MakeSpecializedCallable(dlg, analyze, item).SelfSet);
            }
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ModuleDefinition.AddDependency(unit);

            return Scope.CreateEphemeralVariable(node, unit, name).Types;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            if (variable.AddTypes(unit, value)) {
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

        public override PythonMemberType ResultType {
            get {
                return PythonMemberType.Module;
            }
        }

        public override string ToString() {
            return "Module " + base.ToString();
        }

        public override string ShortDescription {
            get {
                return "Python module " + Name;
            }
        }

        public override string Description {
            get {
                var result = new StringBuilder("Python module ");
                result.Append(Name);
                var doc = ProjectEntry.Tree.Body.Documentation.TrimDocumentation();
                if (doc != null) {
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

        public override IEnumerable<LocationInfo> Locations {
            get {
                return new[] { new LocationInfo(ProjectEntry, 1, 1) };
            }
        }

        public Dictionary<Node, ISet<Namespace>> NodeVariables {
            get { return _sequences; }
        }

        /// <summary>
        /// Provides a mapping from functions/classes declared in this module to their interpreter scope.
        /// </summary>
        public Dictionary<Node, InterpreterScope> NodeScopes {
            get { return _scopes; }
        }

        /// <summary>
        /// Cached node variables so that we don't continually create new entries for basic nodes such
        /// as sequences, lambdas, etc...
        /// </summary>
        public ISet<Namespace> GetOrMakeNodeVariable(Node node, Func<Node, ISet<Namespace>> maker) {
            ISet<Namespace> result;
            if (!NodeVariables.TryGetValue(node, out result)) {
                result = NodeVariables[node] = maker(node);
            }
            return result;
        }

        public override IPythonType PythonType {
            get { return this.ProjectEntry.ProjectState.Types.Module; }
        }

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            VariableDef def;
            if (_scope.Variables.TryGetValue(name, out def)) {
                yield return def;
            }
        }

        #endregion

    }
}
