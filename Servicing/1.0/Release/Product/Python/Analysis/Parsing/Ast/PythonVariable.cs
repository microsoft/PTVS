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

namespace Microsoft.PythonTools.Parsing.Ast {
    public class PythonVariable {
        private readonly string _name;
        private readonly ScopeStatement/*!*/ _scope;
        private VariableKind _kind;    // the type of variable, 

        // variables used during the named binding to report errors
        private bool _deleted;                  // del x, the variable gets deleted at some point
        private bool _accessedInNestedScope;    // the variable is accessed in a nested scope and therefore needs to be a closure var

        internal PythonVariable(string name, VariableKind kind, ScopeStatement/*!*/ scope) {
            _name = name;
            _kind = kind;
            _scope = scope;
        }

        /// <summary>
        /// The name of the variable.
        /// </summary>
        public string Name {
            get { return _name; }
        }

        /// <summary>
        /// The scope the variable was declared in.
        /// </summary>
        public ScopeStatement Scope {
            get { return _scope; }
        }

        /// <summary>
        /// True if the variable is a global variable (either referenced from an inner scope, or referenced from the global scope);
        /// </summary>
        internal bool IsGlobal {
            get {
                return Kind == VariableKind.Global || Scope.IsGlobal;
            }
        }

        internal VariableKind Kind {
            get { return _kind; }
            set { _kind = value; }
        }

        internal bool Deleted {
            get { return _deleted; }
            set { _deleted = value; }
        }

        /// <summary>
        /// True iff the variable is referred to from the inner scope.
        /// </summary>
        internal bool AccessedInNestedScope {
            get { return _accessedInNestedScope; }
            set { _accessedInNestedScope = value; }
        }
    }
}
