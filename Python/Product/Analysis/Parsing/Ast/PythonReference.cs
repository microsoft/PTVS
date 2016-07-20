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
    /// <summary>
    /// Represents a reference to a name.  A PythonReference is created for each locatio
    /// where a name is referred to in a scope (global, class, or function).  
    /// </summary>
    public class PythonReference {
        private readonly string/*!*/ _name;
        private PythonVariable _variable;

        public PythonReference(string/*!*/ name) {
            _name = name;
        }

        public string/*!*/ Name {
            get { return _name; }
        }

        public PythonVariable Variable {
            get { return _variable; }
            set { _variable = value; }
        }
    }
}
