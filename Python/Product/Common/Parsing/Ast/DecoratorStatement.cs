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

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class DecoratorStatement : Statement {
        public DecoratorStatement(Expression[] decorators) {
            Decorators = decorators ?? Array.Empty<Expression>();
        }

        public Expression[] Decorators { get; }

        public override IEnumerable<Node> GetChildNodes() => Decorators.ExcludeDefault();

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var decorator in Decorators) {
                    decorator?.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var decorator in Decorators.ExcludeDefault()) {
                    await decorator.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            for (int i = 0, curWhiteSpace = 0; i < Decorators.Length; i++) {
                if (decorateWhiteSpace != null) {
                    format.ReflowComment(res, decorateWhiteSpace[curWhiteSpace++]);
                }
                res.Append('@');
                if (Decorators[i] != null) {
                    Decorators[i].AppendCodeString(res, ast, format);
                    if (decorateWhiteSpace != null) {
                        format.ReflowComment(res, decorateWhiteSpace[curWhiteSpace++]);
                    } else {
                        res.Append(Environment.NewLine);
                    }
                }
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (decorateWhiteSpace != null && decorateWhiteSpace.Length > 0) {
                return decorateWhiteSpace[0];
            }
            return "";
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (decorateWhiteSpace != null && decorateWhiteSpace.Length > 0) {
                decorateWhiteSpace[0] = whiteSpace;
            }

        }
    }
}
