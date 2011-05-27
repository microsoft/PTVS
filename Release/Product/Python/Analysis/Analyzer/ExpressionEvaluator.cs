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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    internal class ExpressionEvaluator {
        private readonly AnalysisUnit _unit;
        private readonly InterpreterScope[] _currentScopes;

        internal static NameExpression[] EmptyNames = new NameExpression[0];

        /// <summary>
        /// Creates a new ExpressionEvaluator that will evaluate in the context of the top-level module.
        /// </summary>
        public ExpressionEvaluator(AnalysisUnit unit) {
            _unit = unit;
            _currentScopes = unit.Scopes;
        }

        public ExpressionEvaluator(AnalysisUnit unit, InterpreterScope[] scopes) {
            _unit = unit;
            _currentScopes = scopes;
        }

        #region Public APIs

        /// <summary>
        /// Returns possible variable refs associated with the expr in the expression evaluators scope.
        /// </summary>
        public ISet<Namespace> Evaluate(Expression node) {
            var res = EvaluateWorker(node);
            Debug.Assert(res != null);
            return res;
        }

        public ISet<Namespace> EvaluateMaybeNull(Expression node) {
            if (node == null) {
                return null;
            }

            return Evaluate(node);
        }

        /// <summary>
        /// Returns a sequence of possible types associated with the name in the expression evaluators scope.
        /// </summary>
        public ISet<Namespace> LookupNamespaceByName(Node node, string name, bool addRef = true) {
            for (int i = Scopes.Length - 1; i >= 0; i--) {
                if (i == Scopes.Length - 1 || Scopes[i].VisibleToChildren) {
                    var refs = Scopes[i].GetVariable(node, _unit, name, addRef);
                    if (refs != null) {
                        if (addRef) {
                            var linkedVars = Scopes[i].GetLinkedVariablesNoCreate(name);
                            if (linkedVars != null) {
                                foreach (var linkedVar in linkedVars) {
                                    linkedVar.AddDependency(_unit);
                                }
                            }
                        }
                        return refs.Types;
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
        /// Gets the list of scopes which define the current context.
        /// </summary>
        private InterpreterScope[] Scopes {
            get { return _currentScopes; }
        }

        private ISet<Namespace>[] Evaluate(IList<Arg> nodes) {
            var result = new ISet<Namespace>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++) {
                result[i] = Evaluate(nodes[i].Expression);
            }
            return result;
        }

        private ISet<Namespace> EvaluateWorker(Node node) {
            EvalDelegate eval;
            if (_evaluators.TryGetValue(node.GetType(), out eval)) {
                return eval(this, node);
            }

            return EmptySet<Namespace>.Instance;
        }

        delegate ISet<Namespace> EvalDelegate(ExpressionEvaluator ee, Node node);

        private static Dictionary<Type, EvalDelegate> _evaluators = new Dictionary<Type, EvalDelegate> {
            { typeof(AndExpression),  ExpressionEvaluator.EvaluateAnd }, 
            { typeof(BackQuoteExpression),  ExpressionEvaluator.EvaluateBackQuote },
            { typeof(BinaryExpression),  ExpressionEvaluator.EvaluateBinary },
            { typeof(CallExpression),  ExpressionEvaluator.EvaluateCall},
            { typeof(ConditionalExpression),  ExpressionEvaluator.EvaluateConditional},
            { typeof(ConstantExpression),  ExpressionEvaluator.EvaluateConstant},
            { typeof(DictionaryExpression),  ExpressionEvaluator.EvaluateDictionary},
            { typeof(SetExpression),  ExpressionEvaluator.EvaluateSet},
            { typeof(GeneratorExpression),  ExpressionEvaluator.EvaluateGenerator},
            { typeof(IndexExpression),  ExpressionEvaluator.EvaluateIndex},
            { typeof(LambdaExpression),  ExpressionEvaluator.EvaluateLambda},
            { typeof(ListComprehension),  ExpressionEvaluator.EvaluateListComprehension},
            { typeof(MemberExpression),  ExpressionEvaluator.EvaluateMember},
            { typeof(NameExpression),  ExpressionEvaluator.EvaluateName},
            { typeof(OrExpression),  ExpressionEvaluator.EvaluateOr},
            { typeof(ParenthesisExpression),  ExpressionEvaluator.EvaluateParenthesis},
            { typeof(UnaryExpression),  ExpressionEvaluator.EvaluateUnary },
            { typeof(YieldExpression),  ExpressionEvaluator.EvaluateYield},
            { typeof(TupleExpression),  ExpressionEvaluator.EvaluateSequence},
            { typeof(ListExpression),  ExpressionEvaluator.EvaluateSequence},            
            { typeof(SliceExpression),  ExpressionEvaluator.EvaluateSlice},            
        };

        private static ISet<Namespace> EvaluateSequence(ExpressionEvaluator ee, Node node) {
            // Covers both ListExpression and TupleExpression
            return ee.GlobalScope.GetOrMakeNodeVariable(node, (n) => ee.MakeSequence(ee, n));
        }

        private static ISet<Namespace> EvaluateParenthesis(ExpressionEvaluator ee, Node node) {
            var n = (ParenthesisExpression)node;
            return ee.Evaluate(n.Expression);
        }

        private static ISet<Namespace> EvaluateOr(ExpressionEvaluator ee, Node node) {
            // TODO: Warn if lhs is always false
            var n = (OrExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static ISet<Namespace> EvaluateName(ExpressionEvaluator ee, Node node) {
            var n = (NameExpression)node;
            var res = ee.LookupNamespaceByName(node, n.Name);
            foreach (var value in res) {
                value.AddReference(node, ee._unit);
            }
            return res;
        }

        private static ISet<Namespace> EvaluateMember(ExpressionEvaluator ee, Node node) {
            var n = (MemberExpression)node;
            return ee.Evaluate(n.Target).GetMember(node, ee._unit, n.Name);
        }

        private static ISet<Namespace> EvaluateIndex(ExpressionEvaluator ee, Node node) {
            var n = (IndexExpression)node;

            return ee.Evaluate(n.Target).GetIndex(n, ee._unit, ee.Evaluate(n.Index));
        }

        private static ISet<Namespace> EvaluateSet(ExpressionEvaluator ee, Node node) {
            var n = (SetExpression)node;
            ISet<Namespace> result;
            if (!ee.GlobalScope.NodeVariables.TryGetValue(node, out result)) {
                var values = new HashSet<Namespace>();
                foreach (var x in n.Items) {
                    values.Union(ee.Evaluate(x));
                }

                result = new DictionaryInfo(values, values, ee.ProjectState).SelfSet;
                ee.GlobalScope.NodeVariables[node] = result;
            }
            return result;
        }

        private static ISet<Namespace> EvaluateDictionary(ExpressionEvaluator ee, Node node) {
            var n = (DictionaryExpression)node;
            ISet<Namespace> result;
            if (!ee.GlobalScope.NodeVariables.TryGetValue(node, out result)) {
                var keys = new HashSet<Namespace>();
                var values = new HashSet<Namespace>();
                foreach (var x in n.Items) {
                    foreach (var keyVal in ee.Evaluate(x.SliceStart)) {
                        keys.Add(keyVal);
                    }
                    foreach (var itemVal in ee.Evaluate(x.SliceStop)) {
                        values.Add(itemVal);
                    }
                }

                result = new DictionaryInfo(keys, values, ee.ProjectState).SelfSet;
                ee.GlobalScope.NodeVariables[node] = result;
            }
            return result;
        }

        private static ISet<Namespace> EvaluateConstant(ExpressionEvaluator ee, Node node) {
            var n = (ConstantExpression)node;
            return ee.ProjectState.GetConstant(n.Value);
        }

        private static ISet<Namespace> EvaluateConditional(ExpressionEvaluator ee, Node node) {
            var n = (ConditionalExpression)node;
            ee.Evaluate(n.Test);
            var result = ee.Evaluate(n.TrueExpression);
            return result.Union(ee.Evaluate(n.FalseExpression));
        }

        private static ISet<Namespace> EvaluateBackQuote(ExpressionEvaluator ee, Node node) {
            return ee.ProjectState._stringType.SelfSet;
        }

        private static ISet<Namespace> EvaluateAnd(ExpressionEvaluator ee, Node node) {
            var n = (AndExpression)node;
            var result = ee.Evaluate(n.Left);
            return result.Union(ee.Evaluate(n.Right));
        }

        private static ISet<Namespace> EvaluateCall(ExpressionEvaluator ee, Node node) {
            // TODO: Splatting, keyword args

            // Get the argument types that we're providing at this call site
            var n = (CallExpression)node;
            var argTypes = ee.Evaluate(n.Args);

            // Then lookup the possible methods we're calling
            var targetRefs = ee.Evaluate(n.Target);

            ISet<Namespace> res = EmptySet<Namespace>.Instance;
            bool madeSet = false;
            foreach (var target in targetRefs) {
                res = res.Union(target.Call(node, ee._unit, argTypes, GetNamedArguments(n.Args)), ref madeSet);
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

        private static ISet<Namespace> EvaluateUnary(ExpressionEvaluator ee, Node node) {            
            var n = (UnaryExpression)node;
            return ee.Evaluate(n.Expression).UnaryOperation(node, ee._unit, n.Op); ;
        }

        private static ISet<Namespace> EvaluateBinary(ExpressionEvaluator ee, Node node) {
            var n = (BinaryExpression)node;

            return ee.Evaluate(n.Left).BinaryOperation(node, ee._unit, n.Operator, ee.Evaluate(n.Right));
        }

        private static ISet<Namespace> EvaluateYield(ExpressionEvaluator ee, Node node) {
            var yield = (YieldExpression)node;
            var funcDef = ee._currentScopes[ee._currentScopes.Length - 1].Namespace as FunctionInfo;
            if (funcDef != null) {
                var gen = funcDef.Generator;

                gen.AddYield(ee.Evaluate(yield.Expression));

                return gen.Sends.Types;
            }

            return EmptySet<Namespace>.Instance;
        }

        private static ISet<Namespace> EvaluateListComprehension(ExpressionEvaluator ee, Node node) {
            ListComprehension listComp = (ListComprehension)node;
                       
            for(int i = 0; i<listComp.Iterators.Count;i++) {
                ComprehensionFor compFor = listComp.Iterators[i] as ComprehensionFor;
                if (compFor != null) {
                    foreach (var listType in ee.Evaluate(compFor.List)) {
                        ee.AssignTo(node, compFor.Left, listType.GetEnumeratorTypes(node, ee._unit));
                    }
                }
            }

            return ee.GlobalScope.GetOrMakeNodeVariable(
                node,
                (x) => new ListInfo(new [] { ee.Evaluate(listComp.Item) }, ee._unit.ProjectState._listType).SelfSet);
        }

        private static ISet<Namespace> EvaluateGenerator(ExpressionEvaluator ee, Node node) {
            GeneratorExpression gen = (GeneratorExpression)node;

            for (int i = 0; i < gen.Iterators.Count; i++) {
                ComprehensionFor compFor = gen.Iterators[i] as ComprehensionFor;
                if (compFor != null) {
                    foreach (var listType in ee.Evaluate(compFor.List)) {
                        ee.AssignTo(node, compFor.Left, listType.GetEnumeratorTypes(node, ee._unit));
                    }
                }
            }


            var res = (GeneratorInfo)ee.GlobalScope.GetOrMakeNodeVariable(
                node,
                (x) => new GeneratorInfo(new FunctionInfo(ee._unit)));

            res.AddYield(ee.Evaluate(gen.Item));

            return res.SelfSet;
        }

        internal void AssignTo(Node assignStmt, Expression left, ISet<Namespace> values) {
            if (left is NameExpression) {
                var l = (NameExpression)left;
                var vars = Scopes[Scopes.Length - 1].CreateVariable(l, _unit, l.Name, false);
                    
                vars.AddAssignment(left, _unit);
                vars.AddTypes(l, _unit, values);
            } else if (left is MemberExpression) {
                var l = (MemberExpression)left;
                foreach (var obj in Evaluate(l.Target)) {
                    obj.SetMember(l, _unit, l.Name, values);
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
                foreach (var value in values.ToArray()) {
                    for (int i = 0; i < l.Items.Count; i++) {
                        AssignTo(assignStmt, l.Items[i], value.GetIndex(assignStmt, _unit, ProjectState.GetConstant(i)));
                    }
                }
            }
        }

        private static ISet<Namespace> EvaluateLambda(ExpressionEvaluator ee, Node node) {
            var lambda = (LambdaExpression)node;

            return ee.GlobalScope.GetOrMakeNodeVariable(node, n => MakeLambdaFunction(lambda, ee));
        }

        private static ISet<Namespace> MakeLambdaFunction(LambdaExpression node, ExpressionEvaluator ee) {
            var res = OverviewWalker.AddFunction(node.Function, ee._unit);
            if (res != null) {
                return res.SelfSet;
            }
            return EmptySet<Namespace>.Instance;
        }

        private static ISet<Namespace> EvaluateSlice(ExpressionEvaluator ee, Node node) {
            SliceExpression se = node as SliceExpression;

            return ee.GlobalScope.GetOrMakeNodeVariable(
                node, 
                (n) => new SliceInfo(
                    ee.EvaluateMaybeNull(se.SliceStart),
                    ee.EvaluateMaybeNull(se.SliceStop),
                    se.StepProvided ? ee.EvaluateMaybeNull(se.SliceStep) : null
                )
            );
        }

        private ISet<Namespace> MakeSequence(ExpressionEvaluator ee, Node node) {
            ISet<Namespace> result;
            if (!ee.GlobalScope.NodeVariables.TryGetValue(node, out result)) {
                var seqItems = ((SequenceExpression)node).Items;
                var indexValues = new ISet<Namespace>[seqItems.Count];

                for (int i = 0; i < seqItems.Count; i++) {
                    indexValues[i] = Evaluate(seqItems[i]);
                }

                ISet<Namespace> sequence;
                if (node is ListExpression) {
                    sequence = new ListInfo(indexValues, _unit.ProjectState._listType).SelfSet;
                } else {
                    Debug.Assert(node is TupleExpression);
                    sequence = new SequenceInfo(indexValues, _unit.ProjectState._tupleType).SelfSet;
                }

                ee.GlobalScope.NodeVariables[node] = result = sequence;
            }

            return result;
        }

        #endregion
    }
}
