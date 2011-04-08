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

namespace Microsoft.PythonTools.Parsing.Ast {

    public partial class BinaryExpression : Expression {
        private readonly Expression _left, _right;
        private readonly PythonOperator _op;

        public BinaryExpression(PythonOperator op, Expression left, Expression right) {
            Contract.Assert(left != null);
            Contract.Assert(right != null);
            if (op == PythonOperator.None) throw new ArgumentException("bad operator");

            _op = op;
            _left = left;
            _right = right;
            StartIndex = left.StartIndex;
            EndIndex = right.EndIndex;
        }

        public Expression Left {
            get { return _left; }
        }

        public Expression Right {
            get { return _right; }
        }

        public PythonOperator Operator {
            get { return _op; }
        }

        private bool IsComparison() {
            switch (_op) {
                case PythonOperator.LessThan:
                case PythonOperator.LessThanOrEqual:
                case PythonOperator.GreaterThan:
                case PythonOperator.GreaterThanOrEqual:
                case PythonOperator.Equal:
                case PythonOperator.NotEqual:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                case PythonOperator.IsNot:
                case PythonOperator.Is:
                    return true;
            }
            return false;
        }

        public override string NodeName {
            get {
                return "binary operator";
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _left.Walk(walker);
                _right.Walk(walker);
            }
            walker.PostWalk(this);
        }
    }
}
