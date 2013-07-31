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
    class CPythonMethodDescriptor : IPythonMethodDescriptor {
        private readonly string _name;
        private readonly CPythonFunction _func;
        private readonly bool _isBound;

        public CPythonMethodDescriptor(ITypeDatabaseReader typeDb, string name, Dictionary<string, object> valueDict, IMemberContainer declaringType) {
            _name = name;
            _func = new CPythonFunction(typeDb, name, valueDict, declaringType, isMethod: true);
            object value;
            if (valueDict.TryGetValue("bound", out value)) {
                _isBound = (value as bool?) ?? false;
            }
        }

        #region IBuiltinMethodDescriptor Members

        public IPythonFunction Function {
            get { return _func;  }
        }

        public bool IsBound {
            get { return _isBound; }
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Method; }
        }

        #endregion
    }
}
