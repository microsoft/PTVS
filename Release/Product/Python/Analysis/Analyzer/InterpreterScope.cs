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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    abstract class InterpreterScope {
        private readonly Namespace _ns;
        private readonly ScopeStatement _node;
        public readonly List<InterpreterScope> Children = new List<InterpreterScope>();
        private Dictionary<string, VariableDef> _variables = new Dictionary<string, VariableDef>();
        private Dictionary<string, HashSet<VariableDef>> _linkedVariables;

        public InterpreterScope(Namespace ns, ScopeStatement ast) {
            _ns = ns;
            _node = ast;
        }

        public InterpreterScope(Namespace ns) {
            _ns = ns;
        }

        /// <summary>
        /// Gets the line that the scope actually starts on.  This is the line where the colon
        /// is on for the start of the body if we're a function or class definition.
        /// </summary>
        public virtual int GetBodyStart(PythonAst ast) {
            return GetStart(ast);
        }

        /// <summary>
        /// Gets the line number that this scope starts at.  This is the line number which includes
        /// the definition it's self (e.g. def foo(...) or class foo(...)).
        /// </summary>
        public virtual int GetStart(PythonAst ast) {
            if (_node == null) {
                return 1;
            }
            return _node.GetStart(ast).Line;
        }

        /// <summary>
        /// Gets the line number that this scope ends at.
        /// </summary>
        public virtual int GetStop(PythonAst ast) {
            if (_node == null) {
                return int.MaxValue;
            }
            return _node.GetEnd(ast).Line;
        }

        public abstract string Name {
            get;
        }

        public ScopeStatement Node {
            get {
                return _node;
            }
        }

        public VariableDef AddLocatedVariable(string name, Node location, AnalysisUnit unit, ParameterKind paramKind = ParameterKind.Normal) {
            VariableDef value;
            if (!Variables.TryGetValue(name, out value)) {
                VariableDef def;
                switch (paramKind) {
                    case ParameterKind.List: def = new ListParameterVariableDef(unit, location); break;
                    case ParameterKind.Dictionary: def = new DictParameterVariableDef(unit, location); break;
                    default: def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, location); break;
                }
                return Variables[name] = def;
            } else if (!(value is LocatedVariableDef)) {
                VariableDef def;
                switch (paramKind) {
                    case ParameterKind.List: def = new ListParameterVariableDef(unit, location, value); break;
                    case ParameterKind.Dictionary: def = new DictParameterVariableDef(unit, location, value); break;
                    default: def = new LocatedVariableDef(unit.DeclaringModule.ProjectEntry, location, value); break;
                }
                return Variables[name] = def;
            } else {
                ((LocatedVariableDef)value).Node = location;
            }
            return value;
        }

        public void SetVariable(Node node, AnalysisUnit unit, string name, IEnumerable<Namespace> value, bool addRef = true) {
            var variable = CreateVariable(node, unit, name, false);

            variable.AddTypes(node, unit, value);
            if (addRef) {
                variable.AddAssignment(node, unit);
            }
        }

        public VariableDef GetVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            VariableDef res;
            if (_variables.TryGetValue(name, out res)) {
                if (addRef) {
                    res.AddReference(node, unit);
                }
                return res;
            }
            return null;
        }

        public VariableDef CreateVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = GetVariable(node, unit, name, addRef);
            if (res == null) {
                _variables[name] = res = new VariableDef();
                if (addRef) {
                    res.AddReference(node, unit);
                }
            }
            return res;
        }

        public VariableDef CreateEphemeralVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var res = GetVariable(node, unit, name, addRef);
            if (res == null) {
                _variables[name] = res = new EphemeralVariableDef();
                if (addRef) {
                    res.AddReference(node, unit);
                }
            }
            return res;
        }

        protected VariableDef CreateVariableWorker(Node node, AnalysisUnit unit, string name) {
            VariableDef res;
            if (!_variables.TryGetValue(name, out res)) {
                _variables[name] = res = new VariableDef();
            }
            return res;
        }

        public IDictionary<string, VariableDef> Variables {
            get {
                return _variables;
            }
        }

        public virtual bool VisibleToChildren {
            get {
                return true;
            }
        }

        public Namespace Namespace {
            get {
                return _ns;
            }
        }

        internal HashSet<VariableDef> GetLinkedVariables(string saveName) {
            if (_linkedVariables == null) {
                _linkedVariables = new Dictionary<string, HashSet<VariableDef>>();
            }
            HashSet<VariableDef> links;
            if (!_linkedVariables.TryGetValue(saveName, out links)) {
                _linkedVariables[saveName] = links = new HashSet<VariableDef>();
            }
            return links;
        }

        internal HashSet<VariableDef> GetLinkedVariablesNoCreate(string saveName) {
            HashSet<VariableDef> linkedVars;
            if (_linkedVariables == null || ! _linkedVariables.TryGetValue(saveName, out linkedVars)) {
                return null;
            }
            return linkedVars;
        }
    }
}
