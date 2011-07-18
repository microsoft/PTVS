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
using System.Reflection;
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Scripting;
using Microsoft.Scripting.Generation;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonParameterInfo : IParameterInfo {
        private IronPythonInterpreter _interpreter;
        private ParameterInfo _parameterInfo;

        public IronPythonParameterInfo(IronPythonInterpreter _interpreter, ParameterInfo parameterInfo) {
            this._interpreter = _interpreter;
            this._parameterInfo = parameterInfo;
        }

        #region IParameterInfo Members

        public IPythonType ParameterType {
            get { return _interpreter.GetTypeFromType(_parameterInfo.ParameterType); }
        }

        // FIXME
        public string Documentation {
            get { return "";}
        }

        public string Name {
            get { return _parameterInfo.Name; }
        }

        public bool IsParamArray {
            get { return _parameterInfo.IsDefined(typeof(ParamArrayAttribute), false); }
        }

        public bool IsKeywordDict {
            get { return _parameterInfo.IsDefined(typeof(ParamDictionaryAttribute), false); }
        }

        public string DefaultValue {
            get {
                if (_parameterInfo.DefaultValue != DBNull.Value && !(_parameterInfo.DefaultValue is Missing)) {
                    return PythonOps.Repr(DefaultContext.Default, _parameterInfo.DefaultValue);
                } else if (_parameterInfo.IsOptional) {
                    object missing = CompilerHelpers.GetMissingValue(_parameterInfo.ParameterType);
                    if (missing != Missing.Value) {
                        return PythonOps.Repr(DefaultContext.Default, missing);
                    } else {
                        return "";
                    }
                }
                return null;
            }
        }


        #endregion
    }
}
