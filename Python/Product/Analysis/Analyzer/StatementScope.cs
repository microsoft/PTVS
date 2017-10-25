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

using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    class StatementScope : InterpreterScope {
        public int _startIndex, _endIndex;

        public StatementScope(int index, InterpreterScope outerScope)
            : base(null, outerScope) {
            _startIndex = _endIndex = index;
        }

        public override string Name {
            get { return "<statements>"; }
        }

        public override int GetStart(PythonAst ast) {
            return _startIndex;
        }

        public override int GetStop(PythonAst ast) {
            return _endIndex;
        }

        public int EndIndex {
            set {
                _endIndex = value;
            }
        }

        // Forward variable handling to the outer scope.

        public override VariableDef CreateVariable(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            return OuterScope.CreateVariable(node, unit, name, addRef);
        }

        public override VariableDef AddVariable(string name, VariableDef variable = null) {
            return OuterScope.AddVariable(name, variable);
        }

        internal override bool RemoveVariable(string name) {
            return OuterScope.RemoveVariable(name);
        }

        internal override void ClearVariables() {
            OuterScope.ClearVariables();
        }
    }
}
