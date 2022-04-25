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
using Microsoft.PythonTools.Common.Core;
using Microsoft.PythonTools.Common.Core.Collections;
using Microsoft.PythonTools.Common.Core.Extensions;

namespace Microsoft.PythonTools.Common.Parsing.Ast {
    public class TupleExpression : SequenceExpression {
        public TupleExpression(bool expandable, ImmutableArray<Expression> items)
            : base(items) {
            IsExpandable = expandable;
        }

        internal override string CheckAssign() {
            if (Items.Count == 0) {
                return "can't assign to ()";
            }
            return base.CheckAssign();
        }

        public override IEnumerable<Node> GetChildNodes() => Items.WhereNotNull();

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var e in Items.MaybeEnumerate()) {
                    e.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public override async Task WalkAsync(PythonWalkerAsync walker, CancellationToken cancellationToken = default) {
            if (await walker.WalkAsync(this, cancellationToken)) {
                foreach (var e in Items.MaybeEnumerate()) {
                    await e.WalkAsync(walker, cancellationToken);
                }
            }
            await walker.PostWalkAsync(this, cancellationToken);
        }

        public bool IsExpandable { get; }

        /// <summary>
        /// Marks this tuple expression as having no parenthesis for the purposes of round tripping.
        /// </summary>
        public void RoundTripHasNoParenthesis(PythonAst ast) => ast.SetAttribute(this, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (this.IsAltForm(ast)) {
                return Items[0].GetLeadingWhiteSpace(ast);
            }
            return base.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (this.IsAltForm(ast)) {
                Items[0].SetLeadingWhiteSpace(ast, whiteSpace);
            } else {
                base.SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (this.IsAltForm(ast)) {
                ListExpression.AppendItems(res, ast, format, "", "", this, Items);
            } else {
                if (Items.Count == 0 &&
                    format.SpaceWithinEmptyTupleExpression != null) {
                    format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                    res.Append('(');
                    format.Append(res, format.SpaceWithinEmptyTupleExpression, " ", "", this.GetSecondWhiteSpaceDefaultNull(ast));
                    res.Append(')');
                } else {
                    ListExpression.AppendItems(res, ast, format, "(", this.IsMissingCloseGrouping(ast) ? "" : ")", this, Items, format.SpacesWithinParenthesisedTupleExpression);
                }
            }
        }
    }
}
