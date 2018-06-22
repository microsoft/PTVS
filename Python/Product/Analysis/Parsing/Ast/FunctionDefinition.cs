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
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing.Ast {

    public class FunctionDefinition : ScopeStatement, IMaybeAsyncStatement {
        protected Statement _body;
        private readonly NameExpression/*!*/ _name;
        private readonly Parameter[] _parameters;
        private Expression _returnAnnotation;
        private DecoratorStatement _decorators;
        private bool _generator;                        // The function is a generator
        private bool _coroutine;
        private bool _isLambda;

        private PythonVariable _variable;               // The variable corresponding to the function name or null for lambdas
        internal PythonVariable _nameVariable;          // the variable that refers to the global __name__
        internal bool _hasReturn;
        private int _headerIndex;
        private int _defIndex;
        private int? _keywordEndIndex;

        internal static readonly object WhitespaceAfterAsync = new object();

        public FunctionDefinition(NameExpression name, Parameter[] parameters)
            : this(name, parameters, (Statement)null) {            
        }
        
        public FunctionDefinition(NameExpression name, Parameter[] parameters, Statement body, DecoratorStatement decorators = null) {
            if (name == null) {
                _name = new NameExpression("<lambda>");
                _isLambda = true;
            } else {
                _name = name;
            }

            _parameters = parameters;
            _body = body;
            _decorators = decorators;
        }

        public bool IsLambda {
            get {
                return _isLambda;
            }
        }

        public IList<Parameter> Parameters => _parameters;
        internal Parameter[] ParametersInternal => _parameters;

        internal override int ArgCount => _parameters.Length;

        internal void SetKeywordEndIndex(int index) => _keywordEndIndex = index;
        public override int KeywordEndIndex => _keywordEndIndex ?? (DefIndex + (IsCoroutine ? 9 : 3));
        public override int KeywordLength => KeywordEndIndex - StartIndex;

        public Expression ReturnAnnotation {
            get { return _returnAnnotation; }
            set { _returnAnnotation = value; }
        }

        public override Statement Body {
            get { return _body; }
        }

        internal void SetBody(Statement body) {
            _body = body;
        }

        public int HeaderIndex {
            get { return _headerIndex; }
            set { _headerIndex = value; }
        }

        public int DefIndex {
            get { return _defIndex; }
            set { _defIndex = value; }
        }

        public override string/*!*/ Name {
            get { return _name.Name ?? ""; }
        }

        public NameExpression NameExpression {
            get { return _name; }
        }

        public DecoratorStatement Decorators {
            get { return _decorators; }
            internal set { _decorators = value; }
        }

        internal LambdaExpression LambdaExpression { get; set; }

        /// <summary>
        /// True if the function is a generator.  Generators contain at least one yield
        /// expression and instead of returning a value when called they return a generator
        /// object which implements the iterator protocol.
        /// </summary>
        public bool IsGenerator {
            get { return _generator; }
            set { _generator = value; }
        }

        /// <summary>
        /// True if the function is a coroutine. Coroutines are defined using
        /// 'async def'.
        /// </summary>
        public bool IsCoroutine {
            get { return _coroutine; }
            set { _coroutine = value; }
        }

        bool IMaybeAsyncStatement.IsAsync => IsCoroutine;

        /// <summary>
        /// Gets the variable that this function is assigned to.
        /// </summary>
        public PythonVariable Variable {
            get { return _variable; }
            set { _variable = value; }
        }

        /// <summary>
        /// Gets the variable reference for the specific assignment to the variable for this function definition.
        /// </summary>
        public PythonReference GetVariableReference(PythonAst ast) {
            return GetVariableReference(this, ast);
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

        internal override PythonVariable BindReference(PythonNameBinder binder, string name) {
            PythonVariable variable;

            // First try variables local to this scope
            if (TryGetVariable(name, out variable) && variable.Kind != VariableKind.Nonlocal) {
                if (variable.Kind == VariableKind.Global) {
                    AddReferencedGlobal(name);
                }
                return variable;
            }

            // Try to bind in outer scopes
            for (ScopeStatement parent = Parent; parent != null; parent = parent.Parent) {
                if (parent.TryBindOuter(this, name, true, out variable)) {
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
                    "import * is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
            if (ContainsImportStar && Parent is FunctionDefinition) {
                binder.ReportSyntaxError(
                    "import * is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
            if (ContainsImportStar && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    "import * is not allowed in function '{0}' because it contains a nested function with free variables".FormatUI(Name),
                    this);
            }
            if (ContainsUnqualifiedExec && ContainsNestedFreeVariables) {
                binder.ReportSyntaxError(
                    "unqualified exec is not allowed in function '{0}' because it contains a nested function with free variables".FormatUI(Name),
                    this);
            }
            if (ContainsUnqualifiedExec && IsClosure) {
                binder.ReportSyntaxError(
                    "unqualified exec is not allowed in function '{0}' because it is a nested function".FormatUI(Name),
                    this);
            }
        }

        public int GetIndexOfDef(PythonAst ast) {
            if (!IsCoroutine) {
                return DefIndex;
            }
            return DefIndex + NodeAttributes.GetWhiteSpace(this, ast, WhitespaceAfterAsync).Length + 5;
        }

        public override void Walk(PythonWalker walker) {
            if (walker.Walk(this)) {
                _name?.Walk(walker);
                if (_parameters != null) {
                    foreach (Parameter p in _parameters) {
                        p.Walk(walker);
                    }
                }
                if (_decorators != null) {
                    _decorators.Walk(walker);
                }
                if (_body != null) {
                    _body.Walk(walker);
                }
                if (_returnAnnotation != null) {
                    _returnAnnotation.Walk(walker);
                }
            }
            walker.PostWalk(this);
        }

        public SourceLocation Header {
            get { return GlobalParent.IndexToLocation(_headerIndex); }
        }

        public override string GetLeadingWhiteSpace(PythonAst ast) {
            if (Decorators != null) {
                return Decorators.GetLeadingWhiteSpace(ast);
            }
            return base.GetLeadingWhiteSpace(ast);
        }

        public override void SetLeadingWhiteSpace(PythonAst ast, string whiteSpace) {
            if (Decorators != null) {
                Decorators.SetLeadingWhiteSpace(ast, whiteSpace);
                return;
            }
            base.SetLeadingWhiteSpace(ast, whiteSpace);
        }

        internal override void AppendCodeStringStmt(StringBuilder res, PythonAst ast, CodeFormattingOptions format) {
            Decorators?.AppendCodeString(res, ast, format);

            format.ReflowComment(res, this.GetPreceedingWhiteSpaceDefaultNull(ast));

            if (IsCoroutine) {
                res.Append("async");
                res.Append(NodeAttributes.GetWhiteSpace(this, ast, WhitespaceAfterAsync));
            }

            res.Append("def");
            var name = this.GetVerbatimImage(ast) ?? Name;
            if (!string.IsNullOrEmpty(name)) {
                res.Append(this.GetSecondWhiteSpace(ast));
                res.Append(name);
                if (!this.IsIncompleteNode(ast)) {
                    format.Append(
                        res, 
                        format.SpaceBeforeFunctionDeclarationParen, 
                        " ", 
                        "", 
                        this.GetThirdWhiteSpaceDefaultNull(ast)
                    );

                    res.Append('(');
                    if (ParametersInternal.Length != 0) {
                        var commaWhiteSpace = this.GetListWhiteSpace(ast);
                        ParamsToString(res,
                            ast,
                            commaWhiteSpace,
                            format,
                            format.SpaceWithinFunctionDeclarationParens != null ?
                                format.SpaceWithinFunctionDeclarationParens.Value ? " " : "" :
                                null
                        );
                    }

                    string namedOnly = this.GetExtraVerbatimText(ast);
                    if (namedOnly != null) {
                        res.Append(namedOnly);
                    }

                    format.Append(
                        res,
                        ParametersInternal.Length != 0 ? 
                            format.SpaceWithinFunctionDeclarationParens :
                            format.SpaceWithinEmptyParameterList,
                        " ",
                        "",
                        this.GetFourthWhiteSpaceDefaultNull(ast)
                    ); 

                    if (!this.IsMissingCloseGrouping(ast)) {
                        res.Append(')');
                    }

                    if (ReturnAnnotation != null) {
                        format.Append(
                            res,
                            format.SpaceAroundAnnotationArrow,
                            " ",
                            "",
                            this.GetFifthWhiteSpace(ast)
                        ); 
                        res.Append("->");
                        _returnAnnotation.AppendCodeString(
                            res, 
                            ast, 
                            format,
                            format.SpaceAroundAnnotationArrow != null ?
                                format.SpaceAroundAnnotationArrow.Value ? " " : "" :
                                null
                        );
                    }

                    Body?.AppendCodeString(res, ast, format);
                }
            }
        }

        internal void ParamsToString(StringBuilder res, PythonAst ast, string[] commaWhiteSpace, CodeFormattingOptions format, string initialLeadingWhiteSpace = null) {
            for (int i = 0; i < ParametersInternal.Length; i++) {
                if (i > 0) {
                    if (commaWhiteSpace != null) {
                        res.Append(commaWhiteSpace[i - 1]);
                    }
                    res.Append(',');
                }
                ParametersInternal[i].AppendCodeString(res, ast, format, initialLeadingWhiteSpace);
                initialLeadingWhiteSpace = null;
            }

            if (commaWhiteSpace != null && commaWhiteSpace.Length == ParametersInternal.Length && ParametersInternal.Length != 0) {
                // trailing comma
                res.Append(commaWhiteSpace[commaWhiteSpace.Length - 1]);
                res.Append(",");
            }
        }
    }
}
