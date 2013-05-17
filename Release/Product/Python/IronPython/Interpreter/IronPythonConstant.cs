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
    class IronPythonConstant : PythonObject, IPythonConstant {
        private IPythonType _type;
        private PythonMemberType _memType;

        public IronPythonConstant(IronPythonInterpreter interpreter, ObjectIdentityHandle value)
            : base(interpreter, value) {
        }

        public override PythonMemberType MemberType {
            get {
                if (_memType == PythonMemberType.Unknown) {
                    if (!Value.IsNull && Interpreter.Remote.IsEnumValue(Value)) {
                        _memType = PythonMemberType.EnumInstance;
                    } else {
                        _memType = PythonMemberType.Constant;
                    }
                }
                return _memType;
            }
        }

        public IPythonType Type {
            get {
                if (_type == null) {
                    if (Value.IsNull) {
                        _type = Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                    } else {
                        _type = Interpreter.GetTypeFromType(Interpreter.Remote.GetObjectPythonType(Value));
                    }
                }
                return _type;
            }
        }
    }
}
