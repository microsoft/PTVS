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
using System.IO;

namespace Microsoft.PythonTools.Interpreter.Default {
    class TypeDatabase {
        private readonly Dictionary<string, CPythonModule> _modules = new Dictionary<string, CPythonModule>();
        private readonly List<Action> _fixups = new List<Action>();
        private readonly string _dbDir;
        private readonly Dictionary<CPythonType, CPythonConstant> _constants = new Dictionary<CPythonType, CPythonConstant>();
        private CPythonModule _builtinModule;

        public TypeDatabase(string databaseDirectory, bool is3x = false) {
            _dbDir = databaseDirectory;
            _modules["__builtin__"] = _builtinModule = new CPythonModule(this, "__builtin__", Path.Combine(databaseDirectory, is3x ? "builtins.idb" : "__builtin__.idb"), true);

            foreach (var file in Directory.GetFiles(databaseDirectory)) {
                if (!file.EndsWith(".idb", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                } else if (String.Equals(Path.GetFileName(file), is3x ? "builtins.idb" : "__builtin__.idb", StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string modName = Path.GetFileNameWithoutExtension(file);
                _modules[modName] = new CPythonModule(this, modName, file, false);
            }
        }

        public string DatabaseDirectory {
            get {
                return _dbDir;
            }
        }

        /// <summary>
        /// Looks up a type and queues a fixup if the type is not yet available.  Receives a delegate
        /// which assigns the value to the appropriate field.
        /// </summary>
        public void LookupType(object type, Action<CPythonType> assign) {
            var value = LookupType(type);

            if (value == null) {
                AddFixup(
                    () => {
                        var delayedType = LookupType(type);
                        if (delayedType == null) {
                            delayedType= BuiltinModule.GetAnyMember("object") as CPythonType;
                        }
                        Debug.Assert(delayedType != null);
                        assign(delayedType);
                    }
                );
            } else {
                assign(value);
            }
        }

        private CPythonType LookupType(object type) {
            if (type != null) {
                object[] typeInfo = (object[])type;
                if (typeInfo.Length == 2) {
                    string modName = typeInfo[0] as string;
                    string typeName = typeInfo[1] as string;

                    if (modName != null) {
                        if (typeName != null) {
                            var module = GetModule(modName);
                            if (module != null) {
                                return module.GetAnyMember(typeName) as CPythonType;
                            }
                        }
                    }
                }
            } else {
                return BuiltinModule.GetAnyMember("object") as CPythonType;
            }
            return null;
        }

        /// <summary>
        /// Adds a custom action which will attempt to resolve a type lookup which failed because the
        /// type was not yet defined.  All fixups are run after the database is loaded so all types
        /// should be available.
        /// </summary>
        private void AddFixup(Action action) {
            _fixups.Add(action);
        }

        /// <summary>
        /// Runs all of the custom fixup actions.
        /// </summary>
        internal void RunFixups() {
            foreach (var fixup in _fixups) {
                fixup();
            }

            _fixups.Clear();
        }

        public IEnumerable<string> GetModuleNames() {
            return _modules.Keys;
        }

        public CPythonModule GetModule(string name) {
            CPythonModule res;
            if (_modules.TryGetValue(name, out res)) {
                return res;
            }
            return null;
        }

        public CPythonModule BuiltinModule {
            get {
                return _builtinModule;
            }
        }

        internal void ReadMember(string memberName, Dictionary<string, object> memberValue, Action<string, IMember> assign, IMemberContainer container) {
            object memberKind;
            object value;
            Dictionary<string, object> valueDict;

            if (memberValue.TryGetValue("value", out value) &&
                (valueDict = (value as Dictionary<string, object>)) != null &&
                memberValue.TryGetValue("kind", out memberKind) && memberKind is string) {
                switch ((string)memberKind) {
                    case "function":
                        assign(memberName, new CPythonFunction(this, memberName, valueDict, container));
                        break;
                    case "func_ref":
                        string funcName;
                        if (valueDict.TryGetValue("func_name", out value) && (funcName = value as string) != null) {
                            var names = funcName.Split('.');
                            CPythonModule mod;
                            if (this._modules.TryGetValue(names[0], out mod)) {
                                if (names.Length == 2) {
                                    var mem = mod.GetAnyMember(names[1]);
                                    if (mem == null) {
                                        AddFixup(() => {
                                            var tmp = mod.GetAnyMember(names[1]);
                                            if (tmp != null) {
                                                assign(memberName, tmp);
                                            }
                                        });
                                    } else {
                                        assign(memberName, mem);
                                    }
                                } else {
                                    LookupType(new object[] { names[0], names[1] }, (type) => {
                                        var mem = type.GetMember(null, names[2]);
                                        if (mem != null) {
                                            assign(memberName, mem);
                                        }
                                    });
                                }
                            }
                        }
                        break;
                    case "method":
                        assign(memberName, new CPythonMethodDescriptor(this, memberName, valueDict, container));
                        break;
                    case "property":
                        assign(memberName, new CPythonProperty(this, valueDict));
                        break;
                    case "data":
                        object typeInfo;
                        if (valueDict.TryGetValue("type", out typeInfo)) {
                            LookupType(typeInfo, (dataType) => {
                                assign(memberName, GetConstant(dataType));
                            });
                        }
                        break;
                    case "type":
                        assign(memberName, MakeType(memberName, valueDict, container));
                        break;
                    case "multiple":
                        object members;
                        object[] memsArray;
                        if (valueDict.TryGetValue("members", out members) && (memsArray = members as object[]) != null) {
                            IMember[] finalMembers = GetMultipleMembers(memberName, container, memsArray);
                            assign(memberName, new CPythonMultipleMembers(finalMembers));
                        }
                        break;
                    case "typeref":
                        object typeName;
                        if (valueDict.TryGetValue("type_name", out typeName)) {
                            LookupType(typeName, (dataType) => {
                                assign(memberName, dataType);
                            });
                        }
                        break;
                    case "moduleref":
                        object modName;
                        if (!valueDict.TryGetValue("module_name", out modName) || !(modName is string)) {
                            throw new InvalidOperationException("Failed to find module name: " + modName);
                        }

                        assign(memberName, GetModule((string)modName));
                        break;
                }
            }
        }

        private IMember[] GetMultipleMembers(string memberName, IMemberContainer container, object[] memsArray) {
            IMember[] finalMembers = new IMember[memsArray.Length];
            for (int i = 0; i < finalMembers.Length; i++) {
                var curMember = memsArray[i] as Dictionary<string, object>;
                var tmp = i;    // close over the current value of i, not the last one...
                if (curMember != null) {
                    ReadMember(memberName, curMember, (name, newMemberValue) => finalMembers[tmp] = newMemberValue, container);
                }
            }
            return finalMembers;
        }

        private CPythonType MakeType(string typeName, Dictionary<string, object> valueDict, IMemberContainer container) {
            BuiltinTypeId typeId = BuiltinTypeId.Unknown;
            if (container == _builtinModule) {
                typeId = GetBuiltinTypeId(typeName);
            }

            return new CPythonType(container, this, typeName, valueDict, typeId);
        }

        private static BuiltinTypeId GetBuiltinTypeId(string typeName) {
            switch (typeName) {
                case "list": return BuiltinTypeId.List;
                case "tuple": return BuiltinTypeId.Tuple;
                case "float": return BuiltinTypeId.Float;
                case "int": return BuiltinTypeId.Int;
                case "complex": return BuiltinTypeId.Complex;
                case "dict": return BuiltinTypeId.Dict;
                case "bool": return BuiltinTypeId.Bool;
                case "generator": return BuiltinTypeId.Generator;
                case "function": return BuiltinTypeId.Function;
                case "set": return BuiltinTypeId.Set;
                case "type": return BuiltinTypeId.Type;
                case "object": return BuiltinTypeId.Object;
                case "str": return BuiltinTypeId.Str;
                case "builtin_function": return BuiltinTypeId.BuiltinFunction;
                case "builtin_method_descriptor": return BuiltinTypeId.BuiltinMethodDescriptor;
                case "NoneType": return BuiltinTypeId.NoneType;
                case "ellipsis": return BuiltinTypeId.Ellipsis;
            }
            return BuiltinTypeId.Unknown;
        }

        internal CPythonConstant GetConstant(CPythonType type) {
            CPythonConstant constant;
            if (!_constants.TryGetValue(type, out constant)) {
                _constants[type] = constant = new CPythonConstant(type);
            }
            return constant;
        }
    }
}
