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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Interpreter.LegacyDB {
    /// <summary>
    /// Cached state that's shared between multiple PythonTypeDatabase instances.
    /// </summary>
    class SharedDatabaseState : ITypeDatabaseReader {
        private readonly ConcurrentDictionary<string, IPythonModule> _modules = new ConcurrentDictionary<string, IPythonModule>();
        private readonly object _moduleLoadLock = new object();
#if DEBUG
        private Thread _moduleLoadLockThread;
#endif
        private readonly List<IPythonModule> _modulesLoading = new List<IPythonModule>();
        private List<Action> _fixups;
        private List<Action<IPythonType>> _objectTypeFixups = new List<Action<IPythonType>>();
        private readonly Dictionary<string, List<Action<IMember>>> _moduleFixups = new Dictionary<string, List<Action<IMember>>>();
        private string _dbDir;
        private readonly Dictionary<IPythonType, CPythonConstant> _constants = new Dictionary<IPythonType, CPythonConstant>();
        private readonly Dictionary<string, IPythonType> _sequenceTypes = new Dictionary<string, IPythonType>();
        private readonly bool _isDefaultDb;
        private readonly Version _langVersion;
        private readonly List<WeakReference> _corruptListeners = new List<WeakReference>();
        private IBuiltinPythonModule _builtinModule;
        private IPythonType _objectType;
        internal readonly SharedDatabaseState _inner;

        private readonly string _builtinName;

        public SharedDatabaseState(Version languageVersion) {
            _langVersion = languageVersion;
            _builtinName = BuiltinTypeId.Unknown.GetModuleName(_langVersion);
        }

        public SharedDatabaseState(Version languageVersion, string databaseDirectory)
            : this(languageVersion, databaseDirectory, false) { }

        internal SharedDatabaseState(Version languageVersion, string databaseDirectory, bool defaultDatabase)
            : this(languageVersion) {
            _dbDir = databaseDirectory;
            _isDefaultDb = defaultDatabase;

            if (!string.IsNullOrEmpty(databaseDirectory)) {
                LoadDatabase(databaseDirectory);
            }
        }

        internal SharedDatabaseState(SharedDatabaseState inner) {
            _inner = inner;
            _langVersion = _inner.LanguageVersion;
            if (_inner.BuiltinModule != null) {
                _builtinName = _inner.BuiltinModule.Name;
            } else {
                _builtinName = BuiltinTypeId.Unknown.GetModuleName(_langVersion);
            }
        }

        /// <summary>
        /// Gets the Python language version associated with this database.
        /// </summary>
        public Version LanguageVersion {
            get { return _langVersion; }
        }

        internal void LoadDatabase(string databaseDirectory) {
            var addedModules = new Dictionary<string, IMember>();

            if (_dbDir == null) {
                _dbDir = databaseDirectory;
            }

            if (_builtinModule == null) {
                _builtinModule = MakeBuiltinModule(databaseDirectory);
            }
            _modules[_builtinName] = _builtinModule;
            if (_isDefaultDb && _langVersion.Major == 3) {
                _modules[BuiltinTypeId.Unknown.GetModuleName(PythonLanguageVersion.V27)] = _builtinModule;
            }

            foreach (var file in Directory.GetFiles(databaseDirectory)) {
                if (!file.EndsWith(".idb", StringComparison.OrdinalIgnoreCase) || file.IndexOf('$') != -1) {
                    continue;
                } else if (String.Equals(Path.GetFileNameWithoutExtension(file), _builtinName, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                } else if (_isDefaultDb && String.Equals(Path.GetFileNameWithoutExtension(file), BuiltinTypeId.Unknown.GetModuleName(PythonLanguageVersion.V27), StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string modName = Path.GetFileNameWithoutExtension(file);
                if (_isDefaultDb && _langVersion.Major == 3) {
                    // aliases for 3.x when using the default completion DB
                    switch (modName) {
                        case "cPickle": modName = "_pickle"; break;
                        case "thread": modName = "_thread"; break;
                    }
                }
                addedModules[modName] = _modules[modName] = new CPythonModule(this, modName, file, false);
            }

            foreach (var keyValue in addedModules) {
                List<Action<IMember>> fixups;
                if (_moduleFixups.TryGetValue(keyValue.Key, out fixups)) {
                    _moduleFixups.Remove(keyValue.Key);
                    foreach (var fixup in fixups) {
                        fixup(keyValue.Value);
                    }
                }
            }
        }

        private CPythonBuiltinModule MakeBuiltinModule(string databaseDirectory) {
            string filename = Path.Combine(databaseDirectory, _builtinName + ".idb");
            if (_langVersion.Major == 3 && !File.Exists(filename)) {
                // Python 3.x the module is builtins, but we may have __builtin__.idb if
                // we're using the default completion DB that we install w/ PTVS.
                filename = Path.Combine(databaseDirectory, "__builtin__.idb");
            }

            return new CPythonBuiltinModule(this, _builtinName, filename, true);
        }

        public IPythonModule GetModule(string name) {
            IPythonModule res;
            if (_modules.TryGetValue(name, out res)) {
                return res;
            }
            
            if (_isDefaultDb && _langVersion.Major == 3) {
                // aliases for 3.x when using the default completion DB
                switch (name) {
                    case "cPickle": return GetModule("_pickle");
                    case "thread": return GetModule("_thread");
                }
            }

            if (name == BuiltinTypeId.Unknown.GetModuleName(PythonLanguageVersion.V27) ||
                name == BuiltinTypeId.Unknown.GetModuleName(PythonLanguageVersion.V35)) {
                // Handle both names for builtins if the correct one was not
                // found above.
                var mod = BuiltinModule;
                if (mod != null) {
                    return mod;
                }
            }

            if (_inner != null && (res = _inner.GetModule(name)) != null) {
                return res;
            }

            return null;
        }

        private void AddObjectTypeFixup(Action<IPythonType> assign) {
            var obj = ObjectType;
            if (obj != null) {
                assign(obj);
                return;
            }

            var fixups = _objectTypeFixups;
            if (fixups != null) {
                lock (fixups) {
                    // Ensure it was not changed while waiting for the lock
                    if (ReferenceEquals(_objectTypeFixups, fixups)) {
                        // Mutate the list under the lock - later we will read
                        // from the list after clearing _objectTypeFixups under
                        // the same lock. If there were a race, we would have
                        // failed the comparison above.
                        fixups.Add(assign);
                        return;
                    }
                }

                // _objectTypeFixups changed, and it only changes to null, so we
                // should call assign directly
                Debug.Assert(_objectTypeFixups == null, "_objectTypeFixups was changed to something other than null");
                obj = ObjectType;
                if (obj != null) {
                    assign(obj);
                    return;
                }
            } 

            throw new InvalidOperationException("Cannot find builtin type 'object'");
        }

        private IPythonType RunObjectTypeFixups() {
            var objectType = Volatile.Read(ref _objectType);
            if (objectType == null) {
                var newObjectType = BuiltinModule.GetAnyMember(GetBuiltinTypeName(BuiltinTypeId.Object)) as IPythonType;

                if (newObjectType == null) {
                    // No type available, so don't do fixups
                    return null;
                }

                // Either set _objectType to newObjectType, or replace our local
                // objectType with whatever got there first
                objectType = Interlocked.CompareExchange(ref _objectType, newObjectType, null) ?? newObjectType;
            }

            var fixups = _objectTypeFixups;
            if (fixups != null) {
                lock (fixups) {
                    if (!ReferenceEquals(_objectTypeFixups, fixups)) {
                        // _objectTypeFixups changed while we waited to lock
                        // This means someone else now owns the list and will
                        // run the fixups, so we can just return the new object
                        // type.
                        return objectType;
                    }
                    _objectTypeFixups = null;
                }

                // At this point, nobody else has a reference to the list. If
                // they did (see AddObjectTypeFixup), they just entered the lock
                // now and discovered that _objectTypeFixups has changed and
                // will not use the reference they have.
                foreach (var assign in fixups) {
                    assign(objectType);
                }
            }

            return objectType;
        }

        private IPythonType ObjectType {
            get {
                var objectType = Volatile.Read(ref _objectType);
                if (objectType == null) {
                    objectType = RunObjectTypeFixups();
                }
                return objectType;
            }
        }

        private IPythonModule GetModuleOrClass(string modName, ref string typeName) {
            // Some scraped libraries (PySide) put the class name in __module__:
            //
            //      >>> import PySide.QtCore
            //      >>> PySide.QtCore.Qt.ApplicationAttribute.__module__
            //      'PySide.QtCore.Qt'
            //      >>> isinstance(PySide.QtCore, types.ModuleType)
            //      True
            //      >>> isinstance(PySide.QtCore.Qt, types.ModuleType)
            //      False
            //
            // As a result, we cannot resolve what they claim is their module.
            // When we cannot find a module and it has a dot in its name, we now
            // try and find the parent module and then treat the last part of
            // the module name as the first part of the type name.
            var module = GetModule(modName);
            if (module != null) {
                return module;
            }
            int lastDot = modName.LastIndexOf('.');
            if (lastDot > 0 && lastDot < modName.Length - 1) {
                var modParentName = modName.Remove(lastDot);
                module = GetModule(modParentName);
                if (module != null) {
                    typeName = modName.Substring(lastDot + 1) + "." + typeName;
                    return module;
                }
            }
            return null;
        }

        /// <summary>
        /// Looks up a type and queues a fixup if the type is not yet available.
        /// Receives a delegate which assigns the value to the appropriate field.
        /// </summary>
        public void LookupType(object typeRefOrList, Action<IPythonType> assign) {
            var typeRef = typeRefOrList as object[];
            if (typeRef != null && typeRef.Length >= 2) {
                string modName = null, typeName = null;
                List<object> indexTypes = null;
                IPythonType res = null;

                modName = typeRef[0] as string;
                typeName = typeRef[1] as string;
                if (typeRef.Length > 2) {
                    indexTypes = typeRef[2] as List<object>;
                }

                if (typeName == null) {
                    Debug.Assert(modName == null, "moduleref should not be passed to LookupType");
                    AddObjectTypeFixup(assign);
                    return;
                } else {
                    IPythonModule module;
                    if (modName == null) {
                        res = BuiltinModule.GetAnyMember(typeName) as IPythonType;
                        if (res != null) {
                            assign(res);
                        } else {
                            AddObjectTypeFixup(assign);
                        }
                    } else {
                        string alternateTypeName = typeName;
                        module = GetModuleOrClass(modName, ref alternateTypeName);
                        if (module == null) {
                            AddFixup(() => {
                                // Fixup 1: Module was not found.
                                var mod2 = GetModuleOrClass(modName, ref alternateTypeName);
                                if (mod2 != null) {
                                    AssignMemberFromModule(mod2, alternateTypeName, null, indexTypes, assign, true);
                                }
                            });
                            return;
                        }
                        AssignMemberFromModule(module, alternateTypeName, null, indexTypes, assign, true);
                    }
                }
                return;
            }

            var multiple = typeRefOrList as List<object>;
            if (multiple != null) {
                foreach (var typeInfo in multiple) {
                    LookupType(typeInfo, assign);
                }
            }
        }

        private void AssignMemberFromModule(IPythonModule module, string typeName, IMember memb, List<object> indexTypes, Action<IPythonType> assign, bool addFixups) {
            if (memb == null) {
                IBuiltinPythonModule builtin;
                if ((builtin = module as IBuiltinPythonModule) != null) {
                    memb = builtin.GetAnyMember(typeName);
                } else {
                    memb = module.GetMember(null, typeName);
                }
            }

            IPythonType type;
            CPythonMultipleMembers mm;
            if (memb == null) {
                if (addFixups) {
                    AddFixup(() => {
                        // Fixup 2: Type was not found in module
                        AssignMemberFromModule(module, typeName, null, indexTypes, assign, false);
                    });
                    return;
                } else {
                    // TODO: Maybe skip this to reduce noise in loaded database
                    AddObjectTypeFixup(assign);
                    return;
                }
            } else if ((type = memb as IPythonType) != null) {
                if (indexTypes != null) {
                    assign(new CPythonSequenceType(type, this, indexTypes));
                } else {
                    assign(type);
                }
            } else if ((mm = memb as CPythonMultipleMembers) != null) {
                mm.AssignTypes(this, assign);
            }
        }

        public string GetBuiltinTypeName(BuiltinTypeId id) => id.GetTypeName(_langVersion);

        public bool BeginModuleLoad(IPythonModule module, int millisecondsTimeout) {
#if DEBUG
            var stopwatch = new Stopwatch();
            stopwatch.Start();
#endif
            bool needUnlock = false;
            try {
                Monitor.TryEnter(_moduleLoadLock, millisecondsTimeout, ref needUnlock);
                if (!needUnlock) {
                    return false;
                }
#if DEBUG
                stopwatch.Stop();
                if (stopwatch.ElapsedMilliseconds > 100) {
                    string heldByMessage = "";
                    if (_moduleLoadLockThread != null) {
                        heldByMessage = string.Format(" (held by {0}:{1})",
                            _moduleLoadLockThread.Name, _moduleLoadLockThread.ManagedThreadId
                        );
                    }
                    Console.WriteLine(
                        "Waited {0}ms for module loader lock on thread {1}:{2}{3}",
                        stopwatch.ElapsedMilliseconds,
                        Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId,
                        heldByMessage
                    );
                }
                _moduleLoadLockThread = Thread.CurrentThread;
#endif
                _modulesLoading.Add(module);
                needUnlock = false;
            } finally {
                if (needUnlock) {
                    Monitor.Exit(_moduleLoadLock);
                }
            }
            return true;
        }

        public void EndModuleLoad(IPythonModule module) {
            try {
                bool wasRemoved = _modulesLoading.Remove(module);
                Debug.Assert(wasRemoved);
                if (_modulesLoading.Count == 0) {
                    RunFixups();
                }
            } finally {
                Monitor.Exit(_moduleLoadLock);
            }
        }

        /// <summary>
        /// Adds a custom action which will attempt to resolve a type lookup
        /// which failed because the type was not yet defined. Fixups are run
        /// immediately before the module lock is released, which should ensure
        /// that all required modules are, or can be, loaded.
        /// </summary>
        private void AddFixup(Action action) {
            Debug.Assert(action != null);
            if (action != null) {
                var fixups = _fixups;
                if (fixups == null) {
                    var newFixups = new List<Action>();
                    fixups = Interlocked.CompareExchange(ref _fixups, newFixups, null) ?? newFixups;
                }
                lock (fixups) {
                    fixups.Add(action);
                }
            }
        }

        private void RunFixups() {

            var fixups = Interlocked.Exchange(ref _fixups, null);
            if (fixups != null) {
                List<Action> fixupsCopy;
                lock (fixups) {
                    fixupsCopy = fixups.ToList();
                }
                foreach (var fixup in fixupsCopy) {
                    Debug.Assert(fixup != null);
                    if (fixup != null) {
                        fixup();
                    }
                }
            }

            RunObjectTypeFixups();
        }

        private static object[] MakeTypeRef(IEnumerable<string> nameParts) {
            string typeName = null;
            var moduleName = new StringBuilder();
            foreach (var name in nameParts) {
                if (!string.IsNullOrEmpty(typeName)) {
                    moduleName.Append(typeName);
                    moduleName.Append(".");
                }
                typeName = name;
            }

            if (moduleName.Length > 1) {
                moduleName.Length -= 1;
            }

            return new object[] {
                moduleName.ToString(),
                typeName
            };
        }

        private void ResolveMemberRef(
            string[] names,
            string memberName,
            Action<string, IMember> assign,
            int fixupsLevel = -1
        ) {
            IMemberContainer container;
            IMember member;
            var module = GetModule(names[0]);
            member = module;

            if (module == null) {
                if (fixupsLevel < 0) {
                    // Fixup 1: Could not resolve module.
                    AddFixup(() => ResolveMemberRef(names, memberName, assign, 0));
                }
                return;
            }

            for (int i = 1; i < names.Length - 1; ++i) {
                if ((module = member as IPythonModule) != null) {
                    member = GetModule(module.Name + "." + names[i]) ?? module.GetMember(null, names[i]);
                } else if ((container = member as IMemberContainer) != null) {
                    member = container.GetMember(null, names[i]);
                }
                if (member == null) {
                    if (fixupsLevel < i) {
                        // Fixup 2: Could not resolve member
                        AddFixup(() => ResolveMemberRef(names, memberName, assign, i));
                    }
                    return;
                }
            }

            if ((container = member as IMemberContainer) != null) {
                int i = names.Length - 1;
                member = container.GetMember(null, names[i]);
                if (member != null) {
                    assign(memberName, member);
                } else if (fixupsLevel < i) {
                    // Fixup 3: Could not resolve final member
                    AddFixup(() => ResolveMemberRef(names, memberName, assign, i));
                } else {
                    // Complete failure to resolve - assign `object`
                    AddObjectTypeFixup(t => assign(memberName, t));
                }
            }
        }

        public void ReadMember(
            string memberName,
            Dictionary<string, object> memberValue,
            Action<string, IMember> assign,
            IMemberContainer container
        ) {
            object memberKind;
            object value;
            Dictionary<string, object> valueDict;
            List<object> valueList;
            object[] valueArray;

            if (memberValue.TryGetValue("value", out value) &&
                memberValue.TryGetValue("kind", out memberKind) && memberKind is string) {
                if ((valueDict = (value as Dictionary<string, object>)) != null) {
                    switch ((string)memberKind) {
                        case "function":
                            if (CheckVersion(memberValue)) {
                                assign(memberName, new CPythonFunction(this, memberName, valueDict, container));
                            }
                            break;
                        case "funcref":
                            string funcName;
                            if (valueDict.TryGetValue("func_name", out value) && (funcName = value as string) != null) {
                                ResolveMemberRef(funcName.Split('.'), memberName, assign);
                            }
                            break;
                        case "method":
                            if (CheckVersion(memberValue)) {
                                assign(memberName, new CPythonMethodDescriptor(this, memberName, valueDict, container));
                            }
                            break;
                        case "property":
                            if (CheckVersion(memberValue)) {
                                assign(memberName, new CPythonProperty(this, valueDict, container));
                            }
                            break;
                        case "data":
                            object typeInfo;
                            if (valueDict.TryGetValue("type", out typeInfo) && CheckVersion(memberValue)) {
                                LookupType(
                                    typeInfo,
                                    dataType => {
                                        if (!(dataType is IPythonSequenceType)) {
                                            assign(memberName, GetConstant(dataType));
                                        } else {
                                            assign(memberName, dataType);
                                        }
                                    }
                                );
                            }
                            break;
                        case "type":
                            if (CheckVersion(memberValue)) {
                                assign(memberName, MakeType(memberName, valueDict, container));
                            }
                            break;
                        case "multiple":
                            object members;
                            object[] memsArray;
                            if (valueDict.TryGetValue("members", out members) && (memsArray = members as object[]) != null) {
                                assign(memberName, new CPythonMultipleMembers(container, this, memberName, memsArray));
                            }
                            break;
                        default:
                            Debug.Fail("Unexpected member kind: " + (string)memberKind);
                            break;
                    }
                } else if ((valueList = value as List<object>) != null) {
                    switch ((string)memberKind) {
                        case "typeref":
                            if (CheckVersion(memberValue)) {
                                LookupType(valueList, dataType => assign(memberName, dataType));
                            }
                            break;
                        default:
                            Debug.Fail("Unexpected member kind: " + (string)memberKind);
                            break;
                    }
                } else if ((valueArray = value as object[]) != null) {
                    switch ((string)memberKind) {
                        case "moduleref":
                            string modName;
                            if (valueArray.Length >= 1 && !string.IsNullOrEmpty(modName = valueArray[0] as string)) {
                                var module = GetModule(modName);
                                if (module == null) {
                                    List<Action<IMember>> fixups;
                                    if (!_moduleFixups.TryGetValue(modName, out fixups)) {
                                        _moduleFixups[modName] = fixups = new List<Action<IMember>>();
                                    }
                                    fixups.Add(m => assign(memberName, m));
                                } else {
                                    assign(memberName, module);
                                }
                            }
                            break;
                        default:
                            Debug.Fail("Unexpected member kind: " + (string)memberKind);
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Raises the notification that the database is corrupt, called when reading
        /// a module definition fails.
        /// </summary>
        public void OnDatabaseCorrupt() {
            WeakReference[] listeners;
            lock (_corruptListeners) {
                listeners = _corruptListeners.ToArray();
            }
            for (int i = 0; i < listeners.Length; i++) {
                var target = listeners[i].Target;
                if (target != null) {
                    ((PythonTypeDatabase)target).OnDatabaseCorrupt();
                }
            }
        }

        /// <summary>
        /// Sets up a weak reference for notification of when the shared database
        /// has become corrupted.  Doesn't keep the listening database alive.
        /// </summary>
        public void ListenForCorruptDatabase(PythonTypeDatabase db) {
            lock (_corruptListeners) {
                for (int i = 0; i < _corruptListeners.Count; i++) {
                    var target = _corruptListeners[i].Target;
                    if (target == null) {
                        _corruptListeners[i].Target = db;
                        return;
                    }
                }

                _corruptListeners.Add(new WeakReference(db));
            }
        }

        private bool CheckVersion(Dictionary<string, object> valueDict) {
            object version;
            return !valueDict.TryGetValue("version", out version) || VersionApplies(version);
        }

        /// <summary>
        /// Checks to see if this member is applicable to our current language version for the shared DB.
        /// 
        /// Version formats are specified in the format:
        /// 
        /// version_check|version_checks
        /// 
        /// version_check:
        ///     greater_equals_check
        ///     less_equals_check
        ///     equals_check
        ///     
        /// greater_equals_check:   &gt;=version
        /// less_equals_check:      &lt;=version
        /// equals_check            ==version
        /// 
        /// version:    major_version.minor_version
        /// major_version: number
        /// minor_version: number
        /// 
        /// version_checks:  version_check(;version_check)+
        /// 
        /// For the member to be included all checks must pass.
        /// </summary>
        internal bool VersionApplies(object version) {
            if (_langVersion == null || version == null) {
                return true;
            }

            string strVer = version as string;
            if (strVer != null) {
                if (strVer.IndexOf(';') != -1) {
                    foreach (var curVersion in strVer.Split(';')) {
                        if (!OneVersionApplies(curVersion)) {
                            return false;
                        }
                    }
                    return true;
                } else {
                    return OneVersionApplies(strVer);
                }
            }
            return false;
        }

        private bool OneVersionApplies(string strVer) {
            Version specifiedVer;
            if (strVer.StartsWith(">=")) {
                if (Version.TryParse(strVer.Substring(2), out specifiedVer) && _langVersion >= specifiedVer) {
                    return true;
                }
            } else if (strVer.StartsWith("<=")) {
                if (Version.TryParse(strVer.Substring(2), out specifiedVer) && _langVersion <= specifiedVer) {
                    return true;
                }
            } else if (strVer.StartsWith("==")) {
                if (Version.TryParse(strVer.Substring(2), out specifiedVer) && _langVersion == specifiedVer) {
                    return true;
                }
            }
            return false;
        }

        private CPythonType MakeType(string typeName, Dictionary<string, object> valueDict, IMemberContainer container) {
            BuiltinTypeId typeId = BuiltinTypeId.Unknown;
            if (container is IBuiltinPythonModule) {
                typeId = GetBuiltinTypeId(typeName);
            }

            return new CPythonType(container, this, typeName, valueDict, typeId);
        }

        private BuiltinTypeId GetBuiltinTypeId(string typeName) {
            // Never return BuiltinTypeId.Str, StrIterator, or any value where
            // IsVirtualId() is true from this function.
            switch (typeName) {
                case "list": return BuiltinTypeId.List;
                case "tuple": return BuiltinTypeId.Tuple;
                case "float": return BuiltinTypeId.Float;
                case "int": return BuiltinTypeId.Int;
                case "complex": return BuiltinTypeId.Complex;
                case "dict": return BuiltinTypeId.Dict;
                case "bool": return BuiltinTypeId.Bool;
                case "generator": return BuiltinTypeId.Generator;
                case "ModuleType": return BuiltinTypeId.Module;
                case "function": return BuiltinTypeId.Function;
                case "set": return BuiltinTypeId.Set;
                case "type": return BuiltinTypeId.Type;
                case "object": return BuiltinTypeId.Object;
                case "long": return BuiltinTypeId.Long;
                case "str": return _langVersion.Major == 3 ? BuiltinTypeId.Unicode : BuiltinTypeId.Bytes;
                case "unicode": return BuiltinTypeId.Unicode;
                case "bytes": return BuiltinTypeId.Bytes;
                case "builtin_function": return BuiltinTypeId.BuiltinFunction;
                case "builtin_method_descriptor": return BuiltinTypeId.BuiltinMethodDescriptor;
                case "NoneType": return BuiltinTypeId.NoneType;
                case "ellipsis": return BuiltinTypeId.Ellipsis;
                case "dict_keys": return BuiltinTypeId.DictKeys;
                case "dict_values": return BuiltinTypeId.DictValues;
                case "dict_items": return BuiltinTypeId.DictItems;
                case "list_iterator": return BuiltinTypeId.ListIterator;
                case "tuple_iterator": return BuiltinTypeId.TupleIterator;
                case "set_iterator": return BuiltinTypeId.SetIterator;
                case "str_iterator": return _langVersion.Major == 3 ? BuiltinTypeId.UnicodeIterator : BuiltinTypeId.BytesIterator;
                case "unicode_iterator": return BuiltinTypeId.UnicodeIterator;
                case "bytes_iterator": return BuiltinTypeId.BytesIterator;
                case "callable_iterator": return BuiltinTypeId.CallableIterator;
                case "property": return BuiltinTypeId.Property;
                case "classmethod": return BuiltinTypeId.ClassMethod;
                case "staticmethod": return BuiltinTypeId.StaticMethod;
                case "frozenset": return BuiltinTypeId.FrozenSet;
            }
            return BuiltinTypeId.Unknown;
        }

        internal CPythonConstant GetConstant(IPythonType type) {
            CPythonConstant constant;
            if (_constants.TryGetValue(type, out constant) ||
                (constant = _inner?.GetConstant(type)) != null) {
                return constant;
            }
            _constants[type] = constant = new CPythonConstant(type);
            return constant;
        }

        public IBuiltinPythonModule BuiltinModule {
            get {
                if (_builtinModule == null && _inner != null) {
                    return _inner.BuiltinModule;
                }
                return _builtinModule;
            }
            set {
                Modules[value.Name] = value;
                _builtinModule = value;
            }
        }

        /// <summary>
        /// Returns all module names in this and any inner instances. Use
        /// <see cref="Modules"/> to find only those modules belonging to this
        /// instance.
        /// </summary>
        public IEnumerable<string> GetModuleNames() {
            for (var db = this; db != null; db = db._inner) {
                foreach (var name in db._modules.Keys) {
                    yield return name;
                }
            }
        }

        /// <summary>
        /// Returns all modules associated with this instance. Unlike
        /// <see cref="GetModuleNames"/>, this does not recurse.
        /// </summary>
        public ConcurrentDictionary<string, IPythonModule> Modules {
            get {
                return _modules;
            }
        }

        public string DatabaseDirectory {
            get {
                return _dbDir;
            }
        }
    }
}
