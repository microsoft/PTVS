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

using IronPython.Runtime.Operations;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinMethodDescriptor : PythonObject, IPythonMethodDescriptor {
        private IPythonFunction _function;

        public IronPythonBuiltinMethodDescriptor(IronPythonInterpreter interpreter, ObjectIdentityHandle desc)
            : base(interpreter, desc) {
        }

        #region IBuiltinMethodDescriptor Members

        public IPythonFunction Function {
            get {
                if (_function == null) {
                    var func = Interpreter.Remote.GetBuiltinMethodDescriptorTemplate(Value);

                    _function = (IPythonFunction)Interpreter.MakeObject(func);
                }
                return _function;
            }
        }

        #endregion

        #region IMember Members

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Method; }
        }

        #endregion
    }
}
