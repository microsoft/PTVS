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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    class CPythonMultipleMembers : IPythonMultipleMembers, ILocatedMember {
        private readonly List<IMember> _members;
        
        private List<object> _memberNames;
        private IMember[] _memberObjects;

        public CPythonMultipleMembers(IMemberContainer container, ITypeDatabaseReader typeDb, string name, IEnumerable<object> members) {
            _members = new List<IMember>();
            _memberNames = members.OfType<Dictionary<string, object>>().ToList<object>();
            _memberObjects = new IMember[_memberNames.Count];

            // Copy the count because _memberNames may be cleared before the
            // loop finishes executing.
            int count = _memberNames.Count;
            for (int i = 0; i < count; ++i) {
                var capturedI = i;
                typeDb.ReadMember(name, (Dictionary<string, object>)_memberNames[i], (_, member) => AddType(capturedI, member), container);
            }
        }

        private void AddType(int index, IMember member) {
            if (_memberObjects == null || _memberObjects[index] != null) {
                // We may end up being called multiple times for the same member
                // or being called after we've added all of our members.
                return;
            }

            _memberObjects[index] = member;
            if (_memberObjects.All(m => m != null)) {
                _members.AddRange(_memberObjects.Where(m => m != this));
                _memberNames = null;
                _memberObjects = null;
            }
        }

        internal void AssignTypes(ITypeDatabaseReader typeDb, Action<IPythonType> assign) {
            var names = _memberNames;
            var objects = _memberObjects;

            if (names == null || objects == null) {
                foreach (var member in _members.OfType<IPythonType>()) {
                    assign(member);
                }
            } else {
                for (int i = 0; i < names.Count; ++i) {
                    IPythonType type;
                    if ((type = objects[i] as IPythonType) != null) {
                        assign(type);
                    } else if (objects[i] == null) {
                        typeDb.LookupType(names[i], assign);
                    }
                }
            }
        }

        #region IPythonMultipleMembers Members

        public IList<IMember> Members {
            get {
                return _members;
            }
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Multiple; }
        }

        #endregion

        #region ILocatedMember Members

        public IEnumerable<LocationInfo> Locations {
            get {
                foreach (var member in _members) {
                    ILocatedMember locatedMember = member as ILocatedMember;
                    if (locatedMember != null) {
                        foreach (var location in locatedMember.Locations) {
                            yield return location;
                        }
                    }
                }
            }
        }

        #endregion
    }
}
