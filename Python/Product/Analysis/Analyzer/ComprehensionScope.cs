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

using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
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

        public override IAnalysisSet AddNodeValue(Node node, IAnalysisSet variable) {
            return OuterScope.AddNodeValue(node, variable);
        }

        internal override bool RemoveNodeValue(Node node) {
            return OuterScope.RemoveNodeValue(node);
        }

        internal override void ClearNodeValues() {
            OuterScope.ClearNodeValues();
        }
    }
}
