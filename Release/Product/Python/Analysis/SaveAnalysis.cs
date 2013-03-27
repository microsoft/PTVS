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
        private Dictionary<Namespace, string> _classNames = new Dictionary<Namespace, string>();
        private List<Namespace> _path = new List<Namespace>();
        private Dictionary<string, Dictionary<string, object>> _typeNames = new Dictionary<string, Dictionary<string, object>>();
        private Dictionary<string, string> _MemoizedStrings = new Dictionary<string, string>();
        private Dictionary<string, Dictionary<string, object>> _moduleNames = new Dictionary<string, Dictionary<string, object>>();
        private static readonly List<object> _EmptyMro = new List<object>();

        public void Save(PythonAnalyzer state, string outDir) {

            foreach (var modKeyValue in state.Modules) {
                string name = modKeyValue.Key;
                var moduleInfo = modKeyValue.Value.Module as ModuleInfo;

                if (moduleInfo != null) {
                    var info = SerializeModule(moduleInfo);
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
                                foreach (var keyValue in moduleInfo.Scope.Variables) {
                                    writer.WriteLine(keyValue.Key);
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
            }

            if (_errors.Count > 0) {
                foreach (var error in _errors) {
                    Console.WriteLine(error);
                }
            }
        }

        private Dictionary<string, object> SerializeModule(ModuleInfo moduleInfo) {
            return new Dictionary<string, object>() {
                { "members", GenerateMembers(moduleInfo) },
                { "doc", MemoizeString(moduleInfo.Documentation) },
                { "children", GenerateChildModules(moduleInfo) },
                { "filename", moduleInfo.ProjectEntry.FilePath }
            };
        }

        private List<object> GenerateChildModules(ModuleInfo moduleInfo) {
            List<object> res = new List<object>();
            foreach (var keyValue in moduleInfo.GetChildrenPackages(null)) {
                res.Add(keyValue.Key);
            }
            return res;
        }

        private Dictionary<string, object> GenerateMembers(ModuleInfo moduleInfo) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            foreach (var keyValue in moduleInfo.Scope.Variables) {
                res[keyValue.Key] = GenerateMember(keyValue.Value, moduleInfo);
            }
            return res;
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

        private object GetMemberValue(INamespaceSet types, ModuleInfo declModule, bool isRef) {
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

        private object GetMemberValueInternal(Namespace type, ModuleInfo declModule, bool isRef) {
            SpecializedNamespace specialCallable = type as SpecializedNamespace;
            if (specialCallable != null) {
                return GetMemberValueInternal(specialCallable.Original, declModule, isRef);
            }

            switch (type.MemberType) {
                case PythonMemberType.Function:
                    FunctionInfo fi = type as FunctionInfo;
                    if (fi != null) {
                        return GenerateFunction(fi);
                    }

                    BuiltinFunctionInfo bfi = type as BuiltinFunctionInfo;
                    if (bfi != null) {
                        return GenerateFunction(bfi);
                    }

                    return "function";
                case PythonMemberType.Method:
                    BoundMethodInfo mi = type as BoundMethodInfo;
                    if (mi != null) {
                        return GenerateFunction(mi.Function);
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
                        if (isRef || ci.DeclaringModule.MyScope != declModule) {
                            return GetTypeName(ci.DeclaringModule.ModuleName, ci.Name);
                        } else {
                            return GenerateClass(ci, declModule);
                        }
                    }

                    BuiltinClassInfo bci = type as BuiltinClassInfo;
                    if (bci != null) {
                        return GetTypeName(bci.PythonType.DeclaringModule.Name, bci.PythonType.Name);
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
                        { "type", GenerateTypeName(type, declModule, true) }
                    };
                default:
                    return new Dictionary<string, object>() {
                        { "type", GenerateTypeName(type.PythonType) }
                    };
            }
            return null;
        }

        private object GenerateFunction(BuiltinFunctionInfo bfi) {
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

        private static string GetMemberKind(Namespace type, ModuleInfo declModule, bool isRef) {
            SpecializedNamespace specialCallable = type as SpecializedNamespace;
            if (specialCallable != null) {
                return GetMemberKind(specialCallable.Original, declModule, isRef);
            }

            switch (type.MemberType) {
                case PythonMemberType.Function:
                    if (type is BuiltinFunctionInfo) {
                        return "func_ref";
                    }
                    return "function";
                case PythonMemberType.Method: return "method";
                case PythonMemberType.Property: return "property";
                case PythonMemberType.Class:
                    if (isRef || type is BuiltinClassInfo || (type is ClassInfo && ((ClassInfo)type).DeclaringModule.MyScope != declModule)) {
                        return "typeref";
                    }
                    return "type";
                case PythonMemberType.Module:
                    return "moduleref";
                case PythonMemberType.Instance:
                default: return "data";
            }
        }

        private object GenerateProperty(FunctionInfo prop) {
            return new Dictionary<string, object>() {
                { "doc", MemoizeString(prop.Documentation) },
                { "type", GenerateTypeName(GetFunctionReturnTypes(prop), prop.DeclaringModule.MyScope, true) },
                { "location", GenerateLocation(prop.Location) }
            };
        }

        private static INamespaceSet GetFunctionReturnTypes(FunctionInfo func) {
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
                { "location", GenerateLocation(ci.Location) }
            };
        }

        private object GetTypeName(string moduleName, string className) {
            // memoize types names for a more efficient on disk representation.
            object typeName;
            Dictionary<string, object> typeNames;
            if (!_typeNames.TryGetValue(moduleName, out typeNames)) {
                _typeNames[moduleName] = typeNames = new Dictionary<string, object>();
            }

            if (!typeNames.TryGetValue(className, out typeName)) {
                typeNames[className] = typeName = new Dictionary<string, object> {
                    { "module_name", MemoizeString(moduleName) },
                    { "type_name", MemoizeString(className) }
                };
            }
            return typeName;
        }

        private Dictionary<string, object> GetModuleName(string moduleName) {
            // memoize types names for a more efficient on disk representation.
            Dictionary<string, object> name;
            if (!_moduleNames.TryGetValue(moduleName, out name)) {
                _moduleNames[moduleName] = name = new Dictionary<string, object>() { { "module_name", MemoizeString(moduleName) } };
            }
            return name;
        }

        private object GetClassMembers(ClassInfo ci, ModuleInfo declModule) {
            Dictionary<string, object> memTable = new Dictionary<string, object>();
            foreach (var keyValue in ci.Scope.Variables) {
                memTable[keyValue.Key] = GenerateMember(keyValue.Value, declModule, true);
            }
            if (ci.Instance.InstanceAttributes != null) {
                foreach (var keyValue in ci.Instance.InstanceAttributes) {
                    memTable[keyValue.Key] = GenerateMember(keyValue.Value, declModule, true);
                }
            }

            return memTable;
        }

        private List<object> GetClassBases(ClassInfo ci) {
            List<object> res = new List<object>();
            foreach (var baseClassSet in ci.Bases) {
                foreach (var baseClass in baseClassSet) {
                    var typeName = GenerateTypeName(baseClass, ci.DeclaringModule.MyScope, true);
                    if (typeName != null) {
                        res.Add(typeName);
                    }
                }
            }
            return res;
        }

        private object GenerateTypeName(INamespaceSet name, ModuleInfo declModule, bool isRef) {
            if (name.Count == 0) {
                return null;
            } else if (name.Count == 1) {
                return GenerateTypeName(name.First(), declModule, isRef);
            }

            return name.Select(ns => GenerateTypeName(ns, declModule, isRef)).ToList<object>();
        }

        private object GenerateTypeName(Namespace baseClass, ModuleInfo declModule, bool isRef) {
            ClassInfo ci = baseClass as ClassInfo;
            if (ci != null) {
                return GetTypeName(ci.DeclaringModule.MyScope.Name, ci.ClassDefinition.Name);
            }

            BuiltinClassInfo bci = baseClass as BuiltinClassInfo;
            if (bci != null) {
                return GenerateTypeName(bci._type);
            }

            IterableInfo iteri = baseClass as IterableInfo;
            if (iteri != null) {
                return GenerateTypeName(iteri.PythonType, declModule, isRef, iteri.IndexTypes);
            }

            InstanceInfo ii = baseClass as InstanceInfo;
            if (ii != null) {
                return GenerateTypeName(ii.ClassInfo, declModule, isRef);
            }

            BuiltinInstanceInfo bii = baseClass as BuiltinInstanceInfo;
            if (bii != null) {
                return GenerateTypeName(bii.ClassInfo, declModule, isRef);
            }

            return GenerateTypeName(baseClass.PythonType);
        }

        private object GenerateTypeName(IPythonType type) {
            if (type != null) {
                return GetTypeName(type.DeclaringModule.Name, type.Name);
            }
            return null;
        }

        private object GenerateTypeName(IPythonType type, ModuleInfo declModule, bool isRef, VariableDef[] indexTypes) {
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
                    typeNames[className] = typeName = new Dictionary<string, object> {
                        { "module_name", mModuleName },
                        { "type_name", mTypeName }
                    };

                    typeNames[className] = typeName = new Dictionary<string, object> {
                        { "module_name", mModuleName },
                        { "type_name", mTypeName },
                        { "index_types", indexTypes.Select(vd => GenerateTypeName(vd.TypesNoCopy, declModule, isRef)).ToList<object>() },
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
                    var typeName = GenerateTypeName(mroClass, ci.DeclaringModule.MyScope, true);
                    if (typeName != null) {
                        res.Add(typeName);
                    }
                }
            }
            return res;
        }

        private Dictionary<string, object> GenerateFunction(FunctionInfo fi) {
            return new Dictionary<string, object>() {
                { "doc", fi.Documentation },
                { "builtin", false },
                { "static", fi.IsStatic },
                { "location", GenerateLocation(fi.Location) },
                { "overloads", GenerateOverloads(fi) }
            };
        }

        private static object[] GenerateLocation(LocationInfo location) {
            return new object[] { location.Line, location.Column };
        }

        private List<object> GenerateOverloads(FunctionInfo fi) {
            var res = new List<object>();

            // TODO: Store distinct calls as separate overloads
            res.Add(new Dictionary<string, object> {
                { "args", GenerateArgInfo(fi, fi.GetParameterTypes()) },
                { "ret_type", GenerateTypeName(fi.GetReturnValue(), fi.DeclaringModule.MyScope, true) }
            });

            return res;
        }

        private object[] GenerateArgInfo(FunctionInfo fi, INamespaceSet[] parameters) {
            var res = new object[Math.Min(fi.FunctionDefinition.Parameters.Count, parameters.Length)];
            for (int i = 0; i < res.Length; i++) {
                res[i] = GenerateParameter(fi.FunctionDefinition.Parameters[i], parameters[i], fi.DeclaringModule.MyScope);
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

        private object GenerateParameter(Parameter param, INamespaceSet typeInfo, ModuleInfo declModule) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            // TODO: Serialize default values
            if (param.Kind == ParameterKind.Dictionary) {
                res["arg_format"] = "**";
            } else if (param.Kind == ParameterKind.List) {
                res["arg_format"] = "*";
            }
            res["name"] = MemoizeString(param.Name);
            res["type"] = GenerateTypeName(typeInfo, declModule, true);
            return res;
        }

    }
}
