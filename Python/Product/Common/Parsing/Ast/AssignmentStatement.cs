// Python Tools for Visual Studio
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
    public class AssignmentStatement : Statement {
        // _left.Length is 1 for simple assignments like "x = 1"
        // _left.Length will be 3 for "x = y = z = 1"
        private readonly Expression[] _left;

        public AssignmentStatement(Expression[] left, Expression right) {
            _left = left;
            Right = right;
        }

        public IList<Expression> Left => _left;

        public Expression Right { get; }

        public override IEnumerable<Node> GetChildNodes() {
            foreach (var expression in _left) {
                yield return expression;
            }
            if (Right != null) {
                yield return Right;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var e in _left) {
                    e.Walk(walker);
                }
                Right?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var e in _left) {
                    await e.WalkAsync(walker, cancellationToken);
                }
                if (Right != null) {
                    await Right.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var lhs = this.GetListWhiteSpace(ast);
            for (var i = 0; i < Left.Count; i++) {
                if (lhs != null && i != 0) {
                    format.Append(
                        res,
                        format.SpacesAroundAssignmentOperator,
                        " ",
                        "",
                        lhs[i - 1]
                    );
                    res.Append("=");
                }
                Left[i].AppendCodeString(
                    res,
                    ast,
                    format,
                    i != 0 && format.SpacesAroundAssignmentOperator != null ?
                        format.SpacesAroundAssignmentOperator.Value ? " " : string.Empty :
                        null
                );
            }
            if (lhs != null) {
                format.Append(
                    res,
                    format.SpacesAroundAssignmentOperator,
                    " ",
                    string.Empty,
                    lhs[lhs.Length - 1]
                );
            }
            res.Append("=");

            Right.AppendCodeString(
                res,
                ast,
                format,
                format.SpacesAroundAssignmentOperator != null ?
                    format.SpacesAroundAssignmentOperator.Value ? " " : string.Empty :
                    null
            );
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (_left.Length > 0 && _left[0] != null) {
                return _left[0].GetLeadingWhiteSpace(ast);
            }

            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (_left.Length > 0 && _left[0] != null) {
                _left[0].SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }
    }
}
