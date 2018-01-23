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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    sealed class VariablesResult : IEnumerable<IAnalysisVariable> {
        private readonly IEnumerable<IAnalysisVariable> _vars;
        private readonly PythonAst _ast;

        internal VariablesResult(IEnumerable<IAnalysisVariable> variables, PythonAst expr) {
            _vars = variables;
            _ast = expr;
        }

        public IEnumerator<IAnalysisVariable> GetEnumerator() {
            return _vars.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _vars.GetEnumerator();
        }

        public PythonAst Ast {
            get {
                return _ast;
            }
        }
    }
}
