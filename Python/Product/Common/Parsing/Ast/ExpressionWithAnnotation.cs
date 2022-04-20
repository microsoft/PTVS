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
    public class ExpressionWithAnnotation : Expression {
        public ExpressionWithAnnotation(Expression expression, Expression annotation) {
            Expression = expression;
            Annotation = annotation;
        }

        public override string ToString() {
            if (Annotation != null) {
                return Expression + ":" + Annotation;
            }
            return Expression.ToString();
        }

        public Expression Expression { get; }
        public Expression Annotation { get; }

        public override string NodeName => "annotated expression";

        internal override string CheckAssign() => null;
        internal override string CheckAugmentedAssign() => "cannot assign to " + NodeName;
        internal override string CheckDelete() => "cannot delete " + NodeName;

        public override IEnumerable<Node> GetChildNodes() => new[] {Expression, Annotation};

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Expression.Walk(walker);
                Annotation?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Expression != null) {
                    await Expression.WalkAsync(walker, cancellationToken);
                }
                if (Annotation != null) {
                    await Annotation.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Expression.AppendCodeString(res, ast, format);
            if (Annotation != null) {
                // For now, use same formatting as around an assignment
                if (format.SpacesAroundAssignmentOperator == null) {
                    res.Append(this.GetSecondWhiteSpaceDefaultNull(ast) ?? string.Empty);
                } else if (format.SpacesAroundAssignmentOperator == true) {
                    res.Append(' ');
                }
                res.Append(':');
                Annotation.AppendCodeString(res, ast, format);
            }
        }
    }
}
