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
    public class IndexExpression : Expression {
        private readonly Expression _target;
        private readonly Expression _index;

        public IndexExpression(Expression target, Expression index) {
            _target = target;
            _index = index;
        }

        public Expression Target {
            get { return _target; }
        }

        public Expression Index {
            get { return _index; }
        }

        internal override string CheckAssign() {
            return null;
        }

        internal override string CheckDelete() {
            return null;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_target != null) {
                    _target.Walk(walker);
                }
                if (_index != null) {
                    _index.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        private bool IsSlice {
            get {
                return _index is SliceExpression;
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Target.AppendCodeString(res, ast, format);
            format.Append(
                res,
                format.SpaceBeforeIndexBracket,
                " ",
                "",
                this.GetPreceedingWhiteSpace(ast)
            );

            res.Append('[');
            _index.AppendCodeString(
                res, 
                ast, 
                format, 
                format.SpaceWithinIndexBrackets != null ? format.SpaceWithinIndexBrackets.Value ? " " : "" : null
            );

            if (!this.IsMissingCloseGrouping(ast)) {
                format.Append(
                    res,
                    format.SpaceWithinIndexBrackets,
                    " ",
                    "",
                    this.GetSecondWhiteSpace(ast)
                );
                res.Append(']');
            }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            return Target.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            Target.SetLeadingWhiteSpace(ast, whiteSpace);
        }
    }
}
