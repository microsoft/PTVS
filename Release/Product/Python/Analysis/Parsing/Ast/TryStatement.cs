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

namespace Microsoft.PythonTools.Parsing.Ast {

    public class TryStatement : Statement {
        private int _headerIndex;
        /// <summary>
        /// The statements under the try-block.
        /// </summary>
        private Statement _body;

        /// <summary>
        /// Array of except (catch) blocks associated with this try. NULL if there are no except blocks.
        /// </summary>
        private readonly TryStatementHandler[] _handlers;

        /// <summary>
        /// The body of the optional Else block for this try. NULL if there is no Else block.
        /// </summary>
        private Statement _else;

        /// <summary>
        /// The body of the optional finally associated with this try. NULL if there is no finally block.
        /// </summary>
        private Statement _finally;

        public TryStatement(Statement body, TryStatementHandler[] handlers, Statement else_, Statement finally_) {
            _body = body;
            _handlers = handlers;
            _else = else_;
            _finally = finally_;
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
        }

        public Statement Body {
            get { return _body; }
        }

        public Statement Else {
            get { return _else; }
        }

        public Statement Finally {
            get { return _finally; }
        }

        public IList<TryStatementHandler> Handlers {
            get { return _handlers; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_body != null) {
                    _body.Walk(walker);
                }
                if (_handlers != null) {
                    foreach (TryStatementHandler handler in _handlers) {
                        handler.Walk(walker);
                    }
                }
                if (_else != null) {
                    _else.Walk(walker);
                }
                if (_finally != null) {
                    _finally.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }

    // A handler corresponds to the except block.
    public class TryStatementHandler : Node {
        private int _headerIndex;
        private readonly Expression _test, _target;
        private readonly Statement _body;

        public TryStatementHandler(Expression test, Expression target, Statement body) {
            _test = test;
            _target = target;
            _body = body;
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public Expression Test {
            get { return _test; }
        }

        public Expression Target {
            get { return _target; }
        }

        public Statement Body {
            get { return _body; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_test != null) {
                    _test.Walk(walker);
                }
                if (_target != null) {
                    _target.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
