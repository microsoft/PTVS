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
    public class IfStatementTest : Node {
        private int _headerIndex;
        private readonly Expression _test;
        private Statement _body;

        public IfStatementTest(Expression test, Statement body) {
            _test = test;
            _body = body;
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
            get { return _headerIndex; }
        }

        public Expression Test {
            get { return _test; }
        }

        public Statement Body {
            get { return _body; }
            set { _body = value; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public SourceLocation GetHeader(PythonAst ast) {
            return ast.IndexToLocation(_headerIndex);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            _test.AppendCodeString(res, ast, format);
            _body.AppendCodeString(res, ast, format);
        }
    }
}
