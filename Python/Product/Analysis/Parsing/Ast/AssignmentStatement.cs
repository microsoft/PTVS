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

using System.Collections.Generic;
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

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var lhs = this.GetListWhiteSpace(ast);
            for (int i = 0; i < Left.Count; i++) {
                if (lhs != null && i != 0) {
                    format.Append(
                        res,
                        format.SpacesAroundAssignmentOperator,
                        " ",
                        "",
                        lhs[i - 1]
                    );
                    res.Append("=");
                }
                Left[i].AppendCodeString(
                    res, 
                    ast, 
                    format,
                    i != 0 && format.SpacesAroundAssignmentOperator != null ?
                        format.SpacesAroundAssignmentOperator.Value ? " " : "" :
                        null
                );
            }
            if (lhs != null) {
                format.Append(
                    res,
                    format.SpacesAroundAssignmentOperator, 
                    " ", 
                    "",
                    lhs[lhs.Length - 1]
                );
            }
            res.Append("=");

            Right.AppendCodeString(
                res, 
                ast, 
                format, 
                format.SpacesAroundAssignmentOperator != null ? 
                    format.SpacesAroundAssignmentOperator.Value ? " " : "" : 
                    null
            );
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (_left.Length > 0 && _left[0] != null) {
                return _left[0].GetLeadingWhiteSpace(ast);
            }

            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (_left.Length > 0 && _left[0] != null) {
                _left[0].SetLeadingWhiteSpace(ast, whiteSpace);
            }
        }
    }
}
