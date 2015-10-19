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
using System.Diagnostics;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonConstructorFunctionTarget : IPythonFunctionOverload {
        private readonly IronPythonInterpreter _interpreter;
        private RemoteInterpreterProxy _remote;
        private readonly ObjectIdentityHandle _overload;
        private readonly IronPythonType _declaringType;
        private IParameterInfo[] _params;
        private List<IPythonType> _returnType;

        public IronPythonConstructorFunctionTarget(IronPythonInterpreter interpreter, ObjectIdentityHandle overload, IronPythonType declType) {
            Debug.Assert(interpreter.Remote.TypeIs<MethodBase>(overload));
            _interpreter = interpreter;
            _interpreter.UnloadingDomain += Interpreter_UnloadingDomain;
            _remote = _interpreter.Remote;
            _overload = overload;
            _declaringType = declType;
        }

        private void Interpreter_UnloadingDomain(object sender, EventArgs e) {
            _remote = null;
            _interpreter.UnloadingDomain -= Interpreter_UnloadingDomain;
        }

        #region IBuiltinFunctionTarget Members

        public string Documentation {
            get { return ""; }
        }

        public string ReturnDocumentation {
            get { return ""; }
        }

        public IParameterInfo[] GetParameters() {
            if (_params == null) {
                var ri = _remote;
                bool isInstanceExtensionMethod = ri != null ? ri.IsInstanceExtensionMethod(_overload, _declaringType.Value) : false;

                var parameters = ri != null ? ri.GetParametersNoCodeContext(_overload) : new ObjectIdentityHandle[0];
                var res = new List<IParameterInfo>(parameters.Length + 1);
                res.Add(new IronPythonNewClsParameterInfo(_declaringType));
                foreach (var param in parameters) {
                    if (res.Count == 0 && isInstanceExtensionMethod) {
                        // skip instance parameter
                        isInstanceExtensionMethod = false;
                        continue;
                    } else {
                        res.Add(new IronPythonParameterInfo(_interpreter, param));
                    }
                }

                _params = res.ToArray();
            }
            return _params;
        }

        public IList<IPythonType> ReturnType {
            get {
                if (_returnType == null) {
                    _returnType = new List<IPythonType>();
                    var ri = _remote;
                    if (ri != null) {
                        _returnType.Add(_interpreter.GetTypeFromType(ri.GetBuiltinFunctionOverloadReturnType(_overload)));
                    }
                }
                return _returnType;
            }
        }

        #endregion
    }
}
