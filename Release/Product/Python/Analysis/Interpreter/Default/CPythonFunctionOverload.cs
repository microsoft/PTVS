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
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonFunctionOverload : IPythonFunctionOverload {
        private readonly CPythonParameterInfo[] _parameters;
        private readonly string _doc, _returnDoc;
        private IPythonType _retType;
        private static readonly CPythonParameterInfo[] EmptyParameters = new CPythonParameterInfo[0];

        public CPythonFunctionOverload(TypeDatabase typeDb, Dictionary<string, object> argInfo, bool isMethod) {
            if (argInfo != null) {
                object args;
                IList<object> argList;
                if (argInfo.TryGetValue("args", out args)) {
                    argList = (IList<object>)args;
                    if (argList != null) {
                        if (argList.Count == 0 || (isMethod && argList.Count == 1)) {
                            _parameters = EmptyParameters;
                        } else {
                            _parameters = new CPythonParameterInfo[isMethod ? argList.Count - 1 : argList.Count];
                            for (int i = 0; i < _parameters.Length; i++) {
                                _parameters[i] = new CPythonParameterInfo(typeDb, (isMethod ? argList[i + 1] : argList[i]) as Dictionary<string, object>);
                            }
                        }
                    }
                }

                object docObj;
                if (argInfo.TryGetValue("doc", out docObj)) {
                    _doc = docObj as string;
                }

                if (argInfo.TryGetValue("return_doc", out docObj)) {
                    _returnDoc = docObj as string;
                }

                object retTypeObj;
                argInfo.TryGetValue("ret_type", out retTypeObj);

                typeDb.LookupType(retTypeObj, (value) => _retType = value);
            }
        }
        
        #region IBuiltinFunctionOverload Members

        public string Documentation {
            get { return _doc;  }
        }

        public string ReturnDocumentation {
            get { return _returnDoc; }
        }

        public IParameterInfo[] GetParameters() {
            return _parameters;
        }

        public IPythonType ReturnType {
            get { return _retType; }
        }

        #endregion
    }
}
