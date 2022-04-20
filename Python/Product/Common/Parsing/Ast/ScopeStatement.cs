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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public abstract class ScopeStatement : Statement {
        // due to "exec" or call to dir, locals, eval, vars...

        private List<PythonVariable> _freeVars;                         // list of variables accessed from outer scopes
        private List<string> _globalVars;                               // global variables accessed from this scope
        private List<string> _cellVars;                                 // variables accessed from nested scopes
        private List<NameExpression> _nonLocalVars;                             // variables declared as nonlocal within this scope
        private Dictionary<string, List<PythonReference>> _references;        // names of all variables referenced, null after binding completes

        internal const string NameForExec = "module: <exec>";

        public ScopeStatement Parent { get; set; }

        public abstract Statement Body {
            get;
        }

        internal bool ContainsImportStar { get; set; }
        internal bool ContainsExceptionHandling { get; set; }

        internal bool ContainsUnqualifiedExec { get; set; }

        /// <summary>
        /// True if this scope accesses a variable from an outer scope.
        /// </summary>
        public bool IsClosure => FreeVariables != null && FreeVariables.Count > 0;

        /// <summary>
        /// True if an inner scope is accessing a variable defined in this scope.
        /// </summary>
        public bool ContainsNestedFreeVariables { get; set; }

        /// <summary>
        /// True if we are forcing the creation of a dictionary for storing locals.
        /// 
        /// This occurs for calls to locals(), dir(), vars(), unqualified exec, and
        /// from ... import *.
        /// </summary>
        public bool NeedsLocalsDictionary { get; set; }

        public virtual string/*!*/ Name => "<unknown>";

        /// <summary>
        /// True if variables can be set in a late bound fashion that we don't
        /// know about at code gen time - for example via from fob import *.
        /// 
        /// This is tracked independently of the ContainsUnqualifiedExec/NeedsLocalsDictionary
        /// </summary>
        internal virtual bool HasLateBoundVariableSets { get; set; }

        internal Dictionary<string, PythonVariable> Variables { get; private set; }

        /// <summary>
        /// Gets the variables for this scope.
        /// </summary>
        public ICollection<PythonVariable> ScopeVariables
            => Variables?.Values ?? Array.Empty<PythonVariable>() as ICollection<PythonVariable>;

        public virtual bool IsGlobal => false;

        public PythonAst GlobalParent {
            get {
                var cur = this;
                while (!(cur is PythonAst)) {
                    Debug.Assert(cur != null);
                    cur = cur.Parent;
                }
                return (PythonAst)cur;
            }
        }

        protected void Clear() {
            _references?.Clear();
            _cellVars?.Clear();
            _freeVars?.Clear();
            _globalVars?.Clear();
            _nonLocalVars?.Clear();
        }

        internal void AddFreeVariable(PythonVariable variable, bool accessedInScope) {
            _freeVars = _freeVars ?? new List<PythonVariable>();
            if (!_freeVars.Contains(variable)) {
                _freeVars.Add(variable);
            }
        }

        internal string AddReferencedGlobal(string name) {
            _globalVars = _globalVars ?? new List<string>();
            if (!_globalVars.Contains(name)) {
                _globalVars.Add(name);
            }
            return name;
        }

        internal void AddNonLocalVariable(NameExpression name) {
            _nonLocalVars = _nonLocalVars ?? new List<NameExpression>();
            _nonLocalVars.Add(name);
        }

        internal void AddCellVariable(PythonVariable variable) {
            _cellVars = _cellVars ?? new List<string>();
            if (!_cellVars.Contains(variable.Name)) {
                _cellVars.Add(variable.Name);
            }
        }

        /// <summary>
        /// Variables that are bound in an outer scope - but not a global scope
        /// </summary>
        public IReadOnlyList<PythonVariable> FreeVariables => _freeVars;

        internal abstract bool ExposesLocalVariable(PythonVariable variable);

        public bool TryGetVariable(string name, out PythonVariable variable) {
            if (Variables != null && name != null) {
                return Variables.TryGetValue(name, out variable);
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
                    var bound = false;
                    for (var parent = Parent; parent != null; parent = parent.Parent) {
                        if (parent.TryBindOuter(this, variableName.Name, false, out var variable)) {
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

                foreach (var variable in Variables.Values) {
                    if (!HasClosureVariable(closureVariables, variable) &&
                        !variable.IsGlobal && (variable.AccessedInNestedScope || ExposesLocalVariable(variable))) {
                        closureVariables.Add(new ClosureInfo(variable, true));
                    }

                    if (variable.Kind == VariableKind.Local) {
                        Debug.Assert(variable.Scope == this);
                    }
                }
            }

            // no longer needed
            _references = null;
        }

        private static bool HasClosureVariable(List<ClosureInfo> closureVariables, PythonVariable variable) {
            if (closureVariables == null) {
                return false;
            }

            for (var i = 0; i < closureVariables.Count; i++) {
                if (closureVariables[i].Variable == variable) {
                    return true;
                }
            }

            return false;
        }

        private void EnsureVariables() {
            if (Variables == null) {
                Variables = new Dictionary<string, PythonVariable>(StringComparer.Ordinal);
            }
        }

        internal void AddVariable(PythonVariable variable) {
            EnsureVariables();
            Variables[variable.Name] = variable;
        }

        internal PythonReference Reference(string/*!*/ name) {
            if (_references == null) {
                _references = new Dictionary<string, List<PythonReference>>(StringComparer.Ordinal);
            }

            if (!_references.TryGetValue(name, out var references)) {
                _references[name] = references = new List<PythonReference>();
            }
            var reference = new PythonReference(name);
            references.Add(reference);
            return reference;
        }

        internal bool IsReferenced(string name) => _references != null && _references.ContainsKey(name);

        internal PythonVariable/*!*/ CreateVariable(string name, VariableKind kind) {
            EnsureVariables();
            PythonVariable variable;
            Variables[name] = variable = new PythonVariable(name, kind, this);
            return variable;
        }

        internal PythonVariable/*!*/ EnsureVariable(string/*!*/ name) {
            if (!TryGetVariable(name, out var variable)) {
                return CreateVariable(name, VariableKind.Local);
            }
            return variable;
        }

        internal PythonVariable DefineParameter(string name) => CreateVariable(name, VariableKind.Parameter);

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
