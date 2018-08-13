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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    abstract class InterpreterScope {
        public readonly InterpreterScope OuterScope;
        private readonly List<InterpreterScope> _linkedScopes;

        public List<InterpreterScope> Children;
        public bool ContainsImportStar;

        private AnalysisDictionary<Node, InterpreterScope> _nodeScopes;
        private AnalysisDictionary<Node, NodeValue> _nodeValues;
        private AnalysisDictionary<string, VariableDef> _variables;
        private AnalysisDictionary<string, HashSet<VariableDef>> _linkedVariables;

        public InterpreterScope(AnalysisValue av, Node ast, InterpreterScope outerScope) {
            AnalysisValue = av;
            Node = ast;
            OuterScope = outerScope;

            Children = new List<InterpreterScope>();
            _nodeScopes = new AnalysisDictionary<Node, InterpreterScope>();
            _nodeValues = new AnalysisDictionary<Node, NodeValue>();
            _variables = new AnalysisDictionary<string, VariableDef>();
            _linkedVariables = new AnalysisDictionary<string, HashSet<VariableDef>>();
            _linkedScopes = new List<InterpreterScope>();
        }

        public InterpreterScope(AnalysisValue av, InterpreterScope outerScope)
            : this(av, null, outerScope) { }

        protected InterpreterScope(AnalysisValue av, InterpreterScope cloned, bool isCloned) {
            Debug.Assert(isCloned);
            AnalysisValue = av;
            Children = cloned.Children.ToList();
            _nodeScopes = cloned._nodeScopes;
            _nodeValues = cloned._nodeValues;
            _variables = cloned._variables;
            _linkedVariables = cloned._linkedVariables;
            OriginalScope = cloned.OriginalScope;
            _linkedScopes = cloned._linkedScopes;
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

        public IEnumerable<InterpreterScope> EnumerateFromGlobal => EnumerateTowardsGlobal.Reverse();

        internal InterpreterScope OriginalScope { get; private set; }

        /// <summary>
        /// Gets the index in the file/buffer that the scope actually starts on.  This is the index where the colon
        /// is on for the start of the body if we're a function or class definition.
        /// </summary>
        public virtual int GetBodyStart(PythonAst ast) => GetStart(ast);

        /// <summary>
        /// Gets the index in the file/buffer that this scope starts at.  This is the index which includes
        /// the definition it's self (e.g. def fob(...) or class fob(...)).
        /// </summary>
        public virtual int GetStart(PythonAst ast) {
            if (Node == null) {
                return 1;
            }
            return Node.StartIndex;
        }

        /// <summary>
        /// Gets the index in the file/buffer that this scope ends at.
        /// </summary>
        public virtual int GetStop(PythonAst ast) {
            if (Node == null) {
                return int.MaxValue;
            }
            return Node.EndIndex;
        }

        public abstract string Name { get; }

        public Node Node { get; }

        internal IEnumerable<KeyValuePair<string, VariableDef>> AllVariables  => _variables;
        internal IEnumerable<KeyValuePair<Node, InterpreterScope>> AllNodeScopes => _nodeScopes;
        internal bool ContainsVariable(string name) => _variables.ContainsKey(name);
        internal VariableDef GetVariable(string name) => _variables[name];
        internal bool TryGetVariable(string name, out VariableDef value) => _variables.TryGetValue(name, out value);
        internal int VariableCount => _variables.Count;

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
            vars.MakeUnionStrongerIfMoreThan(unit.State.Limits.AssignedTypes, values);
            return vars.AddTypes(unit, values);
        }

        public VariableDef AddLocatedVariable(string name, Node location, AnalysisUnit unit) {
            VariableDef value;
            if (!TryGetVariable(name, out value)) {
                var def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location));
                return AddVariable(name, def);
            }

            if (value is LocatedVariableDef lv) {
                lv.Location = new EncodedLocation(unit, location);
                lv.DeclaringVersion = unit.ProjectEntry.AnalysisVersion;
            } else {
                var def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location), value);
                return AddVariable(name, def);
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
            if (!_variables.TryGetValue(name, out var variable)) {
                return null;
            }

            if (addRef) {
                variable.AddReference(node, unit);
            }
            return variable;
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
            var res = GetVariable(node, unit, name, false) ?? AddVariable(name);
            if (addRef) {
                res.AddReference(node, unit);
            }
            return res;
        }

        public virtual VariableDef CreateLocatedVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var variable = GetVariable(node, unit, name, false);
            if (variable is LocatedVariableDef locatedVariable) {
                locatedVariable.Location = new EncodedLocation(unit, node);
                locatedVariable.DeclaringVersion = unit.ProjectEntry.AnalysisVersion;
            } else {
                var oldVariable = variable;
                variable = AddVariable(name, new LocatedVariableDef(unit.ProjectEntry, new EncodedLocation(unit, node)));
                oldVariable?.CopyTo(variable);
            }

            if (addRef) {
                variable.AddReference(node, unit);
            }
            return variable;
        }

        public VariableDef CreateEphemeralVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = GetVariable(node, unit, name, false) ?? AddVariable(name, new EphemeralVariableDef());
            if (addRef) {
                res.AddReference(node, unit);
            }
            return res;
        }

        public virtual VariableDef AddVariable(string name, VariableDef variable = null)
            => _variables[name] = variable ?? new VariableDef();

        internal virtual bool RemoveVariable(string name) {
            _linkedVariables.Remove(name);
            return _variables.Remove(name);
        }

        internal bool RemoveVariable(string name, out VariableDef value) {
            _linkedVariables.Remove(name);
            return _variables.TryGetValue(name, out value) && _variables.Remove(name);
        }

        internal virtual bool TryPropagateVariable(Node node, AnalysisUnit unit, string name, IAnalysisSet values, VariableDef ifNot = null, bool addRef = true) {
            if (!TryGetVariable(name, out var vd) || vd == ifNot) {
                return false;
            }
            if (addRef) {
                vd.AddReference(node, unit);
            }
            return vd.AddTypes(unit, values);
        }

        internal virtual void ClearVariables() {
            _variables = new AnalysisDictionary<string, VariableDef>();
        }

        public virtual InterpreterScope AddNodeScope(Node node, InterpreterScope scope) {
            return _nodeScopes[node] = scope;
        }

        internal virtual bool RemoveNodeScope(Node node) {
            return _nodeScopes.Remove(node);
        }

        internal virtual void ClearNodeScopes() {
            _nodeScopes = new AnalysisDictionary<Node, InterpreterScope>();
        }

        public virtual IAnalysisSet AddNodeValue(Node node, NodeValueKind kind, IAnalysisSet variable) {
            NodeValue next;
            _nodeValues.TryGetValue(node, out next);
#if DEBUG
            var tmp = next;
            while (tmp != null) {
                Debug.Assert(tmp.Kind != kind);
                tmp = tmp.Next;
            }
#endif
            _nodeValues[node] = new NodeValue(kind, variable, next);
            return variable;
        }

        internal virtual bool RemoveNodeValue(Node node) {
            return _nodeValues.Remove(node);
        }

        internal virtual void ClearNodeValues() {
            _nodeValues = new AnalysisDictionary<Node, NodeValue>();
        }

        public virtual bool VisibleToChildren => true;

        public AnalysisValue AnalysisValue { get; }

        public void ClearLinkedVariables() {
            _linkedVariables = new AnalysisDictionary<string, HashSet<VariableDef>>();
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

        internal void AddLinkedScope(InterpreterScope scope) {
            lock (_linkedScopes) {
                Debug.Assert(!_linkedScopes.Contains(scope));
                Debug.Assert(scope.OriginalScope == null);
                scope.OriginalScope = this;
                _linkedScopes.Add(scope);
            }
        }

        internal IEnumerable<InterpreterScope> GetLinkedScopes() => _linkedScopes.AsLockedEnumerable();

        internal bool TryGetNodeValue(Node node, NodeValueKind kind, out IAnalysisSet variable) {
            foreach (var s in EnumerateTowardsGlobal) {
                NodeValue value;
                if (s._nodeValues.TryGetValue(node, out value)) {
                    while (value != null) {
                        if (value.Kind == kind) {
                            variable = value.Variable;
                            return true;
                        }
                        value = value.Next;
                    }
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
        public IAnalysisSet GetOrMakeNodeValue(Node node, NodeValueKind kind, Func<Node, IAnalysisSet> maker) {
            IAnalysisSet result;
            if (!TryGetNodeValue(node, kind, out result)) {
                result = maker(node);
                AddNodeValue(node, kind, result);
            }
            return result;
        }
    }

    class NodeValue {
        public readonly IAnalysisSet Variable;
        public readonly NodeValueKind Kind;
        public NodeValue Next;

        public NodeValue(NodeValueKind kind, IAnalysisSet variable, NodeValue value) {
            Kind = kind;
            Variable = variable;
            Next = value;
        }
    }

    enum NodeValueKind {
        None,
        Set,
        DictLiteral,
        ListComprehension,
        LambdaFunction,
        Sequence,
        Range,
        ListOfString,
        StrDict,
        Iterator,
        Super,
        PartialFunction,
        Wraps,
        SpecializedInstance,
        Dictionary,
        TypeAnnotation,
        ParameterInfo
    }

}
