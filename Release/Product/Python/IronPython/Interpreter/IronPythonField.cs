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

using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonField : PythonObject, IBuiltinProperty {
        private IPythonType _fieldType;
        private bool? _isStatic;

        public IronPythonField(IronPythonInterpreter interpreter, ObjectIdentityHandle field)
            : base(interpreter, field) {
        }

        #region IBuiltinProperty Members

        public IPythonType Type {
            get {
                if (_fieldType == null) {
                    _fieldType = (IPythonType)Interpreter.MakeObject(Interpreter.Remote.GetFieldType(Value));
                }
                return _fieldType;
            }
        }

        public bool IsStatic {
            get {
                if (_isStatic == null) {
                    _isStatic = Interpreter.Remote.IsFieldStatic(Value);
                }

                return _isStatic.Value;
            }
        }

        public string Documentation {
            get { return Interpreter.Remote.GetFieldDocumentation(Value); }
        }

        public string Description {
            get { return Documentation; }
        }

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Field; }
        }

        #endregion
    }
}
