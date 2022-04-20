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
    public class ComprehensionFor : ComprehensionIterator {
        public ComprehensionFor(Expression lhs, Expression list) {
            Left = lhs;
            List = list;
        }

        public ComprehensionFor(Expression lhs, Expression list, bool isAsync)
            : this(lhs, list) {
            IsAsync = isAsync;
        }

        public Expression Left { get; }

        public Expression List { get; }

        public bool IsAsync { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Left != null) yield return Left;
            if (List != null) yield return List;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                List?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Left != null) {
                    await Left.WalkAsync(walker, cancellationToken);
                }
                if (List != null) {
                    await List.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public int GetIndexOfFor(PythonAst ast)
            => !IsAsync ? StartIndex : StartIndex + 5 + this.GetPreceedingWhiteSpace(ast).Length;

        public int GetIndexOfIn(PythonAst ast)
            => this.IsIncompleteNode(ast) ? -1 : Left.EndIndex + this.GetSecondWhiteSpace(ast).Length;

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (IsAsync) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append("async");
            }
            res.Append(this.GetPreceedingWhiteSpace(ast));
            res.Append("for");
            Left.AppendCodeString(res, ast, format);
            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("in");
                List.AppendCodeString(res, ast, format);
            }
        }
    }
}
