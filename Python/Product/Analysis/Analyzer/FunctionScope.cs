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

        internal void AddReturnTypes(Node node, AnalysisUnit unit, IAnalysisSet types, bool enqueue = true) {
            if (types?.Any() != true) {
                return;
            }
            if (Coroutine != null) {
                Coroutine.AddReturn(node, unit, types, enqueue);
            } else if (Generator != null) {
                Generator.AddReturn(node, unit, types, enqueue);
            } else {
                ReturnValue.MakeUnionStrongerIfMoreThan(unit.ProjectState.Limits.ReturnTypes, types);
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

        internal void EnsureParameters(FunctionAnalysisUnit unit) {
            var astParams = Function.FunctionDefinition.Parameters;
            for (int i = 0; i < astParams.Count; ++i) {
                var name = astParams[i].Name;
                if (string.IsNullOrEmpty(name)) {
                    continue;
                }

                var node = (Node)astParams[i].NameExpression ?? astParams[i];

                if (astParams[i].Kind == ParameterKind.List) {
                    if (_seqParameters == null) {
                        _seqParameters = new ListParameterVariableDef(unit, node, name);
                        AddParameter(unit, name, node);
                    }
                } else if (astParams[i].Kind == ParameterKind.Dictionary) {
                    if (_dictParameters == null) {
                        _dictParameters = new DictParameterVariableDef(unit, node, name);
                        AddParameter(unit, name, node);
                    }
                } else if (!_parameters.ContainsKey(name)) {
                    _parameters[name] = new LocatedVariableDef(unit.ProjectEntry, node);
                    AddParameter(unit, name, node);
                }
            }
        }

        private VariableDef AddParameter(AnalysisUnit unit, string name, Node node) {
            var p = CreateVariable(node, unit, name);
            p.AddTypes(unit, GetOrMakeNodeValue(node, NodeValueKind.ParameterInfo, n => new ParameterInfo(Function, n, name)), false);
            return p;
        }

        internal void AddParameterReferences(AnalysisUnit caller, NameExpression[] names) {
            foreach (var name in names) {
                VariableDef param;
                if (name != null && TryGetVariable(name.Name, out param)) {
                    param.AddReference(name, caller);
                }
            }
        }

        internal bool UpdateParameters(FunctionAnalysisUnit unit, ArgumentSet others, bool enqueue = true, FunctionScope scopeWithDefaultParameters = null) {
            EnsureParameters(unit);

            var astParams = Function.FunctionDefinition.Parameters;
            bool added = false;
            var entry = unit.DependencyProject;
            var state = unit.ProjectState;
            var limits = state.Limits;

            for (int i = 0; i < others.Args.Length && i < astParams.Count; ++i) {
                var name = astParams[i].Name;
                VariableDef param;
                if (name == _seqParameters?.Name) {
                    param = _seqParameters;
                } else if (name == _dictParameters?.Name) {
                    param = _dictParameters;
                } else if (!_parameters.TryGetValue(name, out param)) {
                    Debug.Fail($"Parameter {name} has no variable in this function");
                    _parameters[name] = param = new LocatedVariableDef(Function.AnalysisUnit.ProjectEntry, (Node)astParams[i].NameExpression ?? astParams[i]);
                }
                var arg = others.Args[i].Resolve(unit);
                param.MakeUnionStrongerIfMoreThan(limits.NormalArgumentTypes, arg);
                added |= param.AddTypes(entry, arg, false, unit.ProjectEntry);
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
                for (int i = 0; i < others.Args.Length && i < astParams.Count; ++i) {
                    VariableDef defParam, param;
                    if (TryGetVariable(astParams[i].Name, out param) &&
                        !param.HasTypes &&
                        scopeWithDefaultParameters.TryGetVariable(astParams[i].Name, out defParam)) {
                        param.MakeUnionStrongerIfMoreThan(
                            limits.NormalArgumentTypes, 
                            defParam.GetTypesNoCopy(unit, AnalysisValue.DeclaringModule)
                        );
                        added |= param.AddTypes(entry, defParam.GetTypesNoCopy(unit, AnalysisValue.DeclaringModule), false, unit.ProjectEntry);
                    }
                }
            }

            if (enqueue && added) {
                unit.Enqueue();
            }
            return added;
        }


        public FunctionInfo Function {
            get {
                return (FunctionInfo)AnalysisValue;
            }
        }

        public override IEnumerable<KeyValuePair<string, VariableDef>> GetAllMergedVariables() {
            return AllVariables;
        }

        public override IEnumerable<VariableDef> GetMergedVariables(string name) {
            VariableDef res;
            FunctionScope fnScope;

            var nodes = new HashSet<Node>();
            var seen = new HashSet<InterpreterScope>();
            var queue = new Queue<FunctionScope>();
            queue.Enqueue(this);

            while (queue.Any()) {
                var scope = queue.Dequeue();
                if (scope == null || !seen.Add(scope)) {
                    continue;
                }

                if (scope.Node == Node && scope.TryGetVariable(name, out res)) {
                    yield return res;
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
