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

        public void Analyze(Deque<AnalysisUnit>queue) {
            while (queue.Count > 0) {
                _unit = queue.PopLeft();
                _unit.IsInQueue = false;

                 _eval = new ExpressionEvaluator(_unit);
                 _unit.Analyze(this);
            }
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
            foreach (var listType in _eval.Evaluate(node.List).ToArray()) {
                _eval.AssignTo(node, node.Left, listType.GetEnumeratorTypes(node, _unit));
            }

            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }
            return false;
        }

        private void WalkFromImportWorker(FromImportStatement node, Namespace userMod, string impName, string newName) {
            var saveName = (newName == null) ? impName : newName;
            GlobalScope.Imports[node].Types.Add(new[] { impName, newName });

            // TODO: Better node would be the name node but we don't have a name node (they're just strings in the AST w/ no position info)
            var variable = Scopes[Scopes.Length - 1].CreateVariable(node, _unit, saveName);

            ISet<Namespace> newTypes = EmptySet<Namespace>.Instance;
            bool madeSet = false;

            // look for builtin / user-defined modules first
            ModuleInfo module = userMod as ModuleInfo;
            if (module != null) {
                var importedValue = module.Scope.CreateVariable(node, _unit, impName);
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
                    curPackage = curPackage.GetChildPackage(GlobalScope.InterpreterContext, relativeName.Names[i]) as ModuleInfo;
                }

                userMod = curPackage;
            }

            if (userMod == null) {
                var modName = node.Root.MakeString();

                if (!ProjectState.Modules.TryGetValue(modName, out moduleRef)) {
                    userMod = ProjectState.ImportBuiltinModule(modName);
                }

                if (moduleRef != null) {
                    userMod = moduleRef.Module;
                } else if (userMod == null) {
                    moduleRef = ProjectState.Modules[modName] = new ModuleReference();
                }
            }

            var asNames = node.AsNames ?? node.Names;
            var impInfo = new ImportInfo(node.Root.MakeString(), node.Span);
            GlobalScope.Imports[node] = impInfo;

            int len = Math.Min(node.Names.Count, asNames.Count);
            for (int i = 0; i < len; i++) {
                var impName = node.Names[i];
                var newName = asNames[i];

                if (impName == null) {
                    // incomplete import statement
                    continue;
                } else if (impName == "*") {
                    // Handle "import *"
                    if (userMod != null) {
                        foreach (var varName in GetModuleKeys(userMod)) {
                            WalkFromImportWorker(node, userMod, varName, null);
                        }
                    }
                } else {
                    WalkFromImportWorker(node, userMod, impName, newName);
                }
            }

            return true;
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
                        var value = klass.GetMember(node, unit, "name"); // curType.GetVariable(name);
                        if (value != null) {
                            result.AddRange(value);
                        }
                    }
                }
            }
            return result;
        }

        internal void PropagateBaseParams(FunctionInfo newScope, Namespace method) {
            foreach (var overload in method.Overloads) {
                var p = overload.Parameters;
                if (p.Length == newScope.ParameterTypes.Length) {
                    for (int i = 1; i < p.Length; i++) {
                        var baseParam = p[i];
                        var newParam = newScope.ParameterTypes[i];
                        // TODO: baseParam.Type isn't right, it's a string, not a type object
                        var baseType = ProjectState.GetNamespaceFromObjects(baseParam.Type);
                        if (baseType != null) {
                            newParam.Types.Add(baseType);
                        }
                    }
                }
            }
        }

        internal void ProcessFunctionDecorators(FunctionDefinition funcdef, FunctionInfo newScope) {
            if (funcdef.Decorators != null) {
                foreach (var d in funcdef.Decorators) {
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
            
            if (newScope.IsClassMethod) {
                if (newScope.ParameterTypes.Length > 0) {
                    newScope.ParameterTypes[0].AddTypes(funcdef.Parameters[0], _unit, ProjectState._typeObj.SelfSet);
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
                    newScope.ParameterTypes[0].AddTypes(funcdef.Parameters[0], _unit, selfInst.SelfSet);
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
            var iinfo = new ImportInfo("", node.Span);
            GlobalScope.Imports[node] = iinfo;
            int len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (int i = 0; i < len; i++) {
                var impNode = node.Names[i];
                var newName = node.AsNames[i];
                var strImpName = impNode.MakeString();
                iinfo.Types.Add(new[] { strImpName, newName });
                                
                var saveName = (String.IsNullOrEmpty(newName)) ? strImpName : newName;
                ModuleReference modRef;

                var def = Scopes[Scopes.Length - 1].CreateVariable(impNode, _unit, saveName);
                if (!ProjectState.Modules.TryGetValue(strImpName, out modRef)) {
                    var builtinModule = ProjectState.ImportBuiltinModule(strImpName, impNode.Names.Count > 1 && !String.IsNullOrEmpty(newName));

                    if (builtinModule != null) {
                        builtinModule.InterpreterModule.Imported(_unit.DeclaringModule.InterpreterContext);

                        def.AddTypes(impNode, _unit, builtinModule.SelfSet);
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

                        def.AddTypes(impNode, _unit, modRef.Module.SelfSet);
                        continue;
                    }                    
                } else {
                    ProjectState.Modules[strImpName] = modRef = new ModuleReference();
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
            var ctxMgr = _eval.Evaluate(node.ContextManager);
            if (node.Variable != null) {
                _eval.AssignTo(node, node.Variable, ctxMgr);
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
                var variable = _eval.LookupVariableByName(name.Name, expr);
                if (variable != null) {
                    variable.AddReference(name, _unit);
                }
            }

            IndexExpression index = expr as IndexExpression;
            if (index != null) {
                var values = _eval.Evaluate(index.Target);
                var indexValues = _eval.Evaluate(index.Index);
                foreach (var value in values) {
                    value.DeleteIndex(index, _unit, indexValues);
                }
            }

            MemberExpression member = expr as MemberExpression;
            if (member != null) {
                var values = _eval.Evaluate(member.Target);
                foreach (var value in values) {
                    value.DeleteMember(member, _unit, member.Name);
                }
            }

            ParenthesisExpression paren = expr as ParenthesisExpression;
            if (paren != null) {
                DeleteExpression(paren.Expression);
            }

            SequenceExpression seq = expr as SequenceExpression;
            if (seq != null) {
                foreach (var item in seq.Items) {
                    DeleteExpression(item);
                }
            }
        }

        public override bool Walk(RaiseStatement node) {
            _eval.EvaluateMaybeNull(node.Value);
            _eval.EvaluateMaybeNull(node.Traceback);
            _eval.EvaluateMaybeNull(node.ExceptType);
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
    }
}
