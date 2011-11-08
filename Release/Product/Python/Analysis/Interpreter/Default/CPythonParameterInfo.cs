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
        private readonly object _typeObj;
        private IPythonType _type;

        public CPythonParameterInfo(ITypeDatabaseReader typeDb, Dictionary<string, object> parameterTable) {
            if (parameterTable != null) {
                object typeObj;
                
                if (parameterTable.TryGetValue("type", out typeObj)) {
                    typeDb.LookupType(typeObj, (value, fromInstanceDb) => _type = value);
                }
                _typeObj = typeObj;
                
                object nameObj;
                if (parameterTable.TryGetValue("name", out nameObj)) {
                    _name = nameObj as string;
                }

                object docObj;
                if (parameterTable.TryGetValue("doc", out docObj)) {
                    _doc = docObj as string;
                }

                object defaultValueObj;
                if (parameterTable.TryGetValue("default_value", out defaultValueObj)) {
                    _defaultValue = defaultValueObj as string;
                }

                object argFormatObj;
                if (parameterTable.TryGetValue("arg_format", out argFormatObj)) {
                    switch (argFormatObj as string) {
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

        public IPythonType ParameterType {
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
