// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.PythonTools.Analysis.Infrastructure;


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
            node.AddVariable(_binder._globalScope, _binder._bindRefs, _binder.DefineParameter(node.Name));
            return false;
        }
        public override bool Walk(SublistParameter node) {
            node.AddVariable(_binder._globalScope, _binder._bindRefs, _binder.DefineParameter(node.Name));
            // we walk the node by hand to avoid walking the default values.
            WalkTuple(node.Tuple);
            return false;
        }

        private void WalkTuple(TupleExpression tuple) {
            foreach (Expression innerNode in tuple.Items) {
                NameExpression name = innerNode as NameExpression;
                if (name != null) {
                    _binder.DefineName(name.Name);
                    name.AddVariableReference(_binder._globalScope, _binder._bindRefs, _binder.Reference(name.Name));
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
        internal PythonAst _globalScope;
        internal ScopeStatement _currentScope;
        private readonly PythonAst _ast;
        private readonly PythonLanguageVersion _langVersion;
        private List<ScopeStatement> _scopes = new List<ScopeStatement>();
        private List<int> _finallyCount = new List<int>();
        internal readonly bool _bindRefs;

        #region Recursive binders

        private DefineBinder _define;
        private DeleteBinder _delete;
        private ParameterBinder _parameter;

        #endregion

        private readonly ErrorSink _errorSink;

        private PythonNameBinder(PythonLanguageVersion langVersion, PythonAst ast, ErrorSink context, bool bindReferences) {
            _ast = ast;
            _define = new DefineBinder(this);
            _delete = new DeleteBinder(this);
            _parameter = new ParameterBinder(this);
            _errorSink = context;
            _langVersion = langVersion;
            _bindRefs = bindReferences;
        }

        #region Public surface

        internal static void BindAst(PythonLanguageVersion langVersion, PythonAst ast, ErrorSink context, bool bindReferences) {
            PythonNameBinder binder = new PythonNameBinder(langVersion, ast, context, bindReferences);
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
                node.Variable = DefineName(node.Name);
                node.AddVariableReference(_globalScope, _bindRefs, Reference(node.Name));
            }

            if (node.BasesInternal != null) {
                // Base references are in the outer context
                foreach (var b in node.BasesInternal) b.Expression.Walk(this);
            }

            // process the decorators in the outer context
            if (node.Decorators != null) {
                foreach (Expression dec in node.Decorators.DecoratorsInternal) {
                    if (dec != null) {
                        dec.Walk(this);
                    }
                }
            }
            
            PushScope(node);

            node.ModuleNameVariable = _globalScope.EnsureGlobalVariable("__name__");

            // define the __doc__ and the __module__
            if (node.Body.Documentation != null) {
                node.DocVariable = DefineName("__doc__");
            }
            node.ModVariable = DefineName("__module__");
            if (_langVersion.Is3x()) {
                node.ClassVariable = DefineName("__class__");
            }

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
            if (node.Names.Count != 1 || node.Names[0].Name !="*") {
                PythonVariable[] variables = new PythonVariable[node.Names.Count];
                PythonReference[] references = null;
                if (_bindRefs) {
                    references = new PythonReference[node.Names.Count];
                }
                for (int i = 0; i < node.Names.Count; i++) {
                    variables[i] = DefineName(node.AsNames[i] != null ? node.AsNames[i].Name : node.Names[i].Name);
                    if (references != null) {
                        references[i] = Reference(variables[i].Name);
                    }
                }
                node.Variables = variables;
                node.AddVariableReference(_ast, _bindRefs, references);
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
            if (!node.IsLambda) {
                node.Variable = DefineName(node.Name);
                node.AddVariableReference(_globalScope, _bindRefs, Reference(node.Name));
            }
            
            // process the default arg values and annotations in the outer
            // context
            foreach (Parameter p in node.ParametersInternal) {
                p.DefaultValue?.Walk(this);
                p.Annotation?.Walk(this);
            }
            // process the decorators in the outer context
            if (node.Decorators != null) {
                foreach (var dec in node.Decorators.DecoratorsInternal) {
                    dec?.Walk(this);
                }
            }
            // process the return annotation in the outer context
            node.ReturnAnnotation?.Walk(this);

            PushScope(node);

            foreach (var p in node.ParametersInternal) {
                p.Walk(_parameter);
            }

            node.Body?.Walk(this);
            return false;
        }

        // FunctionDefinition
        public override void PostWalk(FunctionDefinition node) {
            Debug.Assert(_currentScope == node);
            PopScope();
        }

        // GlobalStatement
        public override bool Walk(GlobalStatement node) {
            foreach (NameExpression nameNode in node.Names) {
                string n = nameNode.Name;
                if (n == null) {
                    continue;
                }

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
                                "name '{0}' is assigned to before global declaration".FormatUI(n),
                                node
                            );
                            break;
                        
                        case VariableKind.Parameter:
                            ReportSyntaxError(
                                "Name '{0}' is a function parameter and declared global".FormatUI(n),
                                node);
                            break;
                    }
                }

                // Check for the name being referenced previously. If it has been, issue warning.
                if (_currentScope.IsReferenced(n) && !assignedGlobal) {
                    ReportSyntaxWarning(
                        "name '{0}' is used prior to global declaration".FormatUI(n),
                        node);
                }


                // Create the variable in the global context and mark it as global
                PythonVariable variable = _globalScope.EnsureGlobalVariable(n);
                variable.Kind = VariableKind.Global;

                if (conflict == null) {
                    // no previously definied variables, add it to the current scope
                    _currentScope.AddVariable(variable);
                }
                
                nameNode.AddVariableReference(_globalScope, _bindRefs, Reference(n));
            }
            return true;
        }

        public override bool Walk(NonlocalStatement node) {
            foreach (NameExpression nameNode in node.Names) {
                string n = nameNode.Name;
                if (n == null) {
                    continue;
                }

                PythonVariable conflict;
                // Check current scope for conflicting variable
                bool assignedLocal = false;
                if (_currentScope.TryGetVariable(n, out conflict)) {
                    // conflict?
                    switch (conflict.Kind) {
                        case VariableKind.Global:
                            ReportSyntaxError("name '{0}' is nonlocal and global".FormatUI(n), node);
                            break;
                        case VariableKind.Local:
                            assignedLocal = true;
                            ReportSyntaxWarning(
                                "name '{0}' is assigned to before nonlocal declaration".FormatUI(n),
                                node
                            );
                            break;
                        case VariableKind.Parameter:
                            ReportSyntaxError(
                                "name '{0}' is a parameter and nonlocal".FormatUI(n),
                                node);
                            break;
                    }
                }

                // Check for the name being referenced previously. If it has been, issue warning.
                if (_currentScope.IsReferenced(n) && !assignedLocal) {
                    ReportSyntaxWarning(
                        "name '{0}' is used prior to nonlocal declaration".FormatUI(n),
                        node);
                }


                if (conflict == null) {
                    // no previously definied variables, add it to the current scope
                    _currentScope.CreateVariable(n, VariableKind.Nonlocal);
                }
                _currentScope.AddNonLocalVariable(nameNode);
                nameNode.AddVariableReference(_globalScope, _bindRefs, Reference(n));
            }
            return true;
        }

        public override bool Walk(NameExpression node) {
            node.AddVariableReference(_globalScope, _bindRefs, Reference(node.Name));
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
            PythonReference[] references = null;
            if (_bindRefs) {
                references = new PythonReference[variables.Length];
            }
            for (int i = 0; i < node.Names.Count; i++) {
                string name;
                if(node.AsNames[i] != null) {
                    name = node.AsNames[i].Name;
                } else if (node.Names[i].Names.Count > 0) {
                    name = node.Names[i].Names[0].Name;
                } else {
                    name = null;
                }
                if (name != null) {
                    variables[i] = DefineName(name);
                    if (references != null) {
                        references[i] = Reference(name);
                    }
                }
            }
            node.Variables = variables;
            node.AddVariableReference(_ast, _bindRefs, references);
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
