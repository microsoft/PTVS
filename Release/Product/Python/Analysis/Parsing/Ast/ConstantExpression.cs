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

namespace Microsoft.PythonTools.Parsing.Ast {
    public class ConstantExpression : Expression {
        private readonly object _value;

        public ConstantExpression(object value) {
            _value = value;
        }

        internal static ConstantExpression MakeUnicode(string value) {
            return new ConstantExpression(new UnicodeWrapper(value));
        }

        public object Value {
            get {
                UnicodeWrapper wrapper;
                if ((wrapper = _value as UnicodeWrapper) != null) {
                    return wrapper.Value;
                }
                
                return _value; 
            }
        }

        internal bool IsUnicodeString {
            get {
                return _value is UnicodeWrapper;
            }
        }

        internal override string CheckAssign() {
            if (_value == null) {
                return "assignment to None";
            }

            return "can't assign to literal";
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
            }
            walker.PostWalk(this);
        }

        public override string NodeName {
            get {
                return "literal";
            }
        }

        class UnicodeWrapper {
            public readonly object Value;

            public UnicodeWrapper(string value) {
                Value = value;
            }
        }
    }
}
