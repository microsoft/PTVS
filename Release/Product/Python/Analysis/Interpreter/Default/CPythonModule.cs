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
using System.IO;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonModule : IPythonModule {
        private readonly string _modName;
        private readonly string _dbFile;
        private readonly TypeDatabase _typeDb;
        private readonly bool _isBuiltin;
        private readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();
        private Dictionary<string, IMember> _hiddenMembers;
        private string _docString;
        private bool _loaded;
        [ThreadStatic] private static int _loadDepth;

        public CPythonModule(TypeDatabase typeDb, string moduleName, string filename, bool isBuiltin) {
            _modName = moduleName;
            _dbFile = filename;
            _typeDb = typeDb;
            _isBuiltin = isBuiltin;
        }

        private void EnsureLoaded() {
            if (!_loaded) {
                // mark as loading now (before it completes), if we have circular references we'll fix them up after loading completes.
                _loaded = true;

                _loadDepth++;
                using (var stream = new FileStream(_dbFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var contents = (Dictionary<string, object>)Unpickle.Load(stream);

                    LoadModule((Dictionary<string, object>)contents);
                }
                _loadDepth--;

                // don't run fixups until we've processed all of the inter-dependent modules.
                if (_loadDepth == 0) {
                    _typeDb.RunFixups();
                }
            }
        }

        private void LoadModule(Dictionary<string, object> data) {
            object membersValue;
            if (data.TryGetValue("members", out membersValue)) {
                var dataVal = membersValue as Dictionary<string, object>;
                if (dataVal != null) {
                    LoadMembers(dataVal);
                }
            }

            object doc;
            if (data.TryGetValue("doc", out doc)) {
                _docString = doc as string;
            }
        }

        private void LoadMembers(Dictionary<string, object> membersTable) {
            foreach (var dataInfo in membersTable) {
                var memberName = dataInfo.Key;
                var memberTable = dataInfo.Value as Dictionary<string, object>;

                if (memberTable != null) {
                    _typeDb.ReadMember(memberName, memberTable, StoreMember, this);
                }
            }
        }

        private void StoreMember(string memberName, IMember value) {
            CPythonType type = value as CPythonType;
            if (type != null && !type.IncludeInModule) {
                if (_hiddenMembers == null) {
                    _hiddenMembers = new Dictionary<string, IMember>();
                }
                _hiddenMembers[memberName] = type;
            } else {
                _members[memberName] = value;
            }
        }

        internal TypeDatabase TypeDb {
            get {
                return _typeDb;
            }
        }
        
        #region IPythonModule Members

        public string Name {
            get { return _modName; }
        }

        public void Imported(IModuleContext context) {
        }

        #endregion

        #region IMemberContainer Members

        public IMember GetMember(IModuleContext context, string name) {
            EnsureLoaded();

            IMember res;
            if (_members.TryGetValue(name, out res)) {
                return res;
            }
            return null;
        }

        public IEnumerable<string> GetMemberNames(IModuleContext moduleContext) {
            EnsureLoaded();

            return _members.Keys;
        }

        #endregion

        public IMember GetAnyMember(string name) {
            EnsureLoaded();

            IMember res;
            if (_members.TryGetValue(name, out res) || (_hiddenMembers != null && _hiddenMembers.TryGetValue(name, out res))) {
                return res;
            }
            return null;
        }

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Module; }
        }

        #endregion
    }
}
