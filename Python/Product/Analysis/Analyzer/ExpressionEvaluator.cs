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
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    internal class ExpressionEvaluator {
        private readonly AnalysisUnit _unit;
        private readonly bool _mergeScopes;

        internal static readonly IAnalysisSet[] EmptySets = new IAnalysisSet[0];
        internal static readonly NameExpression[] EmptyNames = new NameExpression[0];

        /// <summary>
        /// Creates a new ExpressionEvaluator that will evaluate in the context of the top-level module.
        /// </summary>
        public ExpressionEvaluator(AnalysisUnit unit) {
            _unit = unit;
            Scope = unit.Scope;
        }

        public ExpressionEvaluator(AnalysisUnit unit, InterpreterScope scope, bool mergeScopes = false) {
            _unit = unit;
            Scope = scope;
            _mergeScopes = mergeScopes;
        }

        #region Public APIs

        /// <summary>
        /// Returns possible variable refs associated with the expr in the expression evaluators scope.
        /// </summary>
        public IAnalysisSet Evaluate(Expression node) {
            var res = EvaluateWorker(node);
            Debug.Assert(res != null);
            return res;
        }

        /// <summary>
        /// Returns possible variable refs associated with the expr in the expression evaluators scope.
        /// </summary>
        internal IAnalysisSet EvaluateNoMemberRecursion(Expression node, HashSet<AnalysisValue> seenValues = null) {
            if (seenValues == null) {
                seenValues = new HashSet<AnalysisValue>();
            }

            MemberExpression member = node as MemberExpression;
            if (member != null) {
                var target = EvaluateNoMemberRecursion(member.Target, seenValues);
                IAnalysisSet unseenValues = AnalysisSet.Empty;
                foreach (var value in target) {
                    if (!seenValues.Add(value)) {
                        unseenValues = unseenValues.Add(value, true);
                    }
                }
                if (unseenValues.Count > 0) {
                    return unseenValues.GetMember(member, _unit, member.Name);
                }
                return AnalysisSet.Empty;
            }

            var res = EvaluateWorker(node);
            Debug.Assert(res != null);
            return res;
        }

        public IAnalysisSet EvaluateMaybeNull(Expression node) {
            if (node == null) {
                return null;
            }

            return Evaluate(node);
        }

        public IAnalysisSet EvaluateAnnotation(Expression annotation) {
            // Ensure that the annotation references are evaluated, but we
            // don't care about the result.
            Evaluate(annotation);

            return new TypeAnnotation(_unit.State.LanguageVersion, annotation)
                .GetValue(new ExpressionEvaluatorAnnotationConverter(this, annotation, _unit)) ?? AnalysisSet.Empty;
        }

        /// <summary>
        /// Returns a sequence of possible types associated with the name in the expression evaluators scope.
        /// </summary>
        public IAnalysisSet LookupAnalysisSetByName(Node node, string name, bool addRef = true, bool addDependency = false) {
            InterpreterScope createIn = null;
            VariableDef refs = null;

            if (_mergeScopes) {
                var scope = Scope.EnumerateTowardsGlobal
                    .FirstOrDefault(s => (s == Scope || s.VisibleToChildren) && s.ContainsVariable(name));
                if (scope != null) {
                    return scope.GetMergedVariableTypes(name);
                }
            } else {
                foreach (var scope in Scope.EnumerateTowardsGlobal) {
                    if (scope == Scope || scope.VisibleToChildren) {
                        refs = scope.GetVariable(node, _unit, name, addRef);
                        if (refs != null) {
                            if (addRef) {
                                scope.AddReferenceToLinkedVariables(node, _unit, name);
                            }
                            break;
                        } else if (addRef && createIn == null && scope.ContainsImportStar) {
                            // create the variable so that we can appropriately
                            // add any dependent reads to it.
                            createIn = scope;
                        }
                    }
                }
            }

            if (_unit.ForEval) {
                return refs?.Types ?? ProjectState.BuiltinModule.GetMember(node, _unit, name);
            }

            bool warn = false;
            var res = refs?.Types;
            if (res == null) {
                // No variable found, so look in builtins
                res = ProjectState.BuiltinModule.GetMember(node, _unit, name);
                if (!res.Any()) {
                    // No builtin found, so ...
                    if (createIn != null) {
                        // ... create a variable in the best known scope
                        refs = createIn.CreateVariable(node, _unit, name, addRef);
                        res = refs.Types;
                    } else {
                        // ... warn the user
                        warn = true;
                    }
                }
            } else if (!res.Any() && !refs.IsAssigned) {
                // Variable has no values, so if we also don't know about any
                // definitions then warn.
                warn = true;
            }

            if (addDependency && refs != null) {
                refs.AddDependency(_unit);
            }

            if (warn) {
                ProjectState.AddDiagnostic(node, _unit, ErrorMessages.UsedBeforeAssignment(name), DiagnosticSeverity.Warning, ErrorMessages.UsedBeforeAssignmentCode);
            } else {
                ProjectState.ClearDiagnostic(node, _unit, ErrorMessages.UsedBeforeAssignmentCode);
            }

            return res;
        }

        #endregion

        #region Implementation Details

        private ModuleInfo GlobalScope {
            get { return _unit.DeclaringModule; }
        }

        private PythonAnalyzer ProjectState {
            get { return _unit.State; }
        }

        /// <summary>
        /// The list of scopes which define the current context.
        /// </summary>
#if DEBUG
        public InterpreterScope Scope {
            get { return _currentScope; }
            set {
                // Scopes must be from a common stack.
                Debug.Assert(_currentScope == null ||
                    _currentScope.OuterScope == value.OuterScope ||
                    _currentScope.EnumerateTowardsGlobal.Contains(value) ||
                    value.EnumerateTowardsGlobal.Contains(_currentScope));
                _currentScope = value;
            }
        }

        private InterpreterScope _currentScope;
#else
        public InterpreterScope Scope;
#endif

        private IAnalysisSet[] Evaluate(IList<Arg> nodes) {
            var result = new IAnalysisSet[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) {
                result[i] = Evaluate(nodes[i].Expression);
            }
            return result;
        }

        private IAnalysisSet EvaluateWorker(Node node) {
            EvalDelegate eval;
            if (_evaluators.TryGetValue(node.GetType(), out eval)) {
                return eval(this, node);
            }

            return AnalysisSet.Empty;
        }

        delegate IAnalysisSet EvalDelegate(ExpressionEvaluator ee, Node node);

        private static Dictionary<Type, EvalDelegate> _evaluators = new Dictionary<Type, EvalDelegate> {
            { typeof(AndExpression), ExpressionEvaluator.EvaluateAnd },
            { typeof(AwaitExpression), ExpressionEvaluator.EvaluateAwait },
            { typeof(BackQuoteExpression), ExpressionEvaluator.EvaluateBackQuote },
            { typeof(BinaryExpression), ExpressionEvaluator.EvaluateBinary },
            { typeof(CallExpression), ExpressionEvaluator.EvaluateCall },
            { typeof(ConditionalExpression), ExpressionEvaluator.EvaluateConditional},
            { typeof(ConstantExpression), ExpressionEvaluator.EvaluateConstant },
            { typeof(DictionaryExpression), ExpressionEvaluator.EvaluateDictionary },
            { typeof(SetExpression), ExpressionEvaluator.EvaluateSet },
            { typeof(DictionaryComprehension), ExpressionEvaluator.EvaluateComprehension },
            { typeof(SetComprehension), ExpressionEvaluator.EvaluateComprehension },
            { typeof(GeneratorExpression), ExpressionEvaluator.EvaluateComprehension },
            { typeof(IndexExpression), ExpressionEvaluator.EvaluateIndex },
            { typeof(LambdaExpression), ExpressionEvaluator.EvaluateLambda },
            { typeof(ListComprehension), ExpressionEvaluator.EvaluateListComprehension },
            { typeof(MemberExpression), ExpressionEvaluator.EvaluateMember },
            { typeof(NameExpression), ExpressionEvaluator.EvaluateName },
            { typeof(OrExpression), ExpressionEvaluator.EvaluateOr },
            { typeof(ParenthesisExpression), ExpressionEvaluator.EvaluateParenthesis },
            { typeof(UnaryExpression), ExpressionEvaluator.EvaluateUnary },
            { typeof(YieldExpression), ExpressionEvaluator.EvaluateYield },
            { typeof(YieldFromExpression), ExpressionEvaluator.EvaluateYieldFrom },
            { typeof(TupleExpression), ExpressionEvaluator.EvaluateSequence },
            { typeof(ListExpression), ExpressionEvaluator.EvaluateSequence },
            { typeof(SliceExpression), ExpressionEvaluator.EvaluateSlice },
            { typeof(ExpressionWithAnnotation), ExpressionEvaluator.EvaluateAnnotatedExpression },
        };

        private static IAnalysisSet EvaluateSequence(ExpressionEvaluator ee, Node node) {
            // Covers both ListExpression and TupleExpression
            // TODO: We need to update the sequence on each re-evaluation, not just
            // evaluate it once.
            return ee.MakeSequence(ee, node);
        }

        private static IAnalysisSet EvaluateAnnotatedExpression(ExpressionEvaluator ee, Node node) {
            var n = (ExpressionWithAnnotation)node;
            if (n.Annotation != null) {
                ee.Evaluate(n.Annotation);
            }
            return ee.Evaluate(n.Expression);
        }

        private static IAnalysisSet EvaluateParenthesis(ExpressionEvaluator ee, Node node) {
            var n = (ParenthesisExpression)node;
            return ee.Evaluate(n.Expression);
        }

        private static IAnalysisSet EvaluateOr(ExpressionEvaluator ee, Node node) {
            // TODO: Warn if lhs is always false
            var n = (OrExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static IAnalysisSet EvaluateName(ExpressionEvaluator ee, Node node) {
            var n = (NameExpression)node;
            var res = ee.LookupAnalysisSetByName(node, n.Name);
            foreach (var value in res) {
                value.AddReference(node, ee._unit);
            }
            return res;
        }

        private static IAnalysisSet EvaluateMember(ExpressionEvaluator ee, Node node) {
            var n = (MemberExpression)node;
            var target = ee.Evaluate(n.Target);
            if (string.IsNullOrEmpty(n.Name)) {
                return AnalysisSet.Empty;
            }
            return target.GetMember(node, ee._unit, n.Name);
        }

        private static IAnalysisSet EvaluateIndex(ExpressionEvaluator ee, Node node) {
            var n = (IndexExpression)node;

            return ee.Evaluate(n.Target).GetIndex(n, ee._unit, ee.Evaluate(n.Index));
        }

        private static IAnalysisSet EvaluateSet(ExpressionEvaluator ee, Node node) {
            var n = (SetExpression)node;

            var setInfo = (SetInfo)ee.Scope.GetOrMakeNodeValue(node, NodeValueKind.Set, x => new SetInfo(
                ee.ProjectState,
                x,
                ee._unit.ProjectEntry
            ));
            foreach (var x in n.Items) {
                setInfo.AddTypes(ee._unit, ee.Evaluate(x));
            }

            return setInfo;
        }

        private static IAnalysisSet EvaluateDictionary(ExpressionEvaluator ee, Node node) {
            var n = (DictionaryExpression)node;
            IAnalysisSet result = ee.Scope.GetOrMakeNodeValue(node, NodeValueKind.DictLiteral, _ => {
                var dictInfo = new DictionaryInfo(ee._unit.ProjectEntry, node);
                result = dictInfo.SelfSet;

                var keys = new HashSet<AnalysisValue>();
                var values = new HashSet<AnalysisValue>();
                foreach (var x in n.Items) {
                    dictInfo.SetIndex(
                        node,
                        ee._unit,
                        ee.EvaluateMaybeNull(x.SliceStart) ?? AnalysisSet.Empty,
                        ee.EvaluateMaybeNull(x.SliceStop) ?? AnalysisSet.Empty
                    );
                }

                return result;
            });
            return result;
        }

        private static IAnalysisSet EvaluateConstant(ExpressionEvaluator ee, Node node) {
            var n = (ConstantExpression)node;
            if (n.Value is double ||
                (n.Value is int && ((int)n.Value) > 100)) {
                return ee.ProjectState.GetAnalysisValueFromObjects(ee.ProjectState.GetTypeFromObject(n.Value)).GetInstanceType();
            }

            return ee.ProjectState.GetConstant(n.Value);
        }

        private static IAnalysisSet EvaluateConditional(ExpressionEvaluator ee, Node node) {
            var n = (ConditionalExpression)node;
            ee.Evaluate(n.Test);
            var result = ee.Evaluate(n.TrueExpression);
            return result.Union(ee.Evaluate(n.FalseExpression));
        }

        private static IAnalysisSet EvaluateBackQuote(ExpressionEvaluator ee, Node node) {
            return ee.ProjectState.ClassInfos[BuiltinTypeId.Str].SelfSet;
        }

        private static IAnalysisSet EvaluateAnd(ExpressionEvaluator ee, Node node) {
            var n = (AndExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static IAnalysisSet EvaluateAwait(ExpressionEvaluator ee, Node node) {
            var n = (AwaitExpression)node;
            return ee.Evaluate(n.Expression).Await(node, ee._unit);
        }

        private static IAnalysisSet EvaluateCall(ExpressionEvaluator ee, Node node) {
            // Get the argument types that we're providing at this call site
            var n = (CallExpression)node;
            var argTypes = ee.Evaluate(n.Args);

            // Then lookup the possible methods we're calling
            var targetRefs = ee.Evaluate(n.Target);

            var res = AnalysisSet.Empty;
            var namedArgs = GetNamedArguments(n.Args);
            foreach (var target in targetRefs) {
                res = res.Union(target.Call(node, ee._unit, argTypes, namedArgs));
            }

            return res;
        }


        private static NameExpression[] GetNamedArguments(IList<Arg> args) {
            NameExpression[] res = null;
            for (int i = 0; i < args.Count; i++) {
                if (args[i].Name != null) {
                    if (res == null) {
                        res = new NameExpression[args.Count - i];
                    }

                    res[i - (args.Count - res.Length)] = (NameExpression)args[i].NameExpression;
                } else if (res != null) {
                    res[i - (args.Count - res.Length)] = NameExpression.Empty;
                }
            }
            return res ?? EmptyNames;
        }

        private static IAnalysisSet EvaluateUnary(ExpressionEvaluator ee, Node node) {
            var n = (UnaryExpression)node;
            return ee.Evaluate(n.Expression).UnaryOperation(node, ee._unit, n.Op); ;
        }

        private static IAnalysisSet EvaluateBinary(ExpressionEvaluator ee, Node node) {
            var n = (BinaryExpression)node;

            return ee.Evaluate(n.Left).BinaryOperation(node, ee._unit, n.Operator, ee.Evaluate(n.Right));
        }

        private static IAnalysisSet EvaluateYield(ExpressionEvaluator ee, Node node) {
            var yield = (YieldExpression)node;
            var scope = ee.Scope as FunctionScope;
            if (scope != null && scope.Generator != null) {
                var gen = scope.Generator;
                var res = ee.Evaluate(yield.Expression);

                gen.AddYield(node, ee._unit, res);

                gen.Sends.AddDependency(ee._unit);
                return gen.Sends.Types;
            }
            return AnalysisSet.Empty;
        }

        private static IAnalysisSet EvaluateYieldFrom(ExpressionEvaluator ee, Node node) {
            var yield = (YieldFromExpression)node;
            var scope = ee.Scope as FunctionScope;
            if (scope != null && scope.Generator != null) {
                var gen = scope.Generator;
                var res = ee.Evaluate(yield.Expression);

                gen.AddYieldFrom(node, ee._unit, res);

                return res.GetReturnForYieldFrom(node, ee._unit);
            }

            return AnalysisSet.Empty;
        }

        private static IAnalysisSet EvaluateListComprehension(ExpressionEvaluator ee, Node node) {
            if (ee._unit.State.LanguageVersion.Is2x()) {
                // list comprehension is in enclosing scope in 2.x
                ListComprehension listComp = (ListComprehension)node;

                WalkComprehension(ee, listComp, start: 0);

                var listInfo = (ListInfo)ee.Scope.GetOrMakeNodeValue(
                    node,
                    NodeValueKind.ListComprehension,
                    (x) => new ListInfo(
                        VariableDef.EmptyArray,
                        ee._unit.State.ClassInfos[BuiltinTypeId.List],
                        node,
                        ee._unit.ProjectEntry
                    ).SelfSet);

                listInfo.AddTypes(ee._unit, new[] { ee.Evaluate(listComp.Item) });

                return listInfo.SelfSet;
            } else {
                // list comprehension has its own scope in 3.x
                return EvaluateComprehension(ee, node);
            }
        }

        internal static void WalkComprehension(ExpressionEvaluator ee, Comprehension comp, int start = 1) {
            foreach (var compFor in comp.Iterators.Skip(start).OfType<ComprehensionFor>()) {
                var listTypes = ee.Evaluate(compFor.List);
                ee.AssignTo(comp, compFor.Left, listTypes.GetEnumeratorTypes(comp, ee._unit));
            }

            foreach (var compIf in comp.Iterators.OfType<ComprehensionIf>()) {
                ee.EvaluateMaybeNull(compIf.Test);
            }
        }

        private static IAnalysisSet EvaluateComprehension(ExpressionEvaluator ee, Node node) {
            InterpreterScope scope;
            if (!ee._unit.Scope.TryGetNodeScope(node, out scope)) {
                // we can fail to find the module if the underlying interpreter triggers a module
                // reload.  In that case we're already parsing, we start to clear out all of 
                // our module state in ModuleInfo.Clear, and then we queue the nodes to be
                // re-analyzed immediately after.  We continue analyzing, and we don't find
                // the node.  We can safely ignore it here as the re-analysis will kick in
                // and get us the right info.
                return AnalysisSet.Empty;
            }

            ComprehensionScope compScope = (ComprehensionScope)scope;

            return compScope.AnalysisValue.SelfSet;
        }

        internal void AssignTo(Node assignStmt, Expression left, IAnalysisSet values) {
            if (left is ExpressionWithAnnotation) {
                left = ((ExpressionWithAnnotation)left).Expression;
                // "x:t=..." is a recommended pattern - we do not want to
                // actually assign the ellipsis in this case.
                if (values.Any(v => v.TypeId == BuiltinTypeId.Ellipsis)) {
                    values = AnalysisSet.Create(values.Where(v => v.TypeId != BuiltinTypeId.Ellipsis), values.Comparer);
                }
            }

            if (left is NameExpression) {
                var l = (NameExpression)left;
                if (!string.IsNullOrEmpty(l.Name)) {
                    Scope.AssignVariable(
                        l.Name,
                        l,
                        _unit,
                        values
                    );
                }
            } else if (left is MemberExpression) {
                var l = (MemberExpression)left;
                if (!string.IsNullOrEmpty(l.Name)) {
                    foreach (var obj in Evaluate(l.Target).Resolve(_unit)) {
                        obj.SetMember(l, _unit, l.Name, values.Resolve(_unit));
                    }
                }
            } else if (left is IndexExpression) {
                var l = (IndexExpression)left;
                var indexObj = Evaluate(l.Index);
                foreach (var obj in Evaluate(l.Target).Resolve(_unit)) {
                    obj.SetIndex(assignStmt, _unit, indexObj, values.Resolve(_unit));
                }
            } else if (left is SequenceExpression) {
                // list/tuple
                var l = (SequenceExpression)left;
                var valuesArr = values.ToArray();
                for (int i = 0; i < l.Items.Count; i++) {
                    if (valuesArr.Length > 0) {
                        foreach (var value in valuesArr) {
                            AssignTo(assignStmt, l.Items[i], value.GetIndex(assignStmt, _unit, ProjectState.GetConstant(i)));
                        }
                    } else {
                        AssignTo(assignStmt, l.Items[i], AnalysisSet.Empty);
                    }
                }
            }
        }

        private static IAnalysisSet EvaluateLambda(ExpressionEvaluator ee, Node node) {
            var lambda = (LambdaExpression)node;

            return ee.Scope.GetOrMakeNodeValue(node, NodeValueKind.LambdaFunction, n => MakeLambdaFunction(lambda, ee));
        }

        private static IAnalysisSet MakeLambdaFunction(LambdaExpression node, ExpressionEvaluator ee) {
            var res = OverviewWalker.AddFunction(node.Function, ee._unit, ee.Scope);
            if (res != null) {
                return res.SelfSet;
            }
            return AnalysisSet.Empty;
        }

        private static IAnalysisSet EvaluateSlice(ExpressionEvaluator ee, Node node) {
            SliceExpression se = node as SliceExpression;
            ee.EvaluateMaybeNull(se.SliceStart);
            ee.EvaluateMaybeNull(se.SliceStop);
            if (se.StepProvided) {
                ee.EvaluateMaybeNull(se.SliceStep);
            }

            return SliceInfo.Instance;/*
            return ee.GlobalScope.GetOrMakeNodeValue(
                node, 
                x=> SliceInfo.Instance
            );*/
        }

        private IAnalysisSet MakeSequence(ExpressionEvaluator ee, Node node) {
            var sequence = (SequenceInfo)ee.Scope.GetOrMakeNodeValue(node, NodeValueKind.Sequence, x => {
                if (node is ListExpression) {
                    return new ListInfo(
                        VariableDef.EmptyArray,
                        _unit.State.ClassInfos[BuiltinTypeId.List],
                        node,
                        _unit.ProjectEntry
                    ).SelfSet;
                } else {
                    Debug.Assert(node is TupleExpression);
                    return new SequenceInfo(
                        VariableDef.EmptyArray,
                        _unit.State.ClassInfos[BuiltinTypeId.Tuple],
                        node,
                        _unit.ProjectEntry
                    ).SelfSet;
                }
            });
            var seqItems = ((SequenceExpression)node).Items;
            var indexValues = new IAnalysisSet[seqItems.Count];

            for (int i = 0; i < seqItems.Count; i++) {
                indexValues[i] = Evaluate(seqItems[i]);
            }
            sequence.AddTypes(ee._unit, indexValues);
            return sequence.SelfSet;
        }

        #endregion
    }
}
