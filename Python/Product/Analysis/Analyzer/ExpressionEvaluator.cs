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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    internal class ExpressionEvaluator {
        private readonly AnalysisUnit _unit;
        private readonly bool _mergeScopes;

        internal static readonly INamespaceSet[] EmptyNamespaces = new INamespaceSet[0];
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
        public INamespaceSet Evaluate(Expression node) {
            var res = EvaluateWorker(node);
            Debug.Assert(res != null);
            return res;
        }

        public INamespaceSet EvaluateMaybeNull(Expression node) {
            if (node == null) {
                return null;
            }

            return Evaluate(node);
        }

        /// <summary>
        /// Returns a sequence of possible types associated with the name in the expression evaluators scope.
        /// </summary>
        public INamespaceSet LookupNamespaceByName(Node node, string name, bool addRef = true) {
            if (_mergeScopes) {
                var scope = Scope.EnumerateTowardsGlobal.FirstOrDefault(s => (s == Scope || s.VisibleToChildren) && s.Variables.ContainsKey(name));
                if (scope != null) {
                    return scope.GetMergedVariableTypes(name);
                }
            } else {
                foreach (var scope in Scope.EnumerateTowardsGlobal) {
                    if (scope == Scope || scope.VisibleToChildren) {
                        var refs = scope.GetVariable(node, _unit, name, addRef);
                        if (refs != null) {
                            if (addRef) {
                                var linkedVars = scope.GetLinkedVariablesNoCreate(name);
                                if (linkedVars != null) {
                                    foreach (var linkedVar in linkedVars) {
                                        linkedVar.AddReference(node, _unit);
                                    }
                                }
                            }
                            return refs.Types;
                        }
                    }
                }
            }

            return ProjectState.BuiltinModule.GetMember(node, _unit, name);
        }

        #endregion

        #region Implementation Details

        private ModuleInfo GlobalScope {
            get { return _unit.DeclaringModule; }
        }

        private PythonAnalyzer ProjectState {
            get { return _unit.ProjectState; }
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

        private INamespaceSet[] Evaluate(IList<Arg> nodes) {
            var result = new INamespaceSet[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) {
                result[i] = Evaluate(nodes[i].Expression);
            }
            return result;
        }

        private INamespaceSet EvaluateWorker(Node node) {
            EvalDelegate eval;
            if (_evaluators.TryGetValue(node.GetType(), out eval)) {
                return eval(this, node);
            }

            return NamespaceSet.Empty;
        }

        delegate INamespaceSet EvalDelegate(ExpressionEvaluator ee, Node node);

        private static Dictionary<Type, EvalDelegate> _evaluators = new Dictionary<Type, EvalDelegate> {
            { typeof(AndExpression), ExpressionEvaluator.EvaluateAnd },
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
        };

        private static INamespaceSet EvaluateSequence(ExpressionEvaluator ee, Node node) {
            // Covers both ListExpression and TupleExpression
            // TODO: We need to update the sequence on each re-evaluation, not just
            // evaluate it once.
            return ee.MakeSequence(ee, node);
        }

        private static INamespaceSet EvaluateParenthesis(ExpressionEvaluator ee, Node node) {
            var n = (ParenthesisExpression)node;
            return ee.Evaluate(n.Expression);
        }

        private static INamespaceSet EvaluateOr(ExpressionEvaluator ee, Node node) {
            // TODO: Warn if lhs is always false
            var n = (OrExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static INamespaceSet EvaluateName(ExpressionEvaluator ee, Node node) {
            var n = (NameExpression)node;
            var res = ee.LookupNamespaceByName(node, n.Name);
            foreach (var value in res) {
                value.AddReference(node, ee._unit);
            }
            return res;
        }

        private static INamespaceSet EvaluateMember(ExpressionEvaluator ee, Node node) {
            var n = (MemberExpression)node;
            return ee.Evaluate(n.Target).GetMember(node, ee._unit, n.Name);
        }

        private static INamespaceSet EvaluateIndex(ExpressionEvaluator ee, Node node) {
            var n = (IndexExpression)node;

            return ee.Evaluate(n.Target).GetIndex(n, ee._unit, ee.Evaluate(n.Index));
        }

        private static INamespaceSet EvaluateSet(ExpressionEvaluator ee, Node node) {
            var n = (SetExpression)node;

            var setInfo = (SetInfo)ee.Scope.GetOrMakeNodeValue(node, x => new SetInfo(ee.ProjectState, x));
            foreach (var x in n.Items) {
                setInfo.AddTypes(ee._unit, ee.Evaluate(x));
            }

            return setInfo;
        }

        private static INamespaceSet EvaluateDictionary(ExpressionEvaluator ee, Node node) {
            var n = (DictionaryExpression)node;
            INamespaceSet result = ee.Scope.GetOrMakeNodeValue(node, _ => {
                var dictInfo = new DictionaryInfo(ee._unit.ProjectEntry, node);
                result = dictInfo.SelfSet;

                var keys = new HashSet<Namespace>();
                var values = new HashSet<Namespace>();
                foreach (var x in n.Items) {
                    dictInfo.SetIndex(
                        node,
                        ee._unit,
                        ee.EvaluateMaybeNull(x.SliceStart) ?? NamespaceSet.Empty,
                        ee.EvaluateMaybeNull(x.SliceStop) ?? NamespaceSet.Empty
                    );
                }

                return result;
            });
            return result;
        }

        private static INamespaceSet EvaluateConstant(ExpressionEvaluator ee, Node node) {
            var n = (ConstantExpression)node;
            if (n.Value is double ||
                (n.Value is int && ((int)n.Value) > 100)) {
                return ((BuiltinClassInfo)ee.ProjectState.GetNamespaceFromObjects(ee.ProjectState.GetTypeFromObject(n.Value))).Instance.SelfSet;
            }

            return ee.ProjectState.GetConstant(n.Value);
        }

        private static INamespaceSet EvaluateConditional(ExpressionEvaluator ee, Node node) {
            var n = (ConditionalExpression)node;
            ee.Evaluate(n.Test);
            var result = ee.Evaluate(n.TrueExpression);
            return result.Union(ee.Evaluate(n.FalseExpression));
        }

        private static INamespaceSet EvaluateBackQuote(ExpressionEvaluator ee, Node node) {
            return ee.ProjectState.ClassInfos[BuiltinTypeId.Str].SelfSet;
        }

        private static INamespaceSet EvaluateAnd(ExpressionEvaluator ee, Node node) {
            var n = (AndExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static INamespaceSet EvaluateCall(ExpressionEvaluator ee, Node node) {
            // Get the argument types that we're providing at this call site
            var n = (CallExpression)node;
            var argTypes = ee.Evaluate(n.Args);

            // Then lookup the possible methods we're calling
            var targetRefs = ee.Evaluate(n.Target);

            var res = NamespaceSet.Empty;
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
                }
            }
            return res ?? EmptyNames;
        }

        private static INamespaceSet EvaluateUnary(ExpressionEvaluator ee, Node node) {
            var n = (UnaryExpression)node;
            return ee.Evaluate(n.Expression).UnaryOperation(node, ee._unit, n.Op); ;
        }

        private static INamespaceSet EvaluateBinary(ExpressionEvaluator ee, Node node) {
            var n = (BinaryExpression)node;

            return ee.Evaluate(n.Left).BinaryOperation(node, ee._unit, n.Operator, ee.Evaluate(n.Right));
        }

        private static INamespaceSet EvaluateYield(ExpressionEvaluator ee, Node node) {
            var yield = (YieldExpression)node;
            var scope = ee.Scope as FunctionScope;
            if (scope != null && scope.Generator != null) {
                var gen = scope.Generator;
                var res = ee.Evaluate(yield.Expression);

                gen.AddYield(node, ee._unit, res);

                gen.Sends.AddDependency(ee._unit);
                return gen.Sends.Types;
            }
            return NamespaceSet.Empty;
        }

        private static INamespaceSet EvaluateYieldFrom(ExpressionEvaluator ee, Node node) {
            var yield = (YieldFromExpression)node;
            var scope = ee.Scope as FunctionScope;
            if (scope != null && scope.Generator != null) {
                var gen = scope.Generator;
                var res = ee.Evaluate(yield.Expression);

                gen.AddYieldFrom(node, ee._unit, res);

                gen.Returns.AddDependency(ee._unit);
                return gen.Returns.Types;
            }

            return NamespaceSet.Empty;
        }

        private static INamespaceSet EvaluateListComprehension(ExpressionEvaluator ee, Node node) {
            if (!ee._unit.ProjectState.LanguageVersion.Is3x()) {
                // list comprehension is in enclosing scope in 2.x
                ListComprehension listComp = (ListComprehension)node;

                WalkComprehension(ee, listComp, start: 0);

                var listInfo = (ListInfo)ee.Scope.GetOrMakeNodeValue(
                    node,
                    (x) => new ListInfo(VariableDef.EmptyArray, ee._unit.ProjectState.ClassInfos[BuiltinTypeId.List], node).SelfSet);

                listInfo.AddTypes(ee._unit, new[] { ee.Evaluate(listComp.Item) });

                return listInfo.SelfSet;
            } else {
                // list comprehension has its own scope in 3.x
                return EvaluateComprehension(ee, node);
            }
        }

        internal static void WalkComprehension(ExpressionEvaluator ee, Comprehension comp, int start = 1) {
            for (int i = start; i < comp.Iterators.Count; i++) {
                ComprehensionFor compFor = comp.Iterators[i] as ComprehensionFor;
                if (compFor != null) {
                    foreach (var listType in ee.Evaluate(compFor.List)) {
                        ee.AssignTo(comp, compFor.Left, listType.GetEnumeratorTypes(comp, ee._unit));
                    }
                }

                ComprehensionIf compIf = comp.Iterators[i] as ComprehensionIf;
                if (compIf != null) {
                    ee.EvaluateMaybeNull(compIf.Test);
                }
            }
        }

        private static INamespaceSet EvaluateComprehension(ExpressionEvaluator ee, Node node) {
            InterpreterScope scope;
            if (!ee._unit.Scope.TryGetNodeScope(node, out scope)) {
                // we can fail to find the module if the underlying interpreter triggers a module
                // reload.  In that case we're already parsing, we start to clear out all of 
                // our module state in ModuleInfo.Clear, and then we queue the nodes to be
                // re-analyzed immediately after.  We continue analyzing, and we don't find
                // the node.  We can safely ignore it here as the re-analysis will kick in
                // and get us the right info.
                return NamespaceSet.Empty;
            }

            ComprehensionScope compScope = (ComprehensionScope)scope;

            return compScope.Namespace.SelfSet;
        }

        internal void AssignTo(Node assignStmt, Expression left, INamespaceSet values) {
            if (left is NameExpression) {
                var l = (NameExpression)left;
                if (l.Name != null) {
                    var vars = Scope.CreateVariable(l, _unit, l.Name, false);

                    if (Scope is IsInstanceScope && Scope.OuterScope != null) {
                        var outerVar = Scope.OuterScope.GetVariable(l, _unit, l.Name, false);
                        if (outerVar != null && outerVar != vars) {
                            outerVar.AddAssignment(left, _unit);
                            outerVar.AddTypes(_unit, values);
                        }
                    }

                    vars.AddAssignment(left, _unit);
                    vars.AddTypes(_unit, values);

                    if (Scope is ClassScope && l.Name == "__metaclass__") {
                        // assignment to __metaclass__, save it in our metaclass variable
                        ((ClassScope)Scope).Class.GetOrCreateMetaclassVariable().AddTypes(_unit, values);
                    }
                }
            } else if (left is MemberExpression) {
                var l = (MemberExpression)left;
                if (l.Name != null) {
                    foreach (var obj in Evaluate(l.Target)) {
                        obj.SetMember(l, _unit, l.Name, values);
                    }
                }
            } else if (left is IndexExpression) {
                var l = (IndexExpression)left;
                var indexObj = Evaluate(l.Index);
                foreach (var obj in Evaluate(l.Target)) {
                    obj.SetIndex(assignStmt, _unit, indexObj, values);
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
                        AssignTo(assignStmt, l.Items[i], NamespaceSet.Empty);
                    }
                }
            }
        }

        private static INamespaceSet EvaluateLambda(ExpressionEvaluator ee, Node node) {
            var lambda = (LambdaExpression)node;

            return ee.Scope.GetOrMakeNodeValue(node, n => MakeLambdaFunction(lambda, ee));
        }

        private static INamespaceSet MakeLambdaFunction(LambdaExpression node, ExpressionEvaluator ee) {
            var res = OverviewWalker.AddFunction(node.Function, ee._unit, ee.Scope);
            if (res != null) {
                return res.SelfSet;
            }
            return NamespaceSet.Empty;
        }

        private static INamespaceSet EvaluateSlice(ExpressionEvaluator ee, Node node) {
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

        private INamespaceSet MakeSequence(ExpressionEvaluator ee, Node node) {
            var sequence = (SequenceInfo)ee.Scope.GetOrMakeNodeValue(node, x => {
                if (node is ListExpression) {
                    return new ListInfo(VariableDef.EmptyArray, _unit.ProjectState.ClassInfos[BuiltinTypeId.List], node).SelfSet;
                } else {
                    Debug.Assert(node is TupleExpression);
                    return new SequenceInfo(VariableDef.EmptyArray, _unit.ProjectState.ClassInfos[BuiltinTypeId.Tuple], node).SelfSet;
                }
            });
            var seqItems = ((SequenceExpression)node).Items;
            var indexValues = new INamespaceSet[seqItems.Count];

            for (int i = 0; i < seqItems.Count; i++) {
                indexValues[i] = Evaluate(seqItems[i]);
            }
            sequence.AddTypes(ee._unit, indexValues);
            return sequence.SelfSet;
        }

        #endregion
    }
}
