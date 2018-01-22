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
    public class ComprehensionFor : ComprehensionIterator {
        private readonly Expression _lhs, _list;
        private readonly bool _isAsync;

        public ComprehensionFor(Expression lhs, Expression list) {
            _lhs = lhs;
            _list = list;
        }

        public ComprehensionFor(Expression lhs, Expression list, bool isAsync)
            : this(lhs, list) {
            _isAsync = isAsync;
        }

        public Expression Left {
            get { return _lhs; }
        }

        public Expression List {
            get { return _list; }
        }

        public bool IsAsync => _isAsync;

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_lhs != null) {
                    _lhs.Walk(walker);
                }
                if (_list != null) {
                    _list.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public int GetIndexOfFor(PythonAst ast) {
            if (!IsAsync) {
                return StartIndex;
            }
            return StartIndex + 5 + this.GetPreceedingWhiteSpace(ast).Length;
        }

        public int GetIndexOfIn(PythonAst ast) {
            if (this.IsIncompleteNode(ast)) {
                return -1;
            }
            return Left.EndIndex + this.GetSecondWhiteSpace(ast).Length;
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            if (_isAsync) {
                res.Append(this.GetThirdWhiteSpace(ast));
                res.Append("async");
            }
            res.Append(this.GetPreceedingWhiteSpace(ast));
            res.Append("for");
            _lhs.AppendCodeString(res, ast, format);
            if (!this.IsIncompleteNode(ast)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append("in");
                _list.AppendCodeString(res, ast, format);
            }
        }
    }
}
