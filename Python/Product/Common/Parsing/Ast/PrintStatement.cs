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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Collections;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class PrintStatement : Statement {
        public PrintStatement(Expression destination, ImmutableArray<Expression> expressions, bool trailingComma) {
            Destination = destination;
            Expressions = expressions;
            TrailingComma = trailingComma;
        }

        public Expression Destination { get; }

        public ImmutableArray<Expression> Expressions { get; }

        public bool TrailingComma { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Destination != null) yield return Destination;
            foreach (var expression in Expressions) {
                yield return expression;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Destination?.Walk(walker);
                foreach (var expression in Expressions.MaybeEnumerate()) {
                    expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Destination != null) {
                    await Destination.WalkAsync(walker, cancellationToken);
                }
                foreach (var expression in Expressions.MaybeEnumerate()) {
                    await expression.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("print");
            if (Destination != null) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(">>");
                Destination.AppendCodeString(res, ast, format);
                if (Expressions.Count > 0) {
                    res.Append(this.GetThirdWhiteSpace(ast));
                    res.Append(',');
                }
            }
            ListExpression.AppendItems(res, ast, format, string.Empty, string.Empty, this, Expressions);
        }
    }
}
