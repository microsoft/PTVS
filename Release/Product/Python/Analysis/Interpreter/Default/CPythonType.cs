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

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonType : IPythonType {
        private readonly string _typeName, _doc;
        private readonly bool _includeInModule;
        private readonly BuiltinTypeId _typeId;
        private readonly CPythonModule _module;
        private readonly bool _isBuiltin;
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();

        public CPythonType(IMemberContainer parent, PythonTypeDatabase typeDb, string typeName, Dictionary<string, object> typeTable, BuiltinTypeId typeId) {
            Debug.Assert(parent is CPythonType || parent is CPythonModule);

            _typeName = typeName;
            _typeId = typeId;
            _module = GetDeclaringModule(parent);

            object value;
            if (typeTable.TryGetValue("is_hidden", out value)) {
                _includeInModule = !Convert.ToBoolean(value);
            } else {
                _includeInModule = true;
            }

            if (typeTable.TryGetValue("doc", out value)) {
                _doc = value as string;
            }

            if (typeTable.TryGetValue("builtin", out value)) {
                _isBuiltin = Convert.ToBoolean(value);
            } else {
                _isBuiltin = true;
            }

            object membersData;
            if (typeTable.TryGetValue("members", out membersData)) {
                var membersTable = membersData as Dictionary<string, object>;
                if (membersTable != null) {
                    LoadMembers(typeDb, membersTable);
                }
            }
        }

        private CPythonModule GetDeclaringModule(IMemberContainer parent) {
            return  parent as CPythonModule ?? (CPythonModule)((CPythonType)parent).DeclaringModule;
        }

        private void LoadMembers(PythonTypeDatabase typeDb, Dictionary<string, object> membersTable) {
            foreach (var memberEntry in membersTable) {
                var memberName = memberEntry.Key;
                var memberValue = memberEntry.Value as Dictionary<string, object>;

                if (memberValue != null) {
                    typeDb.ReadMember(memberName, memberValue, StoreMember, this);
                }
            }
        }

        private void StoreMember(string memberName, IMember value) {
            _members[memberName] = value;
        }

        public bool IncludeInModule {
            get {
                return _includeInModule;
            }
        }

        #region IPythonType Members

        public IMember GetMember(IModuleContext context, string name) {
            IMember res;
            if (_members.TryGetValue(name, out res)) {
                return res;
            }
            return null;
        }

        public IPythonFunction GetConstructors() {
            IMember member;
            if (_members.TryGetValue("__new__", out member) || _members.TryGetValue("__init__", out member)) {
                return member as IPythonFunction;
            }
            return null;
        }

        public string Name {
            get {
                if (TypeId != BuiltinTypeId.Unknown) {
                    return _module.TypeDb.GetBuiltinTypeName(TypeId);
                }
                return _typeName; 
            }
        }

        public string Documentation {
            get { return _doc ?? ""; }
        }

        public BuiltinTypeId TypeId {
            get {
                return _typeId;
            }
        }

        public IPythonModule DeclaringModule {
            get {
                return _module;
            }
        }

        public bool IsBuiltin {
            get {
                return _isBuiltin;
            }
        }

        #endregion

        #region IMemberContainer Members

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            return _members.Keys;
        }

        #endregion

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Class; }
        }

        #endregion

        public override string ToString() {
            return String.Format("CPythonType('{0}')", Name);
        }
    }
}
