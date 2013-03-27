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

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonParameterInfo : IParameterInfo {
        private readonly string _name, _doc, _defaultValue;
        private readonly bool _isSplat, _isKeywordSplat;
        private List<IPythonType> _type;

        public CPythonParameterInfo(ITypeDatabaseReader typeDb, Dictionary<string, object> parameterTable) {
            if (parameterTable != null) {
                object value;

                if (parameterTable.TryGetValue("type", out value)) {
                    _type = new List<IPythonType>();
                    typeDb.LookupType(value, (type, fromInstanceDb) => _type.Add(type));
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
