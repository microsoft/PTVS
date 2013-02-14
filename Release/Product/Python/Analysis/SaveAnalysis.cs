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
        private Dictionary<string, Dictionary<string, object[]>> _typeNames = new Dictionary<string, Dictionary<string, object[]>>();
        private Dictionary<string, string> _MemoizedStrings = new Dictionary<string, string>();
        private Dictionary<string, Dictionary<string, object>> _moduleNames = new Dictionary<string, Dictionary<string, object>>();
        private static readonly List<object> _EmptyMro = new List<object>();
        private static object[] _objectType = new object[] { "__builtin__", "object" };

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

        private object[] GenerateChildModules(ModuleInfo moduleInfo) {
            List<object> res = new List<object>();
            foreach (var keyValue in moduleInfo.GetChildrenPackages(null)) {
                res.Add(keyValue.Key);
            }
            return res.ToArray();
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
                {"kind", GetMemberKind(variableDef, declModule, isRef) },
                {"value", GetMemberValue(variableDef, declModule, isRef) }
            };

            return memberEntry;
        }

        private object GetMemberValue(VariableDef variableDef, ModuleInfo declModule, bool isRef) {
            if (variableDef.Types.Count == 1) {
                var type = variableDef.Types.First();
                var res = GetMemberValue(type, declModule, isRef);
                if (res == null) {
                    _errors.Add(String.Format("Cannot save single member: {0}", variableDef.Types.First()));
                }
                return res;
            } else if (variableDef.Types.Count == 0) {
                return new Dictionary<string, object>() {
                    { "type",  _objectType }
                };
            } else {
                List<object> res = new List<object>();
                foreach (var type in variableDef.Types) {
                    res.Add(
                        new Dictionary<string, object>() { 
                            { "kind", GetMemberKind(type, declModule, isRef) }, 
                            { "value", GetMemberValue(type, declModule, isRef) }
                        }
                    );
                }
                return new Dictionary<string, object>() {
                    {"members", res.ToArray() }
                };
            }
        }

        private object GetMemberValue(Namespace type, ModuleInfo declModule, bool isRef) {
            SpecializedNamespace specialCallable = type as SpecializedNamespace;
            if (specialCallable != null) {
                return GetMemberValue(specialCallable.Original, declModule, isRef);
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
                            return GenerateClassRef(ci);
                        } else {
                            return GenerateClass(ci, declModule);
                        }
                    }

                    BuiltinClassInfo bci = type as BuiltinClassInfo;
                    if (bci != null) {
                        return GenerateClassRef(bci);
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
                    InstanceInfo instInfo = type as InstanceInfo;
                    if (instInfo != null) {
                        return new Dictionary<string, object>() {
                            { "type" , GenerateTypeName(instInfo.ClassInfo) }
                        };
                    }

                    BuiltinInstanceInfo builtinInst = type as BuiltinInstanceInfo;
                    if (builtinInst != null) {
                        return new Dictionary<string, object>() {
                            { "type" , GenerateTypeName(builtinInst.ClassInfo) }
                        };
                    }

                    SequenceInfo seqInfo = type as SequenceInfo;
                    if (seqInfo != null) {
                        return new Dictionary<string, object>() {
                            { "type" , GenerateTypeName(seqInfo.ClassInfo) }
                        };
                    }
                    break;
                default:
                    return new Dictionary<string, object>() {
                        { "type" , GenerateTypeName(type.PythonType) }
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
                {"doc", MemoizeString(prop.Documentation) },
                {"type", GenerateTypeName( GetFunctionReturnTypes(prop)) },
                {"location", GenerateLocation(prop.Location) }
            };
        }

        private static INamespaceSet GetFunctionReturnTypes(FunctionInfo func) {
            return func.GetReturnValue();
        }

        private Dictionary<string, object> GenerateConstant(ConstantInfo constantInfo) {
            return new Dictionary<string, object>() {
                {"type", GenerateTypeName(constantInfo.PythonType) }
            };
        }

        private Dictionary<string, object> GenerateClass(ClassInfo ci, ModuleInfo declModule) {
            return new Dictionary<string, object>() {
                { "mro", GetClassMro(ci) },
                { "bases", GetClassBases(ci) },
                { "members" , GetClassMembers(ci, declModule) },
                { "doc", MemoizeString(ci.Documentation) },
                { "builtin", false },
                { "location", GenerateLocation(ci.Location) }
            };
        }

        private Dictionary<string, object> GenerateClassRef(ClassInfo ci) {
            return new Dictionary<string, object>() {
                { "type_name",  GetTypeName(ci.DeclaringModule.ModuleName, ci.ClassDefinition.Name) },
            };
        }

        private object[] GetTypeName(string moduleName, string className) {
            // memoize types names for a more efficient on disk representation.
            object[] typeName;
            Dictionary<string, object[]> typeNames;
            if (!_typeNames.TryGetValue(moduleName, out typeNames)) {
                _typeNames[moduleName] = typeNames = new Dictionary<string, object[]>();
            }

            if (!typeNames.TryGetValue(className, out typeName)) {
                typeNames[className] = typeName = new object[] { MemoizeString(moduleName), MemoizeString(className) };
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

        private Dictionary<string, object> GenerateClassRef(BuiltinClassInfo ci) {
            return new Dictionary<string, object>() {
                { "type_name", GetTypeName(ci._type.DeclaringModule.Name, ci._type.Name) },
            };
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
                    var typeName = GenerateTypeName(baseClass);
                    if (typeName != null) {
                        res.Add(typeName);
                    }
                }
            }
            return res;
        }

        private object[] GenerateTypeName(INamespaceSet name) {
            if (name.Count == 0) {
                return null;
            }

            // TODO: Multiple type names
            return GenerateTypeName(name.First());
        }

        private object[] GenerateTypeName(Namespace baseClass) {
            ClassInfo ci = baseClass as ClassInfo;
            if (ci != null) {
                return GetTypeName(ci.DeclaringModule.MyScope.Name, ci.ClassDefinition.Name );
            }

            BuiltinClassInfo bci = baseClass as BuiltinClassInfo;
            if (bci != null) {
                return GenerateTypeName(bci._type);
            }

            return GenerateTypeName(baseClass.PythonType);
        }

        private object[] GenerateTypeName(IPythonType type) {
            if (type != null) {
                return GetTypeName(type.DeclaringModule.Name, type.Name);
            }
            return null;
        }

        
        private object GetClassMro(ClassInfo ci) {
            // TODO: return correct mro
            return _EmptyMro;
        }

        private Dictionary<string, object> GenerateFunction(FunctionInfo fi) {
            return new Dictionary<string, object>() {
                {"doc", fi.Documentation },
                {"overloads", GenerateOverloads(fi) },
                {"builtin", false},
                {"static", fi.IsStatic},
                {"location", GenerateLocation(fi.Location) }
            };
        }

        private static object[] GenerateLocation(LocationInfo location) {
            return new object[] { location.Line, location.Column };
        }

        private object[] GenerateOverloads(FunctionInfo fi) {
            List<object> overloads = new List<object>();
            var types = GetFunctionReturnTypes(fi);
            if (types.Count > 0) {
                foreach (var retType in types) {
                    overloads.Add(
                        new Dictionary<string, object>() {
                            {"args", GenerateArgInfo(fi) },
                            {"ret_type", GenerateTypeName(retType) },
                        }
                    );
                }
            } else {
                overloads.Add(
                    new Dictionary<string, object>() {
                        {"args", GenerateArgInfo(fi) }
                    }
                );
            }
            return overloads.ToArray();
        }

        private List<object> GenerateArgInfo(FunctionInfo fi) {
            var res = new List<object>(fi.FunctionDefinition.Parameters.Count);
            var parameters = fi.GetParameterTypes();
            for (int i = 0; i < fi.FunctionDefinition.Parameters.Count && i < parameters.Length; i++) {
                res.Add(GenerateParameter(fi.FunctionDefinition.Parameters[i], parameters[i]));
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

        private object GenerateParameter(Parameter param, INamespaceSet typeInfo) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            // TODO: Serialize default values and type name
            if (param.Kind == ParameterKind.Dictionary) {
                res["arg_format"] = "**";
            } else if (param.Kind == ParameterKind.List) {
                res["arg_format"] = "*";
            }
            res["name"] = MemoizeString(param.Name);
            res["type"] = GenerateTypeName(typeInfo);
            return res;
        }

    }
}
