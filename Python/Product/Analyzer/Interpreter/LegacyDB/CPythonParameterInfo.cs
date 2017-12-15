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

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    class CPythonParameterInfo : IParameterInfo {
        private readonly string _name, _doc, _defaultValue;
        private readonly bool _isSplat, _isKeywordSplat;
        private List<IPythonType> _type;

        public CPythonParameterInfo(ITypeDatabaseReader typeDb, Dictionary<string, object> parameterTable) {
            if (parameterTable != null) {
                object value;

                if (parameterTable.TryGetValue("type", out value)) {
                    _type = new List<IPythonType>();
                    typeDb.LookupType(value, type => _type.Add(type));
                }
                
                if (parameterTable.TryGetValue("name", out value)) {
                    _name = value as string;
                }

                if (parameterTable.TryGetValue("doc", out value)) {
                    _doc = value as string;
                }

                if (parameterTable.TryGetValue("default_value", out value)) {
                    _defaultValue = value as string;
                }
                
                if (parameterTable.TryGetValue("arg_format", out value)) {
                    switch (value as string) {
                        case "*": _isSplat = true; break;
                        case "**": _isKeywordSplat = true; break;
                    }

                }
            }
        }

        #region IParameterInfo Members

        public string Name {
            get { return _name; }
        }

        public IList<IPythonType> ParameterTypes {
            get { return _type; }
        }

        public string Documentation {
            get { return _doc; }
        }

        public bool IsParamArray {
            get { return _isSplat; }
        }

        public bool IsKeywordDict {
            get { return _isKeywordSplat; }
        }

        public string DefaultValue {
            get { return _defaultValue; }
        }

        #endregion
    }
}
