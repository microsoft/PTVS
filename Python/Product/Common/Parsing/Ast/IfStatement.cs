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
using Microsoft.PythonTools.Common.Core.Collections;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class IfStatement : Statement {
        public IfStatement(ImmutableArray<IfStatementTest> tests, Statement else_) {
            Tests = tests;
            ElseStatement = else_;
        }

        public ImmutableArray<IfStatementTest> Tests { get; }

        public Statement ElseStatement { get; }

        public int ElseIndex { get; set; }

        public override IEnumerable<Node> GetChildNodes() {
            foreach (var test in Tests) {
                yield return test;
            }

            if (ElseStatement != null) yield return ElseStatement;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var test in Tests) {
                    test.Walk(walker);
                }

                ElseStatement?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var test in Tests) {
                    await test.WalkAsync(walker, cancellationToken);
                }
                if (ElseStatement != null) {
                    await ElseStatement.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            for (var i = 0; i < Tests.Count; i++) {
                if (itemWhiteSpace != null) {
                    format.ReflowComment(res, itemWhiteSpace[i]);
                }

                if (i == 0) {
                    res.Append("if");
                } else {
                    res.Append("elif");
                }
                Tests[i].AppendCodeString(res, ast, format);
            }

            if (ElseStatement != null) {
                format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                res.Append("else");
                ElseStatement.AppendCodeString(res, ast, format);
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            if (itemWhiteSpace != null && itemWhiteSpace.Length > 0) {
                return itemWhiteSpace[0];
            }
            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            if (itemWhiteSpace != null && itemWhiteSpace.Length > 0) {
                itemWhiteSpace[0] = whiteSpace;
            }
        }
    }
}
