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
        //private readonly Dictionary<Node, ImportInfo> _imports;         // imports performed during the module
        private readonly ModuleScope _scope;
        private readonly Dictionary<Node, InterpreterScope> _scopes;    // scopes from Ast node to InterpreterScope
        private readonly WeakReference _weakModule;
        private readonly IModuleContext _context;
        private Dictionary<string, WeakReference> _packageModules;
        private ModuleInfo _parentPackage;
        private DependentData _definition = new DependentData();

        public ModuleInfo(string moduleName, ProjectEntry projectEntry, IModuleContext moduleContext) {
            _name = moduleName;
            _projectEntry = projectEntry;
            _sequences = new Dictionary<Node, ISet<Namespace>>();
            //_imports = new Dictionary<Node, ImportInfo>();
            _scope = new ModuleScope(this);
            _weakModule = new WeakReference(this);
            _context = moduleContext;
            _scopes = new Dictionary<Node, InterpreterScope>();
        }
        
        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            var res = new Dictionary<string, ISet<Namespace>>();
            foreach (var kvp in _scope.Variables) {
                foreach (var module in kvp.Value._dependencies.Keys.ToArray()) {
                    kvp.Value.ClearOldValues(module);
                }
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

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            ModuleDefinition.AddDependency(unit);

            return Scope.CreateVariable(node, unit, name).Types;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            if (variable.AddTypes(node, unit, value)) {
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

        public string Name {
            get { return _name; }
        }

        public ProjectEntry ProjectEntry {
            get { return _projectEntry; }
        }
        /*
        public Dictionary<Node, ImportInfo> Imports {
            get {
                return _imports;
            }
        }*/

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
                var doc = ProjectEntry.Tree.Documentation.TrimDocumentation();
                if (doc != null) {
                    result.Append("\n\n");
                    result.Append(doc);
                }
                return result.ToString();
            }
        }

        public override LocationInfo Location {
            get {
                return new LocationInfo(ProjectEntry, 1, 1);
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
