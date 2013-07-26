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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Default {
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
                Debug.Assert(_members != null, "Cannot retrieve members until loading has completed");
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
