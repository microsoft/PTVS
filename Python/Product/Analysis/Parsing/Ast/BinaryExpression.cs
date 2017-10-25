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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {

    public partial class BinaryExpression : Expression {
        private readonly Expression _left, _right;
        private readonly PythonOperator _op;

        public BinaryExpression(PythonOperator op, Expression left, Expression right) {
            Contract.Assert(left != null);
            Contract.Assert(right != null);
            if (op == PythonOperator.None) throw new ArgumentException("bad operator");

            _op = op;
            _left = left;
            _right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public PythonOperator Operator {
            get { return _op; }
        }

        private bool IsComparison() {
            switch (_op) {
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.GreaterThan:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.Equal:
                case PythonOperator.NotEqual:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                case PythonOperator.IsNot:
                case PythonOperator.Is:
                    return true;
            }
            return false;
        }

        public override string NodeName {
            get {
                return "binary operator";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _left.Walk(walker);
                _right.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Expression left = _left;
            Expression right = _right;
            string op1, op2;

            if (Operator == PythonOperator.NotIn) {
                op1 = "not";
                if (!this.IsIncompleteNode(ast)) {
                    op2 = "in";
                } else {
                    op2 = null;
                }
            } else if (Operator == PythonOperator.IsNot) {
                op1 = "is";
                op2 = "not";
            } else if ((op1 = this.GetVerbatimImage(ast)) != null) {
                // operator image differs from the operator enum, for example <> is always NotEqual which is !=
                // so we store the verbatim image and use it here.
                op2 = null;
            } else {
                op1 = Operator.ToCodeString();
                op2 = null;
            }
            BinaryToCodeString(res, ast, format, this, _left, _right, op1, op2);
        }

        internal static void BinaryToCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, Expression node, Expression left, Expression right, string op1, string op2 = null) {
            left.AppendCodeString(res, ast, format);

            format.Append(
                res,
                format.SpacesAroundBinaryOperators,
                " ",
                Char.IsLetter(op1[0]) ? " " : "",   // spaces required for is not, not in, etc...
                node.GetPreceedingWhiteSpace(ast)
            );

            if (op2 == null) {
                res.Append(op1);
                right.AppendCodeString(
                    res,
                    ast,
                    format,
                    format.SpacesAroundBinaryOperators != null ?
                        format.SpacesAroundBinaryOperators.Value ?
                            " " :
                            (Char.IsLetter(op1[0]) ? " " : "") :
                        null
                );
            } else {
                Debug.Assert(Char.IsLetter(op1[0]));

                res.Append(op1);
                res.Append(node.GetSecondWhiteSpace(ast));
                res.Append(op2);
                right.AppendCodeString(res, ast, format, format.SpacesAroundBinaryOperators != null ? " " : null); // force single space if setting is on or off
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return _left.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            _left.SetLeadingWhiteSpace(ast, whiteSpace);
        }
    }
}
