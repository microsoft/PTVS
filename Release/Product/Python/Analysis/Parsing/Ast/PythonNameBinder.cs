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


/*
 * The name binding:
 *
 * The name binding happens in 2 passes.
 * In the first pass (full recursive walk of the AST) we resolve locals.
 * The second pass uses the "processed" list of all context statements (functions and class
 * bodies) and has each context statement resolve its free variables to determine whether
 * they are globals or references to lexically enclosing scopes.
 *
 * The second pass happens in post-order (the context statement is added into the "processed"
 * list after processing its nested functions/statements). This way, when the function is
 * processing its free variables, it also knows already which of its locals are being lifted
 * to the closure and can report error if such closure variable is being deleted.
 *
 * This is illegal in Python:
 *
 * def f():
 *     x = 10
 *     if (cond): del x        # illegal because x is a closure variable
 *     def g():
 *         print x
 */

namespace Microsoft.PythonTools.Parsing.Ast {
    class DefineBinder : PythonWalkerNonRecursive {
        private PythonNameBinder _binder;
        public DefineBinder(PythonNameBinder binder) {
            _binder = binder;
        }
        public override bool Walk(NameExpression node) {
            if (node.Name != null) {
                _binder.DefineName(node.Name);
            }
            return false;
        }
        public override bool Walk(ParenthesisExpression node) {
            return true;
        }
        public override bool Walk(TupleExpression node) {
            return true;
        }
        public override bool Walk(ListExpression node) {
            return true;
        }
    }

    class ParameterBinder : PythonWalkerNonRecursive {
        private PythonNameBinder _binder;
        public ParameterBinder(PythonNameBinder binder) {
            _binder = binder;
        }

        public override bool Walk(Parameter node) {
#if NAME_BINDING
            node.PythonVariable = 
#endif
            _binder.DefineParameter(node.Name);
            return false;
        }
        public override bool Walk(SublistParameter node) {
#if NAME_BINDING
            node.PythonVariable = 
#endif
            _binder.DefineParameter(node.Name);
            // we walk the node by hand to avoid walking the default values.
            WalkTuple(node.Tuple);
            return false;
        }

        private void WalkTuple(TupleExpression tuple) {
            foreach (Expression innerNode in tuple.Items) {
                NameExpression name = innerNode as NameExpression;
                if (name != null) {
                    _binder.DefineName(name.Name);
#if NAME_BINDING
                    name.Reference = 
#endif
                    _binder.Reference(name.Name);
                } else if (innerNode is TupleExpression) {                    
                    WalkTuple((TupleExpression)innerNode);
                }
            }
        }
        public override bool Walk(TupleExpression node) {
            return true;
        }
    }

    class DeleteBinder : PythonWalkerNonRecursive {
        private PythonNameBinder _binder;
        public DeleteBinder(PythonNameBinder binder) {
            _binder = binder;
        }
        public override bool Walk(NameExpression node) {
            _binder.DefineDeleted(node.Name);
            return false;
        }
    }

    class PythonNameBinder : PythonWalker {
        private PythonAst _globalScope;
        internal ScopeStatement _currentScope;
        private readonly PythonAst _ast;
        private readonly PythonLanguageVersion _langVersion;
        private List<ScopeStatement> _scopes = new List<ScopeStatement>();
        private List<int> _finallyCount = new List<int>();

        #region Recursive binders

        private DefineBinder _define;
        private DeleteBinder _delete;
        private ParameterBinder _parameter;

        #endregion

        private readonly ErrorSink _errorSink;

        private PythonNameBinder(PythonLanguageVersion langVersion, PythonAst ast, ErrorSink context) {
            _ast = ast;
            _define = new DefineBinder(this);
            _delete = new DeleteBinder(this);
            _parameter = new ParameterBinder(this);
            _errorSink = context;
            _langVersion = langVersion;
        }

        #region Public surface

        internal static void BindAst(PythonLanguageVersion langVersion, PythonAst ast, ErrorSink context) {
            PythonNameBinder binder = new PythonNameBinder(langVersion, ast, context);
            binder.Bind(ast);
        }

        #endregion

        public PythonLanguageVersion LanguageVersion {
            get {
                return _langVersion;
            }
        }

        private void Bind(PythonAst unboundAst) {
            _currentScope = _globalScope = unboundAst;
            _finallyCount.Add(0);

            // Find all scopes and variables
            unboundAst.Walk(this);

            // Bind
            foreach (ScopeStatement scope in _scopes) {
                scope.Bind(this);
            }

            // Finish the globals
            unboundAst.Bind(this);

            // Finish Binding w/ outer most scopes first.
            for (int i = _scopes.Count - 1; i >= 0; i--) {
                _scopes[i].FinishBind(this);
            }

            // Finish the globals
            unboundAst.FinishBind(this);

            // Run flow checker
            foreach (ScopeStatement scope in _scopes) {
                FlowChecker.Check(scope);
            }
        }

        private void PushScope(ScopeStatement node) {
            node.Parent = _currentScope;
            _currentScope = node;
            _finallyCount.Add(0);
        }

        private void PopScope() {
            _scopes.Add(_currentScope);
            _currentScope = _currentScope.Parent;
            _finallyCount.RemoveAt(_finallyCount.Count - 1);
        }

        internal PythonReference Reference(string/*!*/ name) {
            return _currentScope.Reference(name);
        }

        internal PythonVariable DefineName(string/*!*/ name) {
            return _currentScope.EnsureVariable(name);
        }

        internal PythonVariable DefineParameter(string/*!*/ name) {
            return _currentScope.DefineParameter(name);
        }

        internal PythonVariable DefineDeleted(string/*!*/ name) {
            PythonVariable variable = _currentScope.EnsureVariable(name);
            variable.Deleted = true;
            return variable;
        }

        internal void ReportSyntaxWarning(string message, Node node) {
            _errorSink.Add(message, _ast._lineLocations, node.StartIndex, node.EndIndex, ErrorCodes.SyntaxError, Severity.Warning);
        }

        internal void ReportSyntaxError(string message, Node node) {
            _errorSink.Add(message, _ast._lineLocations, node.StartIndex, node.EndIndex, ErrorCodes.SyntaxError, Severity.FatalError);
        }

        #region AstBinder Overrides

        // AssignmentStatement
        public override bool Walk(AssignmentStatement node) {
            foreach (Expression e in node.Left) {
                e.Walk(_define);
            }
            return true;
        }

        public override bool Walk(AugmentedAssignStatement node) {
            node.Left.Walk(_define);
            return true;
        }

        public override void PostWalk(CallExpression node) {
            if (node.NeedsLocalsDictionary()) {
                _currentScope.NeedsLocalsDictionary = true;
            }
        }

        // ClassDefinition
        public override bool Walk(ClassDefinition node) {
            if (node.Name != null) {
                node.PythonVariable = DefineName(node.Name);
            }

            if (node.Bases != null) {
                // Base references are in the outer context
                foreach (var b in node.Bases) b.Expression.Walk(this);
            }

            // process the decorators in the outer context
            if (node.Decorators != null) {
                foreach (Expression dec in node.Decorators) {
                    dec.Walk(this);
                }
            }
            
            PushScope(node);

            node.ModuleNameVariable = _globalScope.EnsureGlobalVariable("__name__");

            // define the __doc__ and the __module__
            if (node.Body.Documentation != null) {
                node.DocVariable = DefineName("__doc__");
            }
            node.ModVariable = DefineName("__module__");

            // Walk the body
            node.Body.Walk(this);
            return false;
        }

        // ClassDefinition
        public override void PostWalk(ClassDefinition node) {
            Debug.Assert(node == _currentScope);
            PopScope();
        }

        // DelStatement
        public override bool Walk(DelStatement node) {
            foreach (Expression e in node.Expressions) {
                e.Walk(_delete);
            }
            return true;
        }

        // ExecStatement
        public override bool Walk(ExecStatement node) {
            if (node.Locals == null && node.Globals == null) {
                Debug.Assert(_currentScope != null);
                _currentScope.ContainsUnqualifiedExec = true;
            }
            return true;
        }

        public override void PostWalk(ExecStatement node) {
            if (node.NeedsLocalsDictionary()) {
                _currentScope.NeedsLocalsDictionary = true;
            }

            if (node.Locals == null) {
                _currentScope.HasLateBoundVariableSets = true;
            }
        }

        // ForEachStatement
        public override bool Walk(ForStatement node) {
            // we only push the loop for the body of the loop
            // so we need to walk the for statement ourselves
            node.Left.Walk(_define);

            if (node.Left != null) {
                node.Left.Walk(this);
            }
            if (node.List != null) {
                node.List.Walk(this);
            }
            
            if (node.Body != null) {
                node.Body.Walk(this);
            }
            
            if (node.Else != null) {
                node.Else.Walk(this);
            }

            return false;
        }

        public override bool Walk(WhileStatement node) {
            // we only push the loop for the body of the loop
            // so we need to walk the while statement ourselves
            if (node.Test != null) {
                node.Test.Walk(this);
            }
            
            if (node.Body != null) {
                node.Body.Walk(this);
            }

            if (node.ElseStatement != null) {
                node.ElseStatement.Walk(this);
            }
            
            return false;
        }

        public override bool Walk(ReturnStatement node) {
            FunctionDefinition funcDef = _currentScope as FunctionDefinition;
            if (funcDef != null) {
                funcDef._hasReturn = true;
            }
            return base.Walk(node);
        }

        // WithStatement
        public override bool Walk(WithStatement node) {
            _currentScope.ContainsExceptionHandling = true;

            for (int i = 0; i < node.Items.Count; i++) {
                if (node.Items[i].Variable != null) {
                    node.Items[i].Variable.Walk(_define);
                }
            }
            return true;
        }

        // FromImportStatement
        public override bool Walk(FromImportStatement node) {
            if (node.Names != FromImportStatement.Star) {
                PythonVariable[] variables = new PythonVariable[node.Names.Count];
                for (int i = 0; i < node.Names.Count; i++) {
                    variables[i] = DefineName(node.AsNames[i] ?? node.Names[i]);
                }
                node.Variables = variables;
            } else {
                Debug.Assert(_currentScope != null);
                _currentScope.ContainsImportStar = true;
                _currentScope.NeedsLocalsDictionary = true;
                _currentScope.HasLateBoundVariableSets = true;
            }
            return true;
        }

        // FunctionDefinition
        public override bool Walk(FunctionDefinition node) {
            node._nameVariable = _globalScope.EnsureGlobalVariable("__name__");
            
            // Name is defined in the enclosing context
            if (!node.IsLambda && !node.IsGenerator) {
                node.PythonVariable = DefineName(node.Name);
            }
            
            // process the default arg values in the outer context
            foreach (Parameter p in node.Parameters) {
                if (p.DefaultValue != null) {
                    p.DefaultValue.Walk(this);
                }
            }
            // process the decorators in the outer context
            if (node.Decorators != null) {
                foreach (Expression dec in node.Decorators) {
                    dec.Walk(this);
                }
            }

            PushScope(node);

            foreach (Parameter p in node.Parameters) {
                p.Walk(_parameter);
            }

            if (node.Body != null) {
                node.Body.Walk(this);
            }
            return false;
        }

        // FunctionDefinition
        public override void PostWalk(FunctionDefinition node) {
            Debug.Assert(_currentScope == node);
            PopScope();
        }

        // GlobalStatement
        public override bool Walk(GlobalStatement node) {
            foreach (string n in node.Names) {
                PythonVariable conflict;
                // Check current scope for conflicting variable
                bool assignedGlobal = false;
                if (_currentScope.TryGetVariable(n, out conflict)) {
                    // conflict?
                    switch (conflict.Kind) {
                        case VariableKind.Global:
                        case VariableKind.Local:
                            assignedGlobal = true;
                            ReportSyntaxWarning(
                                String.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "name '{0}' is assigned to before global declaration",
                                    n
                                ),
                                node
                            );
                            break;
                        
                        case VariableKind.Parameter:
                            ReportSyntaxError(
                                String.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "Name '{0}' is a function parameter and declared global",
                                    n),
                                node);
                            break;
                    }
                }

                // Check for the name being referenced previously. If it has been, issue warning.
                if (_currentScope.IsReferenced(n) && !assignedGlobal) {
                    ReportSyntaxWarning(
                        String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "name '{0}' is used prior to global declaration",
                        n),
                    node);
                }


                // Create the variable in the global context and mark it as global
                PythonVariable variable = _globalScope.EnsureGlobalVariable(n);
                variable.Kind = VariableKind.Global;

                if (conflict == null) {
                    // no previously definied variables, add it to the current scope
                    _currentScope.AddVariable(variable);
                }
            }
            return true;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (string n in node.Names) {
                PythonVariable conflict;
                // Check current scope for conflicting variable
                bool assignedLocal = false;
                if (_currentScope.TryGetVariable(n, out conflict)) {
                    // conflict?
                    switch (conflict.Kind) {
                        case VariableKind.Global:
                            ReportSyntaxError(String.Format("name '{0}' is nonlocal and global", n), node);
                            break;
                        case VariableKind.Local:
                            assignedLocal = true;
                            ReportSyntaxWarning(
                                String.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "name '{0}' is assigned to before nonlocal declaration",
                                    n
                                ),
                                node
                            );
                            break;
                        case VariableKind.Parameter:
                            ReportSyntaxError(
                                String.Format(
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    "name '{0}' is a parameter and nonlocal",
                                    n),
                                node);
                            break;
                    }
                }

                // Check for the name being referenced previously. If it has been, issue warning.
                if (_currentScope.IsReferenced(n) && !assignedLocal) {
                    ReportSyntaxWarning(
                        String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "name '{0}' is used prior to nonlocal declaration",
                        n),
                    node);
                }

                _currentScope.AddNonLocalVariable(n);
            }
            return true;
        }

        public override bool Walk(NameExpression node) {
#if NAME_BINDING
            node.Reference = 
#endif
            Reference(node.Name);
            
            return true;
        }

        public override bool Walk(PrintStatement node) {
            return base.Walk(node);
        }

        public override bool Walk(IfStatement node) {
            return base.Walk(node);
        }

        public override bool Walk(AssertStatement node) {
            return base.Walk(node);
        }        

        // PythonAst
        public override bool Walk(PythonAst node) {
            return true;
        }

        // PythonAst
        public override void PostWalk(PythonAst node) {
            // Do not add the global suite to the list of processed nodes,
            // the publishing must be done after the class local binding.
            Debug.Assert(_currentScope == node);
            _currentScope = _currentScope.Parent;
            _finallyCount.RemoveAt(_finallyCount.Count - 1);
        }

        // ImportStatement
        public override bool Walk(ImportStatement node) {
            PythonVariable[] variables = new PythonVariable[node.Names.Count];
            for (int i = 0; i < node.Names.Count; i++) {
                string name = node.AsNames[i] != null ? node.AsNames[i] : node.Names[i].Names[0];
                if (name != null) {
                    variables[i] = DefineName(name);
                }
            }
            node.Variables = variables;
            return true;
        }

        // TryStatement
        public override bool Walk(TryStatement node) {
            // we manually walk the TryStatement so we can track finally blocks.
            _currentScope.ContainsExceptionHandling = true;

            node.Body.Walk(this);

            if (node.Handlers != null) {
                foreach (TryStatementHandler tsh in node.Handlers) {
                    if (tsh.Target != null) {
                        tsh.Target.Walk(_define);
                    }
                    tsh.Walk(this);
                }
            }

            if (node.Else != null) {
                node.Else.Walk(this);
            }

            if (node.Finally != null) {
                _finallyCount[_finallyCount.Count - 1]++;
                node.Finally.Walk(this);
                _finallyCount[_finallyCount.Count - 1]--;
            }

            return false;
        }

        // ListComprehensionFor
        public override bool Walk(ComprehensionFor node) {
            node.Left.Walk(_define);
            return true;
        }

        #endregion
    }
}
