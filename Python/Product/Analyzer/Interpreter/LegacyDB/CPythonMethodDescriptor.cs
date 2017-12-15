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
