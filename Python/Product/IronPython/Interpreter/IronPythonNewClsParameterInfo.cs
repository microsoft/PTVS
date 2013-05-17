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
    class IronPythonNewClsParameterInfo : IParameterInfo {
        private readonly IronPythonType _declaringType;

        public IronPythonNewClsParameterInfo(IronPythonType declaringType) {
            _declaringType = declaringType;
        }

        #region IParameterInfo Members

        public IList<IPythonType> ParameterTypes {
            get {
                return new[] { _declaringType };
            }
        }

        public string Documentation {
            get { return ""; }
        }

        public string Name {
            get {
                return "cls";
            }
        }

        public bool IsParamArray {
            get {
                return false;
            }
        }

        public bool IsKeywordDict {
            get {
                return false;
            }
        }

        public string DefaultValue {
            get {
                return null;
            }
        }

        #endregion
    }
}
