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
using System.Text;

namespace Microsoft.PythonTools.Parsing.Ast {
    public class WithStatement : Statement {
        private int _headerIndex;
        private readonly WithItem[] _items;
        private Statement _body;

        public WithStatement(WithItem[] items, Statement body) {
            _items = items;
            _body = body;
        }

        public IList<WithItem> Items {
            get {
                return _items;
            }
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
        }

        public Statement Body {
            get { return _body; }
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

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            res.Append(this.GetProceedingWhiteSpace(ast));
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

                item.ContextManager.AppendCodeString(res, ast);
                if (item.Variable != null) {
                    if (itemWhiteSpace != null) {
                        res.Append(itemWhiteSpace[whiteSpaceIndex++]);
                    } else {
                        res.Append(' ');
                    }
                    res.Append("as");
                    item.Variable.AppendCodeString(res, ast);
                }
            }
            
            _body.AppendCodeString(res, ast);
        }
    }

    public sealed class WithItem : Node {
        private readonly Expression _contextManager;
        private readonly Expression _variable;

        public WithItem(Expression contextManager, Expression variable) {
            _contextManager = contextManager;
            _variable = variable;
        }

        public Expression ContextManager {
            get {
                return _contextManager;
            }
        }

        public Expression Variable {
            get {
                return _variable;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (ContextManager != null) {
                ContextManager.Walk(walker);
            }
            if (Variable != null) {
                Variable.Walk(walker);
            }
        }

        internal override void AppendCodeString(StringBuilder res, PythonAst ast) {
            // WithStatement expands us 
            throw new InvalidOperationException();
        }
    }
}
