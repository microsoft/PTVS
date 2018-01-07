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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class TupleExpression : SequenceExpression {
        private bool _expandable;

        public TupleExpression(bool expandable, params Expression[] items)
            : base(items) {
            _expandable = expandable;
        }

        internal override string CheckAssign() {
            if (Items.Count == 0) {
                return "can't assign to ()";
            }
            return base.CheckAssign();
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (Items != null) {
                    foreach (Expression e in Items) {
                        e.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        public bool IsExpandable {
            get {
                return _expandable;
            }
        }

        /// <summary>
        /// Marks this tuple expression as having no parenthesis for the purposes of round tripping.
        /// </summary>
        public void RoundTripHasNoParenthesis(PythonAst ast) {
            ast.SetAttribute(this, NodeAttributes.IsAltFormValue, NodeAttributes.IsAltFormValue);
        }

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
