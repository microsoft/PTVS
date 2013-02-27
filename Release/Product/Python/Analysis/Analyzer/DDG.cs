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
using System.Threading;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    internal class DDG : PythonWalker {
        internal AnalysisUnit _unit;
        internal ExpressionEvaluator _eval;
        private SuiteStatement _curSuite;

        public void Analyze(Deque<AnalysisUnit> queue, CancellationToken cancel) {
            if (cancel.IsCancellationRequested) {
                return;
            }
            
            // Including a marker at the end of the queue allows us to see in
            // the log how frequently the queue empties.
            var endOfQueueMarker = new AnalysisUnit(null, null);
            int queueCountAtStart = queue.Count;

            if (queueCountAtStart > 0) {
                queue.Append(endOfQueueMarker);
            }

            while (queue.Count > 0 && !cancel.IsCancellationRequested) {
                _unit = queue.PopLeft();

                if (_unit == endOfQueueMarker) {
                    AnalysisLog.EndOfQueue(queueCountAtStart, queue.Count);
                    queueCountAtStart = queue.Count;
                    if (queueCountAtStart > 0) {
                        queue.Append(endOfQueueMarker);
                    }
                    continue;
                }

                AnalysisLog.Dequeue(queue, _unit);

                _unit.IsInQueue = false;
                SetCurrentUnit(_unit);
                _unit.Analyze(this, cancel);
            }

            if (cancel.IsCancellationRequested) {
                AnalysisLog.Cancelled(queue);
            }
        }

        public void SetCurrentUnit(AnalysisUnit unit) {
            _eval = new ExpressionEvaluator(unit);
            _unit = unit;
        }

        public InterpreterScope Scope {
            get {
                return _eval.Scope;
            }
            set {
                _eval.Scope = value;
            }
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
            return Scope.EnumerateTowardsGlobal.OfType<T>().FirstOrDefault();
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
            if (Scope.OuterScope != null) {
                foreach (var name in node.Names) {
                    Scope.OuterScope.GetVariable(name, _unit, name.Name);
                }
            }
            return false;
        }

        public override bool Walk(ClassDefinition node) {
            return false;
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
                    _eval.AssignTo(node, node.Left, NamespaceSet.Empty);
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

        private void WalkFromImportWorker(NameExpression node, IModule userMod, string impName, string newName) {
            var saveName = (newName == null) ? impName : newName;

            bool addRef = node.Name != "*";

            var variable = Scope.CreateVariable(node, _unit, saveName, addRef);
            if (userMod != null) {
                variable.AddTypes(_unit, userMod.GetModuleMember(node, _unit, impName, addRef, Scope, saveName));
            }

            if (addRef) {
                variable.AddAssignment(node, _unit);
            }
        }

        public override bool Walk(FromImportStatement node) {
            ModuleReference moduleRef;
            IModule userMod = null;
            RelativeModuleName relativeName = node.Root as RelativeModuleName;
            if (relativeName != null) {
                // attempt relative import...
                var curPackage = GlobalScope;
                int dotCount = relativeName.DotCount;
                if (curPackage.ProjectEntry.FilePath.EndsWith("\\__init__.py") ||
                    curPackage.ProjectEntry.FilePath.EndsWith("\\__init__.pyw")) {
                    // relative import from inside of a package definition, we don't go out
                    // of our own package.
                    dotCount--;
                }

                for (int i = 0; i < dotCount && curPackage != null; i++) {
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
                        userMod = moduleRef.Module as IModule;
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
                        foreach (var varName in userMod.GetModuleMemberNames(GlobalScope.InterpreterContext)) {
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
            if (ProjectState.Limits.CrossModule != null &&
                ProjectState.ModulesByFilename.Count > ProjectState.Limits.CrossModule) {
                // too many modules loaded, disable cross module analysis by blocking
                // scripts from seeing other modules.
                moduleRef = null;
                return false;
            }

            // look for absolute name, then relative name
            if (ProjectState.Modules.TryGetValue(modName, out moduleRef) ||
                ProjectState.Modules.TryGetValue(_unit.DeclaringModule.Name + "." + modName, out moduleRef)) {
                return true;
            }

            // search relative name in our parents.
            int lastDot;
            string name = _unit.DeclaringModule.Name;
            while ((lastDot = name.LastIndexOf('.')) != -1) {
                name = name.Substring(0, lastDot);
                if (ProjectState.Modules.TryGetValue(name + "." + modName, out moduleRef)) {
                    return true;
                }
            }

            return false;
        }

        internal List<Namespace> LookupBaseMethods(string name, IEnumerable<INamespaceSet> bases, Node node, AnalysisUnit unit) {
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

        public override bool Walk(FunctionDefinition node) {
            InterpreterScope funcScope;
            if (_unit.Scope.TryGetNodeScope(node, out funcScope)) {
                var function = ((FunctionScope)funcScope).Function;
                var analysisUnit = (FunctionAnalysisUnit)((FunctionScope)funcScope).Function.AnalysisUnit;

                var curClass = Scope as ClassScope;
                if (curClass != null) {
                    var bases = LookupBaseMethods(
                        analysisUnit.Ast.Name,
                        curClass.Class.Mro,
                        analysisUnit.Ast,
                        analysisUnit
                    );
                    foreach (var method in bases.OfType<BuiltinMethodInfo>()) {
                        foreach (var overload in method.Function.Overloads) {
                            function.UpdateDefaultParameters(_unit, overload.GetParameters());
                        }
                    }
                }
            }

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

                TryPushIsInstanceScope(test, test.Test);

                test.Body.Walk(this);
            }
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }
            return true;
        }

        public override bool Walk(ImportStatement node) {
            int len = Math.Min(node.Names.Count, node.AsNames.Count);
            for (int i = 0; i < len; i++) {
                var curName = node.Names[i];
                var asName = node.AsNames[i];

                string importing, saveName;
                Node nameNode;
                bool bottom = false;
                if (curName.Names.Count == 0) {
                    continue;
                } else if (curName.Names.Count > 1) {
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

                var def = Scope.CreateVariable(nameNode, _unit, saveName);
                if (!TryGetUserModule(importing, out modRef)) {
                    var builtinModule = ProjectState.ImportBuiltinModule(importing, bottom);
                    var ns = builtinModule as Namespace;

                    if (builtinModule != null && ns != null) {
                        builtinModule.Imported(_unit);

                        def.AddTypes(_unit, ns);
                        def.AddAssignment(nameNode, _unit);
                        continue;
                    }
                }

                if (modRef != null) {
                    if (modRef.Module != null) {
                        modRef.Module.Imported(_unit);

                        def.AddTypes(_unit, modRef.Namespace);
                        def.AddAssignment(nameNode, _unit);
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
            var fnScope = CurrentFunction;
            if (node.Expression != null && fnScope != null) {
                var lookupRes = _eval.Evaluate(node.Expression);
                fnScope.AddReturnTypes(node, _unit, lookupRes);
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
            TryPushIsInstanceScope(node, node.Test);

            _eval.EvaluateMaybeNull(node.Test);
            _eval.EvaluateMaybeNull(node.Message);
            return false;
        }

        private void TryPushIsInstanceScope(Node node, Expression test) {
            InterpreterScope newScope;
            if (Scope.TryGetNodeScope(node, out newScope)) {
                var outerScope = Scope;
                var isInstanceScope = (IsInstanceScope)newScope;

                // magic assert isinstance statement alters the type information for a node
                var namesAndExpressions = OverviewWalker.GetIsInstanceNamesAndExpressions(test);
                foreach (var nameAndExpr in namesAndExpressions) {
                    var name = nameAndExpr.Key;
                    var type = nameAndExpr.Value;

                    var typeObj = _eval.EvaluateMaybeNull(type);
                    isInstanceScope.CreateTypedVariable(name, _unit, name.Name, typeObj);
                }

                // push the scope, it will be popped when we leave the current SuiteStatement.
                Scope = newScope;
            }
        }

        public override bool Walk(SuiteStatement node) {
            var prevSuite = _curSuite;
            var prevScope = Scope;

            _curSuite = node;
            if (node.Statements != null) {
                foreach (var statement in node.Statements) {
                    statement.Walk(this);
                }
            }

            Scope = prevScope;
            _curSuite = prevSuite;
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
                var var = Scope.CreateVariable(name, _unit, name.Name);

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
                    var test = NamespaceSet.Empty;
                    if (handler.Test != null) {
                        var testTypes = _eval.Evaluate(handler.Test);

                        if (handler.Target != null) {
                            foreach (var type in testTypes) {
                                ClassInfo klass = type as ClassInfo;
                                if (klass != null) {
                                    test = test.Union(klass.Instance.SelfSet);
                                }

                                BuiltinClassInfo builtinClass = type as BuiltinClassInfo;
                                if (builtinClass != null) {
                                    test = test.Union(builtinClass.Instance.SelfSet);
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
