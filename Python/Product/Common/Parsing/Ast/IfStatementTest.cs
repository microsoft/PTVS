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
using Microsoft.PythonTools.Common.Core.Text;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class IfStatementTest : Node {
        public IfStatementTest(Expression test, Statement body) {
            Test = test;
            Body = body;
        }

        public int HeaderIndex { get; set; }

        public Expression Test { get; }

        public Statement Body { get; set; }

        public override IEnumerable<Node> GetChildNodes() {
            if (Test != null) yield return Test;
            if (Body != null) yield return Body;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Body?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Test != null) {
                    await Test.WalkAsync(walker, cancellationToken);
                }
                if (Body != null) {
                    await Body.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public SourceLocation GetHeader(PythonAst ast) => ast.IndexToLocation(HeaderIndex);

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Test.AppendCodeString(res, ast, format);
            Body.AppendCodeString(res, ast, format);
        }
    }
}
