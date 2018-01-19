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
    public class ConditionalExpression : Expression {
        public ConditionalExpression(Expression testExpression, Expression trueExpression, Expression falseExpression, int ifIndex, int elseIndex) {
            Test = testExpression;
            TrueExpression = trueExpression;
            FalseExpression = falseExpression;
            IfIndex = ifIndex;
            ElseIndex = elseIndex;
        }

        public override string NodeName => "conditional expression";
        public Expression FalseExpression { get; }
        public Expression Test { get; }
        public Expression TrueExpression { get; }

        public int IfIndex { get; }
        public int ElseIndex { get; }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                TrueExpression?.Walk(walker);
                FalseExpression?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            TrueExpression.AppendCodeString(res, ast, format);
            res.Append(this.GetPreceedingWhiteSpace(ast));
            res.Append("if");
            if (!ast.HasVerbatim) {
                res.Append(' ');
            }
            Test.AppendCodeString(res, ast, format);
            res.Append(this.GetSecondWhiteSpace(ast));
            if (!this.IsIncompleteNode(ast)) {
                res.Append("else");
                if (!ast.HasVerbatim) {
                    res.Append(' ');
                }
                FalseExpression.AppendCodeString(res, ast, format);
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return TrueExpression.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            TrueExpression.SetLeadingWhiteSpace(ast, whiteSpace);
        }
    }
}
