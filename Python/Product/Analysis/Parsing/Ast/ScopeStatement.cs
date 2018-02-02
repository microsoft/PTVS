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
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing.Ast {

    public abstract class ScopeStatement : Statement {
        private bool _importStar;                   // from module import *
        private bool _unqualifiedExec;              // exec "code"
        private bool _nestedFreeVariables;          // nested function with free variable
        private bool _locals;                       // The scope needs locals dictionary
        // due to "exec" or call to dir, locals, eval, vars...
        private bool _hasLateboundVarSets;          // calls code which can assign to variables
        private bool _containsExceptionHandling;    // true if this block contains a try/with statement

        private Dictionary<string, PythonVariable> _variables;          // mapping of string to variables
        private ClosureInfo[] _closureVariables;                        // closed over variables, bool indicates if we accessed it in this scope.
        private List<PythonVariable> _freeVars;                         // list of variables accessed from outer scopes
        private List<string> _globalVars;                               // global variables accessed from this scope
        private List<string> _cellVars;                                 // variables accessed from nested scopes
        private List<NameExpression> _nonLocalVars;                             // variables declared as nonlocal within this scope
        private Dictionary<string, List<PythonReference>> _references;        // names of all variables referenced, null after binding completes
        private ScopeStatement _parent;

        internal const string NameForExec = "module: <exec>";

        public ScopeStatement Parent {
            get { return _parent; }
            set { _parent = value; }
        }

        public abstract Statement Body {
            get;
        }

        internal bool ContainsImportStar {
            get { return _importStar; }
            set { _importStar = value; }
        }

        internal bool ContainsExceptionHandling {
            get {
                return _containsExceptionHandling;
            }
            set {
                _containsExceptionHandling = value;
            }
        }

        internal bool ContainsUnqualifiedExec {
            get { return _unqualifiedExec; }
            set { _unqualifiedExec = value; }
        }

        /// <summary>
        /// True if this scope accesses a variable from an outer scope.
        /// </summary>
        internal bool IsClosure {
            get { return FreeVariables != null && FreeVariables.Count > 0; }
        }

        /// <summary>
        /// True if an inner scope is accessing a variable defined in this scope.
        /// </summary>
        internal bool ContainsNestedFreeVariables {
            get { return _nestedFreeVariables; }
            set { _nestedFreeVariables = value; }
        }

        /// <summary>
        /// True if we are forcing the creation of a dictionary for storing locals.
        /// 
        /// This occurs for calls to locals(), dir(), vars(), unqualified exec, and
        /// from ... import *.
        /// </summary>
        internal bool NeedsLocalsDictionary {
            get { return _locals; }
            set { _locals = value; }
        }

        public virtual string/*!*/ Name {
            get {
                return "<unknown>";
            }
        }

        /// <summary>
        /// True if variables can be set in a late bound fashion that we don't
        /// know about at code gen time - for example via from fob import *.
        /// 
        /// This is tracked independently of the ContainsUnqualifiedExec/NeedsLocalsDictionary
        /// </summary>
        internal virtual bool HasLateBoundVariableSets {
            get {
                return _hasLateboundVarSets;
            }
            set {
                _hasLateboundVarSets = value;
            }
        }

        internal Dictionary<string, PythonVariable> Variables {
            get { return _variables; }
        }

        /// <summary>
        /// Gets the variables for this scope.
        /// </summary>
        public ICollection<PythonVariable> ScopeVariables {
            get {
                if (_variables != null) {
                    return _variables.Values;
                }
                return null;
            }
        }

        internal virtual bool IsGlobal {
            get { return false; }
        }

        internal virtual bool NeedsLocalContext {
            get {
                return NeedsLocalsDictionary || ContainsNestedFreeVariables;
            }
        }

        internal virtual int ArgCount {
            get {
                return 0;
            }
        }

        public PythonAst GlobalParent {
            get {
                ScopeStatement cur = this;
                while (!(cur is PythonAst)) {
                    Debug.Assert(cur != null);
                    cur = cur.Parent;
                }
                return (PythonAst)cur;
            }
        }

        internal void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            if (_freeVars == null) {
                _freeVars = new List<PythonVariable>();
            }

            if (!_freeVars.Contains(variable)) {
                _freeVars.Add(variable);
            }
        }

        internal string AddReferencedGlobal(string name) {
            if (_globalVars == null) {
                _globalVars = new List<string>();
            }
            if (!_globalVars.Contains(name)) {
                _globalVars.Add(name);
            }
            return name;
        }

        internal void AddNonLocalVariable(NameExpression name) {
            if (_nonLocalVars == null) {
                _nonLocalVars = new List<NameExpression>();
            }
            _nonLocalVars.Add(name);
        }

        internal void AddCellVariable(PythonVariable variable) {
            if (_cellVars == null) {
                _cellVars = new List<string>();
            }

            if (!_cellVars.Contains(variable.Name)) {
                _cellVars.Add(variable.Name);
            }
        }

        /// <summary>
        /// Variables that are bound in an outer scope - but not a global scope
        /// </summary>
        public IList<PythonVariable> FreeVariables {
            get {
                return _freeVars;
            }
        }

        /// <summary>
        /// Variables that are bound to the global scope
        /// </summary>
        public IList<string> GlobalVariables {
            get {
                return _globalVars;
            }
        }

        /// <summary>
        /// Variables that are referred to from a nested scope and need to be
        /// promoted to cells.
        /// </summary>
        internal IList<string> CellVariables {
            get {
                return _cellVars;
            }
        }

        internal abstract bool ExposesLocalVariable(PythonVariable variable);

        public bool TryGetVariable(string name, out PythonVariable variable) {
            if (_variables != null && name != null) {
                return _variables.TryGetValue(name, out variable);
            } else {
                variable = null;
                return false;
            }
        }

        internal virtual bool TryBindOuter(ScopeStatement from, string name, bool allowGlobals, out PythonVariable variable) {
            // Hide scope contents by default (only functions expose their locals)
            variable = null;
            return false;
        }

        internal abstract PythonVariable BindReference(PythonNameBinder binder, string name);

        internal virtual void Bind(PythonNameBinder binder) {
            if (_references != null) {
                foreach (var refList in _references.Values) {
                    foreach (var reference in refList) {
                        PythonVariable variable;
                        reference.Variable = variable = BindReference(binder, reference.Name);

                        // Accessing outer scope variable which is being deleted?
                        if (variable != null) {
                            if (variable.Deleted && variable.Scope != this && !variable.Scope.IsGlobal && binder.LanguageVersion < PythonLanguageVersion.V32) {

                                // report syntax error
                                binder.ReportSyntaxError(
                                    "can not delete variable '{0}' referenced in nested scope".FormatUI(reference.Name),
                                    this);
                            }
                        }
                    }
                }
            }
        }

        internal virtual void FinishBind(PythonNameBinder binder) {
            List<ClosureInfo> closureVariables = null;

            if (_nonLocalVars != null) {
                foreach (var variableName in _nonLocalVars) {
                    bool bound = false;
                    for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                        PythonVariable variable;

                        if (parent.TryBindOuter(this, variableName.Name, false, out variable)) {
                            bound = !variable.IsGlobal;
                            break;
                        }
                    }

                    if (!bound) {
                        binder.ReportSyntaxError("no binding for nonlocal '{0}' found".FormatUI(variableName.Name), variableName);
                    }
                }
            }

            if (FreeVariables != null && FreeVariables.Count > 0) {
                closureVariables = new List<ClosureInfo>();

                foreach (var variable in FreeVariables) {
                    closureVariables.Add(new ClosureInfo(variable, !(this is ClassDefinition)));
                }
            }

            if (Variables != null && Variables.Count > 0) {
                if (closureVariables == null) {
                    closureVariables = new List<ClosureInfo>();
                }

                foreach (PythonVariable variable in Variables.Values) {
                    if (!HasClosureVariable(closureVariables, variable) &&
                        !variable.IsGlobal && (variable.AccessedInNestedScope || ExposesLocalVariable(variable))) {
                        closureVariables.Add(new ClosureInfo(variable, true));
                    }

                    if (variable.Kind == VariableKind.Local) {
                        Debug.Assert(variable.Scope == this);
                    }
                }
            }

            if (closureVariables != null) {
                _closureVariables = closureVariables.ToArray();
            }

            // no longer needed
            _references = null;
        }

        private static bool HasClosureVariable(List<ClosureInfo> closureVariables, PythonVariable variable) {
            if (closureVariables == null) {
                return false;
            }

            for (int i = 0; i < closureVariables.Count; i++) {
                if (closureVariables[i].Variable == variable) {
                    return true;
                }
            }

            return false;
        }

        private void EnsureVariables() {
            if (_variables == null) {
                _variables = new Dictionary<string, PythonVariable>(StringComparer.Ordinal);
            }
        }

        internal void AddVariable(PythonVariable variable) {
            EnsureVariables();
            _variables[variable.Name] = variable;
        }

        internal PythonReference Reference(string/*!*/ name) {
            if (_references == null) {
                _references = new Dictionary<string, List<PythonReference>>(StringComparer.Ordinal);
            }
            List<PythonReference> references;
            if (!_references.TryGetValue(name, out references)) {
                _references[name] = references = new List<PythonReference>();
            }
            var reference = new PythonReference(name);
            references.Add(reference);
            return reference;
        }

        internal bool IsReferenced(string name) {
            return _references != null && _references.ContainsKey(name);
        }

        internal PythonVariable/*!*/ CreateVariable(string name, VariableKind kind) {
            EnsureVariables();
            PythonVariable variable;
            _variables[name] = variable = new PythonVariable(name, kind, this);
            return variable;
        }

        internal PythonVariable/*!*/ EnsureVariable(string/*!*/ name) {
            PythonVariable variable;
            if (!TryGetVariable(name, out variable)) {
                return CreateVariable(name, VariableKind.Local);
            }
            return variable;
        }

        internal PythonVariable DefineParameter(string name) {
            return CreateVariable(name, VariableKind.Parameter);
        }

        struct ClosureInfo {
            public PythonVariable Variable;
            public bool AccessedInScope;

            public ClosureInfo(PythonVariable variable, bool accessedInScope) {
                Variable = variable;
                AccessedInScope = accessedInScope;
            }
        }
    }
}
