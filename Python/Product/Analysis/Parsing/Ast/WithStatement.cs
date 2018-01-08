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

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class WithStatement : Statement {
        private int _headerIndex;
        private readonly WithItem[] _items;
        private readonly Statement _body;
        private readonly bool _isAsync;

        public WithStatement(WithItem[] items, Statement body) {
            _items = items;
            _body = body;
        }

        public WithStatement(WithItem[] items, Statement body, bool isAsync) : this(items, body) {
            _isAsync = isAsync;
        }


        public IList<WithItem> Items {
            get {
                return _items;
            }
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public Statement Body {
            get { return _body; }
        }

        public bool IsAsync {
            get { return _isAsync; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var item in _items) {
                    item.Walk(walker);
                }

                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public int GetIndexOfWith(PythonAst ast) {
            if (!IsAsync) {
                return StartIndex;
            }
            return StartIndex + this.GetSecondWhiteSpace(ast).Length + 5;
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            format.ReflowComment(res, this.GetPreceedingWhiteSpace(ast));
            if (IsAsync) {
                res.Append("async");
                res.Append(this.GetSecondWhiteSpace(ast));
            }
            res.Append("with");
            var itemWhiteSpace = this.GetListWhiteSpace(ast);
            int whiteSpaceIndex = 0;
            for (int i = 0; i < _items.Length; i++) {
                var item = _items[i];
                if (i != 0) {
                    if (itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[whiteSpaceIndex++]);
                    }
                    res.Append(',');
                }

                item.ContextManager.AppendCodeString(res, ast, format);
                if (item.Variable != null) {
                    if (itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[whiteSpaceIndex++]);
                    } else {
                        res.Append(' ');
                    }
                    res.Append("as");
                    item.Variable.AppendCodeString(res, ast, format);
                }
            }

            _body.AppendCodeString(res, ast, format);
        }
    }

    public sealed class WithItem : Node {
        public WithItem(Expression contextManager, Expression variable, int asIndex) {
            ContextManager = contextManager;
            Variable = variable;
            AsIndex = asIndex;
        }

        public Expression ContextManager { get; }
        public Expression Variable { get; }
        public int AsIndex { get; }

        public override void Walk(PythonWalker walker) {
            ContextManager?.Walk(walker);
            Variable?.Walk(walker);
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            // WithStatement expands us 
            throw new InvalidOperationException();
        }
    }
}
