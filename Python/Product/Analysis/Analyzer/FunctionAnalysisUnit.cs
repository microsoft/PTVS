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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    class FunctionAnalysisUnit : AnalysisUnit {
        public readonly FunctionInfo Function;

        internal readonly AnalysisUnit _declUnit;
        private readonly bool _concreteParameters;
        private readonly Dictionary<Node, Expression> _decoratorCalls;

        internal FunctionAnalysisUnit(
            FunctionInfo function,
            AnalysisUnit declUnit,
            InterpreterScope declScope,
            IPythonProjectEntry declEntry,
            bool concreteParameters
        )
            : base(function.FunctionDefinition, null) {
            _declUnit = declUnit;
            Function = function;
            _concreteParameters = concreteParameters;
            _decoratorCalls = new Dictionary<Node, Expression>();

            var scope = new FunctionScope(Function, Function.FunctionDefinition, declScope, declEntry);
            _scope = scope;

            if (GetType() == typeof(FunctionAnalysisUnit)) {
                AnalysisLog.NewUnit(this);
            }
        }

        internal virtual void EnsureParameters() {
            ((FunctionScope)Scope).EnsureParameters(this, usePlaceholders: !_concreteParameters);
        }

        internal virtual void EnsureParameterZero() {
            ((FunctionScope)Scope).EnsureParameterZero(this);
        }

        internal virtual bool UpdateParameters(ArgumentSet callArgs, bool enqueue = true) {
            return ((FunctionScope)Scope).UpdateParameters(this, callArgs, enqueue, null, usePlaceholders: !_concreteParameters);
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

            var funcType = ProcessFunctionDecorators(ddg);
            EnsureParameterZero();

            _declUnit.Scope.AddLocatedVariable(Ast.Name, Ast.NameExpression, this);

            // Set the scope to within the function
            ddg.Scope = Scope;

            Ast.Body.Walk(ddg);

            _declUnit.Scope.AssignVariable(Ast.Name, Ast.NameExpression, this, funcType);
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

        private bool ProcessAbstractDecorators(IAnalysisSet decorator) {
            var res = false;

            // Only handle these if they are specialized
            foreach (var d in decorator.OfType<SpecializedCallable>()) {
                if (d.DeclaringModule?.ModuleName != "abc") {
                    continue;
                }

                switch (d.Name) {
                    case "abstractmethod":
                        res = true;
                        break;
                    case "abstractstaticmethod":
                        Function.IsStatic = true;
                        res = true;
                        break;
                    case "abstractclassmethod":
                        Function.IsClassMethod = true;
                        res = true;
                        break;
                    case "abstractproperty":
                        Function.IsProperty = true;
                        res = true;
                        break;
                }
            }

            return res;
        }

        internal IAnalysisSet ProcessFunctionDecorators(DDG ddg) {
            var types = Function.SelfSet;
            if (Ast.Decorators != null) {
                Expression expr = Ast.NameExpression;

                foreach (var d in Ast.Decorators.DecoratorsInternal) {
                    if (d != null) {
                        var decorator = ddg._eval.Evaluate(d);

                        if (decorator.Contains(State.ClassInfos[BuiltinTypeId.Property])) {
                            Function.IsProperty = true;
                        } else if (decorator.Contains(State.ClassInfos[BuiltinTypeId.StaticMethod])) {
                            // TODO: Warn if IsClassMethod is set
                            Function.IsStatic = true;
                        } else if (decorator.Contains(State.ClassInfos[BuiltinTypeId.ClassMethod])) {
                            // TODO: Warn if IsStatic is set
                            Function.IsClassMethod = true;
                        } else if (ProcessAbstractDecorators(decorator)) {
                            // No-op
                        } else {
                            Expression nextExpr;
                            if (!_decoratorCalls.TryGetValue(d, out nextExpr)) {
                                nextExpr = _decoratorCalls[d] = new CallExpression(d, new[] { new Arg(expr) });
                                nextExpr.SetLoc(d.IndexSpan);
                            }
                            expr = nextExpr;
                            var decorated = AnalysisSet.Empty;
                            bool anyResults = false;
                            foreach (var ns in decorator) {
                                var fd = ns as FunctionInfo;
                                if (fd != null && Scope.EnumerateTowardsGlobal.Any(s => s.AnalysisValue == fd)) {
                                    continue;
                                }
                                decorated = decorated.Union(ns.Call(expr, this, new[] { types }, ExpressionEvaluator.EmptyNames));
                                anyResults = true;
                            }

                            // If processing decorators, update the current
                            // function type. Otherwise, we are acting as if
                            // each decorator returns the function unmodified.
                            if (ddg.ProjectState.Limits.ProcessCustomDecorators && anyResults) {
                                types = decorated;
                            }
                        }
                    }
                }
            }

            return types;
        }

        internal void AnalyzeDefaultParameters(DDG ddg) {
            VariableDef param;
            var scope = (FunctionScope)Scope;
            for (int i = 0; i < Ast.ParametersInternal.Length; ++i) {
                var p = Ast.ParametersInternal[i];
                if (p.Annotation != null) {
                    var val = ddg._eval.EvaluateAnnotation(p.Annotation);
                    if (val?.Any() == true && Scope.TryGetVariable(p.Name, out param)) {
                        param.AddTypes(this, val, false);
                        var vd = scope.GetParameter(p.Name);
                        if (vd != null && vd != param) {
                            vd.AddTypes(this, val, false);
                        }
                    }
                }

                if (p.DefaultValue != null && p.Kind != ParameterKind.List && p.Kind != ParameterKind.Dictionary &&
                    Scope.TryGetVariable(p.Name, out param)) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        param.AddTypes(this, val, false);
                        var vd = scope.GetParameter(p.Name);
                        if (vd != null && vd != param) {
                            vd.AddTypes(this, val, false);
                        }
                    }
                }
            }
            if (Ast.ReturnAnnotation != null) {
                var ann = ddg._eval.EvaluateAnnotation(Ast.ReturnAnnotation);
                var resType = ann;
                if (Ast.IsGenerator) {
                    if (ann.Split<ProtocolInfo>(out var gens, out resType)) {
                        var gen = ((FunctionScope)Scope).Generator;
                        foreach (var g in gens.SelectMany(p => p.GetProtocols<GeneratorProtocol>())) {
                            gen.Yields.AddTypes(ProjectEntry, g.Yielded);
                            gen.Sends.AddTypes(ProjectEntry, g.Sent);
                            gen.Returns.AddTypes(ProjectEntry, g.Returned);
                        }
                    }
                } else {
                    ((FunctionScope)Scope).AddReturnTypes(
                        Ast.ReturnAnnotation,
                        ddg._unit,
                        resType
                    );
                }
            }
        }

        public override string ToString() {
            return "{0}{1}({2})->{3}".FormatInvariant(
                base.ToString(),
                " def:",
                string.Join(", ", Ast.ParametersInternal.Select(p => Scope.TryGetVariable(p.Name, out var v) ? v.TypesNoCopy.ToString() : "{}")),
                ((FunctionScope)Scope).ReturnValue.TypesNoCopy.ToString()
            );
        }
    }

    class FunctionClosureAnalysisUnit : FunctionAnalysisUnit {
        private readonly FunctionAnalysisUnit _originalUnit;
        private readonly IVersioned _agg;

        internal FunctionClosureAnalysisUnit(IVersioned agg, FunctionAnalysisUnit originalUnit, CallChain callChain) :
            base(originalUnit.Function, originalUnit._declUnit, originalUnit._declUnit.Scope, originalUnit.ProjectEntry, true) {
            _originalUnit = originalUnit;
            _agg = agg;
            CallChain = callChain;
            _originalUnit.Scope.AddLinkedScope(Scope);

            var node = originalUnit.Function.FunctionDefinition;
            node.Body.Walk(new OverviewWalker(originalUnit.ProjectEntry, this, originalUnit.Tree));

            AnalysisLog.NewUnit(this);
        }

        public override IVersioned DependencyProject => _agg;
        public FunctionAnalysisUnit OriginalUnit => _originalUnit;
        internal override ILocationResolver AlternateResolver => _originalUnit;

        public CallChain CallChain { get; }

        public override string ToString() {
            return base.ToString() + " " + CallChain.ToString();
        }
    }
}
