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
    class IronPythonConstant : PythonObject, IPythonConstant
    {
        private IPythonType _type;
        private PythonMemberType _memType;

        public IronPythonConstant(IronPythonInterpreter interpreter, ObjectIdentityHandle value)
            : base(interpreter, value)
        {
        }

        public override PythonMemberType MemberType
        {
            get
            {
                if (_memType == PythonMemberType.Unknown)
                {
                    var ri = RemoteInterpreter;
                    if (!Value.IsNull && ri != null && ri.IsEnumValue(Value))
                    {
                        _memType = PythonMemberType.EnumInstance;
                    }
                    else
                    {
                        _memType = PythonMemberType.Constant;
                    }
                }
                return _memType;
            }
        }

        public IPythonType Type
        {
            get
            {
                if (_type == null)
                {
                    if (Value.IsNull)
                    {
                        _type = Interpreter.GetBuiltinType(BuiltinTypeId.NoneType);
                    }
                    else
                    {
                        var ri = RemoteInterpreter;
                        _type = ri != null ? Interpreter.GetTypeFromType(ri.GetObjectPythonType(Value)) : null;
                    }
                }
                return _type;
            }
        }
    }
}
