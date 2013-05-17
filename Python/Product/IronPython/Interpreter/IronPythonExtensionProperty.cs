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
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonExtensionProperty : PythonObject, IBuiltinProperty {
        private IPythonType _propertyType;

        public IronPythonExtensionProperty(IronPythonInterpreter interpreter, ObjectIdentityHandle property)
            : base(interpreter, property) {
        }

        #region IBuiltinProperty Members

        public IPythonType Type {
            get {
                if (_propertyType == null) {
                    _propertyType = Interpreter.GetTypeFromType(Interpreter.Remote.GetExtensionPropertyType(Value));
                }
                return _propertyType;
            }
        }

        public bool IsStatic {
            get {
                return false;
            }
        }

        public string Documentation {
            get { return Interpreter.Remote.GetExtensionPropertyDocumentation(Value); }
        }

        public string Description {
            get { return Documentation; }
        }

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Property; }
        }

        #endregion
    }
}
