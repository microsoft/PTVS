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

using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class IfStatement : Statement {
        private readonly IfStatementTest[] _tests;
        private readonly Statement _else;

        public IfStatement(IfStatementTest[] tests, Statement else_) {
            _tests = tests;
            _else = else_;
        }

        public IList<IfStatementTest> Tests {
            get { return _tests; }
        }

        public Statement ElseStatement {
            get { return _else; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_tests != null) {
                    foreach (IfStatementTest test in _tests) {
                        test.Walk(walker);
                    }
                }
                if (_else != null) {
                    _else.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            for (int i = 0; i < _tests.Length; i++) {
                if (itemWhiteSpace != null) {
                    res.Append(itemWhiteSpace[i]);
                }

                if (i == 0) {
                    res.Append("if");
                } else {
                    res.Append("elif");
                }
                _tests[i].AppendCodeString(res, ast);
            }

            if (_else != null) {
                res.Append(this.GetProceedingWhiteSpace(ast));
                res.Append("else");
                _else.AppendCodeString(res, ast);
            }
        }


        internal override string GetLeadingWhiteSpace(PythonAst ast) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            if (itemWhiteSpace != null && itemWhiteSpace.Length > 0) {
                return itemWhiteSpace[0];
            }
            return null;
        }

    }
}
