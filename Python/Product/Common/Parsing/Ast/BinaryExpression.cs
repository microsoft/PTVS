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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class BinaryExpression : Expression {
        public BinaryExpression(PythonOperator op, Expression left, Expression right, int operatorIndex) {
            if (op == PythonOperator.None) {
                throw new ArgumentException("bad operator");
            }

            Operator = op;
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
            OperatorIndex = operatorIndex;
        }

        public Expression Left { get; }

        public Expression Right { get; }

        public PythonOperator Operator { get; }

        public int OperatorIndex { get; }

        private bool IsComparison() {
            switch (Operator) {
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

        public override string NodeName => "binary operator";

        public override IEnumerable<Node> GetChildNodes() {
            if (Left != null) yield return Left;
            if (Right != null) yield return Right;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                Right?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Left != null) {
                    await Left.WalkAsync(walker, cancellationToken);
                }
                if (Right != null) {
                    await Right.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
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
            BinaryToCodeString(res, ast, format, this, Left, Right, op1, op2);
        }

        internal static void BinaryToCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format, Expression node, Expression left, Expression right, string op1, string op2 = null) {
            left.AppendCodeString(res, ast, format);

            format.Append(
                res,
                format.SpacesAroundBinaryOperators,
                " ",
                char.IsLetter(op1[0]) ? " " : "",   // spaces required for is not, not in, etc...
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
                            (char.IsLetter(op1[0]) ? " " : string.Empty) :
                        null
                );
            } else {
                Debug.Assert(char.IsLetter(op1[0]));

                res.Append(op1);
                res.Append(node.GetSecondWhiteSpace(ast));
                res.Append(op2);
                right.AppendCodeString(res, ast, format, format.SpacesAroundBinaryOperators != null ? " " : null); // force single space if setting is on or off
            }
        }

        public int GetIndexOfSecondOp(PythonAst ast) {
            if (Operator == PythonOperator.NotIn) {
                return OperatorIndex + 3 + this.GetSecondWhiteSpace(ast).Length;
            }
            if (Operator == PythonOperator.IsNot) {
                return OperatorIndex + 2 + this.GetSecondWhiteSpace(ast).Length;
            }
            return -1;
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) => Left.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => Left.SetLeadingWhiteSpace(ast, whiteSpace);
    }
}
