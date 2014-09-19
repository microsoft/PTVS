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
    class IronPythonProperty : PythonObject, IBuiltinProperty {
        private IPythonType _propertyType;
        private bool? _isStatic;

        public IronPythonProperty(IronPythonInterpreter interpreter, ObjectIdentityHandle property)
            : base(interpreter, property) {
        }

        #region IBuiltinProperty Members

        public IPythonType Type {
            get {
                if (_propertyType == null) {
                    _propertyType = (IPythonType)Interpreter.MakeObject(Interpreter.Remote.GetPropertyType(Value));
                }
                return _propertyType;
            }
        }

        public bool IsStatic {
            get {
                if (_isStatic == null) {
                    _isStatic = Interpreter.Remote.IsPropertyStatic(Value);
                }
                return _isStatic.Value;
            }
        }

        public string Documentation {
            get {
                return Interpreter.Remote.GetPropertyDocumentation(Value);
            }
        }

        public string Description {
            get {
                if (Type == null) {
                    return "property of unknown type";
                } else {
                    return "property of type " + Type.Name;
                }
            }
        }

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Property; }
        }

        #endregion
    }
}
