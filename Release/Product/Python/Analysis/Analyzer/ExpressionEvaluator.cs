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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    internal class ExpressionEvaluator {
        private readonly AnalysisUnit _unit;
        internal InterpreterScope[] _currentScopes;

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
                                    linkedVar.AddReference(node, _unit);
                                }
                            }

                            IsInstanceScope isInstScope = Scopes[Scopes.Length - 1] as IsInstanceScope;
                            VariableDef outerVar;
                            if (isInstScope != null && isInstScope.OuterVariables.TryGetValue(name, out outerVar)) {
                                outerVar.AddReference(node, _unit);
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
            { typeof(DictionaryComprehension),  ExpressionEvaluator.EvaluateDictionaryComp},
            { typeof(SetComprehension),  ExpressionEvaluator.EvaluateSetComp},
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
            // TODO: We need to update the sequence on each re-evaluation, not just
            // evaluate it once.
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
            
            var setInfo = (SetInfo)ee.GlobalScope.GetOrMakeNodeVariable(node, x => new SetInfo(ee.ProjectState));            
            foreach (var x in n.Items) {
                setInfo.AddTypes(ee._unit, ee.Evaluate(x));
            }

            return setInfo;
        }

        private static ISet<Namespace> EvaluateSetComp(ExpressionEvaluator ee, Node node) {
            ComprehensionScope compScope = (ComprehensionScope)ee._unit.DeclaringModule.NodeScopes[node];

            return compScope.Namespace.SelfSet;
        }

        private static ISet<Namespace> EvaluateDictionary(ExpressionEvaluator ee, Node node) {
            var n = (DictionaryExpression)node;
            ISet<Namespace> result;
            if (!ee.GlobalScope.NodeVariables.TryGetValue(node, out result)) {
                var dictInfo = new DictionaryInfo(ee._unit.ProjectEntry);
                result = dictInfo.SelfSet;

                var keys = new HashSet<Namespace>();
                var values = new HashSet<Namespace>();
                foreach (var x in n.Items) {
                    dictInfo.SetIndex(
                        node,
                        ee._unit,
                        ee.EvaluateMaybeNull(x.SliceStart) ?? EmptySet<Namespace>.Instance,
                        ee.EvaluateMaybeNull(x.SliceStop) ?? EmptySet<Namespace>.Instance
                    );
                }
                
                ee.GlobalScope.NodeVariables[node] = result;
            }
            return result;
        }

        private static ISet<Namespace> EvaluateDictionaryComp(ExpressionEvaluator ee, Node node) {
            ComprehensionScope compScope = (ComprehensionScope)ee._unit.DeclaringModule.NodeScopes[node];

            return compScope.Namespace.SelfSet;
        }

        private static ISet<Namespace> EvaluateConstant(ExpressionEvaluator ee, Node node) {
            var n = (ConstantExpression)node;
            if (n.Value is double ||
                (n.Value is int && ((int)n.Value) > 100)) {
                return ((BuiltinClassInfo)ee.ProjectState.GetNamespaceFromObjects(ee.ProjectState.GetTypeFromObject(n.Value))).Instance.SelfSet;
            }

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
            var namedArgs = GetNamedArguments(n.Args);
            foreach (var target in targetRefs) {
                res = res.Union(target.Call(node, ee._unit, argTypes, namedArgs), ref madeSet);
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
            var funcDef = ee._currentScopes[ee._currentScopes.Length - 1].Namespace as GeneratorFunctionInfo;
            if (funcDef != null) {
                var gen = funcDef.Generator;

                gen.AddYield(ee.Evaluate(yield.Expression));

                return gen.Sends.Types;
            }

            return EmptySet<Namespace>.Instance;
        }

        private static ISet<Namespace> EvaluateListComprehension(ExpressionEvaluator ee, Node node) {
            if (!ee._unit.ProjectState.LanguageVersion.Is3x()) {
                // list comprehension is in enclosing scope in 2.x
                ListComprehension listComp = (ListComprehension)node;

                WalkComprehension(ee, listComp);

                var listInfo = (ListInfo)ee.GlobalScope.GetOrMakeNodeVariable(
                    node,
                    (x) => new ListInfo(VariableDef.EmptyArray, ee._unit.ProjectState._listType).SelfSet);

                listInfo.AddTypes(ee._unit, new[] { ee.Evaluate(listComp.Item) });
                
                return listInfo.SelfSet;
            } else {
                // list comprehension has its own scope in 3.x
                ComprehensionScope compScope = (ComprehensionScope)ee._unit.DeclaringModule.NodeScopes[node];

                return compScope.Namespace.SelfSet;
            }
        }

        internal static void WalkComprehension(ExpressionEvaluator ee, Comprehension comp) {
            for (int i = 0; i < comp.Iterators.Count; i++) {
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

        private static ISet<Namespace> EvaluateGenerator(ExpressionEvaluator ee, Node node) {
            ComprehensionScope compScope = (ComprehensionScope)ee._unit.DeclaringModule.NodeScopes[node];

            return compScope.Namespace.SelfSet;
        }

        internal void AssignTo(Node assignStmt, Expression left, ISet<Namespace> values) {
            if (left is NameExpression) {
                var l = (NameExpression)left;
                var vars = Scopes[Scopes.Length - 1].CreateVariable(l, _unit, l.Name, false);

                IsInstanceScope isInstScope = Scopes[Scopes.Length - 1] as IsInstanceScope;
                VariableDef outerVar;
                if (isInstScope != null && isInstScope.OuterVariables.TryGetValue(l.Name, out outerVar)) {
                    outerVar.AddAssignment(left, _unit);
                }
                    
                vars.AddAssignment(left, _unit);
                vars.AddTypes(_unit, values);

                if (Scopes[Scopes.Length - 1] is ClassScope && l.Name == "__metaclass__") {
                    // assignment to __metaclass__, save it in our metaclass variable
                    ((ClassScope)Scopes[Scopes.Length - 1]).Class.GetOrCreateMetaclassVariable().AddTypes(_unit, values);
                }
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
                var valuesArr = values.ToArray();
                for (int i = 0; i < l.Items.Count; i++) {
                    if (valuesArr.Length > 0) {
                        foreach (var value in valuesArr) {
                            AssignTo(assignStmt, l.Items[i], value.GetIndex(assignStmt, _unit, ProjectState.GetConstant(i)));
                        }
                    } else {
                        AssignTo(assignStmt, l.Items[i], EmptySet<Namespace>.Instance);
                    }
                }
            }
        }

        private static ISet<Namespace> EvaluateLambda(ExpressionEvaluator ee, Node node) {
            var lambda = (LambdaExpression)node;

            return ee.GlobalScope.GetOrMakeNodeVariable(node, n => MakeLambdaFunction(lambda, ee));
        }

        private static ISet<Namespace> MakeLambdaFunction(LambdaExpression node, ExpressionEvaluator ee) {
            var res = OverviewWalker.AddFunction(node.Function, ee._unit, ee.Scopes);
            if (res != null) {
                return res.SelfSet;
            }
            return EmptySet<Namespace>.Instance;
        }

        private static ISet<Namespace> EvaluateSlice(ExpressionEvaluator ee, Node node) {
            SliceExpression se = node as SliceExpression;
            ee.EvaluateMaybeNull(se.SliceStart);
            ee.EvaluateMaybeNull(se.SliceStop);
            if (se.StepProvided) {
                ee.EvaluateMaybeNull(se.SliceStep);
            }

            return SliceInfo.Instance;/*
            return ee.GlobalScope.GetOrMakeNodeVariable(
                node, 
                x=> SliceInfo.Instance
            );*/
        }

        private ISet<Namespace> MakeSequence(ExpressionEvaluator ee, Node node) {
            var sequence = (SequenceInfo)ee.GlobalScope.GetOrMakeNodeVariable(node, x => {
                if (node is ListExpression) {
                    return new ListInfo(VariableDef.EmptyArray, _unit.ProjectState._listType).SelfSet;
                } else {
                    Debug.Assert(node is TupleExpression);
                    return new SequenceInfo(VariableDef.EmptyArray, _unit.ProjectState._tupleType).SelfSet;
                }
            });
            var seqItems = ((SequenceExpression)node).Items;
            var indexValues = new ISet<Namespace>[seqItems.Count];

            for (int i = 0; i < seqItems.Count; i++) {
                indexValues[i] = Evaluate(seqItems[i]);
            }
            sequence.AddTypes(ee._unit, indexValues);
            return sequence.SelfSet;
        }

        internal InterpreterScope[] PushScope(InterpreterScope scope) {
            var newScopes = new InterpreterScope[_currentScopes.Length + 1];
            _currentScopes.CopyTo(newScopes, 0);
            newScopes[_currentScopes.Length] = scope;
            _currentScopes = newScopes;
            return newScopes;
        }

        #endregion
    }
}
