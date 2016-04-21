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
    sealed class ComprehensionScope : InterpreterScope {
        public ComprehensionScope(AnalysisValue comprehensionResult, Comprehension comprehension, InterpreterScope outerScope)
            : base(comprehensionResult, comprehension, outerScope) {
        }

        public override string Name {
            get { return "<comprehension scope>";  }
        }

        public override InterpreterScope AddNodeScope(Node node, InterpreterScope scope) {
            return OuterScope.AddNodeScope(node, scope);
        }

        internal override bool RemoveNodeScope(Node node) {
            return OuterScope.RemoveNodeScope(node);
        }

        internal override void ClearNodeScopes() {
            OuterScope.ClearNodeScopes();
        }

        public override IAnalysisSet AddNodeValue(Node node, NodeValueKind kind, IAnalysisSet variable) {
            return OuterScope.AddNodeValue(node, kind, variable);
        }

        internal override bool RemoveNodeValue(Node node) {
            return OuterScope.RemoveNodeValue(node);
        }

        internal override void ClearNodeValues() {
            OuterScope.ClearNodeValues();
        }
    }
}
