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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    /// <summary>
    /// Provides analysis of a function called with a specific set of arguments.  We analyze each function
    /// with each unique set of arguments (the cartesian product of the arguments).
    /// 
    /// It's possible that we still need to perform the analysis multiple times which can occur 
    /// if we take a dependency on something which later gets updated.
    /// </summary>
    class FunctionAnalysisUnit : AnalysisUnit {
        private readonly FunctionAnalysisUnit _originalUnit;
        public readonly FunctionInfo Function;

        public readonly CallChain CallChain;
        private readonly AnalysisUnit _declUnit;
        private readonly Dictionary<Node, Expression> _decoratorCalls;

        internal FunctionAnalysisUnit(FunctionInfo function, AnalysisUnit declUnit, InterpreterScope declScope)
            : base(function.FunctionDefinition, null) {
            _declUnit = declUnit;
            Function = function;
            _decoratorCalls = new Dictionary<Node, Expression>();

            var scope = new FunctionScope(Function, Function.FunctionDefinition, declScope);
            scope.EnsureParameters(this, null);
            _scope = scope;

            AnalysisLog.NewUnit(this);
        }

        public FunctionAnalysisUnit(FunctionAnalysisUnit originalUnit, CallChain callChain, ArgumentSet callArgs)
            : base(originalUnit.Ast, null) {
            _originalUnit = originalUnit;
            _declUnit = originalUnit._declUnit;
            Function = originalUnit.Function;
            _decoratorCalls = originalUnit._decoratorCalls;

            CallChain = callChain;

            var scope = new FunctionScope(Function, Ast, originalUnit.Scope.OuterScope);
            scope.UpdateParameters(this, callArgs, false, originalUnit.Scope as FunctionScope);
            _scope = scope;

            var walker = new OverviewWalker(originalUnit.ProjectEntry, this);
            if (Ast.Body != null) {
                Ast.Body.Walk(walker);
            }

            AnalysisLog.NewUnit(this);
            Enqueue();
        }

        internal bool UpdateParameters(ArgumentSet callArgs, bool enqueue = true) {
            var defScope = _originalUnit != null ? _originalUnit.Scope as FunctionScope : null;
            return ((FunctionScope)Scope).UpdateParameters(this, callArgs, enqueue, defScope);
        }

        protected override ModuleInfo GetDeclaringModule() {
            return base.GetDeclaringModule() ?? _declUnit.DeclaringModule;
        }

        protected override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            // Resolve default parameters and decorators in the outer scope but
            // continue to associate changes with this unit.
            ddg.Scope = _declUnit.Scope;
            AnalyzeDefaultParameters(ddg);
            ProcessFunctionDecorators(ddg);

            // Set the scope to within the function
            ddg.Scope = Scope;

            Ast.Body.Walk(ddg);
        }


        public new FunctionDefinition Ast {
            get {
                return (FunctionDefinition)base.Ast;
            }
        }

        public VariableDef ReturnValue {
            get {
                return ((FunctionScope)Scope).ReturnValue;
            }
        }

        internal void ProcessFunctionDecorators(DDG ddg) {
            if (Ast.Decorators != null) {
                var types = Function.SelfSet;
                Expression expr = Ast.NameExpression;

                foreach (var d in Ast.Decorators.Decorators) {
                    if (d != null) {
                        var decorator = ddg._eval.Evaluate(d);

                        if (decorator.Contains(ProjectState._propertyObj)) {
                            Function.IsProperty = true;
                        } else if (decorator.Contains(ProjectState._staticmethodObj)) {
                            // TODO: Warn if IsClassMethod is set
                            Function.IsStatic = true;
                        } else if (decorator.Contains(ProjectState._classmethodObj)) {
                            // TODO: Warn if IsStatic is set
                            Function.IsClassMethod = true;
                        } else {
                            Expression nextExpr;
                            if (!_decoratorCalls.TryGetValue(d, out nextExpr)) {
                                nextExpr = _decoratorCalls[d] = new CallExpression(d, new[] { new Arg(expr) });
                            }
                            expr = nextExpr;
                            types = decorator.Call(expr, this, new[] { types }, ExpressionEvaluator.EmptyNames);
                        }
                    }
                }

                ddg.Scope.AddLocatedVariable(Ast.Name, Ast.NameExpression, this).AddTypes(this, types);
            }

            if (!Function.IsStatic && Ast.Parameters.Count > 0) {
                VariableDef param;
                INamespaceSet firstParam;
                var clsScope = ddg.Scope as ClassScope;
                if (clsScope == null) {
                    firstParam = Function.IsClassMethod ? ProjectState._typeObj.SelfSet : NamespaceSet.Empty;
                } else {
                    firstParam = Function.IsClassMethod ? clsScope.Class.SelfSet : clsScope.Class.Instance.SelfSet;
                }

                if (Scope.Variables.TryGetValue(Ast.Parameters[0].Name, out param)) {
                    param.AddTypes(this, firstParam, false);
                }
            }
        }

        internal void AnalyzeDefaultParameters(DDG ddg) {
            VariableDef param;
            for (int i = 0; i < Ast.Parameters.Count; ++i) {
                var p = Ast.Parameters[i];
                ddg._eval.EvaluateMaybeNull(p.Annotation);

                if (p.DefaultValue != null && p.Kind != ParameterKind.List && p.Kind != ParameterKind.Dictionary &&
                    Scope.Variables.TryGetValue(p.Name, out param)) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        param.AddTypes(this, val, false);
                    }
                }
            }
            ddg._eval.EvaluateMaybeNull(Ast.ReturnAnnotation);
        }

        public override string ToString() {
            return string.Format("{0}{1}({2})->{3}",
                base.ToString(),
                _originalUnit == null ? " def:" : "",
                string.Join(", ", Ast.Parameters.Select(p => Scope.Variables[p.Name].TypesNoCopy.ToString())),
                ((FunctionScope)Scope).ReturnValue.TypesNoCopy.ToString()
            );
        }
    }

}
