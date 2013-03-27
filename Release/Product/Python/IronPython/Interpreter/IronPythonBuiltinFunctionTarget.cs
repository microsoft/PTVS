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
using System.Diagnostics;
using System.Reflection;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinFunctionTarget : IPythonFunctionOverload {
        private readonly IronPythonInterpreter _interpreter;
        private readonly ObjectIdentityHandle _overload;
        private readonly IronPythonType _declaringType;
        private IParameterInfo[] _params;
        private List<IPythonType> _returnType;

        public IronPythonBuiltinFunctionTarget(IronPythonInterpreter interpreter, ObjectIdentityHandle overload, IronPythonType declType) {
            Debug.Assert(interpreter.Remote.TypeIs<MethodBase>(overload));
            _interpreter = interpreter;
            _overload = overload;
            _declaringType = declType;
        }

        #region IBuiltinFunctionTarget Members

        // FIXME
        public string Documentation {
            get { return ""; }
        }

        // FIXME
        public string ReturnDocumentation {
            get { return "";  }
        }

        public IParameterInfo[] GetParameters() {
            if (_params == null) {
                bool isInstanceExtensionMethod = _interpreter.Remote.IsInstanceExtensionMethod(_overload, _declaringType.Value);

                var parameters = _interpreter.Remote.GetParametersNoCodeContext(_overload);
                var res = new List<IParameterInfo>(parameters.Length);
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
                    _returnType.Add(_interpreter.GetTypeFromType(_interpreter.Remote.GetBuiltinFunctionOverloadReturnType(_overload)));
                }
                return _returnType;
            }
        }

        #endregion
    }
}
