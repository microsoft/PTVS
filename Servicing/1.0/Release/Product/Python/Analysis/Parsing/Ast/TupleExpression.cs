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

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            if (this.IsAltForm(ast)) {
                ListExpression.AppendItems(res, ast, "", "", this, Items);
            } else {
                ListExpression.AppendItems(res, ast, "(", this.IsMissingCloseGrouping(ast) ? "" : ")", this, Items);
            }
        }
    }
}
