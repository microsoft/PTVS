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
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonType : IPythonType, ILocatedMember {
        private readonly string _typeName, _doc;
        private readonly bool _includeInModule;
        private readonly BuiltinTypeId _typeId;
        private readonly CPythonModule _module;
        private readonly List<IPythonType> _bases;
        private readonly List<CPythonType> _mro;
        private readonly bool _isBuiltin;
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();
        private readonly bool _hasLocation;
        private readonly int _line, _column;

        public CPythonType(IMemberContainer parent, ITypeDatabaseReader typeDb, string typeName, Dictionary<string, object> typeTable, BuiltinTypeId typeId) {
            Debug.Assert(parent is CPythonType || parent is CPythonModule);
            Debug.Assert(!typeId.IsVirtualId());

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

            if (typeTable.TryGetValue("bases", out value)) {
                var basesList = (List<object>)value;
                if (basesList != null) {
                    _bases = new List<IPythonType>();
                    foreach (var baseType in basesList) {
                        typeDb.LookupType(baseType, StoreBase);
                    }
                }
            }

            if (typeTable.TryGetValue("mro", out value)) {
                var mroList = (List<object>)value;
                if (mroList != null) {
                    _mro = new List<CPythonType>();
                    foreach (var mroType in mroList) {
                        typeDb.LookupType(mroType, StoreMro);
                    }
                }
            }

            if (typeTable.TryGetValue("members", out value)) {
                var membersTable = (Dictionary<string, object>)value;
                if (membersTable != null) {
                    LoadMembers(typeDb, membersTable);
                }
            }

            _hasLocation = PythonTypeDatabase.TryGetLocation(typeTable, ref _line, ref _column);
        }

        private CPythonModule GetDeclaringModule(IMemberContainer parent) {
            return parent as CPythonModule ?? (CPythonModule)((CPythonType)parent).DeclaringModule;
        }

        private void StoreBase(IPythonType type, bool isInstance) {
            if (type != null && _bases != null) {
                _bases.Add(type);
            }
        }

        private void StoreMro(IPythonType type, bool isInstance) {
            var cpt = type as CPythonType;
            if (cpt != null && _mro != null) {
                _mro.Add(cpt);
            }
        }

        private void LoadMembers(ITypeDatabaseReader typeDb, Dictionary<string, object> membersTable) {
            foreach (var memberEntry in membersTable) {
                var memberName = memberEntry.Key;
                var memberValue = memberEntry.Value as Dictionary<string, object>;

                if (memberValue != null) {
                    _members[memberName] = null;
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
            if (_mro != null) {
                foreach (var mroType in _mro) {
                    if (mroType._members.TryGetValue(name, out res)) {
                        return res;
                    }
                }
            } else if (_bases != null) {
                foreach (var baseType in _bases) {
                    res = baseType.GetMember(context, name);
                    if (res != null) {
                        return res;
                    }
                }
            }
            return null;
        }

        public IPythonFunction GetConstructors() {
            IMember member;
            if (_members.TryGetValue("__new__", out member) && member is IPythonFunction && ((IPythonFunction)member).Overloads.Count > 0) {
                return member as IPythonFunction;
            } else if (TypeId != BuiltinTypeId.Object && _members.TryGetValue("__init__", out member)) {
                if (member is CPythonMethodDescriptor) {
                    return ((CPythonMethodDescriptor)member).Function;
                }
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
            var seen = new HashSet<string>();
            foreach (var key in _members.Keys) {
                if (seen.Add(key)) {
                    yield return key;
                }
            }
            if (_mro != null) {
                foreach (var type in _mro) {
                    foreach (var key in type._members.Keys) {
                        if (seen.Add(key)) {
                            yield return key;
                        }
                    }
                }
            } else if (_bases != null) {
                foreach (var type in _bases) {
                    foreach (var key in type.GetMemberNames(moduleContext)) {
                        if (seen.Add(key)) {
                            yield return key;
                        }
                    }
                }
            }
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

        #region ILocatedMember Members

        public IEnumerable<LocationInfo> Locations {
            get {
                if (_hasLocation) {
                    yield return new LocationInfo(_module, _line, _column);
                }
            }
        }

        #endregion
    }
}
