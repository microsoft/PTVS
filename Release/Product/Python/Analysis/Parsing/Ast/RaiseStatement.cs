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
    public class RaiseStatement : Statement {
        private readonly Expression _type, _value, _traceback, _cause;

        public RaiseStatement(Expression exceptionType, Expression exceptionValue, Expression traceBack, Expression cause) {
            _type = exceptionType;
            _value = exceptionValue;
            _traceback = traceBack;
            _cause = cause;
        }

        public Expression ExceptType {
            get {
                return _type;
            }
        }

        public Expression Value {
            get { return _value; }
        }

        public Expression Traceback {
            get { return _traceback; }
        }

        public Expression Cause {
            get {
                return _cause;
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_type != null) {
                    _type.Walk(walker);
                }
                if (_value != null) {
                    _value.Walk(walker);
                }
                if (_traceback != null) {
                    _traceback.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }
    }
}
