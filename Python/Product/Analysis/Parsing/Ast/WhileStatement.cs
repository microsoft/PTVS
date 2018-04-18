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
        public WhileStatement(Expression test, Statement body, Statement else_) {
            Test = test;
            Body = body;
            ElseStatement = else_;
        }

        public Expression Test { get; }
        public Statement Body { get; }
        public Statement ElseStatement { get; }

        public int HeaderIndex { get; private set; }
        public int ElseIndex { get; private set; }
        public override int KeywordLength => 5;

        public void SetLoc(int start, int header, int end, int elseIndex) {
            SetLoc(start, end);
            HeaderIndex = header;
            ElseIndex = elseIndex;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                Test?.Walk(walker);
                Body?.Walk(walker);
                ElseStatement?.Walk(walker);
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            res.Append("while");
            Test.AppendCodeString(res, ast, format);
            Body.AppendCodeString(res, ast, format);
            if (ElseStatement != null) {
                format.ReflowComment(res, this.GetSecondWhiteSpaceDefaultNull(ast));
                res.Append("else");
                ElseStatement.AppendCodeString(res, ast, format);
            }
        }
    }
}
