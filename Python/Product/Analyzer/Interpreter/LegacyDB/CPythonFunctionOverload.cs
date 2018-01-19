// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    class CPythonFunctionOverload : IPythonFunctionOverload {
        private readonly CPythonParameterInfo[] _parameters;
        private readonly string _doc, _returnDoc;
        private readonly List<IPythonType> _retType;
        private static readonly CPythonParameterInfo[] EmptyParameters = new CPythonParameterInfo[0];

        internal CPythonFunctionOverload(
            CPythonParameterInfo[] parameters,
            IEnumerable<IPythonType> returnType,
            bool isMethod
        ) {
            _parameters = EmptyParameters;

            if (parameters != null) {
                if (isMethod) {
                    if (parameters.Length > 1) {
                        _parameters = parameters.Skip(1).ToArray();
                    }
                } else {
                    _parameters = parameters;
                }
            }

            _retType = returnType.ToList();
        }

        public CPythonFunctionOverload(
            ITypeDatabaseReader typeDb,
            CPythonFunction function,
            Dictionary<string, object> argInfo,
            bool isMethod
        ) {
            _parameters = EmptyParameters;

            if (argInfo != null) {
                object args;
                object[] argList;
                if (argInfo.TryGetValue("args", out args) && (argList = args as object[]) != null) {
                    if ((isMethod && argList.Length > 1) || (!isMethod && argList.Length > 0)) {
                        _parameters = argList.Skip(isMethod ? 1 : 0)
                            .OfType<Dictionary<string, object>>()
                            .Select(arg => new CPythonParameterInfo(typeDb, arg))
                            .ToArray();
                    }
                }

                object docObj;
                if (argInfo.TryGetValue("doc", out docObj)) {
                    _doc = docObj as string;
                }
                if (string.IsNullOrEmpty(_doc)) {
                    _doc = function.Documentation;
                }

                if (argInfo.TryGetValue("return_doc", out docObj)) {
                    _returnDoc = docObj as string;
                }

                object retType;
                if (argInfo.TryGetValue("ret_type", out retType)) {
                    _retType = new List<IPythonType>();
                    typeDb.LookupType(retType, value => _retType.Add(value));
                }
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

        public IList<IPythonType> ReturnType {
            get { return _retType; }
        }

        #endregion
    }
}
