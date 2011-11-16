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
using System.Collections.Generic;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinFunction : PythonObject, IPythonFunction {
        private IronPythonBuiltinFunctionTarget[] _targets;
        private IPythonType _declaringType;
        private IPythonModule _declaringModule;

        public IronPythonBuiltinFunction(IronPythonInterpreter interpreter, ObjectIdentityHandle function)
            : base(interpreter, function) {
        }

        #region IBuiltinFunction Members

        public string Name {
            get { return Interpreter.Remote.GetBuiltinFunctionName(Value); }
        }

        public string Documentation {
            get { return Interpreter.Remote.GetBuiltinFunctionDocumentation(Value); }
        }

        public IList<IPythonFunctionOverload> Overloads {
            get {
                // skip methods that are virtual base helpers (e.g. methods like
                // object.Equals(Object_#1, object other))

                if (_targets == null) {
                    var overloads = Interpreter.Remote.GetBuiltinFunctionOverloads(Value);
                    var result = new IronPythonBuiltinFunctionTarget[overloads.Length];
                    var decltype = (IronPythonType)DeclaringType;
                    for(int i = 0; i<overloads.Length; i++){
                        result[i] = new IronPythonBuiltinFunctionTarget(
                            Interpreter, 
                            overloads[i], 
                            decltype
                        );

                    }

                    _targets = result;
                }

                return _targets;
            }
        }

        public IPythonType DeclaringType {
            get {
                if (_declaringType == null) {
                    _declaringType = Interpreter.GetTypeFromType(Interpreter.Remote.GetBuiltinFunctionDeclaringPythonType(Value));
                }
                return _declaringType;
            }
        }

        public bool IsBuiltin {
            get {
                return true;
            }
        }

        public bool IsStatic {
            get {
                return true;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                if (_declaringModule == null) {
                    _declaringModule = Interpreter.GetModule(Interpreter.Remote.GetBuiltinFunctionModule(Value));
                }
                return _declaringModule;
            }
        }

        #endregion

        #region IMember Members

        public override PythonMemberType MemberType {
            get { return PythonMemberType.Function; }
        }

        #endregion

    }
}
