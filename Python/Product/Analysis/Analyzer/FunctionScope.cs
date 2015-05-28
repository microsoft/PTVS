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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    sealed class FunctionScope : InterpreterScope {
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
            ReturnValue = new VariableDef();
            if (Function.FunctionDefinition.IsCoroutine) {
                Coroutine = new CoroutineInfo(function.ProjectState, declModule);
                ReturnValue.AddTypes(function.ProjectEntry, Coroutine.SelfSet, false);
            } else if (Function.FunctionDefinition.IsGenerator) {
                Generator = new GeneratorInfo(function.ProjectState, declModule);
                ReturnValue.AddTypes(function.ProjectEntry, Generator.SelfSet, false);
            }
        }

        internal void AddReturnTypes(Node node, AnalysisUnit unit, IAnalysisSet types, bool enqueue = true) {
            if (Coroutine != null) {
                Coroutine.AddReturn(node, unit, types, enqueue);
            } else if (Generator != null) {
                Generator.AddReturn(node, unit, types, enqueue);
            } else {
                ReturnValue.MakeUnionStrongerIfMoreThan(unit.ProjectState.Limits.ReturnTypes, types);
                ReturnValue.AddTypes(unit, types, enqueue);
            }
        }

        internal void EnsureParameters(FunctionAnalysisUnit unit) {
            var astParams = Function.FunctionDefinition.Parameters;
            for (int i = 0; i < astParams.Count; ++i) {
                VariableDef param;
                if (!TryGetVariable(astParams[i].Name, out param)) {
                    if (astParams[i].Kind == ParameterKind.List) {
                        param = _seqParameters = _seqParameters ?? new ListParameterVariableDef(unit, astParams[i]);
                    } else if (astParams[i].Kind == ParameterKind.Dictionary) {
                        param = _dictParameters = _dictParameters ?? new DictParameterVariableDef(unit, astParams[i]);
                    } else {
                        param = new LocatedVariableDef(unit.ProjectEntry, astParams[i]);
                    }
                    AddVariable(astParams[i].Name, param);
                }
            }
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
            var entry = unit.ProjectEntry;
            var state = unit.ProjectState;
            var limits = state.Limits;

            for (int i = 0; i < others.Args.Length && i < astParams.Count; ++i) {
                VariableDef param;
                if (!TryGetVariable(astParams[i].Name, out param)) {
                    Debug.Assert(false, "Parameter " + astParams[i].Name + " has no variable in this scope");
                    param = AddVariable(astParams[i].Name);
                }
                param.MakeUnionStrongerIfMoreThan(limits.NormalArgumentTypes, others.Args[i]);
                added |= param.AddTypes(entry, others.Args[i], false);
            }
            if (_seqParameters != null) {
                _seqParameters.List.MakeUnionStrongerIfMoreThan(limits.ListArgumentTypes, others.SequenceArgs);
                added |= _seqParameters.List.AddTypes(unit, new[] { others.SequenceArgs });
            }
            if (_dictParameters != null) {
                _dictParameters.Dict.MakeUnionStrongerIfMoreThan(limits.DictArgumentTypes, others.DictArgs);
                added |= _dictParameters.Dict.AddTypes(Function.FunctionDefinition, unit, state.GetConstant(""), others.DictArgs);
            }

            if (scopeWithDefaultParameters != null) {
                for (int i = 0; i < others.Args.Length && i < astParams.Count; ++i) {
                    VariableDef defParam, param;
                    if (TryGetVariable(astParams[i].Name, out param) &&
                        !param.TypesNoCopy.Any() &&
                        scopeWithDefaultParameters.TryGetVariable(astParams[i].Name, out defParam)) {
                        param.MakeUnionStrongerIfMoreThan(limits.NormalArgumentTypes, defParam.TypesNoCopy);
                        added |= param.AddTypes(entry, defParam.TypesNoCopy, false);
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
            if (this != Function.AnalysisUnit.Scope) {
                // Many scopes reference one FunctionInfo, which references one
                // FunctionAnalysisUnit which references one scope. Since we
                // are not that scope, we won't look at _allCalls for other
                // variables.
                return AllVariables;
            }
            
            var scopes = new HashSet<InterpreterScope>();
            var result = AllVariables;
            if (Function._allCalls != null) {
                foreach (var callUnit in Function._allCalls.Values) {
                    scopes.Add(callUnit.Scope);
                }
                scopes.Remove(this);
                foreach (var scope in scopes) {
                    result = result.Concat(scope.GetAllMergedVariables());
                }
            }
            return result;
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

                if (scope.Function._allCalls != null) {
                    foreach (var callUnit in scope.Function._allCalls.Values) {
                        fnScope = callUnit.Scope as FunctionScope;
                        if (fnScope != null && fnScope != this) {
                            queue.Enqueue(fnScope);
                        }
                    }
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
            if (Function._allCalls != null) {
                foreach (var callUnit in Function._allCalls.Values) {
                    if (callUnit.Scope != this) {
                        yield return callUnit.Scope.AnalysisValue;
                    }
                }
            }
        }

        public override int GetBodyStart(PythonAst ast) {
            return ast.IndexToLocation(((FunctionDefinition)Node).HeaderIndex).Index;
        }

        public override string Name {
            get { return Function.FunctionDefinition.Name;  }
        }
    }
}
