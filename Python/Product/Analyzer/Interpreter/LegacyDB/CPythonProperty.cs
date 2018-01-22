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

using System;
using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    class CPythonProperty : IBuiltinProperty, ILocatedMember {
        private readonly string _doc;
        private IPythonType _type;
        private readonly CPythonModule _declaringModule;
        private readonly bool _hasLocation;
        private readonly int _line, _column;
        
        public CPythonProperty(ITypeDatabaseReader typeDb, Dictionary<string, object> valueDict, IMemberContainer container) {
            _declaringModule = CPythonModule.GetDeclaringModuleFromContainer(container);

            object value;
            if (valueDict.TryGetValue("doc", out value)) {
                _doc = value as string;
            }

            object type;
            if (!valueDict.TryGetValue("type", out type) || type == null) {
                type = new[] { null, "object" };
            }

            _hasLocation = PythonTypeDatabase.TryGetLocation(valueDict, ref _line, ref _column);

            typeDb.LookupType(type, typeValue => _type = typeValue);
        }

        #region IBuiltinProperty Members

        public IPythonType Type {
            get { return _type; }
        }

        public bool IsStatic {
            get { return false; }
        }

        public string Documentation {
            get { return _doc; }
        }

        public string Description {
            get {
                if (Type == null) {
                    return "property of unknown type";
                } else {
                    return "property of type " + Type.Name;
                }
            }
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Property; }
        }

        #endregion

        #region ILocatedMember Members

        public IEnumerable<LocationInfo> Locations {
            get {
                if (_hasLocation) {
                    yield return new LocationInfo(_declaringModule.FilePath, _declaringModule.DocumentUri, _line, _column);
                }
            }
        }

        #endregion
    }
}
