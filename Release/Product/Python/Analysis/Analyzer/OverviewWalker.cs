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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    /// <summary>
    /// Performs the 1st pass over the AST to gather all of the classes and
    /// function definitions.
    /// </summary>
    internal class OverviewWalker : PythonWalker {
        private List<InterpreterScope> _scopes;
        private readonly ProjectEntry _entry;
        private readonly Stack<AnalysisUnit> _analysisStack = new Stack<AnalysisUnit>();
        private AnalysisUnit _curUnit;
        private SuiteStatement _curSuite;

        public OverviewWalker(ProjectEntry entry, AnalysisUnit topAnalysis) {
            _entry = entry;
            _curUnit = topAnalysis;

            _scopes = new List<InterpreterScope>(topAnalysis.Scopes);
        }

        // TODO: What about names being redefined?
        // remember classes/functions as they start new scopes
        public override bool Walk(ClassDefinition node) {
            return WalkMember(AddClass(node, _curUnit));
        }

        internal ClassInfo AddClass(ClassDefinition node, AnalysisUnit outerUnit) {
            InterpreterScope scope;
            if (!outerUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var scopes = new InterpreterScope[_scopes.Count + 1];
                _scopes.CopyTo(scopes, 0);

                var unit = new ClassAnalysisUnit(node, scopes, outerUnit);
                var klass = new ClassInfo(unit, node);
                var classScope = scope = klass.Scope;
                

                var declScope = outerUnit.Scopes[outerUnit.Scopes.Length - 1];
                var classVar = declScope.AddLocatedVariable(node.Name, node.NameExpression, unit);
                classVar.AddTypes(node.NameExpression, unit, klass.SelfSet);

                declScope.Children.Add(classScope);
                scopes[scopes.Length - 1] = classScope;
                outerUnit.DeclaringModule.NodeScopes[node] = scope;

                unit.Enqueue();
            }
            return scope.Namespace as ClassInfo;
        }

        public override void PostWalk(ClassDefinition node) {
            if (node.Body != null && node.Name != null) {
                InterpreterScope prevScope;
                do {
                    prevScope = _scopes.Pop();
                } while (prevScope.Node != node);
                _curUnit = _analysisStack.Pop();
            }
        }

        public override bool Walk(FunctionDefinition node) {
            return WalkMember(AddFunction(node, _curUnit));
        }

        public override bool Walk(GlobalStatement node) {
            foreach (var name in node.Names) {
                if (name.Name != null) {
                    // set the variable in the local scope to be the real variable in the global scope
                    _scopes[_scopes.Count - 1].Variables[name.Name] = _scopes[0].CreateVariable(node, _curUnit, name.Name, false);
                }
            }
            return false;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (var name in node.Names) {
                if (name.Name != null) {
                    CreateVariable(name);
                }
            }
            return false;
        }

        private VariableDef CreateVariable(NameExpression name) {
            var reference = name.GetVariableReference(_entry.Tree);
            
            if (reference != null && reference.Variable != null) {
                var declScope = reference.Variable.Scope;
                foreach (var scope in _scopes) {
                    if (scope.Node == declScope || (declScope is PythonAst && scope == null)) {
                        return _scopes[_scopes.Count - 1].Variables[name.Name] = scope.CreateVariable(name, _curUnit, name.Name, false);
                    }
                }
            }

            int curScope = _scopes.Count - 1;

            while (_scopes[curScope] is IsInstanceScope) {
                curScope--;
            }

            return _scopes[curScope].CreateVariable(name, _curUnit, name.Name, false);
        }

        internal FunctionInfo AddFunction(FunctionDefinition node, AnalysisUnit outerUnit) {
            return AddFunction(node, outerUnit, _scopes);
        }

        internal static FunctionInfo AddFunction(FunctionDefinition node, AnalysisUnit outerUnit, IList<InterpreterScope> prevScopes) {
            InterpreterScope scope;
            if (!outerUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var scopes = new InterpreterScope[prevScopes.Count + 1];
                prevScopes.CopyTo(scopes, 0);
                
                var unit = new FunctionAnalysisUnit(node, scopes, outerUnit);
                var function = node.IsGenerator ? new GeneratorFunctionInfo(unit) : new FunctionInfo(unit);

                if (node.Decorators != null) {
                    foreach (var d in node.Decorators.Decorators) {
                        NameExpression ne = d as NameExpression;
                        if (ne != null) {
                            if (ne.Name == "property") {
                                function.IsProperty = true;
                            } else if (ne.Name == "staticmethod") {
                                function.IsStatic = true;
                            } else if (ne.Name == "classmethod") {
                                function.IsClassMethod = true;
                            }
                        }
                    }
                }
                var funcScope = new FunctionScope(function, node);
                outerUnit.DeclaringModule.NodeScopes[node] = funcScope;

                var declScope = outerUnit.Scopes[outerUnit.Scopes.Length - 1];
                declScope.Children.Add(funcScope);

                scopes[scopes.Length - 1] = funcScope;
                scope = funcScope;

                if (!node.IsLambda && node.Name != "<genexpr>") {
                    // lambdas don't have their names published        
                    var funcVar = declScope.AddLocatedVariable(node.Name, node.NameExpression, unit);
                    funcVar.AddTypes(node.NameExpression, unit, function.SelfSet);
                }

                var newParams = new VariableDef[node.Parameters.Count];
                int index = 0;
                foreach (var param in node.Parameters) {
                    var variable = newParams[index++] = funcScope.AddLocatedVariable(param.Name, param, unit, param.Kind);
                    
                    if (param.IsList) {
                        variable.AddTypes(param, unit, new SequenceInfo(VariableDef.EmptyArray, outerUnit.ProjectState._tupleType));
                    } else if (param.IsDictionary) {
                        variable.AddTypes(param, unit, new DictionaryInfo(outerUnit.ProjectEntry));
                    }
                }

                function.SetParameters(newParams);
                unit.Enqueue();
            }
            return scope.Namespace as FunctionInfo;
        }

        public override bool Walk(GeneratorExpression node) {
            EnsureComprehensionScope(node, MakeGeneratorComprehensionScope);

            return base.Walk(node);
        }

        public override bool Walk(ListComprehension node) {
            // List comprehension runs in a new scope in 3.x, runs in the same
            // scope in 2.x.  But these don't get their own analysis units
            // because they are still just expressions.
            if (_curUnit.ProjectState.LanguageVersion.Is3x()) {
                // always a new scope for SetComprehension

                EnsureComprehensionScope(node, MakeListComprehensionScope);
            }

            return base.Walk(node);
        }

        public override void PostWalk(SetComprehension node) {
            // always a new scope for SetComprehension
            EnsureComprehensionScope(node, MakeSetComprehensionScope);

            base.PostWalk(node);
        }
        
        public override void PostWalk(DictionaryComprehension node) {
            EnsureComprehensionScope(node, MakeDictComprehensionScope);

            base.PostWalk(node);
        }

        /// <summary>
        /// Makes sure we create a scope for a comprehension (generator, set, dict, or list comprehension in 3.x) where
        /// the variables which are assigned will be stored.  
        /// </summary>
        private void EnsureComprehensionScope(Comprehension node, Func<Comprehension, InterpreterScope[], ComprehensionScope> makeScope) {
            InterpreterScope scope;
            if (!_curUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                var scopes = new InterpreterScope[_scopes.Count + 1];
                _scopes.CopyTo(scopes, 0);

                var compScope = makeScope(node, scopes);

                _curUnit.DeclaringModule.NodeScopes[node] = compScope;
                scopes[scopes.Length - 1] = compScope;

                var declScope = _curUnit.Scopes[_curUnit.Scopes.Length - 1];
                declScope.Children.Add(compScope);
            }
        }

        private ComprehensionScope MakeGeneratorComprehensionScope(Comprehension node, InterpreterScope[] scopes) {
            var unit = new GeneratorComprehensionAnalysisUnit(node, _entry.Tree, scopes, _curUnit);
            var generatorInfo = new GeneratorInfo(unit);
            var compScope = new ComprehensionScope(generatorInfo, node);
            unit.Enqueue();
            return compScope;
        }

        private ComprehensionScope MakeListComprehensionScope(Comprehension node, InterpreterScope[] scopes) {
            var unit = new ListComprehensionAnalysisUnit(node, _entry.Tree, scopes, _curUnit);
            var setInfo = new ListInfo(VariableDef.EmptyArray, _curUnit.ProjectState._listType);
            var compScope = new ComprehensionScope(setInfo, node);
            unit.Enqueue();
            return compScope;
        }

        private ComprehensionScope MakeSetComprehensionScope(Comprehension node, InterpreterScope[] scopes) {
            var unit = new SetComprehensionAnalysisUnit(node, _entry.Tree, scopes, _curUnit);
            var setInfo = new SetInfo(_curUnit.ProjectState);
            var compScope = new ComprehensionScope(setInfo, node);
            unit.Enqueue();
            return compScope;
        }

        private ComprehensionScope MakeDictComprehensionScope(Comprehension node, InterpreterScope[] scopes) {
            var unit = new DictionaryComprehensionAnalysisUnit(node, _entry.Tree, scopes, _curUnit);
            var dictInfo = new DictionaryInfo(_curUnit.ProjectEntry);
            var compScope = new ComprehensionScope(dictInfo, node);
            unit.Enqueue();
            return compScope;
        }

        private bool WalkMember(UserDefinedInfo userInfo) {
            if (userInfo != null) {
                _analysisStack.Push(_curUnit);
                _curUnit = userInfo._analysisUnit;
                _scopes.Push(userInfo._analysisUnit.Scopes[userInfo._analysisUnit.Scopes.Length - 1]);
                return true;
            }
            return false;
        }

        public override void PostWalk(FunctionDefinition node) {
            if (node.Body != null && node.Name != null) {
                InterpreterScope prevScope;
                do {
                    prevScope = _scopes.Pop();
                } while (prevScope.Node != node);                
                _curUnit = _analysisStack.Pop();
            }
        }

        private void UpdateChildRanges(Statement node) {
            var declScope = _curUnit.Scopes[_curUnit.Scopes.Length - 1];
            InterpreterScope prevScope = null;
            if (declScope.Children.Count > 0) {
                prevScope = declScope.Children[declScope.Children.Count - 1];
                StatementScope prevStmtScope = prevScope as StatementScope;
                if (prevStmtScope != null) {
                    prevStmtScope.EndIndex = node.EndIndex;
                } else {
                    IsInstanceScope prevInstanceScope = prevScope as IsInstanceScope;
                    if (prevInstanceScope != null) {
                        prevInstanceScope.EndIndex = node.EndIndex;
                    } else {
                        declScope.Children.Add(new StatementScope(node.StartIndex));
                    }
                }
            } else {
                declScope.Children.Add(new StatementScope(node.StartIndex));
            }
        }

        internal static KeyValuePair<string, Expression>[] GetIsInstanceNamesAndExpressions(Expression node) {
            List<KeyValuePair<string, Expression>> names = null;
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
        internal static void GetIsInstanceNamesAndExpressions(ref List<KeyValuePair<string, Expression>> names, Expression node) {
            CallExpression callExpr = node as CallExpression;
            if (callExpr != null) {
                NameExpression nameExpr = callExpr.Target as NameExpression;
                if (nameExpr != null && nameExpr.Name == "isinstance") {
                    if (callExpr.Args.Count == 2 && callExpr.Args[0].Expression is NameExpression) {
                        if (names == null) {
                            names = new List<KeyValuePair<string, Expression>>();
                        }
                        var name = ((NameExpression)callExpr.Args[0].Expression).Name;
                        var type = callExpr.Args[1].Expression;
                        names.Add(new KeyValuePair<string, Expression>(name, type));
                    }
                }
            }

            AndExpression andExpr = node as AndExpression;
            if (andExpr != null) {
                GetIsInstanceNamesAndExpressions(ref names, andExpr.Left);
                GetIsInstanceNamesAndExpressions(ref names, andExpr.Right);
            }
        }

        public override bool Walk(AssertStatement node)  {
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

        private void PushIsInstanceScope(Node node, KeyValuePair<string, Expression>[] isInstanceNames, SuiteStatement effectiveSuite) {
            InterpreterScope scope;
            if (!_curUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                scope = new IsInstanceScope(node.StartIndex, effectiveSuite);
                var declScope = _curUnit.Scopes[_curUnit.Scopes.Length - 1];
                declScope.Children.Add(scope);
                _scopes.Add(scope);
                _curUnit.DeclaringModule.NodeScopes[node] = scope;
            }
        }

        public override bool Walk(AssignmentStatement node) {
            UpdateChildRanges(node);
            foreach (var left in node.Left) {
                if (left is NameExpression) {
                    var nameExpr = ((NameExpression)left);
                    var variable = CreateVariable(nameExpr);
                    _scopes[_scopes.Count - 1].Variables[nameExpr.Name] = variable;                    
                }
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
                    var variable = CreateVariable(assignTo);
                    _scopes[_scopes.Count - 1].Variables[assignTo.Name] = variable;
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
            IsInstanceScope isInstanceScope = _scopes[_scopes.Count - 1] as IsInstanceScope;
            if (isInstanceScope != null && isInstanceScope._effectiveSuite == node) {
                // pop the isinstance scope
                _scopes.Pop();
                var declScope = _curUnit.Scopes[_curUnit.Scopes.Length - 1];
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
                var closingScope = new StatementScope(offset);
                _scopes.Add(closingScope);
                declScope.Children.Add(closingScope);
            }
            return false;
        }

        public override void PostWalk(SuiteStatement node) {
            base.PostWalk(node);
        }

    }
}
