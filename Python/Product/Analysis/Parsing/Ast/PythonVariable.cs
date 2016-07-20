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
