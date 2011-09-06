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
using System.Linq;

using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    internal class DDG : PythonWalker {
        internal AnalysisUnit _unit;
        internal ExpressionEvaluator _eval;

        public void Analyze(Deque<AnalysisUnit> queue) {
            while (queue.Count > 0) {
                _unit = queue.PopLeft();
                _unit.IsInQueue = false;
                
                _unit.Analyze(this);
            }
        }

        public void SetCurrentUnit(AnalysisUnit unit) {
            _eval = new ExpressionEvaluator(unit);
            _unit = unit;
        }

        public InterpreterScope[] Scopes {
            get { return _unit.Scopes; }
        }

        public ModuleInfo GlobalScope {
            get { return _unit.DeclaringModule; }
        }

        public PythonAnalyzer ProjectState {
            get { return _unit.ProjectState; }
        }

        public override bool Walk(PythonAst node) {
            ModuleReference existingRef;
            Debug.Assert(node == _unit.Ast);

            if (!ProjectState.Modules.TryGetValue(_unit.DeclaringModule.Name, out existingRef)) {
                // publish our module ref now so that we don't collect dependencies as we'll be fully processed
                ProjectState.Modules[_unit.DeclaringModule.Name] = new ModuleReference(_unit.DeclaringModule);
            }

            return base.Walk(node);
        }

        /// <summary>
        /// Gets the function which we are processing code for currently or
        /// null if we are not inside of a function body.
        /// </summary>
        public FunctionScope CurrentFunction {
            get { return CurrentContainer<FunctionScope>(); }
        }

        public ClassScope CurrentClass {
            get { return CurrentContainer<ClassScope>(); }
        }

        private T CurrentContainer<T>() where T : InterpreterScope {
            for (int i = Scopes.Length - 1; i >= 0; i--) {
                T result = Scopes[i] as T;
                if (result != null) {
                    return result;
                }
            }
            return null;
        }

        public T LookupDefinition<T>(Node node, string name) where T : Namespace {
            var defined = _eval.LookupNamespaceByName(node, name, false);
            foreach (var definition in defined) {
                T result = definition as T;
                if (result != null) {
                    return result;
                }
            }
            return null;
        }

        public override bool Walk(AssignmentStatement node) {
            var valueType = _eval.Evaluate(node.Right);
            foreach (var left in node.Left) {
                _eval.AssignTo(node, left, valueType);
            }
            return false;
        }

        public override bool Walk(AugmentedAssignStatement node) {
            var right = _eval.Evaluate(node.Right);

            foreach (var x in _eval.Evaluate(node.Left)) {
                x.AugmentAssign(node, _unit, right);
            }
            return false;
        }

        public override bool Walk(GlobalStatement node) {
            foreach (var name in node.Names) {
                GlobalScope.Scope.GetVariable(name, _unit, name.Name);
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (var name in node.Names) {
                for (int i = Scopes.Length - 2; i >= 0; i--) {
                    var var = Scopes[i].GetVariable(name, _unit, name.Name);
                    if (var != null) {
                        break;
                    }
                }
            }
            return false;
        }

        public override bool Walk(ClassDefinition node) {
            return false;
        }

        public AnalysisUnit PushScope(AnalysisUnit unit) {
            var oldUnit = _unit;
            _unit = unit;
            return oldUnit;
        }

        public void PopScope(AnalysisUnit unit) {
            _unit = unit;
        }

        public override bool Walk(ExpressionStatement node) {
            _eval.Evaluate(node.Expression);
            return false;
        }

        public override bool Walk(ForStatement node) {
            if (node.List != null) {
                var assignedTypes = _eval.Evaluate(node.List).ToArray();
                if (assignedTypes.Length > 0) {
                    foreach (var listType in assignedTypes) {
                        _eval.AssignTo(node, node.Left, listType.GetEnumeratorTypes(node, _unit));
                    }
                } else {
                    _eval.AssignTo(node, node.Left, EmptySet<Namespace>.Instance);
                }
            }

            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }
            return false;
        }

        private void WalkFromImportWorker(NameExpression node, Namespace userMod, string impName, string newName) {
            var saveName = (newName == null) ? impName : newName;

            bool addRef = node.Name != "*";

            var variable = Scopes[Scopes.Length - 1].CreateVariable(node, _unit, saveName, addRef);

            ISet<Namespace> newTypes = EmptySet<Namespace>.Instance;
            bool madeSet = false;

            // look for builtin / user-defined modules first
            ModuleInfo module = userMod as ModuleInfo;
            if (module != null) {
                var importedValue = module.Scope.CreateVariable(node, _unit, impName, addRef);
                Scopes[Scopes.Length - 1].GetLinkedVariables(saveName).Add(importedValue);

                newTypes = newTypes.Union(importedValue.Types, ref madeSet);
            }

            BuiltinModule builtinModule = userMod as BuiltinModule;
            if (builtinModule != null) {
                var importedValue = builtinModule.GetMember(node, _unit, impName);

                newTypes = newTypes.Union(importedValue, ref madeSet);

                builtinModule.InterpreterModule.Imported(_unit.DeclaringModule.InterpreterContext);
            }

            variable.AddTypes(node, _unit, newTypes);
        }

        public override bool Walk(FromImportStatement node) {
            ModuleReference moduleRef;
            Namespace userMod = null;
            RelativeModuleName relativeName = node.Root as RelativeModuleName;
            if (relativeName != null) {
                // attempt relative import...
                var curPackage = GlobalScope;
                for (int i = 0; i < relativeName.DotCount && curPackage != null; i++) {
                    curPackage = curPackage.ParentPackage;
                }

                for (int i = 0; i < relativeName.Names.Count && curPackage != null; i++) {
                    curPackage = curPackage.GetChildPackage(GlobalScope.InterpreterContext, relativeName.Names[i].Name) as ModuleInfo;
                }

                userMod = curPackage;
            }

            if (userMod == null) {
                var modName = node.Root.MakeString();

                if (!TryGetUserModule(modName, out moduleRef) || moduleRef.Module == null) {
                    userMod = ProjectState.ImportBuiltinModule(modName);
                }

                if (moduleRef != null) {
                    if (moduleRef.Module != null) {
                        userMod = moduleRef.Module;
                        if (userMod == null) {
                            moduleRef.AddEphemeralReference(_unit.DeclaringModule);
                        }
                    }
                } else if (userMod == null) {
                    moduleRef = ProjectState.Modules[modName] = new ModuleReference();
                    moduleRef.AddEphemeralReference(_unit.DeclaringModule);
                }
            }

            var asNames = node.AsNames ?? node.Names;

            int len = Math.Min(node.Names.Count, asNames.Count);
            for (int i = 0; i < len; i++) {
                var nameNode = asNames[i] ?? node.Names[i];
                var impName = node.Names[i].Name;
                var newName = asNames[i] != null ? asNames[i].Name : null;

                if (impName == null) {
                    // incomplete import statement
                    continue;
                } else if (impName == "*") {
                    // Handle "import *"
                    if (userMod != null) {
                        foreach (var varName in GetModuleKeys(userMod)) {
                            if (!varName.StartsWith("_")) {
                                WalkFromImportWorker(nameNode, userMod, varName, null);
                            }
                        }
                    }
                } else {
                    WalkFromImportWorker(nameNode, userMod, impName, newName);
                }
            }

            return true;
        }

        private bool TryGetUserModule(string modName, out ModuleReference moduleRef) {
            if (ProjectState.CrossModulAnalysisLimit != null &&
                ProjectState.ModulesByFilename.Count > ProjectState.CrossModulAnalysisLimit) {
                // too many modules loaded, disable cross module analysis by blocking
                // scripts from seeing other modules.
                moduleRef = null;
                return false;
            }

            // look for absolute name, then relative name
            if (ProjectState.Modules.TryGetValue(modName, out moduleRef) ||
                ProjectState.Modules.TryGetValue(_unit.FullName + "." + modName, out moduleRef)) {
                return true;
            }

            // search relative name in our parents.
            int lastDot;
            string name = _unit.FullName;
            while ((lastDot = name.LastIndexOf('.')) != -1) {
                name = name.Substring(0, lastDot);
                if (ProjectState.Modules.TryGetValue(name + "." + modName, out moduleRef)) {
                    return true;
                }
            }

            return false;
        }

        private ICollection<string> GetModuleKeys(Namespace userMod) {
            ModuleInfo mi = userMod as ModuleInfo;
            if (mi != null) {
                return mi.Scope.Variables.Keys;
            }

            BuiltinModule bmi = userMod as BuiltinModule;
            if (bmi != null) {
                return bmi.GetMemberNames(GlobalScope.InterpreterContext).ToArray();
            }

            return new string[0];
        }

        internal List<Namespace> LookupBaseMethods(string name, IEnumerable<ISet<Namespace>> bases, Node node, AnalysisUnit unit) {
            var result = new List<Namespace>();
            foreach (var b in bases) {
                foreach (var curType in b) {
                    BuiltinClassInfo klass = curType as BuiltinClassInfo;
                    if (klass != null) {
                        var value = klass.GetMember(node, unit, name);
                        if (value != null) {
                            result.AddRange(value);
                        }
                    }
                }
            }
            return result;
        }

        internal void PropagateBaseParams(FunctionInfo newScope, BuiltinMethodInfo method) {
            foreach (var overload in method.Function.Overloads) {
                var p = overload.GetParameters();
                if (p.Length + 1 == newScope.ParameterTypes.Length) {
                    for (int i = 0; i < p.Length; i++) {
                        var baseParam = p[i];
                        var baseType = ProjectState.GetNamespaceFromObjects(baseParam.ParameterType);
                        newScope.AddParameterType(_unit, baseType, i);
                    }
                }
            }
        }

        internal void ProcessFunctionDecorators(FunctionDefinition funcdef, FunctionInfo newScope) {
            if (funcdef.Decorators != null) {
                foreach (var d in funcdef.Decorators.Decorators) {
                    if (d != null) {
                        var decorator = _eval.Evaluate(d);

                        if (decorator.Contains(ProjectState._propertyObj)) {
                            newScope.IsProperty = true;
                        } else if (decorator.Contains(ProjectState._staticmethodObj)) {
                            newScope.IsStatic = true;
                        } else if (decorator.Contains(ProjectState._classmethodObj)) {
                            newScope.IsClassMethod = true;
                        }
                    }
                }
            }

            if (newScope.IsClassMethod) {
                if (newScope.ParameterTypes.Length > 0) {
                    newScope.AddParameterType(_unit, ProjectState._typeObj.SelfSet, 0);
                }
            } else if (!newScope.IsStatic) {
                // self is always an instance of the class
                // TODO: Check for __new__ (auto static) and
                // @staticmethod and @classmethod and @property
                InstanceInfo selfInst = null;
                for (int i = Scopes.Length - 1; i >= 0; i--) {
                    if (Scopes[i] is ClassScope) {
                        selfInst = ((ClassScope)Scopes[i]).Class.Instance;
                        break;
                    }
                }
                if (selfInst != null && newScope.ParameterTypes.Length > 0) {
                    newScope.AddParameterType(_unit, selfInst.SelfSet, 0);
                }
            }
        }

        public override bool Walk(FunctionDefinition node) {
            return false;
        }

        internal void WalkBody(Node node, AnalysisUnit unit) {
            var oldUnit = _unit;
            var eval = _eval;
            _unit = unit;
            _eval = new ExpressionEvaluator(unit);
            try {
                node.Walk(this);
            } finally {
                _unit = oldUnit;
                _eval = eval;
            }
        }

        public override bool Walk(IfStatement node) {
            foreach (var test in node.Tests) {
                _eval.Evaluate(test.Test);
                test.Body.Walk(this);
            }
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }
            return true;
        }

        public override bool Walk(ImportStatement node) {
            var x = _unit.ProjectEntry.Tree;

            int len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (int i = 0; i < len; i++) {
                var curName = node.Names[i];
                var asName = node.AsNames[i];

                string importing, saveName;
                Node nameNode;
                bool bottom = false;
                if (curName.Names.Count > 1) {
                    // import foo.bar
                    if (asName != null) {
                        // import foo.bar as baz, baz becomes the value of the bar module
                        importing = curName.MakeString();
                        saveName = asName.Name;
                        nameNode = asName;
                        bottom = true;
                    } else {
                        // plain import foo.bar, we bring in foo into the scope
                        saveName = importing = curName.Names[0].Name;
                        nameNode = curName.Names[0];
                    }
                } else {
                    // import foo
                    importing = curName.Names[0].Name;
                    if (asName != null) {
                        saveName = asName.Name;
                        nameNode = asName;
                    } else {
                        saveName = importing;
                        nameNode = curName.Names[0];
                    }
                }

                ModuleReference modRef;

                var def = Scopes[Scopes.Length - 1].CreateVariable(nameNode, _unit, saveName);
                if (!TryGetUserModule(importing, out modRef)) {
                    var builtinModule = ProjectState.ImportBuiltinModule(importing, bottom);

                    if (builtinModule != null) {
                        builtinModule.InterpreterModule.Imported(_unit.DeclaringModule.InterpreterContext);

                        def.AddTypes(nameNode, _unit, builtinModule.SelfSet);
                        continue;
                    }
                }

                if (modRef != null) {
                    if (modRef.Module != null) {
                        ModuleInfo mi = modRef.Module as ModuleInfo;
                        if (mi != null) {
                            mi.ModuleDefinition.AddDependency(_unit);
                        }

                        BuiltinModule builtinModule = modRef.Module as BuiltinModule;
                        if (builtinModule != null) {
                            builtinModule.InterpreterModule.Imported(_unit.DeclaringModule.InterpreterContext);
                        }

                        def.AddTypes(nameNode, _unit, modRef.Module.SelfSet);
                        continue;
                    } else {
                        modRef.AddEphemeralReference(_unit.DeclaringModule);
                    }
                } else {
                    ProjectState.Modules[importing] = modRef = new ModuleReference();
                    modRef.AddEphemeralReference(_unit.DeclaringModule);
                }
            }
            return true;
        }

        public override bool Walk(ReturnStatement node) {
            var curFunc = CurrentFunction;
            if (node.Expression != null && curFunc != null) {
                var lookupRes = _eval.Evaluate(node.Expression);

                var retVal = curFunc.Function.ReturnValue;
                int typeCount = retVal.Types.Count;
                foreach (var type in lookupRes) {
                    retVal.AddTypes(node, _unit, type);
                }
                if (typeCount != retVal.Types.Count) {
                    retVal.EnqueueDependents();
                }
            }
            return true;
        }

        public override bool Walk(WithStatement node) {
            foreach (var item in node.Items) {
                var ctxMgr = _eval.Evaluate(item.ContextManager);
                if (item.Variable != null) {
                    _eval.AssignTo(node, item.Variable, ctxMgr);
                }
            }

            return true;
        }

        public override bool Walk(PrintStatement node) {
            foreach (var expr in node.Expressions) {
                _eval.Evaluate(expr);
            }
            return false;
        }

        public override bool Walk(AssertStatement node) {
            _eval.EvaluateMaybeNull(node.Test);
            _eval.EvaluateMaybeNull(node.Message);
            return false;
        }

        public override bool Walk(DelStatement node) {
            foreach (var expr in node.Expressions) {
                DeleteExpression(expr);
            }
            return false;
        }

        private void DeleteExpression(Expression expr) {
            NameExpression name = expr as NameExpression;
            if (name != null) {
                var var = Scopes[Scopes.Length - 1].CreateVariable(name, _unit, name.Name);

                return;
            }

            IndexExpression index = expr as IndexExpression;
            if (index != null) {
                var values = _eval.Evaluate(index.Target);
                var indexValues = _eval.Evaluate(index.Index);
                foreach (var value in values) {
                    value.DeleteIndex(index, _unit, indexValues);
                }
                return;
            }

            MemberExpression member = expr as MemberExpression;
            if (member != null) {
                var values = _eval.Evaluate(member.Target);
                foreach (var value in values) {
                    value.DeleteMember(member, _unit, member.Name);
                }
                return;
            }

            ParenthesisExpression paren = expr as ParenthesisExpression;
            if (paren != null) {
                DeleteExpression(paren.Expression);
                return;
            }

            SequenceExpression seq = expr as SequenceExpression;
            if (seq != null) {
                foreach (var item in seq.Items) {
                    DeleteExpression(item);
                }
                return;
            }
        }

        public override bool Walk(RaiseStatement node) {
            _eval.EvaluateMaybeNull(node.Value);
            _eval.EvaluateMaybeNull(node.Traceback);
            _eval.EvaluateMaybeNull(node.ExceptType);
            _eval.EvaluateMaybeNull(node.Cause);
            return false;
        }

        public override bool Walk(WhileStatement node) {
            _eval.Evaluate(node.Test);

            node.Body.Walk(this);
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }

            return false;
        }

        public override bool Walk(TryStatement node) {
            node.Body.Walk(this);
            if (node.Handlers != null) {
                foreach (var handler in node.Handlers) {
                    ISet<Namespace> test = EmptySet<Namespace>.Instance;
                    bool madeSet = false;
                    if (handler.Test != null) {
                        var testTypes = _eval.Evaluate(handler.Test);

                        if (handler.Target != null) {
                            foreach (var type in testTypes) {
                                ClassInfo klass = type as ClassInfo;
                                if (klass != null) {
                                    test = test.Union(klass.Instance.SelfSet, ref madeSet);
                                }

                                BuiltinClassInfo builtinClass = type as BuiltinClassInfo;
                                if (builtinClass != null) {
                                    test = test.Union(builtinClass.Instance.SelfSet, ref madeSet);
                                }
                            }

                            _eval.AssignTo(handler, handler.Target, test);
                        }
                    }

                    handler.Body.Walk(this);
                }
            }

            if (node.Finally != null) {
                node.Finally.Walk(this);
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }

            return false;
        }

        public override bool Walk(ExecStatement node) {
            if (node.Code != null) {
                _eval.Evaluate(node.Code);
            }
            if (node.Locals != null) {
                _eval.Evaluate(node.Locals);
            }
            if (node.Globals != null) {
                _eval.Evaluate(node.Globals);
            }
            return false;
        }        
    }
}
