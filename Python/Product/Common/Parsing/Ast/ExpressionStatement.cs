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
    public class ExpressionStatement : Statement {
        public ExpressionStatement(Expression expression) {
            Expression = expression;
        }

        public Expression Expression { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Expression != null) yield return Expression;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression?.Walk(walker);
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

        public override string Documentation {
            get {
                if (Expression is ConstantExpression ce) {
                    if (ce.Value is string s) {
                        return s;
                    }
                    return ce.Value is AsciiString ? ((AsciiString)ce.Value).String : null;
                }
                return null;
            }
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) => Expression.AppendCodeString(res, ast, format);

        public override string GetLeadingWhiteSpace(PythonAst ast) => Expression.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => Expression.SetLeadingWhiteSpace(ast, whiteSpace);
    }
}
