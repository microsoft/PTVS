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

using System.Diagnostics;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class UnaryExpression : Expression {
        private readonly Expression _expression;
        private readonly PythonOperator _op;

        public UnaryExpression(PythonOperator op, Expression expression) {
            _op = op;
            _expression = expression;
            EndIndex = expression.EndIndex;
        }

        public Expression Expression {
            get { return _expression; }
        }

        public PythonOperator Op {
            get { return _op; }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_expression != null) {
                    _expression.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
