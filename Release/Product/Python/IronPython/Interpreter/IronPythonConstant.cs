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

using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonConstant : PythonObject<object>, IPythonConstant {
        public IronPythonConstant(IronPythonInterpreter interpreter, object value)
            : base(interpreter, value) {
        }

        public override PythonMemberType MemberType {
            get {
                if (Value != null && Value.GetType().IsEnum) {
                    return PythonMemberType.EnumInstance;
                }
                return PythonMemberType.Constant;
            }
        }

        public IPythonType Type {
            get {
                if (Value == null) {
                    return Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                }
                return Interpreter.GetTypeFromType(Value.GetType()); 
            }
        }
    }
}
