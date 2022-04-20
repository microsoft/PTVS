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
    public class ForStatement : Statement, IMaybeAsyncStatement {
        private int? _keywordEndIndex;

        public ForStatement(Expression left, Expression list, Statement body, Statement else_) {
            Left = left;
            List = list;
            Body = body;
            Else = else_;
        }

        public ForStatement(Expression left, Expression list, Statement body, Statement else_, bool isAsync)
            : this(left, list, body, else_) {
            IsAsync = isAsync;
        }

        public int ForIndex { get; set; }
        public int InIndex { get; set; }
        public int HeaderIndex { get; set; }
        public int ElseIndex { get; set; }
        internal void SetKeywordEndIndex(int index) => _keywordEndIndex = index;
        public override int KeywordEndIndex => _keywordEndIndex ?? (StartIndex + (IsAsync ? 9 : 3));
        public override int KeywordLength => KeywordEndIndex - StartIndex;

        public Expression Left { get; }
        public Statement Body { get; set; }
        public Expression List { get; set; }
        public Statement Else { get; }
        public bool IsAsync { get; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Left != null) yield return Left;
            if (List != null) yield return List;
            if (Body != null) yield return Body;
            if (Else != null) yield return Else;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Left?.Walk(walker);
                List?.Walk(walker);
                Body?.Walk(walker);
                Else?.Walk(walker);
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
                if (Body != null) {
                    await Body.WalkAsync(walker, cancellationToken);
                }
                if (Else != null) {
                    await Else.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            if (IsAsync) {
                res.Append("async");
                res.Append(this.GetFourthWhiteSpace(ast));
            }
            res.Append("for");
            Left.AppendCodeString(res, ast, format);
            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("in");
                List.AppendCodeString(res, ast, format);
                Body.AppendCodeString(res, ast, format);   // colon is handled by suite statements...
                if (Else != null) {
                    format.ReflowComment(res, this.GetThirdWhiteSpace(ast));
                    res.Append("else");
                    Else.AppendCodeString(res, ast, format);
                }
            }
        }
    }
}
