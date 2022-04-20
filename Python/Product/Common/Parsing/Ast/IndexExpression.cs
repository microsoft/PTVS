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
    public class IndexExpression : Expression {
        public IndexExpression(Expression target, Expression index) {
            Target = target;
            Index = index;
        }

        public Expression Target { get; }

        public Expression Index { get; }

        internal override string CheckAssign() => null;

        internal override string CheckDelete() => null;

        public override string NodeName => "subscript";

        public override IEnumerable<Node> GetChildNodes() {
            if (Target != null) yield return Target;
            if (Index != null) yield return Index;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
                Index?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Target != null) {
                    await Target.WalkAsync(walker, cancellationToken);
                }
                if (Index != null) {
                    await Index.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        private bool IsSlice => Index is SliceExpression;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            format.Append(
                res,
                format.SpaceBeforeIndexBracket,
                " ",
                "",
                this.GetPreceedingWhiteSpaceDefaultNull(ast) ?? string.Empty
            );

            res.Append('[');
            Index.AppendCodeString(
                res,
                ast,
                format,
                format.SpaceWithinIndexBrackets != null ? format.SpaceWithinIndexBrackets.Value ? " " : "" : null
            );

            if (!this.IsMissingCloseGrouping(ast)) {
                format.Append(
                    res,
                    format.SpaceWithinIndexBrackets,
                    " ",
                    "",
                    this.GetSecondWhiteSpaceDefaultNull(ast) ?? string.Empty
                );
                res.Append(']');
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) => Target.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) => Target.SetLeadingWhiteSpace(ast, whiteSpace);
    }
}
