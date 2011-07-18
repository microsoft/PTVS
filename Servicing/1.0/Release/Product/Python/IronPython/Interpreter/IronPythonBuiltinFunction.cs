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

using System.Collections.Generic;
using IronPython.Runtime.Types;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinFunction : PythonObject<BuiltinFunction>, IPythonFunction {
        private IPythonFunctionOverload[] _targets;

        public IronPythonBuiltinFunction(IronPythonInterpreter interpreter, BuiltinFunction function)
            : base(interpreter, function) {
        }

        #region IBuiltinFunction Members

        public string Name {
            get { return Value.__name__; }
        }

        public string Documentation {
            get { return Value.__doc__; }
        }

        public IList<IPythonFunctionOverload> Overloads {
            get {
                // skip methods that are virtual base helpers (e.g. methods like
                // object.Equals(Object_#1, object other))

                if (_targets == null) {
                    var result = new List<IPythonFunctionOverload>();
                    foreach (var ov in Value.Overloads.Functions) {
                        BuiltinFunction overload = (ov as BuiltinFunction);
                        if (overload.Overloads.Targets[0].DeclaringType.IsAssignableFrom(Value.DeclaringType) ||
                            overload.Overloads.Targets[0].DeclaringType.FullName.StartsWith("IronPython.Runtime.Operations.")) {
                            result.Add(new IronPythonBuiltinFunctionTarget(Interpreter, overload.Targets[0], ((IronPythonType)DeclaringType).Value.__clrtype__()));
                        }
                    }
                    _targets = result.ToArray();
                }

                return _targets;
            }
        }

        public IPythonType DeclaringType {
            get { return Interpreter.GetTypeFromType(Value.DeclaringType); }
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
                return Interpreter.GetModule(this.Value.Get__module__(Interpreter.CodeContext));
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
