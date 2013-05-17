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
using IronPython.Runtime;
using IronPython.Runtime.Operations;
using Microsoft.PythonTools.Interpreter;
using Microsoft.Scripting;
using Microsoft.Scripting.Generation;

namespace Microsoft.IronPythonTools.Interpreter {
    class IronPythonParameterInfo : IParameterInfo {
        private IronPythonInterpreter _interpreter;
        private ObjectIdentityHandle _parameterInfo;
        private string _name;
        private ParameterKind _paramKind;
        private IPythonType[] _paramType;
        private string _defaultValue;
        private static readonly string _noDefaultValue = "<No Default Value>";  // sentinel value to mark when an object doesn't have a default value

        public IronPythonParameterInfo(IronPythonInterpreter interpreter, ObjectIdentityHandle parameterInfo) {
            _interpreter = interpreter;
            _parameterInfo = parameterInfo;
        }

        #region IParameterInfo Members

        public IList<IPythonType> ParameterTypes {
            get {
                if (_paramType == null) {
                    _paramType = new [] { _interpreter.GetTypeFromType(_interpreter.Remote.GetParameterPythonType(_parameterInfo)) };
                }
                return _paramType;
            }
        }

        // FIXME
        public string Documentation {
            get { return ""; }
        }

        public string Name {
            get {
                if (_name == null) {
                    _name = _interpreter.Remote.GetParameterName(_parameterInfo);
                }
                return _name;
            }
        }

        public bool IsParamArray {
            get {
                if (_paramKind == ParameterKind.Unknown) {
                    _paramKind = _interpreter.Remote.GetParameterKind(_parameterInfo);
                }
                return _paramKind == ParameterKind.List;
            }
        }

        public bool IsKeywordDict {
            get {
                if (_paramKind == ParameterKind.Unknown) {
                    _paramKind = _interpreter.Remote.GetParameterKind(_parameterInfo);
                }
                return _paramKind == ParameterKind.Dictionary;
            }
        }

        public string DefaultValue {
            get {
                if (_defaultValue == null) {
                    _defaultValue = _interpreter.Remote.GetParameterDefaultValue(_parameterInfo) ?? _noDefaultValue;
                }

                if (Object.ReferenceEquals(_defaultValue, _noDefaultValue)) {
                    return null;
                }

                return _noDefaultValue;
            }
        }


        #endregion
    }
}
