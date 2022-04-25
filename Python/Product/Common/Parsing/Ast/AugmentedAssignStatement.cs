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

using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class AugmentedAssignStatement : Statement {
        public AugmentedAssignStatement(PythonOperator op, Expression left, Expression right) {
            Operator = op;
            Left = left;
            Right = right;
        }

        public PythonOperator Operator { get; }

        public Expression Left { get; }

        public Expression Right { get; }

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

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Left.AppendCodeString(res, ast, format);
            res.Append(this.GetPreceedingWhiteSpace(ast));
            res.Append(Operator.ToCodeString());
            res.Append('=');
            Right.AppendCodeString(res, ast, format);
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) => Left.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => Left.SetLeadingWhiteSpace(ast, whiteSpace);
    }
}
