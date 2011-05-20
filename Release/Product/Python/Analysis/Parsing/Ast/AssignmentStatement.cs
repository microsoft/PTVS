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
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class AssignmentStatement : Statement {
        // _left.Length is 1 for simple assignments like "x = 1"
        // _left.Length will be 3 for "x = y = z = 1"
        private readonly Expression[] _left;
        private readonly Expression _right;

        public AssignmentStatement(Expression[] left, Expression right) {
            _left = left;
            _right = right;
        }

        public IList<Expression> Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (Expression e in _left) {
                    e.Walk(walker);
                }
                _right.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            var lhs = this.GetListWhiteSpace(ast);
            for (int i = 0; i < Left.Count; i++) {
                if (lhs != null && i != 0) {
                    res.Append(lhs[i - 1]);
                    res.Append("=");
                }
                Left[i].AppendCodeString(res, ast);
            }
            if (lhs != null) {
                res.Append(lhs[lhs.Length - 1]);
            }
            res.Append("=");

            Right.AppendCodeString(res, ast);

        }


        internal override string GetLeadingWhiteSpace(PythonAst ast) {
            if (_left.Length > 0 && _left[0] != null) {
                return _left[0].GetLeadingWhiteSpace(ast);
            }

            return null;
        }

    }
}
