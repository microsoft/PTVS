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

using System.Collections.Generic;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Walks the AST, and returns expressions that can be show in VS Data tip
    /// </summary>
    public class DataTipExpressionWalker : PythonWalker {
        private readonly PythonAst _ast;
        private readonly Dictionary<Expression, Expression> _expressions = new Dictionary<Expression, Expression>();

        public DataTipExpressionWalker(PythonAst ast) {
            _ast = ast;
        }

        public override void PostWalk(NameExpression node) {
            _expressions.Add(node, null);
        }

        public override void PostWalk(MemberExpression node) {
            if (IsValidTarget(node.Target)) {
                _expressions.Remove(node.Target);
                _expressions.Add(node, node.Target);
            }
        }

        public override void PostWalk(IndexExpression node) {
            if (IsValidTarget(node.Target) && IsValidTarget(node.Index)) {
                _expressions.Remove(node.Target);
                _expressions.Add(node, node.Target);
            }
        }

        private bool IsValidTarget(Node node) {
            if (node == null || node is ConstantExpression || node is NameExpression) {
                return true;
            }

            var expr = node as Expression;
            if (expr != null && _expressions.ContainsKey(expr)) {
                return true;
            }

            var walker = new DetectSideEffectsWalker();
            node.Walk(walker);
            return !walker.HasSideEffects;
        }

        public IEnumerable<Expression> GetExpressions() {
            return _expressions.Keys;
        }
    }
}
