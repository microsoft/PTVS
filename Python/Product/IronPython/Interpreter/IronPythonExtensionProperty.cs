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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.IronPythonTools.Interpreter
{
    class IronPythonExtensionProperty : PythonObject, IBuiltinProperty
    {
        private IPythonType _propertyType;

        public IronPythonExtensionProperty(IronPythonInterpreter interpreter, ObjectIdentityHandle property)
            : base(interpreter, property)
        {
        }

        #region IBuiltinProperty Members

        public IPythonType Type
        {
            get
            {
                if (_propertyType == null)
                {
                    var ri = RemoteInterpreter;
                    _propertyType = ri != null ? Interpreter.GetTypeFromType(ri.GetExtensionPropertyType(Value)) : null;
                }
                return _propertyType;
            }
        }

        public bool IsStatic
        {
            get
            {
                return false;
            }
        }

        public string Documentation
        {
            get
            {
                var ri = RemoteInterpreter;
                return ri != null ? ri.GetExtensionPropertyDocumentation(Value) : string.Empty;
            }
        }

        public string Description
        {
            get { return Documentation; }
        }

        public override PythonMemberType MemberType
        {
            get { return PythonMemberType.Property; }
        }

        #endregion
    }
}
