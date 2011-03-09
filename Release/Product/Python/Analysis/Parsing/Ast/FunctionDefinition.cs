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
using System.Threading;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class FunctionDefinition : ScopeStatement {
        protected Statement _body;
        private readonly string _name;
        private readonly Parameter[] _parameters;
        private Expression _returnAnnotation;
        private IList<Expression> _decorators;
        private bool _generator;                        // The function is a generator
        private bool _isLambda;

        private PythonVariable _variable;               // The variable corresponding to the function name or null for lambdas
        internal PythonVariable _nameVariable;          // the variable that refers to the global __name__
        internal bool _hasReturn;
        private int _headerIndex;

        private static int _lambdaId;

        public FunctionDefinition(string name, Parameter[] parameters)
            : this(name, parameters, (Statement)null) {            
        }

        
        public FunctionDefinition(string name, Parameter[] parameters, Statement body) {
            if (name == null) {
                _name = "<lambda$" + Interlocked.Increment(ref _lambdaId) + ">";
                _isLambda = true;
            } else {
                _name = name;
            }

            _parameters = parameters;
            _body = body;
        }

        public bool IsLambda {
            get {
                return _isLambda;
            }
        }

        public IList<Parameter> Parameters {
            get { return _parameters; }
        }

        internal override int ArgCount {
            get {
                return _parameters.Length;
            }
        }

        public Expression ReturnAnnotation {
            get { return _returnAnnotation; }
            set { _returnAnnotation = value; }
        }

        public Statement Body {
            get { return _body; }
            set { _body = value; }
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public override string Name {
            get { return _name; }
        }

        public IList<Expression> Decorators {
            get { return _decorators; }
            internal set { _decorators = value; }
        }

        public bool IsGenerator {
            get { return _generator; }
            set { _generator = value; }
        }

        internal PythonVariable PythonVariable {
            get { return _variable; }
            set { _variable = value; }
        }

        internal override bool ExposesLocalVariable(PythonVariable variable) {
            return NeedsLocalsDictionary; 
        }

        internal override bool TryBindOuter(ScopeStatement from, string name, bool allowGlobals, out PythonVariable variable) {
            // Functions expose their locals to direct access
            ContainsNestedFreeVariables = true;
            if (TryGetVariable(name, out variable)) {
                variable.AccessedInNestedScope = true;

                if (variable.Kind == VariableKind.Local || variable.Kind == VariableKind.Parameter) {
                    from.AddFreeVariable(variable, true);

                    for (ScopeStatement scope = from.Parent; scope != this; scope = scope.Parent) {
                        scope.AddFreeVariable(variable, false);
                    }

                    AddCellVariable(variable);
                } else if(allowGlobals) {
                    from.AddReferencedGlobal(name);
                }
                return true;
            }
            return false;
        }

        internal override PythonVariable BindReference(PythonNameBinder binder, PythonReference reference) {
            PythonVariable variable;

            // First try variables local to this scope
            if (TryGetVariable(reference.Name, out variable)) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(reference.Name);
                }
                return variable;
            }

            // Try to bind in outer scopes
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, reference.Name, true, out variable)) {
                    return variable;
                }
            }

            return null;
        }


        internal override void Bind(PythonNameBinder binder) {
            base.Bind(binder);
            Verify(binder);
        }
        
        private void Verify(PythonNameBinder binder) {
            if (ContainsImportStar && IsClosure) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
            if (ContainsImportStar && Parent is FunctionDefinition) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
            if (ContainsImportStar && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "import * is not allowed in function '{0}' because it contains a nested function with free variables",
                        Name),
                    this);
            }
            if (ContainsUnqualifiedExec && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables",
                        Name),
                    this);
            }
            if (ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                    String.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "unqualified exec is not allowed in function '{0}' because it is a nested function",
                        Name),
                    this);
            }
        }
        
        internal override string ScopeDocumentation {
            get {
                return GetDocumentation(_body);
            }
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                if (_parameters != null) {
                    foreach (Parameter p in _parameters) {
                        p.Walk(walker);
                    }
                }
                if (_decorators != null) {
                    foreach (Expression decorator in _decorators) {
                        decorator.Walk(walker);
                    }
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(_headerIndex); }
        }
    }
}
