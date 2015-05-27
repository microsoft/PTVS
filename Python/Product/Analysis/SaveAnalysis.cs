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
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    class SaveAnalysis {
        private List<string> _errors = new List<string>();
        private Dictionary<AnalysisValue, string> _classNames = new Dictionary<AnalysisValue, string>();
        private List<AnalysisValue> _path = new List<AnalysisValue>();
        private Dictionary<string, Dictionary<string, object>> _typeNames = new Dictionary<string, Dictionary<string, object>>();
        private Dictionary<string, string> _MemoizedStrings = new Dictionary<string, string>();
        private Dictionary<string, object[]> _moduleNames = new Dictionary<string, object[]>();
        private static readonly List<object> _EmptyMro = new List<object>();
        private PythonAnalyzer _curAnalyzer;
        private ModuleInfo _curModule;

        public void Save(PythonAnalyzer state, string outDir) {
            _curAnalyzer = state;
            foreach (var modKeyValue in state.Modules) {
                string name = modKeyValue.Key;

                ModuleInfo moduleInfo;
                if ((moduleInfo = modKeyValue.Value.Module as ModuleInfo) != null) {
                    _curModule = moduleInfo;
                    var info = SerializeModule(moduleInfo);
                    WriteModule(outDir, name, info, moduleInfo.Scope.AllVariables.Keys());
                }
            }

            foreach (var error in _errors) {
                Console.WriteLine(error);
            }
        }

        private void WriteModule(string outDir, string name, Dictionary<string, object> info, IEnumerable<string> globals) {
            for (int i = 0; i < 10; i++) {
                try {
                    using (var writer = new FileStream(Path.Combine(outDir, name + ".idb"), FileMode.Create, FileAccess.ReadWrite)) {
                        new Pickler(writer).Dump(info);
                    }
                    break;
                } catch (IOException ex) {
                    // race with a reader, retry... http://pytools.codeplex.com/workitem/570
                    Console.WriteLine("Could not access {0} {1}", name + ".idb", ex);
                    Thread.Sleep(1000);
                }
            }

            for (int i = 0; i < 10; i++) {
                try {
                    using (var writer = new StreamWriter(new FileStream(Path.Combine(outDir, name + ".idb.$memlist"), FileMode.Create, FileAccess.ReadWrite))) {
                        object membersObj;
                        Dictionary<string, object> members;
                        if (!info.TryGetValue("members", out membersObj) ||
                            (members = membersObj as Dictionary<string, object>) == null) {
                            if (globals != null) {
                                members = globals.ToDictionary(k => k, _ => (object)null);
                            } else {
                                members = new Dictionary<string, object>();
                            }
                        }
                        foreach (var memberName in members.Keys) {
                            writer.WriteLine(memberName);
                        }
                    }
                    break;
                } catch (IOException ex) {
                    // race with a reader, retry... http://pytools.codeplex.com/workitem/570
                    Console.WriteLine("Could not access {0} {1}", name + ".idb.$memlist", ex);
                    Thread.Sleep(1000);
                }
            }
        }

        private Dictionary<string, object> SerializeModule(ModuleInfo moduleInfo) {
            var children = GenerateChildModules(moduleInfo);
            return new Dictionary<string, object>() {
                { "members", GenerateMembers(moduleInfo, children) },
                { "doc", MemoizeString(moduleInfo.Documentation) },
                { "children", children },
                { "filename", moduleInfo.ProjectEntry.FilePath }
            };
        }

        private List<object> GenerateChildModules(ModuleInfo moduleInfo) {
            var res = new HashSet<object>(moduleInfo.GetChildrenPackages(null).Select(kv => kv.Key));

            // Add any child built-in modules as well. This will include the modules that are part of the package,
            // but which do not participate in analysis (e.g. native modules).
            foreach (var child in
                from moduleName in moduleInfo.ProjectEntry.ProjectState.Interpreter.GetModuleNames()
                let lastDot = moduleName.LastIndexOf('.')
                where lastDot >= 0
                let packageName = moduleName.Substring(0, lastDot)
                where packageName == moduleInfo.Name
                select moduleName.Substring(lastDot + 1)
            ) {
                // Only include the child if it is actually importable. As a
                // side-effect, we really import the child, which means that
                // GenerateMembers will not exclude it.
                ModuleReference modTableRef;
                if (moduleInfo.ProjectEntry.ProjectState.Modules.TryImport(moduleInfo.Name + "." + child, out modTableRef)) {
                    res.Add(child);
                }
            }

            return res.ToList();
        }

        private Dictionary<string, object> GenerateMembers(ModuleInfo moduleInfo, IEnumerable<object> children) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            foreach (var keyValue in moduleInfo.Scope.AllVariables) {
                if (keyValue.Value.IsEphemeral) {
                    // Never got a value, so leave it out.
                    continue;
                }
                res[keyValue.Key] = GenerateMember(keyValue.Value, moduleInfo);
            }
            foreach (var child in children.OfType<string>()) {
                object modRef = null;
                ModuleReference modTableRef;
                if (moduleInfo.ProjectEntry.ProjectState.Modules.TryGetImportedModule(moduleInfo.Name + "." + child, out modTableRef) &&
                    modTableRef != null &&
                    modTableRef.Module != null
                ) {
                    MultipleMemberInfo mmi;
                    if (modTableRef.Module is ModuleInfo || modTableRef.Module is BuiltinModule) {
                        modRef = new Dictionary<string, object> {
                            { "kind", "moduleref" },
                            { "value", GetModuleName(moduleInfo.Name + "." + child) }
                        };
                    } else if ((mmi = modTableRef.Module as MultipleMemberInfo) != null) {
                        modRef = new Dictionary<string, object> {
                            { "kind", "multiple" },
                            { "value", new Dictionary<string, object> {
                                {
                                    "members",
                                    mmi.Members
                                        .Select(m => m.Name)
                                        .Where(n => !string.IsNullOrEmpty(n))
                                        .Select(GetModuleName)
                                        .ToArray<object>()
                                }
                            } }
                        };
                    }
                }

                object existing;
                if (res.TryGetValue(child, out existing) && IsValidMember(existing)) {
                    var members1 = GetMultipleMembersOrDefault(existing) ?? new object[] { existing };
                    var members2 = GetMultipleMembersOrDefault(modRef) ?? new object[] { modRef };
                    var members = members1.Concat(members2).Distinct(ModuleReferenceComparer.Instance).ToArray();
                    
                    if (members.Length > 1) {
                        res[child] = new Dictionary<string, object> {
                            { "kind", "multiple" },
                            { "value", new Dictionary<string, object> { { "members", members } } }
                        };
                    } else if (members.Length == 1) {
                        res[child] = members[0];
                    }
                } else {
                    res[child] = modRef;
                }
            }
            return res;
        }

        private class ModuleReferenceComparer : IEqualityComparer<object> {
            public static readonly IEqualityComparer<object> Instance = new ModuleReferenceComparer();

            private ModuleReferenceComparer() { }

            public new bool Equals(object x, object y) {
                if (object.ReferenceEquals(x, y)) {
                    return true;
                }
                
                var asDict1 = x as Dictionary<string, object>;
                var asDict2 = y as Dictionary<string, object>;
                if (asDict1 == null || asDict2 == null) {
                    return false;
                }

                object obj;
                if (!asDict1.TryGetValue("kind", out obj) || (obj as string) != "moduleref") {
                    return false;
                }
                if (!asDict2.TryGetValue("kind", out obj) || (obj as string) != "moduleref") {
                    return false;
                }

                object[] modref1, modref2;
                if (!asDict1.TryGetValue("value", out obj) || (modref1 = obj as object[]) == null) {
                    return false;
                }
                if (!asDict2.TryGetValue("value", out obj) || (modref2 = obj as object[]) == null) {
                    return false;
                }

                return modref1.SequenceEqual(modref2);
            }

            public int GetHashCode(object obj) {
                var asDict = obj as Dictionary<string, object>;
                if (asDict == null) {
                    return 0;
                }

                object o;
                if (!asDict.TryGetValue("kind", out o) || (o as string) != "moduleref") {
                    return 0;
                }

                object[] modref;
                if (!asDict.TryGetValue("value", out o) || (modref = o as object[]) == null) {
                    return 0;
                }

                return modref.Aggregate(48527, (a, n) => a ^ (n != null ? n.GetHashCode() : 0));
            }
        }

        private static bool IsValidMember(object info) {
            var asDict = info as Dictionary<string, object>;
            if (asDict == null) {
                return false;
            }

            object obj;
            string kind;
            if (!asDict.TryGetValue("kind", out obj) || (kind = obj as string) == null) {
                return false;
            }

            if (!asDict.TryGetValue("value", out obj)) {
                return false;
            }

            if (kind == "data" && (asDict = obj as Dictionary<string, object>) != null) {
                // { 'data': { 'type': None } } is invalid
                return asDict.TryGetValue("type", out obj) && obj != null;
            }

            return true;
        }

        private static object[] GetMultipleMembersOrDefault(object info) {
            var asDict = info as Dictionary<string, object>;
            if (asDict == null) {
                return null;
            }

            object obj;
            string kind;
            if (!asDict.TryGetValue("kind", out obj) || (kind = obj as string) == null || kind != "multiple") {
                return null;
            }

            if (!asDict.TryGetValue("value", out obj) || (asDict = obj as Dictionary<string, object>) == null) {
                return null;
            }

            if (!asDict.TryGetValue("members", out obj)) {
                return null;
            }

            return obj as object[];
        }

        private object GenerateMember(VariableDef variableDef, ModuleInfo declModule, bool isRef = false) {
            Dictionary<string, object> memberEntry = new Dictionary<string, object>() {
                { "kind", GetMemberKind(variableDef, declModule, isRef) },
                { "value", GetMemberValue(variableDef, declModule, isRef) }
            };

            return memberEntry;
        }

        private object GetMemberValues(VariableDef[] variableDefs, ModuleInfo declModule, bool isRef) {
            List<object> res = new List<object>();
            foreach (var variableDef in variableDefs) {
                res.Add(GetMemberValue(variableDef, declModule, isRef));
            }
            return res;
        }

        private object GetMemberValue(VariableDef variableDef, ModuleInfo declModule, bool isRef) {
            return GetMemberValue(variableDef.TypesNoCopy, declModule, isRef);
        }

        private object GetMemberValue(IAnalysisSet types, ModuleInfo declModule, bool isRef) {
            if (types.Count == 1) {
                var type = types.First();
                var res = GetMemberValueInternal(type, declModule, isRef);
                if (res == null) {
                    _errors.Add(String.Format("Cannot save single member: {0}", types.First()));
                }
                return res;
            } else if (types.Count == 0) {
                return new Dictionary<string, object>() {
                    { "type", null }
                };
            } else {
                List<object> res = new List<object>();
                foreach (var type in types) {
                    res.Add(
                        new Dictionary<string, object>() { 
                            { "kind", GetMemberKind(type, declModule, isRef) }, 
                            { "value", GetMemberValueInternal(type, declModule, isRef) }
                        }
                    );
                }
                return new Dictionary<string, object>() {
                    { "members", res.ToArray() }
                };
            }
        }

        private object GetMemberValueInternal(AnalysisValue type, ModuleInfo declModule, bool isRef) {
            SpecializedNamespace specialCallable = type as SpecializedNamespace;
            if (specialCallable != null) {
                if (specialCallable.Original == null) {
                    return null;
                }
                return GetMemberValueInternal(specialCallable.Original, declModule, isRef);
            }

            switch (type.MemberType) {
                case PythonMemberType.Function:
                    FunctionInfo fi = type as FunctionInfo;
                    if (fi != null) {
                        if (fi.DeclaringModule.GetModuleInfo() != declModule) {
                            return GenerateFuncRef(fi);
                        } else {
                            return GenerateFunction(fi);
                        }
                    }

                    BuiltinFunctionInfo bfi = type as BuiltinFunctionInfo;
                    if (bfi != null) {
                        return GenerateFuncRef(bfi);
                    }

                    return "function";
                case PythonMemberType.Method:
                    BoundMethodInfo mi = type as BoundMethodInfo;
                    if (mi != null) {
                        return GenerateMethod(mi);
                    }
                    return "method";
                case PythonMemberType.Property:
                    FunctionInfo prop = type as FunctionInfo;
                    if (prop != null) {
                        return GenerateProperty(prop);
                    }
                    break;
                case PythonMemberType.Class:
                    ClassInfo ci = type as ClassInfo;
                    if (ci != null) {
                        if (isRef || ci.DeclaringModule.GetModuleInfo() != declModule) {
                            // TODO: Save qualified name so that classes defined in classes/function can be resolved
                            return GetTypeRef(ci.DeclaringModule.ModuleName, ci.Name);
                        } else {
                            return GenerateClass(ci, declModule);
                        }
                    }

                    BuiltinClassInfo bci = type as BuiltinClassInfo;
                    if (bci != null) {
                        return GetTypeRef(bci.PythonType.DeclaringModule.Name, bci.PythonType.Name);
                    }
                    return "type";
                case PythonMemberType.Constant:
                    ConstantInfo constantInfo = type as ConstantInfo;
                    if (constantInfo != null) {
                        return GenerateConstant(constantInfo);
                    }
                    break;
                case PythonMemberType.Module:
                    if (type is ModuleInfo) {
                        return GetModuleName(((ModuleInfo)type).Name);
                    } else if (type is BuiltinModule) {
                        return GetModuleName(((BuiltinModule)type).Name);
                    }
                    break;
                case PythonMemberType.Instance:
                    return new Dictionary<string, object> {
                        { "type", GenerateTypeName(type, true) }
                    };
                default:
                    return new Dictionary<string, object>() {
                        { "type", GenerateTypeName(type.PythonType) }
                    };
            }
            return null;
        }

        private object GenerateFuncRef(FunctionInfo fi) {
            string name = ".";
            for (var cd = fi.FunctionDefinition.Parent as ClassDefinition;
                cd != null;
                cd = cd.Parent as ClassDefinition) {
                name = "." + cd.Name + name;
            }
            name = fi.DeclaringModule.ModuleName + name + fi.Name;

            return new Dictionary<string, object>() {
                { "func_name", MemoizeString(name) }
            };
        }

        private object GenerateFuncRef(BuiltinFunctionInfo bfi) {
            string name = bfi.Function.DeclaringModule.Name;
            if (bfi.Function.DeclaringType != null) {
                name += "." + bfi.Function.DeclaringType.Name;
            }
            
            name += "." + bfi.Function.Name;

            return new Dictionary<string, object>() {
                { "func_name", MemoizeString(name) }
            };
        }

        private string GetMemberKind(VariableDef variableDef, ModuleInfo declModule, bool isRef) {
            if (variableDef.Types.Count == 1) {
                return GetMemberKind(variableDef.Types.First(), declModule, isRef);
            } else if (variableDef.Types.Count == 0) {
                // typed to object
                return "data";
            } else {
                return "multiple";
            }
        }

        private static string GetMemberKind(AnalysisValue type, ModuleInfo declModule, bool isRef) {
            SpecializedNamespace specialCallable = type as SpecializedNamespace;
            if (specialCallable != null) {
                if (specialCallable.Original == null) {
                    return "data";
                }
                return GetMemberKind(specialCallable.Original, declModule, isRef);
            }

            switch (type.MemberType) {
                case PythonMemberType.Function:
                    if (type is BuiltinFunctionInfo || type.DeclaringModule != declModule.ProjectEntry) {
                        return "funcref";
                    }
                    return "function";
                case PythonMemberType.Method: return "method";
                case PythonMemberType.Property: return "property";
                case PythonMemberType.Class:
                    if (isRef || type is BuiltinClassInfo || (type is ClassInfo && type.DeclaringModule.GetModuleInfo() != declModule)) {
                        return "typeref";
                    }
                    return "type";
                case PythonMemberType.Module:
                    return "moduleref";
                case PythonMemberType.Instance:
                default:
                    return "data";
            }
        }

        private object GenerateProperty(FunctionInfo prop) {
            return new Dictionary<string, object>() {
                { "doc", MemoizeString(prop.Documentation) },
                { "type", GenerateTypeName(GetFunctionReturnTypes(prop), true) },
                { "location", GenerateLocation(prop.Locations) }
            };
        }

        private static IAnalysisSet GetFunctionReturnTypes(FunctionInfo func) {
            return func.GetReturnValue();
        }

        private Dictionary<string, object> GenerateConstant(ConstantInfo constantInfo) {
            return new Dictionary<string, object>() {
                { "type", GenerateTypeName(constantInfo.PythonType) }
            };
        }

        private Dictionary<string, object> GenerateClass(ClassInfo ci, ModuleInfo declModule) {
            return new Dictionary<string, object>() {
                { "mro", GetClassMro(ci) },
                { "bases", GetClassBases(ci) },
                { "members", GetClassMembers(ci, declModule) },
                { "doc", MemoizeString(ci.Documentation) },
                { "builtin", false },
                { "location", GenerateLocation(ci.Locations) }
            };
        }

        private object GetTypeRef(string moduleName, string className) {
            return new List<object> { GetTypeName(moduleName, className) };
        }

        private object GetTypeName(string moduleName, string className) {
            // memoize types names for a more efficient on disk representation.
            object typeName;
            Dictionary<string, object> typeNames;
            if (!_typeNames.TryGetValue(moduleName, out typeNames)) {
                _typeNames[moduleName] = typeNames = new Dictionary<string, object>();
            }

            if (!typeNames.TryGetValue(className, out typeName)) {
                typeNames[className] = typeName = new object[] { 
                    MemoizeString(moduleName),
                    MemoizeString(className)
                };
            }
            return typeName;
        }

        private object[] GetModuleName(string moduleName) {
            // memoize types names for a more efficient on disk representation.
            object[] name;
            if (!_moduleNames.TryGetValue(moduleName, out name)) {
                _moduleNames[moduleName] = name = new object[] { MemoizeString(moduleName), string.Empty };
            }
            return name;
        }

        private object GetClassMembers(ClassInfo ci, ModuleInfo declModule) {
            Dictionary<string, object> memTable = new Dictionary<string, object>();
            foreach (var keyValue in ci.Scope.AllVariables) {
                if (keyValue.Value.IsEphemeral) {
                    continue;
                }
                memTable[keyValue.Key] = GenerateMember(keyValue.Value, declModule, true);
            }
            if (ci.Instance.InstanceAttributes != null) {
                foreach (var keyValue in ci.Instance.InstanceAttributes) {
                    if (keyValue.Value.IsEphemeral) {
                        continue;
                    }
                    memTable[keyValue.Key] = GenerateMember(keyValue.Value, declModule, true);
                }
            }

            return memTable;
        }

        private List<object> GetClassBases(ClassInfo ci) {
            List<object> res = new List<object>();
            foreach (var baseClassSet in ci.Bases) {
                foreach (var baseClass in baseClassSet) {
                    var typeName = GenerateTypeName(baseClass, true);
                    if (typeName != null) {
                        res.Add(typeName);
                    }
                }
            }
            return res;
        }

        private object GenerateTypeName(IAnalysisSet name, bool isRef) {
            if (name.Count == 0) {
                return null;
            } else if (name.Count == 1) {
                return GenerateTypeName(name.First(), isRef);
            }

            return name.Select(ns => GenerateTypeName(ns, isRef)).Distinct().ToList<object>();
        }

        private object GenerateTypeName(AnalysisValue baseClass, bool isRef) {
            ClassInfo ci = baseClass as ClassInfo;
            if (ci != null) {
                return GetTypeName(((ProjectEntry)ci.DeclaringModule).GetModuleInfo().Name, ci.ClassDefinition.Name);
            }

            BuiltinClassInfo bci = baseClass as BuiltinClassInfo;
            if (bci != null) {
                return GenerateTypeName(bci._type);
            }

            IterableInfo iteri = baseClass as IterableInfo;
            if (iteri != null) {
                return GenerateTypeName(iteri.PythonType, isRef, iteri.IndexTypes);
            }

            InstanceInfo ii = baseClass as InstanceInfo;
            if (ii != null) {
                return GenerateTypeName(ii.ClassInfo, isRef);
            }

            BuiltinInstanceInfo bii = baseClass as BuiltinInstanceInfo;
            if (bii != null) {
                return GenerateTypeName(bii.ClassInfo, isRef);
            }

            return GenerateTypeName(baseClass.PythonType);
        }

        private object GenerateTypeName(IPythonType type) {
            if (type != null) {
                return GetTypeName(type.DeclaringModule.Name, type.Name);
            }
            return null;
        }

        private object GenerateTypeName(IPythonType type, bool isRef, VariableDef[] indexTypes) {
            if (type != null) {
                if (indexTypes == null || indexTypes.Length == 0) {
                    return GetTypeName(type.DeclaringModule.Name, type.Name);
                }

                var moduleName = type.DeclaringModule.Name;
                var className = type.Name + "`" + string.Join(",", indexTypes.Select(t => t.TypesNoCopy.ToString()));
                object typeName;
                Dictionary<string, object> typeNames;
                if (!_typeNames.TryGetValue(moduleName, out typeNames)) {
                    _typeNames[moduleName] = typeNames = new Dictionary<string, object>();
                }

                if (!typeNames.TryGetValue(className, out typeName)) {
                    var mModuleName = MemoizeString(moduleName);
                    var mTypeName = MemoizeString(type.Name);
                    // Create a type without the typename that will be used if
                    // the index types recurse.
                    typeNames[className] = typeName = new object[] { mModuleName, mTypeName };

                    typeNames[className] = typeName = new object[] {
                        mModuleName,
                        mTypeName,
                        indexTypes.Select(vd => GenerateTypeName(vd.TypesNoCopy, isRef)).ToList<object>(),
                    };
                }

                return typeName;
            }
            return null;
        }

        private object GetClassMro(ClassInfo ci) {
            List<object> res = new List<object>();
            foreach (var mroClassSet in ci.Mro) {
                foreach (var mroClass in mroClassSet) {
                    var typeName = GenerateTypeName(mroClass, true);
                    if (typeName != null) {
                        res.Add(typeName);
                    }
                }
            }
            return res;
        }

        private Dictionary<string, object> GenerateFunction(FunctionInfo fi) {
            // TODO: Include inner classes/functions
            return new Dictionary<string, object>() {
                { "doc", fi.Documentation },
                { "builtin", false },
                { "static", fi.IsStatic },
                { "location", GenerateLocation(fi.Locations) },
                { "overloads", GenerateOverloads(fi) }
            };
        }

        private Dictionary<string, object> GenerateMethod(BoundMethodInfo bmi) {
            var res = GenerateFunction(bmi.Function);
            if (bmi.Instance != null && bmi.Instance.TypeId != BuiltinTypeId.NoneType) {
                res["bound"] = true;
            }
            return res;
        }

        private static object[] GenerateLocation(LocationInfo location) {
            return new object[] { location.Line, location.Column };
        }

        private static object[] GenerateLocation(IEnumerable<LocationInfo> locations) {
            // TODO: Support saving and loading multiple locations for a single definition
            Debug.Assert(locations.Count() == 1);
            var location = locations.FirstOrDefault();
            if (location != null) {
                return GenerateLocation(location);
            }
            return new object[0];
        }

        private List<object> GenerateOverloads(FunctionInfo fi) {
            var res = new List<object>();

            // TODO: Store distinct calls as separate overloads
            res.Add(new Dictionary<string, object> {
                { "args", GenerateArgInfo(fi, fi.GetParameterTypes()) },
                { "ret_type", GenerateTypeName(fi.GetReturnValue(), true) }
            });

            return res;
        }

        private object[] GenerateArgInfo(FunctionInfo fi, IAnalysisSet[] parameters) {
            var res = new object[Math.Min(fi.FunctionDefinition.Parameters.Count, parameters.Length)];
            for (int i = 0; i < res.Length; i++) {
                res[i] = GenerateParameter(fi.FunctionDefinition.Parameters[i], parameters[i]);
            }
            return res;
        }

        private string MemoizeString(string input) {
            if (input == null) {
                return null;
            }
            string res;
            if (!_MemoizedStrings.TryGetValue(input, out res)) {
                _MemoizedStrings[input] = res = input;
            }
            return res;
        }

        private object GenerateParameter(Parameter param, IAnalysisSet typeInfo) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            if (param.Kind == ParameterKind.Dictionary) {
                res["arg_format"] = "**";
            } else if (param.Kind == ParameterKind.List) {
                res["arg_format"] = "*";
            }
            res["name"] = MemoizeString(param.Name);
            res["type"] = GenerateTypeName(typeInfo, true);
            var defaultValue = FunctionInfo.GetDefaultValue(_curAnalyzer, param, _curModule.ProjectEntry.Tree);
            if (defaultValue != null) {
                res["default_value"] = defaultValue;
            }
            return res;
        }

    }
}
