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

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    /// <summary>
    /// Provides analysis of a function called with a specific set of arguments.  We analyze each function
    /// with each unique set of arguments (the cartesian product of the arguments).
    /// 
    /// It's possible that we still need to perform the analysis multiple times which can occur 
    /// if we take a dependency on something which later gets updated.
    /// </summary>
    class FunctionAnalysisUnit : AnalysisUnit {
        public readonly FunctionInfo Function;

        internal readonly AnalysisUnit _declUnit;
        private readonly Dictionary<Node, Expression> _decoratorCalls;

        internal FunctionAnalysisUnit(
            FunctionInfo function,
            AnalysisUnit declUnit,
            InterpreterScope declScope,
            IPythonProjectEntry declEntry
        )
            : base(function.FunctionDefinition, null) {
            _declUnit = declUnit;
            Function = function;
            _decoratorCalls = new Dictionary<Node, Expression>();

            var scope = new FunctionScope(Function, Function.FunctionDefinition, declScope, declEntry);
            scope.EnsureParameters(this);
            _scope = scope;

            AnalysisLog.NewUnit(this);
        }

        internal FunctionAnalysisUnit(FunctionInfo function, AnalysisUnit declUnit) : base(function.FunctionDefinition, null)  {
            _declUnit = declUnit;
            Function = function;
        }
            
        internal virtual bool UpdateParameters(ArgumentSet callArgs, bool enqueue = true) {
            return ((FunctionScope)Scope).UpdateParameters(this, callArgs, enqueue, null);
        }

        internal void AddNamedParameterReferences(AnalysisUnit caller, NameExpression[] names) {
            ((FunctionScope)Scope).AddParameterReferences(caller, names);
        }

        internal override ModuleInfo GetDeclaringModule() {
            return base.GetDeclaringModule() ?? _declUnit.DeclaringModule;
        }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            // Resolve default parameters and decorators in the outer scope but
            // continue to associate changes with this unit.
            ddg.Scope = _declUnit.Scope;
            AnalyzeDefaultParameters(ddg);
            if (_decoratorCalls != null) {
                ProcessFunctionDecorators(ddg);
            }

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

                        if (decorator.Contains(ProjectState.ClassInfos[BuiltinTypeId.Property])) {
                            Function.IsProperty = true;
                        } else if (decorator.Contains(ProjectState.ClassInfos[BuiltinTypeId.StaticMethod])) {
                            // TODO: Warn if IsClassMethod is set
                            Function.IsStatic = true;
                        } else if (decorator.Contains(ProjectState.ClassInfos[BuiltinTypeId.ClassMethod])) {
                            // TODO: Warn if IsStatic is set
                            Function.IsClassMethod = true;
                        } else {
                            Expression nextExpr;
                            if (!_decoratorCalls.TryGetValue(d, out nextExpr)) {
                                nextExpr = _decoratorCalls[d] = new CallExpression(d, new[] { new Arg(expr) });
                            }
                            expr = nextExpr;
                            var decorated = AnalysisSet.Empty;
                            foreach (var ns in decorator) {
                                var fd = ns as FunctionInfo;
                                if (fd != null && Scope.EnumerateTowardsGlobal.Any(s => s.AnalysisValue == fd)) {
                                    continue;
                                }
                                decorated = decorated.Union(ns.Call(expr, this, new[] { types }, ExpressionEvaluator.EmptyNames));
                            }

                            // If processing decorators, update the current
                            // function type. Otherwise, we are acting as if
                            // each decorator returns the function unmodified.
                            if (ddg.ProjectState.Limits.ProcessCustomDecorators) {
                                types = decorated;
                            }
                        }
                    }
                }

                ddg.Scope.AddLocatedVariable(Ast.Name, Ast.NameExpression, this).AddTypes(this, types);
            }

            if (!Function.IsStatic && Ast.Parameters.Count > 0) {
                VariableDef param;
                IAnalysisSet firstParam;
                var clsScope = ddg.Scope as ClassScope;
                if (clsScope == null) {
                    firstParam = Function.IsClassMethod ? ProjectState.ClassInfos[BuiltinTypeId.Type].SelfSet : AnalysisSet.Empty;
                } else {
                    firstParam = Function.IsClassMethod ? clsScope.Class.SelfSet : clsScope.Class.Instance.SelfSet;
                }

                if (Scope.TryGetVariable(Ast.Parameters[0].Name, out param)) {
                    param.AddTypes(this, firstParam, false);
                }
            }
        }

        internal void AnalyzeDefaultParameters(DDG ddg) {
            VariableDef param;
            for (int i = 0; i < Ast.Parameters.Count; ++i) {
                var p = Ast.Parameters[i];
                if (p.Annotation != null) {
                    var val = ddg._eval.EvaluateAnnotation(p.Annotation).GetInstanceType();
                    if (val?.Any() == true && Scope.TryGetVariable(p.Name, out param)) {
                        param.AddTypes(this, val, false);
                    }
                }

                if (p.DefaultValue != null && p.Kind != ParameterKind.List && p.Kind != ParameterKind.Dictionary &&
                    Scope.TryGetVariable(p.Name, out param)) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        param.AddTypes(this, val, false);
                    }
                }
            }
            if (Ast.ReturnAnnotation != null) {
                ((FunctionScope)Scope).AddReturnTypes(
                    Ast.ReturnAnnotation,
                    ddg._unit,
                    ddg._eval.EvaluateAnnotation(Ast.ReturnAnnotation).GetInstanceType()
                );
            }
        }

        public override string ToString() {
            return string.Format("{0}{1}({2})->{3}",
                base.ToString(),
                " def:",
                string.Join(", ", Ast.Parameters.Select(p => Scope.GetVariable(p.Name).TypesNoCopy.ToString())),
                ((FunctionScope)Scope).ReturnValue.TypesNoCopy.ToString()
            );
        }
    }

    class CalledFunctionAnalysisUnit : FunctionAnalysisUnit {
        private readonly FunctionAnalysisUnit _originalUnit;
        public readonly CallChain CallChain;
        private readonly IVersioned _agg;

        public CalledFunctionAnalysisUnit(IVersioned agg, FunctionAnalysisUnit originalUnit, CallChain callChain, ArgumentSet callArgs)
            : base(originalUnit.Function, originalUnit._declUnit) {
            _originalUnit = originalUnit;
            _agg = agg;
            CallChain = callChain;

            var scope = new FunctionScope(
                Function,
                Ast,
                originalUnit.Scope.OuterScope,
                originalUnit.DeclaringModule.ProjectEntry
            );
            scope.UpdateParameters(this, callArgs, false, originalUnit.Scope as FunctionScope);
            _scope = scope;

            var walker = new OverviewWalker(_originalUnit.ProjectEntry, this, Tree);
            if (Ast.Body != null) {
                Ast.Body.Walk(walker);
            }

            AnalysisLog.NewUnit(this);
            Enqueue();
        }

        public override IVersioned DependencyProject => _agg;
        internal override ILocationResolver AlternateResolver => _originalUnit;

        internal override bool UpdateParameters(ArgumentSet callArgs, bool enqueue = true) {
            var defScope = _originalUnit.Scope;
            return ((FunctionScope)Scope).UpdateParameters(this, callArgs, enqueue, (FunctionScope)defScope);
        }

        public override string ToString() {
            return string.Format("{0}{1}({2})->{3}",
                base.ToString(),
                "",
                string.Join(", ", Ast.Parameters.Select(p => Scope.GetVariable(p.Name).TypesNoCopy.ToString())),
                ((FunctionScope)Scope).ReturnValue.TypesNoCopy.ToString()
            );
        }
    }

}
