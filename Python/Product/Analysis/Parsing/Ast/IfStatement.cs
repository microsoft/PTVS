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
    public class IfStatement : Statement {
        private readonly IfStatementTest[] _tests;
        private readonly Statement _else;
        private int _elseIndex;

        public IfStatement(IfStatementTest[] tests, Statement else_) {
            _tests = tests;
            _else = else_;
        }

        public IList<IfStatementTest> Tests => _tests;
        public Statement ElseStatement => _else;
        internal IfStatementTest[] TestsInternal => _tests;

        public int ElseIndex {
            get {
                return _elseIndex;
            }
            set {
                _elseIndex = value;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_tests != null) {
                    foreach (IfStatementTest test in _tests) {
                        test.Walk(walker);
                    }
                }

                _else?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            for (int i = 0; i < _tests.Length; i++) {
                if (itemWhiteSpace != null) {
                    format.ReflowComment(res, itemWhiteSpace[i]);
                }

                if (i == 0) {
                    res.Append("if");
                } else {
                    res.Append("elif");
                }
                _tests[i].AppendCodeString(res, ast, format);
            }

            if (_else != null) {
                format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
                res.Append("else");
                _else.AppendCodeString(res, ast, format);
            }
        }


        public override string GetLeadingWhiteSpace(PythonAst ast) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            if (itemWhiteSpace != null && itemWhiteSpace.Length > 0) {
                return itemWhiteSpace[0];
            }
            return null;
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            if (itemWhiteSpace != null && itemWhiteSpace.Length > 0) {
                itemWhiteSpace[0] = whiteSpace;
            }
        }
    }
}
