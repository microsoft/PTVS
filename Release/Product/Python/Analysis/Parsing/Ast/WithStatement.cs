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

namespace Microsoft.PythonTools.Parsing.Ast {
    public class WithStatement : Statement {
        private int _headerIndex;
        private readonly Expression _contextManager;
        private readonly Expression _var;
        private Statement _body;

        public WithStatement(Expression contextManager, Expression var, Statement body) {
            _contextManager = contextManager;
            _var = var;
            _body = body;
        }

        public int HeaderIndex {
            set { _headerIndex = value; }
        }

        public Expression Variable {
            get { return _var; }
        }

        public Expression ContextManager {
            get { return _contextManager; }
        }

        public Statement Body {
            get { return _body; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_contextManager != null) {
                    _contextManager.Walk(walker);
                }
                if (_var != null) {
                    _var.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
