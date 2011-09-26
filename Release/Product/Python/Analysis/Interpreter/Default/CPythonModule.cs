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
using System.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Interpreter.Default {
    class CPythonModule : IPythonModule, IProjectEntry, ILocatedMember {
        private readonly string _modName;
        private readonly string _dbFile;
        private readonly PythonTypeDatabase _typeDb;
        private readonly bool _isBuiltin;
        internal readonly Dictionary<string, IMember> _members = new Dictionary<string, IMember>();
        private Dictionary<object, object> _properties;
        internal Dictionary<string, IMember> _hiddenMembers;
        private string _docString, _filename;
        private object[] _children;        
        private bool _loaded;
        [ThreadStatic] private static int _loadDepth;

        public CPythonModule(PythonTypeDatabase typeDb, string moduleName, string databaseFilename, bool isBuiltin) {
            _modName = moduleName;
            _dbFile = databaseFilename;
            _typeDb = typeDb;
            _isBuiltin = isBuiltin;
        }

        internal void EnsureLoaded() {
            if (!_loaded) {
                // mark as loading now (before it completes), if we have circular references we'll fix them up after loading completes.
                _loaded = true;

                _loadDepth++;
                using (var stream = new FileStream(_dbFile, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    Dictionary<string, object> contents = null;
                    try {
                        contents = (Dictionary<string, object>)Unpickle.Load(stream);
                    } catch (InvalidOperationException) {
                        // Bug 511 - http://pytools.codeplex.com/workitem/511
                        // Ignore a corrupt database file.
                    }

                    if (contents != null) {
                        LoadModule(contents);
                    }
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

            object filename;
            if (data.TryGetValue("filename", out filename)) {
                _filename = filename as string;
            }

            object children;
            if (data.TryGetValue("children", out children)) {
                _children = children as object[];
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

        internal PythonTypeDatabase TypeDb {
            get {
                return _typeDb;
            }
        }

        #region IPythonModule Members

        public IEnumerable<string> GetChildrenModules() {
            if (_children != null) {
                foreach (var child in _children) {
                    yield return (string)child;
                }
            }
        }

        public string Name {
            get { return _modName; }
        }

        public void Imported(IModuleContext context) {
        }

        #endregion

        #region IMemberContainer Members

        public IMember GetMember(IModuleContext context, string name) {
            if (!_loaded) {
                // avoid deserializing all of the member list if we're just checking if
                // a member exists.
                if (_members.Count > 0 || File.Exists(_dbFile + ".$memlist")) {
                    if (_members.Count == 0) {
                        // populate members dict w/ list of members
                        foreach (var line in File.ReadLines(_dbFile + ".$memlist")) {
                            _members[line] = null;
                        }
                    }

                    if (!_members.ContainsKey(name)) {
                        // the member doesn't exist, no need to load all of the data
                        return null;
                    }
                }

                // the member exists, or we don't have a $memlist file, read everything.
                EnsureLoaded();
            }

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

        #region IMember Members

        public PythonMemberType MemberType {
            get { return PythonMemberType.Module; }
        }

        #endregion

        #region IProjectEntry Members

        public bool IsAnalyzed {
            get { return true; }
        }

        public int AnalysisVersion {
            get { return 1; }
        }

        public string FilePath {
            get { return _filename; }
        }

        public string GetLine(int lineNo) {
            lineNo--; // line is 1 based
            string[] lines = File.ReadAllLines(_filename);
            if (lineNo < lines.Length) {
                return lines[lineNo];
            }
            return null;
        }

        public Dictionary<object, object> Properties {
            get {
                if (_properties == null) {
                    Interlocked.CompareExchange(ref _properties, new Dictionary<object, object>(), null);
                }
                return _properties;
            }
        }

        #endregion

        #region IAnalyzable Members

        public void Analyze() {
        }

        #endregion

        #region ILocatedMember Members

        public LocationInfo Location {
            get { return new LocationInfo(this, 1, 1); }
        }

        #endregion

        internal static CPythonModule GetDeclaringModuleFromContainer(IMemberContainer declaringType) {
            return (declaringType as CPythonModule) ?? (CPythonModule)((CPythonType)declaringType).DeclaringModule;
        }
    }
}
