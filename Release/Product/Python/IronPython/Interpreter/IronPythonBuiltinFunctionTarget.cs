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
using System.Reflection;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Scripting.Runtime;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonBuiltinFunctionTarget : IPythonFunctionOverload {
        private static readonly string _codeCtxType = "IronPython.Runtime.CodeContext";
        private readonly IronPythonInterpreter _interpreter;
        private readonly MethodBase _overload;
        private readonly Type _declaringType;
        private IParameterInfo[] _params;

        public IronPythonBuiltinFunctionTarget(IronPythonInterpreter Interpreter, MethodBase overload, Type declType) {
            _interpreter = Interpreter;
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
                var target = _overload;

                bool isInstanceExtensionMethod = false;
                if (!target.DeclaringType.IsAssignableFrom(_declaringType)) {
                    // extension method
                    isInstanceExtensionMethod = !target.IsDefined(typeof(StaticExtensionMethodAttribute), false);
                }

                var parameters = _overload.GetParameters();
                var res = new List<IParameterInfo>(parameters.Length);
                foreach (var param in parameters) {
                    if (res.Count == 0 && param.ParameterType.FullName == _codeCtxType) {
                        continue;
                    } else if (res.Count == 0 && isInstanceExtensionMethod) {
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

        public IPythonType ReturnType {
            get {
                MethodInfo mi = _overload as MethodInfo;
                if (mi != null) {
                    return _interpreter.GetTypeFromType(mi.ReturnType);
                }

                return _interpreter.GetTypeFromType(_overload.DeclaringType);
            }
        }

        #endregion
    }
}
