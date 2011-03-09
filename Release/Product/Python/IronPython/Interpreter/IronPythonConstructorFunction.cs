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
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonConstructorFunction : IPythonFunction {
        private readonly ConstructorInfo[] _infos;
        private readonly IronPythonInterpreter _interpreter;
        private readonly IronPythonType _type;

        public IronPythonConstructorFunction(IronPythonInterpreter interpreter, ConstructorInfo[] infos, IronPythonType type) {
            _interpreter = interpreter;
            _infos = infos;
            _type = type;
        }

        #region IBuiltinFunction Members

        public string Name {
            get { return "__new__"; }
        }

        // TODO: Documentation
        public string Documentation {
            get { return ""; }
        }

        public IList<IPythonFunctionOverload> Overloads {
            get {
                IPythonFunctionOverload[] res = new IPythonFunctionOverload[_infos.Length];
                for (int i = 0; i < _infos.Length; i++) {
                    res[i] = new IronPythonBuiltinFunctionTarget(_interpreter, _infos[i], _infos[0].DeclaringType);
                }
                return res;
            }
        }

        public IPythonType DeclaringType {
            get { return _interpreter.GetTypeFromType(_infos[0].DeclaringType); }
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
                return _type.DeclaringModule;
            }
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Function; }
        }

        #endregion
    }
}
