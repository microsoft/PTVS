/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
                    format.ReflowComment(res, this.GetProceedingWhiteSpace(ast));
                    res.Append('(');
                    if (String.IsNullOrWhiteSpace(this.GetSecondWhiteSpace(ast))) {
                        res.Append(format.SpaceWithinEmptyTupleExpression.Value ? " " : "");
                    } else {
                        format.ReflowComment(res, this.GetSecondWhiteSpace(ast));
                    }
                    res.Append(')');
                } else {
                    string delimWs =
                     format.SpacesWithinParenthesisedTupleExpression != null ?
                     format.SpacesWithinParenthesisedTupleExpression.Value ? " " : "" : null;

                    ListExpression.AppendItems(res, ast, format, "(", this.IsMissingCloseGrouping(ast) ? "" : ")", this, Items, delimWs);
                } 
            }
        }
    }
}
