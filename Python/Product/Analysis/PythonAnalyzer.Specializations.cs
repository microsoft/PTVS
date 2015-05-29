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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    public delegate IAnalysisSet CallDelegate(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames);

    public partial class PythonAnalyzer {
        /// <summary>
        /// Replaces a built-in function (specified by module name and function
        /// name) with a customized delegate which provides specific behavior
        /// for handling when that function is called.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public void SpecializeFunction(string moduleName, string name, CallDelegate callable, bool mergeOriginalAnalysis = false) {
            SpecializeFunction(moduleName, name, callable, mergeOriginalAnalysis, true);
        }

        /// <summary>
        /// Replaces a built-in function (specified by module name and function
        /// name) with a customized delegate which provides specific behavior
        /// for handling when that function is called.
        /// </summary>
        /// <remarks>New in 2.0</remarks>
        public void SpecializeFunction(string moduleName, string name, string returnType, bool mergeOriginalAnalysis = false) {
            if (returnType.LastIndexOf('.') == -1) {
                throw new ArgumentException(String.Format("Expected module.typename for return type, got '{0}'", returnType));
            }

            SpecializeFunction(moduleName, name, (n, u, a, k) => u.FindAnalysisValueByName(n, returnType), mergeOriginalAnalysis, true);
        }

        /// <summary>
        /// Replaces a built-in function (specified by module name and function
        /// name) with a customized delegate which provides specific behavior
        /// for handling when that function is called.
        /// 
        /// Currently this just provides a hook when the function is called - it
        /// could be expanded to providing the interpretation of when the
        /// function is called as well.
        /// </summary>
        private void SpecializeFunction(string moduleName, string name, CallDelegate callable, bool mergeOriginalAnalysis, bool save) {
            ModuleReference module;

            int lastDot;
            string realModName = null;
            if (Modules.TryGetImportedModule(moduleName, out module)) {
                IModule mod = module.Module as IModule;
                if (mod != null) {
                    mod.SpecializeFunction(name, callable, mergeOriginalAnalysis);
                    return;
                }
            } else if ((lastDot = moduleName.LastIndexOf('.')) != -1 &&
                Modules.TryGetImportedModule(realModName = moduleName.Substring(0, lastDot), out module)) {

                IModule mod = module.Module as IModule;
                if (mod != null) {
                    mod.SpecializeFunction(moduleName.Substring(lastDot + 1, moduleName.Length - (lastDot + 1)) + "." + name, callable, mergeOriginalAnalysis);
                    return;
                }
            }

            if (save) {
                SaveDelayedSpecialization(moduleName, name, callable, realModName, mergeOriginalAnalysis);
            }
        }

        /// <summary>
        /// Processes any delayed specialization for when a module is added for the 1st time.
        /// </summary>
        /// <param name="moduleName"></param>
        internal void DoDelayedSpecialization(string moduleName) {
            lock (_specializationInfo) {
                int lastDot;
                string realModName = null;
                List<SpecializationInfo> specInfo;
                if (_specializationInfo.TryGetValue(moduleName, out specInfo) ||
                    ((lastDot = moduleName.LastIndexOf('.')) != -1 &&
                    !string.IsNullOrEmpty(realModName = moduleName.Remove(lastDot)) &&
                    _specializationInfo.TryGetValue(realModName, out specInfo))) {
                    foreach (var curSpec in specInfo) {
                        SpecializeFunction(curSpec.ModuleName, curSpec.Name, curSpec.Callable, curSpec.SuppressOriginalAnalysis, save: false);
                    }
                }
            }
        }

        private void SaveDelayedSpecialization(string moduleName, string name, CallDelegate callable, string realModName, bool mergeOriginalAnalysis) {
            lock (_specializationInfo) {
                List<SpecializationInfo> specList;
                if (!_specializationInfo.TryGetValue(realModName ?? moduleName, out specList)) {
                    _specializationInfo[realModName ?? moduleName] = specList = new List<SpecializationInfo>();
                }

                specList.Add(new SpecializationInfo(moduleName, name, callable, mergeOriginalAnalysis));
            }
        }

        class SpecializationInfo {
            public readonly string Name, ModuleName;
            public readonly CallDelegate Callable;
            public readonly bool SuppressOriginalAnalysis;

            public SpecializationInfo(string moduleName, string name, CallDelegate callable, bool mergeOriginalAnalysis) {
                ModuleName = moduleName;
                Name = name;
                Callable = callable;
                SuppressOriginalAnalysis = mergeOriginalAnalysis;
            }
        }



        void AddBuiltInSpecializations() {
            SpecializeFunction(_builtinName, "range", RangeConstructor);
            SpecializeFunction(_builtinName, "min", ReturnUnionOfInputs);
            SpecializeFunction(_builtinName, "max", ReturnUnionOfInputs);
            SpecializeFunction(_builtinName, "getattr", SpecialGetAttr);
            SpecializeFunction(_builtinName, "setattr", SpecialSetAttr);
            SpecializeFunction(_builtinName, "next", SpecialNext);
            SpecializeFunction(_builtinName, "iter", SpecialIter);
            SpecializeFunction(_builtinName, "super", SpecialSuper);
            SpecializeFunction(_builtinName, "vars", ReturnsStringToObjectDict);
            SpecializeFunction(_builtinName, "dir", ReturnsListOfString);

            // analyzing the copy module causes an explosion in types (it gets called w/ all sorts of types to be
            // copied, and always returns the same type).  So we specialize these away so they return the type passed
            // in and don't do any analyze.  Ditto for the rest of the functions here...  
            SpecializeFunction("copy", "deepcopy", CopyFunction);
            SpecializeFunction("copy", "copy", CopyFunction);
            SpecializeFunction("pickle", "dumps", ReturnsBytes);
            SpecializeFunction("UserDict.UserDict", "update", Nop);
            SpecializeFunction("pprint", "pprint", Nop);
            SpecializeFunction("pprint", "pformat", ReturnsString);
            SpecializeFunction("pprint", "saferepr", ReturnsString);
            SpecializeFunction("pprint", "_safe_repr", ReturnsString);
            SpecializeFunction("pprint", "_format", ReturnsString);
            SpecializeFunction("pprint.PrettyPrinter", "_format", ReturnsString);
            SpecializeFunction("decimal.Decimal", "__new__", Nop);
            SpecializeFunction("StringIO.StringIO", "write", Nop);
            SpecializeFunction("threading.Thread", "__init__", Nop);
            SpecializeFunction("subprocess.Popen", "__init__", Nop);
            SpecializeFunction("Tkinter.Toplevel", "__init__", Nop);
            SpecializeFunction("weakref.WeakValueDictionary", "update", Nop);
            SpecializeFunction("os._Environ", "get", ReturnsString);
            SpecializeFunction("os._Environ", "update", Nop);
            SpecializeFunction("ntpath", "expandvars", ReturnsString);
            SpecializeFunction("idlelib.EditorWindow.EditorWindow", "__init__", Nop);
            SpecializeFunction("_functools", "partial", PartialFunction);
            SpecializeFunction("functools", "partial", PartialFunction);
            SpecializeFunction("functools", "update_wrapper", UpdateWrapperFunction);
            SpecializeFunction("functools", "wraps", WrapsFunction);

            SpecializeFunction("unittest", "_id", Identity);
            SpecializeFunction("unittest", "skip", IdentityDecorator);
            SpecializeFunction("unittest", "skipIf", IdentityDecorator);
            SpecializeFunction("unittest", "skipUnless", IdentityDecorator);

            // cached for quick checks to see if we're a call to clr.AddReference

            SpecializeFunction("wpf", "LoadComponent", LoadComponent);
        }

        private static IAnalysisSet GetArg(
            IAnalysisSet[] args,
            NameExpression[] keywordArgNames,
            string name,
            int index
        ) {
            for (int i = 0, j = args.Length - keywordArgNames.Length;
                i < keywordArgNames.Length && j < args.Length;
                ++i, ++j) {
                if (keywordArgNames[i].Name == name) {
                    return args[j];
                }
            }

            if (index < args.Length) {
                return args[index];
            }

            return null;
        }

        IAnalysisSet Nop(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return AnalysisSet.Empty;
        }

        IAnalysisSet Identity(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return args.Length > 0 ? args[0] : AnalysisSet.Empty;
        }

        IAnalysisSet IdentityDecorator(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 0) {
                return AnalysisSet.Empty;
            }
            if (args[0].GetMember(node, unit, "__call__").Any()) {
                return args[0];
            }
            return unit.ProjectState.GetCached(" PythonAnalyzer.Identity()", () => {
                return new SpecializedCallable(null, Identity, false);
            });
        }

        IAnalysisSet RangeConstructor(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.Scope.GetOrMakeNodeValue(node, (nn) => new RangeInfo(unit.ProjectState.Types[BuiltinTypeId.List], unit.ProjectState));
        }

        IAnalysisSet CopyFunction(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length > 0) {
                return args[0];
            }
            return AnalysisSet.Empty;
        }

        IAnalysisSet ReturnsBytes(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.ProjectState.ClassInfos[BuiltinTypeId.Bytes].Instance;
        }

        IAnalysisSet ReturnsString(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.ProjectState.ClassInfos[BuiltinTypeId.Str].Instance;
        }

        IAnalysisSet ReturnsListOfString(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.Scope.GetOrMakeNodeValue(node, n => {
                var vars = new VariableDef();
                vars.AddTypes(unit, unit.ProjectState.ClassInfos[BuiltinTypeId.Str].Instance);
                return new ListInfo(
                    new[] { vars },
                    unit.ProjectState.ClassInfos[BuiltinTypeId.List],
                    node,
                    unit.ProjectEntry
                );
            });
        }

        IAnalysisSet ReturnsStringToObjectDict(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return unit.Scope.GetOrMakeNodeValue(node, n => {
                var dict = new DictionaryInfo(unit.ProjectEntry, node);
                dict.AddTypes(
                    node,
                    unit,
                    unit.ProjectState.ClassInfos[BuiltinTypeId.Str].Instance,
                    unit.ProjectState.ClassInfos[BuiltinTypeId.Object].Instance
                );
                return dict;
            });
        }

        IAnalysisSet SpecialGetAttr(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = AnalysisSet.Empty;
            if (args.Length >= 2) {
                if (args.Length >= 3) {
                    // getattr(fob, 'oar', baz), baz is a possible return value.
                    res = args[2];
                }

                foreach (var value in args[0]) {
                    foreach (var name in args[1]) {
                        // getattr(fob, 'oar') - attempt to do the getattr and return the proper value
                        var strValue = name.GetConstantValueAsString();
                        if (strValue != null) {
                            res = res.Union(value.GetMember(node, unit, strValue));
                        }
                    }
                }
            }
            return res;
        }

        IAnalysisSet SpecialSetAttr(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 3) {
                foreach (var ii in args[0].OfType<InstanceInfo>()) {
                    foreach (var key in args[1].GetConstantValueAsString()) {
                        ii.SetMember(node, unit, key, args[2]);
                    }
                }
            }
            return AnalysisSet.Empty;
        }

        IAnalysisSet SpecialNext(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length > 0) {
                var nextName = (unit.ProjectState.LanguageVersion.Is3x()) ? "__next__" : "next";
                var newArgs = args.Skip(1).ToArray();
                var newNames = (keywordArgNames.Any()) ? keywordArgNames.Skip(1).ToArray() : keywordArgNames;

                return args[0].GetMember(node, unit, nextName).Call(node, unit, newArgs, newNames);
            } else {
                return AnalysisSet.Empty;
            }
        }

        IAnalysisSet SpecialIter(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                return args[0].GetIterator(node, unit);
            } else if (args.Length == 2) {
                var iterator = unit.Scope.GetOrMakeNodeValue(node, n => {
                    var iterTypes = new[] { new VariableDef() };
                    return new IteratorInfo(iterTypes, unit.ProjectState.ClassInfos[BuiltinTypeId.CallableIterator], node);
                });
                foreach (var iter in iterator.OfType<IteratorInfo>()) {
                    // call the callable object
                    // the sentinel's type is never seen, so don't include it
                    iter.AddTypes(unit, new[] { args[0].Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames) });
                }
                return iterator;
            }
            return AnalysisSet.Empty;
        }

        IAnalysisSet SpecialSuper(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length < 0 || args.Length > 2) {
                return AnalysisSet.Empty;
            }

            var classes = AnalysisSet.Empty;
            var instances = AnalysisSet.Empty;

            if (args.Length == 0) {
                if (unit.ProjectState.LanguageVersion.Is3x()) {
                    // No-arg version is magic in 3k - first arg is implicitly the enclosing class, and second is implicitly
                    // the first argument of the enclosing method. Look up that information from the scope.
                    // We want to find the nearest enclosing class scope, and the function scope that is immediately beneath
                    // that class scope. If there is no such combo, a no-arg super() is invalid.
                    var scopes = unit.Scope;
                    ClassScope classScope = null;
                    FunctionScope funcScope = null;
                    foreach (var s in scopes.EnumerateTowardsGlobal) {
                        funcScope = s as FunctionScope;
                        if (funcScope != null) {
                            classScope = s.OuterScope as ClassScope;
                            if (classScope != null) {
                                break;
                            }
                        }
                    }

                    if (classScope != null && funcScope != null) {
                        classes = classScope.Class.SelfSet;
                        // Get first arg of function.
                        if (funcScope.Function.FunctionDefinition.Parameters.Count > 0) {
                            instances = classScope.Class.Instance.SelfSet;
                        }
                    }
                }
            } else {
                classes = args[0];
                if (args.Length > 1) {
                    instances = args[1];
                }
            }

            if (classes == null) {
                return AnalysisSet.Empty;
            }

            return unit.Scope.GetOrMakeNodeValue(node, _ => {
                var res = AnalysisSet.Empty;
                foreach (var classInfo in classes.OfType<ClassInfo>()) {
                    res = res.Add(new SuperInfo(classInfo, instances));
                }
                return res;
            });
        }

        IAnalysisSet PartialFunction(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length >= 1) {
                return unit.Scope.GetOrMakeNodeValue(node, n => {
                    return new PartialFunctionInfo(args[0], args.Skip(1).ToArray(), keywordArgNames);
                });
            }

            return AnalysisSet.Empty;
        }

        private static IEnumerable<string> IterateStringConstants(IEnumerable<VariableDef> args) {
            return args
                .SelectMany(arg => arg.TypesNoCopy)
                .Where(obj => obj != null)
                .Select(obj => obj.GetConstantValueAsString())
                .Where(value => !string.IsNullOrEmpty(value));
        }

        IAnalysisSet UpdateWrapperFunction(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var wrapper = GetArg(args, keywordArgNames, "wrapper", 0);
            var wrapped = GetArg(args, keywordArgNames, "wrapped", 1);
            var assigned = GetArg(args, keywordArgNames, "assigned", 2) as IterableInfo;
            var updated = GetArg(args, keywordArgNames, "updated", 3) as IterableInfo;

            if (wrapper == null || wrapped == null) {
                return AnalysisSet.Empty;
            }

            wrapper.SetMember(node, unit, "__wrapped__", wrapped);
            
            var assignedItems = (assigned != null) ?
                IterateStringConstants(assigned.IndexTypes) :
                new[] { "__module__", "__name__", "__qualname__", "__doc__", "__annotations__" };

            foreach (var attr in assignedItems) {
                var member = wrapped.GetMember(node, unit, attr);
                if (member != null && member.Any()) {
                    wrapper.SetMember(node, unit, attr, member);
                }
            }

            var updatedItems = (updated != null) ?
                IterateStringConstants(updated.IndexTypes) :
                new[] { "__dict__" };

            foreach (var attr in updatedItems) {
                var member = wrapped.GetMember(node, unit, attr);
                if (member != null && member.Any()) {
                    var existing = wrapper.GetMember(node, unit, attr);
                    if (existing != null) {
                        var updateMethod = existing.GetMember(node, unit, "update");
                        if (updateMethod != null) {
                            updateMethod.Call(node, unit, new[] { member }, NameExpression.EmptyArray);
                        }
                    }
                }
            }

            return wrapper;
        }

        IAnalysisSet WrapsFunction(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length < 1) {
                return AnalysisSet.Empty;
            }

            return unit.Scope.GetOrMakeNodeValue(node, n => {
                ModuleReference modRef;
                if (!Modules.TryImport("functools", out modRef)) {
                    return AnalysisSet.Empty;
                }
                IAnalysisSet updateWrapper;
                if (!modRef.Module.GetAllMembers(_defaultContext).TryGetValue("update_wrapper", out updateWrapper)) {
                    return AnalysisSet.Empty;
                }

                var newArgs = new [] {
                    args[0],
                    args.Length > 1 ? args[1] : AnalysisSet.Empty,
                    args.Length > 2 ? args[2] : AnalysisSet.Empty
                };
                var newKeywords = new[] {
                    new NameExpression("wrapped"),
                    new NameExpression("assigned"),
                    new NameExpression("updated")
                };

                for (int i = 0; i < keywordArgNames.Length; ++i) {
                    int j = i + args.Length - keywordArgNames.Length;
                    if (j >= 0 && j < args.Length) {
                        if (keywordArgNames[i].Name == "assigned") {
                            newArgs[1] = args[j];
                        } else if (keywordArgNames[i].Name == "updated") {
                            newArgs[2] = args[j];
                        }
                    }
                }

                return new PartialFunctionInfo(
                    updateWrapper,
                    newArgs,
                    newKeywords
                );
            });
        }

        IAnalysisSet ReturnUnionOfInputs(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return AnalysisSet.UnionAll(args);
        }


        IAnalysisSet LoadComponent(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 2 && unit.ProjectState.Interpreter is IDotNetPythonInterpreter) {
                var self = args[0];
                var xaml = args[1];

                foreach (var arg in xaml) {
                    var strConst = arg.GetConstantValueAsString();
                    if (string.IsNullOrEmpty(strConst)) {
                        continue;
                    }

                    // process xaml file, add attributes to self
                    string xamlPath = Path.Combine(Path.GetDirectoryName(unit.DeclaringModule.ProjectEntry.FilePath), strConst);
                    XamlProjectEntry xamlProject;
                    if (unit.ProjectState._xamlByFilename.TryGetValue(xamlPath, out xamlProject)) {
                        // TODO: Get existing analysis if it hasn't changed.
                        var analysis = xamlProject.Analysis;

                        if (analysis == null) {
                            xamlProject.Analyze(CancellationToken.None);
                            analysis = xamlProject.Analysis;
                        }

                        xamlProject.AddDependency(unit.ProjectEntry);

                        var evalUnit = unit.CopyForEval();

                        // add named objects to instance
                        foreach (var keyValue in analysis.NamedObjects) {
                            var type = keyValue.Value;
                            if (type.Type.UnderlyingType != null) {

                                var ns = unit.ProjectState.GetAnalysisValueFromObjects(((IDotNetPythonInterpreter)unit.ProjectState.Interpreter).GetBuiltinType(type.Type.UnderlyingType));
                                var bci = ns as BuiltinClassInfo;
                                if (bci != null) {
                                    ns = bci.Instance;
                                }
                                self.SetMember(node, evalUnit, keyValue.Key, ns.SelfSet);
                            }

                            // TODO: Better would be if SetMember took something other than a node, then we'd
                            // track references w/o this extra effort.
                            foreach (var inst in self) {
                                InstanceInfo instInfo = inst as InstanceInfo;
                                if (instInfo != null && instInfo.InstanceAttributes != null) {
                                    VariableDef def;
                                    if (instInfo.InstanceAttributes.TryGetValue(keyValue.Key, out def)) {
                                        def.AddAssignment(
                                            new EncodedLocation(SourceLocationResolver.Instance, new SourceLocation(1, type.LineNumber, type.LineOffset)),
                                            xamlProject
                                        );
                                    }
                                }
                            }
                        }

                        // add references to event handlers
                        foreach (var keyValue in analysis.EventHandlers) {
                            // add reference to methods...
                            var member = keyValue.Value;

                            // TODO: Better would be if SetMember took something other than a node, then we'd
                            // track references w/o this extra effort.
                            foreach (var inst in self) {
                                InstanceInfo instInfo = inst as InstanceInfo;
                                if (instInfo != null) {
                                    ClassInfo ci = instInfo.ClassInfo;

                                    VariableDef def;
                                    if (ci.Scope.TryGetVariable(keyValue.Key, out def)) {
                                        def.AddReference(
                                            new EncodedLocation(SourceLocationResolver.Instance, new SourceLocation(1, member.LineNumber, member.LineOffset)),
                                            xamlProject
                                        );
                                    }
                                }
                            }
                        }
                    }
                }
                // load component returns self
                return self;
            }

            return AnalysisSet.Empty;
        }
    }
}
