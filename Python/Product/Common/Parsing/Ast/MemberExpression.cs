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
    public sealed class MemberExpression : Expression {
        public MemberExpression(Expression target, string name) {
            Target = target;
            Name = name;
        }

        public void SetLoc(int start, int name, int end) {
            SetLoc(start, end);
            NameHeader = name;
        }

        /// <summary>
        /// Returns the index which is the start of the name.
        /// </summary>
        public int NameHeader { get; set; }

        /// <summary>
        /// The index where the dot appears.
        /// </summary>
        public int DotIndex { get; set; }

        public Expression Target { get; }

        public string Name { get; }

        public override string ToString() => base.ToString() + ":" + Name;

        internal override string CheckAssign() => null;

        internal override string CheckDelete() => null;

        public override string NodeName => "attribute";

        public override IEnumerable<Node> GetChildNodes() {
            if (Target != null) yield return Target;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Target?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                if (Target != null) {
                    await Target.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            format.Append(res, format.SpaceBeforeDot, " ", string.Empty, this.GetPreceedingWhiteSpaceDefaultNull(ast));
            res.Append('.');
            if (!this.IsIncompleteNode(ast)) {
                format.Append(res, format.SpaceAfterDot, " ", string.Empty, this.GetSecondWhiteSpaceDefaultNull(ast));
                if (format.UseVerbatimImage) {
                    res.Append(this.GetVerbatimImage(ast) ?? Name);
                } else {
                    res.Append(Name);
                }
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) => Target.GetLeadingWhiteSpace(ast);

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace)
            => Target.SetLeadingWhiteSpace(ast, whiteSpace);

        /// <summary>
        /// Returns the span of the name component of the expression
        /// </summary>
        public SourceSpan GetNameSpan(PythonAst ast) => new SourceSpan(ast.IndexToLocation(NameHeader), GetEnd(ast));
    }
}
