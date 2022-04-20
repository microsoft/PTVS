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
    public class StarredExpression : Expression {
        public StarredExpression(Expression expr) : this(expr, 1) { }

        public StarredExpression(Expression expr, int starCount) {
            Expression = expr;
            StarCount = starCount;
        }

        public Expression Expression { get; }

        public int StarCount { get; }

        public override string NodeName => "starred";

        public override IEnumerable<Node> GetChildNodes() {
            yield return Expression;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression.Walk(walker);
            }
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                await Expression.WalkAsync(walker, cancellationToken);
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override string CheckAssign()
            => StarCount == 1 ? "starred assignment target must be in a list or tuple" : "invalid syntax";

        internal override string CheckAugmentedAssign() => "invalid syntax";

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            res.Append(this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? "");
            res.Append(new string('*', StarCount));
            Expression.AppendCodeString(res, ast, format);
        }
    }
}
