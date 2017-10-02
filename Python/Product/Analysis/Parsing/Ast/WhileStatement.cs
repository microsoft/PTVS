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

using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class WhileStatement : Statement {
        // Marks the end of the condition of the while loop
        private int _indexHeader, _elseIndex;
        private readonly Expression _test;
        private readonly Statement _body;
        private readonly Statement _else;

        public WhileStatement(Expression test, Statement body, Statement else_) {
            _test = test;
            _body = body;
            _else = else_;
        }

        public Expression Test {
            get { return _test; }
        }

        public Statement Body {
            get { return _body; }
        }

        public Statement ElseStatement {
            get { return _else; }
        }

        public int HeaderIndex {
            get {
                return _indexHeader;
            }
        }

        public int ElseIndex {
            get {
                return _elseIndex;
            }
        }

        public void SetLoc(int start, int header, int end, int elseIndex) {
            SetLoc(start, end);
            _indexHeader = header;
            _elseIndex = elseIndex;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
                if (_else != null) {
                    _else.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("while");
            _test.AppendCodeString(res, ast, format);
            _body.AppendCodeString(res, ast, format);
            if (_else != null) {
                format.ReflowComment(res, this.GetSecondWhiteSpaceDefaultNull(ast));
                res.Append("else");
                _else.AppendCodeString(res, ast, format);
            }
        }
    }
}
