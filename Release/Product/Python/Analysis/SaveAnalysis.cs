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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    class SaveAnalysis {
        private List<string> _errors = new List<string>();
        private Dictionary<Namespace, string> _classNames = new Dictionary<Namespace, string>();
        private List<Namespace> _path = new List<Namespace>();

        public void Save(PythonAnalyzer state, string outDir) {

            foreach (var modKeyValue in state.Modules) {
                string name = modKeyValue.Key;
                var moduleInfo = modKeyValue.Value.Module as ModuleInfo;

                if (moduleInfo != null) {
                    var info = SerializeModule(moduleInfo);
                    using (var writer = new FileStream(Path.Combine(outDir, name + ".idb"), FileMode.Create, FileAccess.ReadWrite)) {
                        new Pickler(writer).Dump(info);
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
                { "doc", moduleInfo.Documentation },
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
                res[keyValue.Key] = GenerateMember(keyValue.Value);
            }
            return res;
        }

        private object GenerateMember(VariableDef variableDef, bool isRef = false) {
            Dictionary<string, object> memberEntry = new Dictionary<string, object>() {
                {"kind", GetMemberKind(variableDef, isRef) },
                {"value", GetMemberValue(variableDef, isRef) }
            };

            return memberEntry;
        }

        private object GetMemberValue(VariableDef variableDef, bool isRef) {
            if (variableDef.Types.Count == 1) {
                var type = variableDef.Types.First();
                var res = GetMemberValue(type, isRef);
                if (res == null) {
                    _errors.Add(String.Format("Cannot save single member: {0}", variableDef.Types.First()));
                }
                return res;
            } else if (variableDef.Types.Count == 0) {
                return new Dictionary<string, object>() {
                    { "type", new object[] { "__builtin__", "object" } }
                };
            } else {
                List<object> res = new List<object>();
                foreach (var type in variableDef.Types) {
                    res.Add(
                        new Dictionary<string, object>() { 
                            { "kind", GetMemberKind(type, isRef) }, 
                            { "value", GetMemberValue(type, isRef) }
                        }
                    );
                }
                return new Dictionary<string, object>() {
                    {"members", res.ToArray() }
                };
            }
        }

        private object GetMemberValue(Namespace type, bool isRef) {
            switch (type.ResultType) {
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
                        if (isRef) {
                            return GenerateClassRef(ci);
                        } else {
                            return GenerateClass(ci);
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
                        return new Dictionary<string, object>() {
                        { "module_name" , ((ModuleInfo)type).Name }
                    };
                    } else if (type is BuiltinModule) {
                        return new Dictionary<string, object>() {
                            { "module_name" , ((BuiltinModule)type).Name }
                        };
                    }
                    break;
                case PythonMemberType.Instance:
                    InstanceInfo instInfo = type as InstanceInfo;
                    if (instInfo != null) {
                        return new Dictionary<string, object>() {
                            { "type" , GenerateTypeName(instInfo.ClassInfo) }
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
                { "func_name", name }
            };
        }

        private string GetMemberKind(VariableDef variableDef, bool isRef) {
            if (variableDef.Types.Count == 1) {
                return GetMemberKind(variableDef.Types.First(), isRef);
            } else if (variableDef.Types.Count == 0) {
                // typed to object
                return "data";
            } else {
                return "multiple";
            }
        }

        private static string GetMemberKind(Namespace type, bool isRef) {
            switch (type.ResultType) {
                case PythonMemberType.Function:
                    if (type is BuiltinFunctionInfo) {
                        return "func_ref";
                    }
                    return "function";
                case PythonMemberType.Method: return "method";
                case PythonMemberType.Property: return "property";
                case PythonMemberType.Class:
                    if (isRef || type is BuiltinClassInfo) {
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
                {"doc", prop.Documentation },
                {"type", GenerateTypeName(prop.ReturnValue.Types) },
                {"location", GenerateLocation(prop.Location) }
            };
        }

        private Dictionary<string, object> GenerateConstant(ConstantInfo constantInfo) {
            return new Dictionary<string, object>() {
                {"type", GenerateTypeName(constantInfo.PythonType) }
            };
        }

        private Dictionary<string, object> GenerateClass(ClassInfo ci) {
            return new Dictionary<string, object>() {
                { "mro", GetClassMro(ci) },
                { "bases", GetClassBases(ci) },
                { "members" , GetClassMembers(ci) },
                { "doc", ci.Documentation },
                { "builtin", false },
                { "location", GenerateLocation(ci.Location) }
            };
        }

        private Dictionary<string, object> GenerateClassRef(ClassInfo ci) {
            return new Dictionary<string, object>() {
                { "type_name", new object[] { ci.DeclaringModule.ModuleName, ci.ClassDefinition.Name } },
            };
        }

        private Dictionary<string, object> GenerateClassRef(BuiltinClassInfo ci) {
            return new Dictionary<string, object>() {
                { "type_name", new object[] { ci._type.DeclaringModule.Name, ci._type.Name } },
            };
        }

        private object GetClassMembers(ClassInfo ci) {
            Dictionary<string, object> memTable = new Dictionary<string, object>();
            foreach (var keyValue in ci.Scope.Variables) {
                memTable[keyValue.Key] = GenerateMember(keyValue.Value, true);
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

        private object[] GenerateTypeName(ISet<Namespace> name) {
            if (name.Count == 0) {
                return null;
            }

            // TODO: Multiple type names
            return GenerateTypeName(name.First());
        }

        private object[] GenerateTypeName(Namespace baseClass) {
            ClassInfo ci = baseClass as ClassInfo;
            if (ci != null) {
                return new object[] { ci.DeclaringModule.MyScope.Name, ci.ClassDefinition.Name };
            }

            BuiltinClassInfo bci = baseClass as BuiltinClassInfo;
            if (bci != null) {
                return GenerateTypeName(bci._type);
            }

            return GenerateTypeName(baseClass.PythonType);
        }

        private object[] GenerateTypeName(IPythonType type) {
            if (type != null) {
                return new object[] { type.DeclaringModule.Name, type.Name };
            }
            return null;
        }

        private object GetClassMro(ClassInfo ci) {
            // TODO: return correct mro
            return new List<object>();
        }

        private Dictionary<string, object> GenerateFunction(FunctionInfo fi) {

            return new Dictionary<string, object>() {
                {"doc", fi.Documentation },
                {"overloads", new object[] { GenerateOverload(fi) } },
                {"builtin", false},
                {"static", fi.IsStatic},
                {"location", GenerateLocation(fi.Location) }
            };
        }

        private static object[] GenerateLocation(LocationInfo location) {
            return new object[] { location.Line, location.Column };
        }

        private Dictionary<string, object> GenerateOverload(FunctionInfo fi) {
            return new Dictionary<string, object>() {
                {"args", GenerateArgInfo(fi) },
                {"ret_type", GenerateTypeName(fi.ReturnValue.Types) },
            };
        }

        private List<object> GenerateArgInfo(FunctionInfo fi) {
            var res = new List<object>(fi.FunctionDefinition.Parameters.Count);
            for (int i = 0; i < fi.FunctionDefinition.Parameters.Count; i++) {
                res.Add(GenerateParameter(fi.FunctionDefinition.Parameters[i], fi.ParameterTypes[i]));
            }
            return res;
        }

        private object GenerateParameter(Parameter param, VariableDef typeInfo) {
            Dictionary<string, object> res = new Dictionary<string, object>();
            // TODO: Serialize default values and type name
            if (param.Name.StartsWith("**")) {
                res["name"] = param.Name.Substring(2);
                res["arg_format"] = "**";
                res["type"] = GenerateTypeName(typeInfo.Types);
            } else if (param.Name.StartsWith("*")) {
                res["name"] = param.Name.Substring(1);
                res["arg_format"] = "*";
                res["type"] = GenerateTypeName(typeInfo.Types);
            } else {
                res["name"] = param.Name;
                res["type"] = GenerateTypeName(typeInfo.Types);
            }
            return res;
        }

    }
}
