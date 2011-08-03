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
using Microsoft.PythonTools.Analysis.Values;
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

        internal static ClassInfo AddClass(ClassDefinition node, AnalysisUnit outerUnit) {
            InterpreterScope scope;
            if (!outerUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var scopes = new InterpreterScope[outerUnit.Scopes.Length + 1];
                outerUnit.Scopes.CopyTo(scopes, 0);

                var unit = new ClassAnalysisUnit(node, scopes);
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
                _scopes.Pop();
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
                    var reference = name.GetVariableReference(_entry.Tree);
                    var declScope = reference.Variable.Scope;
                    foreach (var scope in _scopes) {
                        if (scope.Node == declScope || (declScope is PythonAst && scope == null)) {
                            _scopes[_scopes.Count - 1].Variables[name.Name] = scope.CreateVariable(node, _curUnit, name.Name, false);
                        }
                    }
                }
            }
            return false;
        }

        internal static FunctionInfo AddFunction(FunctionDefinition node, AnalysisUnit outerUnit) {
            InterpreterScope scope;
            if (!outerUnit.DeclaringModule.NodeScopes.TryGetValue(node, out scope)) {
                if (node.Body == null || node.Name == null) {
                    return null;
                }

                var scopes = new InterpreterScope[outerUnit.Scopes.Length + 1];
                outerUnit.Scopes.CopyTo(scopes, 0);

                var unit = new FunctionAnalysisUnit(node, scopes);
                var function = new FunctionInfo(unit);

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
                if (!node.IsLambda) {
                    declScope.Children.Add(funcScope);
                }
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
                    var variable = newParams[index++] = funcScope.AddLocatedVariable(param.Name, param, unit);
                    if (param.IsList) {
                        variable.AddTypes(param, unit, outerUnit.ProjectState._tupleType);
                    } else if (param.IsDictionary) {
                        variable.AddTypes(param, unit, outerUnit.ProjectState._dictType);
                    }
                }

                function.SetParameters(newParams);
                unit.Enqueue();
            }
            return scope.Namespace as FunctionInfo;
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
                _scopes.Pop();
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
                    prevStmtScope.EndNode = node;
                } else {
                    declScope.Children.Add(new StatementScope(node));
                }
            } else {
                declScope.Children.Add(new StatementScope(node));
            }
        }

        public override bool Walk(AssertStatement node) {
            UpdateChildRanges(node);
            return base.Walk(node);
        }

        public override bool Walk(AssignmentStatement node) {
            UpdateChildRanges(node);
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
            return base.Walk(node);
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
            return base.Walk(node);
        }
    }
}
