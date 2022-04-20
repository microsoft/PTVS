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
    public class UnaryExpression : Expression {
        public UnaryExpression(PythonOperator op, Expression expression) {
            Op = op;
            Expression = expression;
            EndIndex = expression.EndIndex;
        }

        public Expression Expression { get; }

        public PythonOperator Op { get; }

        public override string NodeName => "unary operator";

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append(Op.ToCodeString());
            Expression.AppendCodeString(res, ast, format);
        }

        public override IEnumerable<Node> GetChildNodes() {
            yield return Expression;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Expression != null) {
                    await Expression.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }
    }
}
