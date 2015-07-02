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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    abstract class InterpreterScope {
        public readonly InterpreterScope OuterScope;
        public readonly List<InterpreterScope> Children = new List<InterpreterScope>();
        public bool ContainsImportStar;

        private readonly AnalysisValue _av;
        private readonly Node _node;
        private readonly AnalysisDictionary<Node, InterpreterScope> _nodeScopes;
        private readonly AnalysisDictionary<Node, IAnalysisSet> _nodeValues;
        private readonly AnalysisDictionary<string, VariableDef> _variables;
        private readonly AnalysisDictionary<string, HashSet<VariableDef>> _linkedVariables;

        public InterpreterScope(AnalysisValue av, Node ast, InterpreterScope outerScope) {
            _av = av;
            _node = ast;
            OuterScope = outerScope;

            _nodeScopes = new AnalysisDictionary<Node, InterpreterScope>();
            _nodeValues = new AnalysisDictionary<Node, IAnalysisSet>();
            _variables = new AnalysisDictionary<string, VariableDef>();
            _linkedVariables = new AnalysisDictionary<string, HashSet<VariableDef>>();
        }

        public InterpreterScope(AnalysisValue av, InterpreterScope outerScope)
            : this(av, null, outerScope) { }

        protected InterpreterScope(AnalysisValue av, InterpreterScope cloned, bool isCloned) {
            Debug.Assert(isCloned);
            _av = av;
            Children.AddRange(cloned.Children);
            _nodeScopes = cloned._nodeScopes;
            _nodeValues = cloned._nodeValues;
            _variables = cloned._variables;
            _linkedVariables = cloned._linkedVariables;
        }

        public InterpreterScope GlobalScope {
            get {
                for (var scope = this; scope != null; scope = scope.OuterScope) {
                    if (scope.OuterScope == null) {
                        return scope;
                    }
                }
                return null;
            }
        }

        public IEnumerable<InterpreterScope> EnumerateTowardsGlobal {
            get {
                for (var scope = this; scope != null; scope = scope.OuterScope) {
                    yield return scope;
                }
            }
        }

        public IEnumerable<InterpreterScope> EnumerateFromGlobal {
            get {
                return EnumerateTowardsGlobal.Reverse();
            }
        }

        /// <summary>
        /// Gets the index in the file/buffer that the scope actually starts on.  This is the index where the colon
        /// is on for the start of the body if we're a function or class definition.
        /// </summary>
        public virtual int GetBodyStart(PythonAst ast) {
            return GetStart(ast);
        }

        /// <summary>
        /// Gets the index in the file/buffer that this scope starts at.  This is the index which includes
        /// the definition it's self (e.g. def fob(...) or class fob(...)).
        /// </summary>
        public virtual int GetStart(PythonAst ast) {
            if (_node == null) {
                return 1;
            }
            return _node.GetStart(ast).Index;
        }

        /// <summary>
        /// Gets the index in the file/buffer that this scope ends at.
        /// </summary>
        public virtual int GetStop(PythonAst ast) {
            if (_node == null) {
                return int.MaxValue;
            }
            return _node.GetEnd(ast).Index;
        }

        public abstract string Name {
            get;
        }

        public Node Node {
            get {
                return _node;
            }
        }

        internal IEnumerable<KeyValuePair<string, VariableDef>> AllVariables {
            get { return _variables; }
        }

        internal IEnumerable<KeyValuePair<Node, InterpreterScope>> AllNodeScopes {
            get { return _nodeScopes; }
        }

        internal bool ContainsVariable(string name) {
            return _variables.ContainsKey(name);
        }

        internal VariableDef GetVariable(string name) {
            return _variables[name];
        }

        internal bool TryGetVariable(string name, out VariableDef value) {
            return _variables.TryGetValue(name, out value);
        }

        internal int VariableCount {
            get {
                return _variables.Count;
            }
        }

        /// <summary>
        /// Assigns a variable in the given scope, creating the variable if necessary, and performing
        /// any scope specific behavior such as propagating to outer scopes (is instance), updating
        /// __metaclass__ (class scopes), or enqueueing dependent readers (modules).
        /// 
        /// Returns true if a new type has been signed to the variable, false if the variable
        /// is left unchanged.
        /// </summary>
        public virtual bool AssignVariable(string name, Node location, AnalysisUnit unit, IAnalysisSet values) {
            var vars = CreateVariable(location, unit, name, false);

            return AssignVariableWorker(location, unit, values, vars);
        }

        /// <summary>
        /// Handles the base assignment case for assign to a variable, minus variable creation.
        /// </summary>
        protected static bool AssignVariableWorker(Node location, AnalysisUnit unit, IAnalysisSet values, VariableDef vars) {
            vars.AddAssignment(location, unit);
            vars.MakeUnionStrongerIfMoreThan(unit.ProjectState.Limits.AssignedTypes, values);
            return vars.AddTypes(unit, values);
        }

        public VariableDef AddLocatedVariable(string name, Node location, AnalysisUnit unit, ParameterKind paramKind = ParameterKind.Normal) {
            VariableDef value;
            if (!TryGetVariable(name, out value)) {
                VariableDef def;
                switch (paramKind) {
                    case ParameterKind.List: def = new ListParameterVariableDef(unit, location); break;
                    case ParameterKind.Dictionary: def = new DictParameterVariableDef(unit, location); break;
                    default: def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, location); break;
                }
                return AddVariable(name, def);
            } else if (!(value is LocatedVariableDef)) {
                VariableDef def;
                switch (paramKind) {
                    case ParameterKind.List: def = new ListParameterVariableDef(unit, location, value); break;
                    case ParameterKind.Dictionary: def = new DictParameterVariableDef(unit, location, value); break;
                    default: def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, location, value); break;
                }
                return AddVariable(name, def);
            } else {
                ((LocatedVariableDef)value).Node = location;
                ((LocatedVariableDef)value).DeclaringVersion = unit.ProjectEntry.AnalysisVersion;
            }
            return value;
        }

        public void SetVariable(Node node, AnalysisUnit unit, string name, IAnalysisSet value, bool addRef = true) {
            var variable = CreateVariable(node, unit, name, false);

            variable.AddTypes(unit, value);
            if (addRef) {
                variable.AddAssignment(node, unit);
            }
        }

        public virtual VariableDef GetVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            VariableDef res;
            if (!_variables.TryGetValue(name, out res)) {
                return null;
            }
            if (addRef) {
                res.AddReference(node, unit);
            }
            return res;
        }

        public virtual IEnumerable<KeyValuePair<string, VariableDef>> GetAllMergedVariables() {
            return _variables;
        }

        public virtual IEnumerable<VariableDef> GetMergedVariables(string name) {
            VariableDef res;
            if (_variables.TryGetValue(name, out res) && res != null) {
                yield return res;
            }
        }

        public virtual IAnalysisSet GetMergedVariableTypes(string name) {
            var res = AnalysisSet.Empty;
            foreach (var val in GetMergedVariables(name)) {
                res = res.Union(val.Types);
            }

            return res;
        }

        public virtual IEnumerable<AnalysisValue> GetMergedAnalysisValues() {
            yield return AnalysisValue;
        }

        public virtual VariableDef CreateVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = GetVariable(node, unit, name, addRef);
            if (res == null) {
                res = AddVariable(name);
                if (addRef) {
                    res.AddReference(node, unit);
                }
            }
            return res;
        }

        public VariableDef CreateEphemeralVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = GetVariable(node, unit, name, addRef);
            if (res == null) {
                res = AddVariable(name, new EphemeralVariableDef());
                if (addRef) {
                    res.AddReference(node, unit);
                }
            }
            return res;
        }

        public virtual VariableDef AddVariable(string name, VariableDef variable = null) {
            return _variables[name] = variable ?? new VariableDef();
        }

        internal virtual bool RemoveVariable(string name) {
            return _variables.Remove(name);
        }

        internal bool RemoveVariable(string name, out VariableDef value) {
            return _variables.TryGetValue(name, out value) && _variables.Remove(name);
        }

        internal virtual void ClearVariables() {
            _variables.Clear();
        }

        public virtual InterpreterScope AddNodeScope(Node node, InterpreterScope scope) {
            return _nodeScopes[node] = scope;
        }

        internal virtual bool RemoveNodeScope(Node node) {
            return _nodeScopes.Remove(node);
        }

        internal virtual void ClearNodeScopes() {
            _nodeScopes.Clear();
        }

        public virtual IAnalysisSet AddNodeValue(Node node, IAnalysisSet variable) {
            return _nodeValues[node] = variable;
        }

        internal virtual bool RemoveNodeValue(Node node) {
            return _nodeValues.Remove(node);
        }

        internal virtual void ClearNodeValues() {
            _nodeValues.Clear();
        }

        public virtual bool VisibleToChildren {
            get {
                return true;
            }
        }

        public AnalysisValue AnalysisValue {
            get {
                return _av;
            }
        }

        public void ClearLinkedVariables() {
            _linkedVariables.Clear();
        }

        internal bool AddLinkedVariable(string name, VariableDef variable) {
            HashSet<VariableDef> links;
            if (!_linkedVariables.TryGetValue(name, out links) || links == null) {
                _linkedVariables[name] = links = new HashSet<VariableDef>();
            }
            lock (links) {
                return links.Add(variable);
            }
        }

        internal IEnumerable<VariableDef> GetLinkedVariables(string name) {
            HashSet<VariableDef> links;
            _linkedVariables.TryGetValue(name, out links);

            if (links == null) {
                return Enumerable.Empty<VariableDef>();
            }
            return links.AsLockedEnumerable();
        }

        internal void AddReferenceToLinkedVariables(Node node, AnalysisUnit unit, string name) {
            foreach (var linkedVar in GetLinkedVariables(name)) {
                linkedVar.AddReference(node, unit);
            }
        }

        internal bool TryGetNodeValue(Node node, out IAnalysisSet variable) {
            foreach (var s in EnumerateTowardsGlobal) {
                if (s._nodeValues.TryGetValue(node, out variable)) {
                    return true;
                }
            }
            variable = null;
            return false;
        }

        internal bool TryGetNodeScope(Node node, out InterpreterScope scope) {
            foreach (var s in EnumerateTowardsGlobal) {
                if (s._nodeScopes.TryGetValue(node, out scope)) {
                    return true;
                }
            }
            scope = null;
            return false;
        }

        /// <summary>
        /// Cached node variables so that we don't continually create new entries for basic nodes such
        /// as sequences, lambdas, etc...
        /// </summary>
        public IAnalysisSet GetOrMakeNodeValue(Node node, Func<Node, IAnalysisSet> maker) {
            IAnalysisSet result;
            if (!TryGetNodeValue(node, out result)) {
                result = maker(node);
                AddNodeValue(node, result);
            }
            return result;
        }
    }
}
