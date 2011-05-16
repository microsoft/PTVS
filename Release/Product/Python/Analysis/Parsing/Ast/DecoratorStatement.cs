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
using System.Diagnostics.Contracts;
using System.Text;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class DecoratorStatement : Statement {
        private readonly Expression[] _decorators;

        public DecoratorStatement(Expression[] decorators) {
            _decorators = decorators;
        }

        public IList<Expression> Decorators {
            get { return _decorators; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                foreach (var decorator in _decorators) {
                    if (decorator != null) {
                        decorator.Walk(walker);
                    }
                }
            }
            walker.PostWalk(this);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast) {
            var decorateWhiteSpace = this.GetNamesWhiteSpace(ast);
            if (Decorators != null) {
                for (int i = 0, curWhiteSpace = 0; i < Decorators.Count; i++) {
                    if (decorateWhiteSpace != null) {
                        res.Append(decorateWhiteSpace[curWhiteSpace++]);
                    }
                    res.Append('@');
                    if (Decorators[i] != null) {
                        Decorators[i].AppendCodeString(res, ast);
                        if (decorateWhiteSpace != null) {
                            res.Append(decorateWhiteSpace[curWhiteSpace++]);
                        } else {
                            res.Append(Environment.NewLine);
                        }
                    }
                }
            }
        }
    }
}
