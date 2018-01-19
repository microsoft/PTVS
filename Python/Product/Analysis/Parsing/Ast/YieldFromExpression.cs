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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    // New in Pep380 for Python 3.3. Yield From is an expression with a return value.
    //    x = yield from z
    // The return value (x) is taken from the value attribute of a StopIteration
    // error raised by next(z) or z.send().
    public class YieldFromExpression : Expression {
        private readonly Expression _expression;

        public YieldFromExpression(Expression expression) {
            _expression = expression;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override string CheckAugmentedAssign() {
            return CheckAssign();
        }

        public int GetIndexOfFrom(PythonAst ast) {
            return StartIndex + 5 + this.GetSecondWhiteSpace(ast).Length;
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            res.Append(this.GetPreceedingWhiteSpace(ast));
            res.Append("yield");
            res.Append(this.GetSecondWhiteSpace(ast));
            res.Append("from");
            if (!this.IsAltForm(ast)) {
                Expression.AppendCodeString(res, ast, format);
                var itemWhiteSpace = this.GetListWhiteSpace(ast);
                if (itemWhiteSpace != null) {
                    res.Append(",");
                    res.Append(itemWhiteSpace[0]);
                }
            }
        }
    }
}
