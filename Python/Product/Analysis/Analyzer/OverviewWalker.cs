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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    /// <summary>
    /// Performs the 1st pass over the AST to gather all of the classes and
    /// function definitions.
    /// </summary>
    internal class OverviewWalker : PythonWalker {
        private InterpreterScope _scope;
        private readonly ProjectEntry _entry;
        private readonly Stack<AnalysisUnit> _analysisStack = new Stack<AnalysisUnit>();
        private AnalysisUnit _curUnit;
        private SuiteStatement _curSuite;

        public OverviewWalker(ProjectEntry entry, AnalysisUnit topAnalysis) {
            _entry = entry;
            _curUnit = topAnalysis;

            _scope = topAnalysis.Scope;
        }

        // TODO: What about names being redefined?
        // remember classes/functions as they start new scopes
        public override bool Walk(ClassDefinition node) {
            var cls = AddClass(node, _curUnit);
            if (cls != null) {
                _analysisStack.Push(_curUnit);
                _curUnit = cls.AnalysisUnit;
                Debug.Assert(_scope.EnumerateTowardsGlobal.Contains(cls.AnalysisUnit.Scope.OuterScope));
                _scope = cls.AnalysisUnit.Scope;
                return true;
            }
            return false;
        }

        internal ClassInfo AddClass(ClassDefinition node, AnalysisUnit outerUnit) {
            InterpreterScope scope;
            var declScope = outerUnit.Scope;
            if (!declScope.TryGetNodeScope(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var unit = new ClassAnalysisUnit(node, declScope, outerUnit);
                var classScope = (ClassScope)unit.Scope;

                var classVar = declScope.AddLocatedVariable(node.Name, node.NameExpression, unit);
                classVar.AddTypes(unit, classScope.Class.SelfSet);

                declScope.Children.Add(classScope);
                declScope.AddNodeScope(node, classScope);

                unit.Enqueue();
                scope = classScope;
            }
            return scope.AnalysisValue as ClassInfo;
        }

        public override void PostWalk(ClassDefinition node) {
            if (node.Body != null && node.Name != null) {
                Debug.Assert(_scope.Node == node);
                Debug.Assert(_scope.OuterScope.Node != node);
                _scope = _scope.OuterScope;
                _curUnit = _analysisStack.Pop();
                Debug.Assert(_scope.EnumerateTowardsGlobal.Contains(_curUnit.Scope));
            }
        }

        public override bool Walk(FunctionDefinition node) {
            var function = AddFunction(node, _curUnit);
            if (function != null) {
                _analysisStack.Push(_curUnit);
                _curUnit = function.AnalysisUnit;
                Debug.Assert(_scope.EnumerateTowardsGlobal.Contains(function.AnalysisUnit.Scope.OuterScope));
                _scope = function.AnalysisUnit.Scope;
                return true;
            }
            return false;
        }

        public override void PostWalk(FunctionDefinition node) {
            if (node.Body != null && node.Name != null) {
                Debug.Assert(_scope.Node == node);
                Debug.Assert(_scope.OuterScope.Node != node);
                _scope = _scope.OuterScope;
                _curUnit = _analysisStack.Pop();
                Debug.Assert(_scope.EnumerateTowardsGlobal.Contains(_curUnit.Scope));
            }
        }

        public override bool Walk(GlobalStatement node) {
            foreach (var name in node.Names) {
                if (name.Name != null) {
                    // set the variable in the local scope to be the real variable in the global scope
                    _scope.AddVariable(name.Name, _scope.GlobalScope.CreateVariable(node, _curUnit, name.Name, false));
                }
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (var name in node.Names) {
                if (name.Name != null) {
                    _scope.AddVariable(name.Name, CreateVariableInDeclaredScope(name));
                }
            }
            return false;
        }

        private VariableDef CreateVariableInDeclaredScope(NameExpression name) {
            var reference = name.GetVariableReference(_entry.Tree);

            if (reference != null && reference.Variable != null) {
                var declNode = reference.Variable.Scope;
                var declScope = _scope.EnumerateTowardsGlobal.FirstOrDefault(s => s.Node == declNode);
                if (declScope != null) {
                    return declScope.CreateVariable(name, _curUnit, name.Name, false);
                }
            }

            return _scope.CreateVariable(name, _curUnit, name.Name, false);
        }

        internal FunctionInfo AddFunction(FunctionDefinition node, AnalysisUnit outerUnit) {
            return AddFunction(node, outerUnit, _scope);
        }

        internal static FunctionInfo AddFunction(FunctionDefinition node, AnalysisUnit outerUnit, InterpreterScope prevScope) {
            InterpreterScope scope;
            if (!prevScope.TryGetNodeScope(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var func = new FunctionInfo(node, outerUnit, prevScope);
                var unit = func.AnalysisUnit;
                scope = unit.Scope;

                prevScope.Children.Add(scope);
                prevScope.AddNodeScope(node, scope);

                if (!node.IsLambda && node.Name != "<genexpr>") {
                    // lambdas don't have their names published

                    var funcVar = prevScope.AddLocatedVariable(node.Name, node.NameExpression, unit);
                    // Decorated functions don't have their type set yet
                    if (node.Decorators == null) {
                        funcVar.AddTypes(unit, func.SelfSet);
                    }
                }

                unit.Enqueue();
            }
            return scope.AnalysisValue as FunctionInfo;
        }

        public override bool Walk(GeneratorExpression node) {
            EnsureComprehensionScope(node, MakeGeneratorComprehensionScope);
            Debug.Assert(_scope is ComprehensionScope);

            return base.Walk(node);
        }

        public override void PostWalk(GeneratorExpression node) {
            Debug.Assert(_scope is ComprehensionScope);
            _scope = _scope.OuterScope;

            base.PostWalk(node);
        }

        public override bool Walk(ListComprehension node) {
            // List comprehension runs in a new scope in 3.x, runs in the same
            // scope in 2.x.  But these don't get their own analysis units
            // because they are still just expressions.
            if (_curUnit.ProjectState.LanguageVersion.Is3x()) {
                EnsureComprehensionScope(node, MakeListComprehensionScope);
            }

            return base.Walk(node);
        }

        public override void PostWalk(ListComprehension node) {
            if (_curUnit.ProjectState.LanguageVersion.Is3x()) {
                Debug.Assert(_scope is ComprehensionScope);
                _scope = _scope.OuterScope;
            }
            base.PostWalk(node);
        }

        public override bool Walk(SetComprehension node) {
            EnsureComprehensionScope(node, MakeSetComprehensionScope);
            Debug.Assert(_scope is ComprehensionScope);

            return base.Walk(node);
        }

        public override void PostWalk(SetComprehension node) {
            Debug.Assert(_scope is ComprehensionScope);
            _scope = _scope.OuterScope;

            base.PostWalk(node);
        }

        public override bool Walk(DictionaryComprehension node) {
            EnsureComprehensionScope(node, MakeDictComprehensionScope);
            Debug.Assert(_scope is ComprehensionScope);

            return base.Walk(node);
        }

        public override void PostWalk(DictionaryComprehension node) {
            Debug.Assert(_scope is ComprehensionScope);
            _scope = _scope.OuterScope;

            base.PostWalk(node);
        }

        /// <summary>
        /// Makes sure we create a scope for a comprehension (generator, set, dict, or list comprehension in 3.x) where
        /// the variables which are assigned will be stored.  
        /// </summary>
        private void EnsureComprehensionScope(Comprehension node, Func<Comprehension, ComprehensionScope> makeScope) {
            InterpreterScope scope, declScope = _scope;
            if (!declScope.TryGetNodeScope(node, out scope)) {
                scope = makeScope(node);
                
                declScope.AddNodeScope(node, scope);
                declScope.Children.Add(scope);
            }
            _scope = scope;
        }

        private ComprehensionScope MakeGeneratorComprehensionScope(Comprehension node) {
            var unit = new GeneratorComprehensionAnalysisUnit(node, _entry.Tree, _curUnit, _scope);
            unit.Enqueue();
            return (ComprehensionScope)unit.Scope;
        }

        private ComprehensionScope MakeListComprehensionScope(Comprehension node) {
            var unit = new ListComprehensionAnalysisUnit(node, _entry.Tree, _curUnit, _scope);
            unit.Enqueue();
            return (ComprehensionScope)unit.Scope;
        }

        private ComprehensionScope MakeSetComprehensionScope(Comprehension node) {
            var unit = new SetComprehensionAnalysisUnit(node, _entry.Tree, _curUnit, _scope);
            unit.Enqueue();
            return (ComprehensionScope)unit.Scope;
        }

        private ComprehensionScope MakeDictComprehensionScope(Comprehension node) {
            var unit = new DictionaryComprehensionAnalysisUnit(node, _entry.Tree, _curUnit, _scope);
            unit.Enqueue();
            return (ComprehensionScope)unit.Scope;
        }

        private void UpdateChildRanges(Statement node) {
            var declScope = _curUnit.Scope;
            var prevScope = declScope.Children.LastOrDefault();
            StatementScope prevStmtScope;
            IsInstanceScope prevInstanceScope;

            if ((prevStmtScope = prevScope as StatementScope) != null) {
                prevStmtScope.EndIndex = node.EndIndex;
            } else if ((prevInstanceScope = prevScope as IsInstanceScope) != null) {
                prevInstanceScope.EndIndex = node.EndIndex;
            } else {
                declScope.Children.Add(new StatementScope(node.StartIndex, declScope));
            }
        }

        internal static KeyValuePair<NameExpression, Expression>[] GetIsInstanceNamesAndExpressions(Expression node) {
            List<KeyValuePair<NameExpression, Expression>> names = null;
            GetIsInstanceNamesAndExpressions(ref names, node);
            if (names != null) {
                return names.ToArray();
            }
            return null;
        }

        /// <summary>
        /// Gets the names which should be in a new scope for isinstance(...) checks.  We don't
        /// use a walker here because we only support a very limited set of assertions (e.g. isinstance(x, type) and ...
        /// or a bare isinstance(...).
        /// </summary>
        internal static void GetIsInstanceNamesAndExpressions(ref List<KeyValuePair<NameExpression, Expression>> names, Expression node) {
            CallExpression callExpr = node as CallExpression;
            if (callExpr != null && callExpr.Args.Count == 2) {
                NameExpression nameExpr = callExpr.Target as NameExpression;
                if (nameExpr != null && nameExpr.Name == "isinstance") {
                    nameExpr = callExpr.Args[0].Expression as NameExpression;
                    if (nameExpr != null) {
                        if (names == null) {
                            names = new List<KeyValuePair<NameExpression, Expression>>();
                        }
                        var type = callExpr.Args[1].Expression;
                        names.Add(new KeyValuePair<NameExpression, Expression>(nameExpr, type));
                    }
                }
            }

            AndExpression andExpr = node as AndExpression;
            OrExpression orExpr = node as OrExpression;
            if (andExpr != null) {
                GetIsInstanceNamesAndExpressions(ref names, andExpr.Left);
                GetIsInstanceNamesAndExpressions(ref names, andExpr.Right);
            } else if (orExpr != null) {
                GetIsInstanceNamesAndExpressions(ref names, orExpr.Left);
                GetIsInstanceNamesAndExpressions(ref names, orExpr.Right);
            }
        }

        public override bool Walk(AssertStatement node) {
            // check if the assert statement contains any isinstance calls.
            CallExpression callExpr = node.Test as CallExpression;

            var isInstanceNames = GetIsInstanceNamesAndExpressions(node.Test);
            if (isInstanceNames != null) {
                // we need to introduce a new scope
                PushIsInstanceScope(node, isInstanceNames, _curSuite);
            } else {
                UpdateChildRanges(node);
            }
            return base.Walk(node);
        }

        private void PushIsInstanceScope(Node node, KeyValuePair<NameExpression, Expression>[] isInstanceNames, SuiteStatement effectiveSuite) {
            InterpreterScope scope;
            if (!_curUnit.Scope.TryGetNodeScope(node, out scope)) {
                if (_scope is IsInstanceScope) {
                    // Reuse the current scope
                    _curUnit.Scope.AddNodeScope(node, _scope);
                    return;
                }

                // find our parent scope, it may not be just the last entry in _scopes
                // because that can be a StatementScope and we would start a new range.
                var declScope = _scope.EnumerateTowardsGlobal.FirstOrDefault(s => !(s is StatementScope));

                scope = new IsInstanceScope(node.StartIndex, effectiveSuite, declScope);

                declScope.Children.Add(scope);
                declScope.AddNodeScope(node, scope);
                _scope = scope;
            }
        }

        public override bool Walk(AssignmentStatement node) {
            UpdateChildRanges(node);
            foreach (var nameExpr in node.Left.OfType<NameExpression>()) {
                _scope.AddVariable(nameExpr.Name, CreateVariableInDeclaredScope(nameExpr));
            }
            return base.Walk(node);
        }

        public override bool Walk(AugmentedAssignStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(BreakStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ContinueStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(DelStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ErrorStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(EmptyStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ExpressionStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ExecStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ForStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(FromImportStatement node) {
            UpdateChildRanges(node);
            var asNames = node.AsNames ?? node.Names;
            int len = Math.Min(node.Names.Count, asNames.Count);
            for (int i = 0; i < len; i++) {
                var nameNode = asNames[i] ?? node.Names[i];
                if (nameNode != null) {
                    if (nameNode.Name == "*") {
                        _scope.ContainsImportStar = true;
                    } else {
                        CreateVariableInDeclaredScope(nameNode);
                    }
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(IfStatement node) {
            UpdateChildRanges(node);
            if (node.Tests != null) {
                foreach (var test in node.Tests) {
                    var isInstanceNames = GetIsInstanceNamesAndExpressions(test.Test);
                    if (isInstanceNames != null) {
                        if (test.Test != null) {
                            test.Test.Walk(this);
                        }

                        if (test.Body != null && !(test.Body is ErrorStatement)) {
                            Debug.Assert(test.Body is SuiteStatement);

                            PushIsInstanceScope(test, isInstanceNames, (SuiteStatement)test.Body);

                            test.Body.Walk(this);
                        }
                    } else {
                        test.Walk(this);
                    }
                }
            }
            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }
            return false;
        }

        public override bool Walk(ImportStatement node) {
            for (int i = 0; i < node.Names.Count; i++) {
                NameExpression name = null;
                if (i < node.AsNames.Count && node.AsNames[i] != null) {
                    name = node.AsNames[i];
                } else if (node.Names[i].Names.Count > 0) {
                    name = node.Names[i].Names[0];
                }

                if (name != null) {
                    CreateVariableInDeclaredScope(name);
                }
            }

            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(PrintStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(RaiseStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(ReturnStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(TryStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(WhileStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(WithStatement node) {
            UpdateChildRanges(node);
            foreach (var item in node.Items) {
                var assignTo = item.Variable as NameExpression;
                if (assignTo != null) {
                    _scope.AddVariable(assignTo.Name, CreateVariableInDeclaredScope(assignTo));
                }
            }
            return base.Walk(node);
        }

        public override bool Walk(SuiteStatement node) {
            var prevSuite = _curSuite;
            _curSuite = node;

            // recursively walk the statements in the suite
            if (node.Statements != null) {
                foreach (var innerNode in node.Statements) {
                    innerNode.Walk(this);
                }
            }

            _curSuite = prevSuite;

            // then check if we encountered an assert which added an isinstance scope.
            IsInstanceScope isInstanceScope = _scope as IsInstanceScope;
            if (isInstanceScope != null && isInstanceScope._effectiveSuite == node) {
                // pop the isinstance scope
                _scope = _scope.OuterScope;
                var declScope = _curUnit.Scope;
                // transform back into a line number and start the new statement scope on the line
                // after the suite statement.
                var lineNo = _entry.Tree.IndexToLocation(node.EndIndex).Line;

                int offset;
                if (_entry.Tree._lineLocations.Length == 0) {
                    // single line input
                    offset = 0;
                } else {
                    offset = lineNo < _entry.Tree._lineLocations.Length ? _entry.Tree._lineLocations[lineNo] : _entry.Tree._lineLocations[_entry.Tree._lineLocations.Length - 1];
                }
                var closingScope = new StatementScope(offset, declScope);
                _scope = closingScope;
                declScope.Children.Add(closingScope);
            }
            return false;
        }

        public override void PostWalk(SuiteStatement node) {
            while (_scope is StatementScope) {
                _scope = _scope.OuterScope;
            }
            base.PostWalk(node);
        }

    }
}
