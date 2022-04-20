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
    public class DottedName : Node {
        public DottedName(ImmutableArray<NameExpression> names) {
            Names = names;
        }

        public ImmutableArray<NameExpression> Names { get; }

        public virtual string MakeString() {
            if (Names.Count == 0) {
                return string.Empty;
            }

            var ret = new StringBuilder(Names[0].Name);
            for (var i = 1; i < Names.Count; i++) {
                ret.Append('.');
                ret.Append(Names[i].Name);
            }
            return ret.ToString();
        }

        public override IEnumerable<Node> GetChildNodes() => Names;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var whitespace = this.GetNamesWhiteSpace(ast);

            for (int i = 0, whitespaceIndex = 0; i < Names.Count; i++) {
                if (whitespace != null) {
                    res.Append(whitespace[whitespaceIndex++]);
                }
                if (i != 0) {
                    res.Append('.');
                    if (whitespace != null) {
                        res.Append(whitespace[whitespaceIndex++]);
                    }
                }
                Names[i].AppendCodeString(res, ast, format);
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var whitespace = this.GetNamesWhiteSpace(ast);
            if (whitespace != null && whitespace.Length > 0) {
                return whitespace[0];
            }
            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var whitespace = this.GetNamesWhiteSpace(ast);
            if (whitespace != null && whitespace.Length > 0) {
                whitespace[0] = whiteSpace;
            }
        }

    }
}
