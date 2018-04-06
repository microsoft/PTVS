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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    sealed class FunctionScope : InterpreterScope {
        private readonly AnalysisDictionary<string, VariableDef> _parameters;

        private ListParameterVariableDef _seqParameters;
        private DictParameterVariableDef _dictParameters;
        public readonly VariableDef ReturnValue;
        public readonly CoroutineInfo Coroutine;
        public readonly GeneratorInfo Generator;

        public FunctionScope(
            FunctionInfo function,
            Node node,
            InterpreterScope declScope,
            IPythonProjectEntry declModule
        )
            : base(function, node, declScope) {
            _parameters = new AnalysisDictionary<string, VariableDef>();

            ReturnValue = new VariableDef();
            if (Function.FunctionDefinition.IsCoroutine) {
                Coroutine = new CoroutineInfo(function.ProjectState, declModule);
                ReturnValue.AddTypes(function.ProjectEntry, Coroutine.SelfSet, false, declModule);
            } else if (Function.FunctionDefinition.IsGenerator) {
                Generator = new GeneratorInfo(function.ProjectState, declModule);
                ReturnValue.AddTypes(function.ProjectEntry, Generator.SelfSet, false, declModule);
            }
        }

        public static bool IsOriginalClosureScope(InterpreterScope scope) => (scope as FunctionScope)?.Function.IsClosure == true && scope.OriginalScope == null;

        internal void AddReturnTypes(Node node, AnalysisUnit unit, IAnalysisSet types, bool enqueue = true) {
            if (IsOriginalClosureScope(OuterScope)) {
                // Do not add return types to original scope of closure functions
                return;
            }

            if (types?.Any() != true) {
                return;
            }
            if (Coroutine != null) {
                Coroutine.AddReturn(node, unit, types, enqueue);
            } else if (Generator != null) {
                Generator.AddReturn(node, unit, types, enqueue);
            } else {
                ReturnValue.MakeUnionStrongerIfMoreThan(unit.State.Limits.ReturnTypes, types);
                ReturnValue.AddTypes(unit, types, enqueue);
            }
        }

        public VariableDef GetParameter(string name) {
            if (string.IsNullOrEmpty(name)) {
                return null;
            }

            if (name == _seqParameters?.Name) {
                return _seqParameters;
            } else if (name == _dictParameters?.Name) {
                return _dictParameters;
            } else if (_parameters.TryGetValue(name, out var vd)) {
                return vd;
            }
            return null;
        }

        internal void EnsureParameters(FunctionAnalysisUnit unit, bool usePlaceholders) {
            var astParams = Function.FunctionDefinition.ParametersInternal;
            for (int i = 0; i < astParams.Length; ++i) {
                var p = astParams[i];
                var name = p?.Name;
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }

                var node = (Node)astParams[i].NameExpression ?? astParams[i];

                if (astParams[i].Kind == ParameterKind.List) {
                    if (_seqParameters == null) {
                        _seqParameters = new ListParameterVariableDef(unit, p, name);
                        AddParameter(unit, name, p, usePlaceholders ? null : _seqParameters);
                    }
                } else if (astParams[i].Kind == ParameterKind.Dictionary) {
                    if (_dictParameters == null) {
                        _dictParameters = new DictParameterVariableDef(unit, p, name);
                        AddParameter(unit, name, p, usePlaceholders ? null : _dictParameters);
                    }
                } else if (!_parameters.ContainsKey(name)) {
                    var v = _parameters[name] = new LocatedVariableDef(unit.ProjectEntry, new EncodedLocation(unit, p));
                    if (i == 0 &&
                        p.Kind == ParameterKind.Normal &&
                        !Function.IsStatic &&
                        Function.FunctionDefinition.Parent is ClassDefinition) {
                        AddParameter(unit, name, p, v);
                    } else {
                        AddParameter(unit, name, p, usePlaceholders ? null : v);
                    }
                }
            }
        }

        internal void EnsureParameterZero(FunctionAnalysisUnit unit) {
            var p = Function.FunctionDefinition.ParametersInternal.FirstOrDefault();
            if (!string.IsNullOrEmpty(p?.Name) &&
                p.Kind == ParameterKind.Normal &&
                !unit.Function.IsStatic &&
                unit.Scope.OuterScope is ClassScope cls &&
                _parameters.TryGetValue(p.Name, out var v)
            ) {
                v.AddTypes(unit, unit.Function.IsClassMethod ?
                    cls.Class.SelfSet :
                    cls.Class.Instance.SelfSet,
                    enqueue: false);
            }
        }

        private VariableDef AddParameter(AnalysisUnit unit, string name, Parameter node, VariableDef variableDef) {
            if (variableDef != null) {
                return AddVariable(name, variableDef);
            }

            if (!TryGetVariable(name, out var v)) {
                v = CreateLocatedVariable(node, unit, name, addRef: false);
            }
            v.AddTypes(unit, GetOrMakeNodeValue(node, NodeValueKind.ParameterInfo, n => new ParameterInfo(Function, n, name)), false);
            return v;
        }

        internal void AddParameterReferences(AnalysisUnit caller, NameExpression[] names) {
            foreach (var name in names) {
                VariableDef param;
                if (name != null && _parameters.TryGetValue(name.Name, out param)) {
                    param.AddReference(name, caller);
                }
            }
        }

        internal bool UpdateParameters(
            FunctionAnalysisUnit unit,
            ArgumentSet others,
            bool enqueue = true,
            FunctionScope scopeWithDefaultParameters = null,
            bool usePlaceholders = false
        ) {
            EnsureParameters(unit, usePlaceholders);

            var astParams = Function.FunctionDefinition.ParametersInternal;
            var added = false;
            var entry = unit.DependencyProject;
            var state = unit.State;
            var limits = state.Limits;

            for (int i = 0; i < others.Args.Length && i < astParams.Length; ++i) {
                var name = astParams[i].Name;
                VariableDef param;
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }
                if (name == _seqParameters?.Name) {
                    param = _seqParameters;
                } else if (name == _dictParameters?.Name) {
                    param = _dictParameters;
                } else if (!_parameters.TryGetValue(name, out param)) {
                    Debug.Fail($"Parameter {name} has no variable in this function");
                    _parameters[name] = param = new LocatedVariableDef(Function.AnalysisUnit.ProjectEntry,
                        new EncodedLocation(unit, (Node)astParams[i].NameExpression ?? astParams[i]));
                }
                var arg = others.Args[i].Resolve(unit);
                param.MakeUnionStrongerIfMoreThan(limits.NormalArgumentTypes, arg);
                added |= param.AddTypes(entry, arg, enqueue, unit.ProjectEntry);
            }
            if (_seqParameters != null) {
                var arg = others.SequenceArgs.Resolve(unit);
                _seqParameters.List.MakeUnionStrongerIfMoreThan(limits.ListArgumentTypes, arg);
                added |= _seqParameters.List.AddTypes(unit, new[] { arg });
            }
            if (_dictParameters != null) {
                var arg = others.DictArgs.Resolve(unit);
                _dictParameters.Dict.MakeUnionStrongerIfMoreThan(limits.DictArgumentTypes, arg);
                added |= _dictParameters.Dict.AddTypes(Function.FunctionDefinition, unit, state.GetConstant(""), arg);
            }

            if (scopeWithDefaultParameters != null) {
                for (int i = 0; i < others.Args.Length && i < astParams.Length; ++i) {
                    VariableDef defParam, param;
                    if (TryGetVariable(astParams[i].Name, out param) &&
                        !param.HasTypes &&
                        scopeWithDefaultParameters.TryGetVariable(astParams[i].Name, out defParam)) {
                        param.MakeUnionStrongerIfMoreThan(
                            limits.NormalArgumentTypes, 
                            defParam.GetTypesNoCopy(unit, AnalysisValue.DeclaringModule)
                        );
                        added |= param.AddTypes(entry, defParam.GetTypesNoCopy(unit, AnalysisValue.DeclaringModule), enqueue, unit.ProjectEntry);
                    }
                }
            }

            if (enqueue && added) {
                unit.Enqueue();
            }
            return added;
        }

        public FunctionInfo Function => (FunctionInfo)AnalysisValue;

        internal override bool TryPropagateVariable(Node node, AnalysisUnit unit, string name, IAnalysisSet values, VariableDef ifNot = null, bool addRef = true) {
            var vd = GetParameter(name);
            if (vd != null) {
                values.Split(out IReadOnlyList<LazyValueInfo> _, out var nonLazy);
                if (nonLazy.Any()) {
                    vd.AddTypes(unit, nonLazy);
                }
            }
            return base.TryPropagateVariable(node, unit, name, values, ifNot, addRef);
        }

        public override IEnumerable<KeyValuePair<string, VariableDef>> GetAllMergedVariables() {
            var param = _parameters;
            foreach (var kv in param) {
                yield return kv;
            }

            foreach (var kv in AllVariables) {
                if (!param.ContainsKey(kv.Key)) {
                    yield return kv;
                }
            }
        }

        public override IEnumerable<VariableDef> GetMergedVariables(string name) {
            VariableDef res;
            FunctionScope fnScope;

            var nodes = new HashSet<Node>();
            var seen = new HashSet<InterpreterScope>();
            var queue = new Queue<FunctionScope>();
            queue.Enqueue(this);
            foreach (var linked in GetLinkedScopes().OfType<FunctionScope>()) {
                queue.Enqueue(linked);
            }

            while (queue.Any()) {
                var scope = queue.Dequeue();
                if (scope == null || !seen.Add(scope)) {
                    continue;
                }

                if (scope.Node == Node) {
                    if (scope._parameters.TryGetValue(name, out res)) {
                        yield return res;
                    }
                    if (scope.TryGetVariable(name, out res)) {
                        yield return res;
                    }
                }

                foreach (var r2 in scope.GetLinkedVariables(name)) {
                    yield return r2;
                }

                foreach (var keyValue in scope.AllNodeScopes.Where(kv => nodes.Contains(kv.Key))) {
                    if ((fnScope = keyValue.Value as FunctionScope) != null) {
                        queue.Enqueue(fnScope);
                    }
                }

                if ((fnScope = scope.OuterScope as FunctionScope) != null) {
                    nodes.Add(scope.Node);
                    queue.Enqueue(fnScope);
                }
            }
        }

        public override IEnumerable<AnalysisValue> GetMergedAnalysisValues() {
            yield return AnalysisValue;
        }

        public override int GetBodyStart(PythonAst ast) {
            return ((FunctionDefinition)Node).HeaderIndex;
        }

        public override string Name {
            get { return Function.FunctionDefinition.Name;  }
        }
    }
}
