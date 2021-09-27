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
    class IronPythonField : PythonObject, IBuiltinProperty
    {
        private IPythonType _fieldType;
        private bool? _isStatic;

        public IronPythonField(IronPythonInterpreter interpreter, ObjectIdentityHandle field)
            : base(interpreter, field)
        {
        }

        #region IBuiltinProperty Members

        public IPythonType Type
        {
            get
            {
                if (_fieldType == null)
                {
                    var ri = RemoteInterpreter;
                    _fieldType = ri != null ? (IPythonType)Interpreter.MakeObject(ri.GetFieldType(Value)) : null;
                }
                return _fieldType;
            }
        }

        public bool IsStatic
        {
            get
            {
                if (_isStatic == null)
                {
                    var ri = RemoteInterpreter;
                    _isStatic = ri != null ? ri.IsFieldStatic(Value) : false;
                }

                return _isStatic.Value;
            }
        }

        public string Documentation
        {
            get
            {
                var ri = RemoteInterpreter;
                return ri != null ? ri.GetFieldDocumentation(Value) : string.Empty;
            }
        }

        public string Description
        {
            get { return Documentation; }
        }

        public override PythonMemberType MemberType
        {
            get { return PythonMemberType.Field; }
        }

        #endregion
    }
}
