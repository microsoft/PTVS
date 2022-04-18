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

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class PythonVariable {
        internal PythonVariable(string name, VariableKind kind, ScopeStatement/*!*/ scope) {
            Name = name;
            Kind = kind;
            Scope = scope;
        }

        /// <summary>
        /// The name of the variable.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The scope the variable was declared in.
        /// </summary>
        public ScopeStatement Scope { get; }

        /// <summary>
        /// True if the variable is a global variable (either referenced from an inner scope, or referenced from the global scope);
        /// </summary>
        internal bool IsGlobal => Kind == VariableKind.Global || Scope.IsGlobal;

        internal VariableKind Kind { get; set; }

        internal bool Deleted { get; set; }

        /// <summary>
        /// True iff the variable is referred to from the inner scope.
        /// </summary>
        internal bool AccessedInNestedScope { get; set; }
    }
}
