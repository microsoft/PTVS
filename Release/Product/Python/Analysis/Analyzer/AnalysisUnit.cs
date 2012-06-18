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
using System.Text;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    /// <summary>
    /// Encapsulates a single piece of code which can be analyzed.  Currently this could be a top-level module, a class definition, 
    /// a function definition, or a comprehension scope (generator, dict, set, or list on 3.x).  AnalysisUnit holds onto both the 
    /// AST of the code which is to be analyzed along with the scope in which the object is declared.
    /// 
    /// Our dependency tracking scheme works by tracking analysis units - when we add a dependency it is the current
    /// AnalysisUnit which is dependent upon the variable.  If the value of a variable changes then all of the dependent
    /// AnalysisUnit's will be re-enqueued.  This proceeds until we reach a fixed point.
    /// </summary>
    internal class AnalysisUnit {
        private readonly Node _ast;
        private readonly InterpreterScope[] _scopes;
        private bool _inQueue, _forEval;
#if DEBUG
        private long _analysisTime;
        private static Stopwatch _sw = new Stopwatch();

        static AnalysisUnit() {
            _sw.Start();
        }
#endif

        public static AnalysisUnit EvalUnit = new AnalysisUnit(null, null, true);

        public AnalysisUnit(Node node, InterpreterScope[] scopes) {
            _ast = node;
            _scopes = scopes;
        }

        public AnalysisUnit(Node ast, InterpreterScope[] scopes, bool forEval) {
            _ast = ast;
            _scopes = scopes;
            _forEval = forEval;
        }

        public bool IsInQueue {
            get {
                return _inQueue;
            }
            set {
                _inQueue = value;
            }
        }

        /// <summary>
        /// True if this analysis unit is being used to evaluate the result of the analysis.  In this
        /// mode we don't track references or re-queue items.
        /// </summary>
        public bool ForEval {
            get {
                return _forEval;
            }
        }

        public AnalysisUnit CopyForEval() {
            return new AnalysisUnit(_ast, _scopes, true);
        }

        public void Enqueue() {
            if (!ForEval && !IsInQueue) {
                ProjectState.Queue.Append(this);
                this.IsInQueue = true;
            }
        }

        /// <summary>
        /// The global scope that the code associated with this analysis unit is declared within.
        /// </summary>
        public ModuleInfo DeclaringModule {
            get {

                Debug.Assert(_scopes[0] != null);
                return ((ModuleScope)_scopes[0]).Module;
            }
        }

        public ProjectEntry ProjectEntry {
            get {
                return DeclaringModule.ProjectEntry;
            }
        }

        public PythonAnalyzer ProjectState {
            get {
                return DeclaringModule.ProjectEntry.ProjectState;
            }
        }

        /// The AST which will be analyzed when this node is analyzed
        /// </summary>
        public Node Ast {
            get { return _ast; }
        }

        public virtual PythonAst Tree {
            get {
                return ((ScopeStatement)_ast).GlobalParent;
            }
        }

        public void Analyze(DDG ddg) {
#if DEBUG
            long startTime = _sw.ElapsedMilliseconds;
            try {
#endif
                //Console.WriteLine("Analying: {0} ({1})", Ast.Name == "<module>" ? this.ProjectEntry.ModuleName : Ast.Name, Ast.GetType().Name);
                AnalyzeWorker(ddg);
#if DEBUG
            } finally {
                long endTime = _sw.ElapsedMilliseconds;
                _analysisTime += endTime - startTime;
                if (_analysisTime >= 500) {
                    Console.WriteLine("Analyzed: {0} {1} ({2} total)", this, endTime - startTime, _analysisTime);
                }
            }
#endif
        }

        protected virtual void AnalyzeWorker(DDG ddg) {
            DeclaringModule.Scope.ClearLinkedVariables();

            ddg.SetCurrentUnit(this);
            Ast.Walk(ddg);

            List<KeyValuePair<string, VariableDef>> toRemove = null;
            
            foreach (var variableInfo in DeclaringModule.Scope.Variables) {
                variableInfo.Value.ClearOldValues(ProjectEntry);
                if (variableInfo.Value._dependencies.Count == 0 &&
                    variableInfo.Value.Types.Count == 0) {
                    if (toRemove == null) {
                        toRemove = new List<KeyValuePair<string, VariableDef>>();
                    }
                    toRemove.Add(variableInfo);
                }
            }
            if (toRemove != null) {
                foreach (var nameValue in toRemove) {
                    DeclaringModule.Scope.Variables.Remove(nameValue.Key);

                    // if anyone read this value it could now be gone (e.g. user 
                    // deletes a class definition) so anyone dependent upon it
                    // needs to be updated.
                    nameValue.Value.EnqueueDependents();
                }
            }
        }

        /// <summary>
        /// The chain of scopes in which this analysis is defined.
        /// </summary>
        public InterpreterScope[] Scopes {
            get { return _scopes; }
        }

        public override string ToString() {
            return String.Format(
                "<{2}: Name={0}, NodeType={1}>",
                FullName,
                _ast.GetType().Name,
                GetType().Name
            );
        }

        /// <summary>
        /// Returns the fully qualified name of the analysis unit's scope
        /// including all outer scopes.
        /// </summary>
        internal string FullName {
            get {
                var name = DeclaringModule.Name;

                for (int i = 1; i < Scopes.Length; i++) {
                    name = name + "." + Scopes[i].Name;
                }
                return name;
            }
        }
    }

    /// <summary>
    /// Provides the analysis of a function before it is called with any arguments.
    /// </summary>
    class FunctionAnalysisUnit : AnalysisUnit {
        internal readonly AnalysisUnit _outerUnit;
        internal Expression _decoratorCall;

        public FunctionAnalysisUnit(FunctionDefinition node, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, scopes) {
            _outerUnit = outerUnit;
        }

        public new FunctionDefinition Ast {
            get {
                return (FunctionDefinition)base.Ast;
            }
        }

        protected override void AnalyzeWorker(DDG ddg) {
            InterpreterScope interpreterScope;
            if (!DeclaringModule.NodeScopes.TryGetValue(Ast, out interpreterScope)) {
                return;
            }
            var funcScope = (FunctionScope)interpreterScope;
            var function = funcScope.Function;
            Debug.Assert(function != null);
            // TODO: __new__ in class should assign returnValue
            ddg.SetCurrentUnit(_outerUnit);

            ClassScope curClass = Scopes[Scopes.Length - 1] as ClassScope;
            if (curClass != null) {
                // wire up information about the class
                // TODO: Should follow MRO
                var bases = ddg.LookupBaseMethods(Ast.Name, curClass.Class.Bases, Ast, this);
                foreach (var ns in bases) {
                    BuiltinMethodInfo methodInfo = ns as BuiltinMethodInfo;
                    if(methodInfo != null) {
                        ddg.PropagateBaseParams(function, methodInfo);
                    }
                }
            }

            ProcessFunctionDecorators(ddg, Ast, function);

            // analyze the function w/o any parameter types.
            AnalyzeFunction(ddg, function, funcScope);
        }

        internal void ProcessFunctionDecorators(DDG ddg, FunctionDefinition funcdef, FunctionInfo newScope) {
            if (funcdef.Decorators != null) {
                EnsureDecoratorCall(funcdef);
                foreach (var d in funcdef.Decorators.Decorators) {
                    if (d != null) {                        
                        var decorator = ddg._eval.Evaluate(d);

                        if (decorator.Contains(ProjectState._propertyObj)) {
                            newScope.IsProperty = true;
                        } else if (decorator.Contains(ProjectState._staticmethodObj)) {
                            newScope.IsStatic = true;
                        } else if (decorator.Contains(ProjectState._classmethodObj)) {
                            newScope.IsClassMethod = true;
                        }
                    }
                }
                
                ddg._eval.Evaluate(_decoratorCall);
            }

            if (newScope.IsClassMethod) {
                if (newScope.ParameterTypes.Length > 0) {
                    var outerScope = ddg.Scopes[ddg.Scopes.Length - 1] as ClassScope;
                    if (outerScope != null) {
                        newScope.AddParameterType(ddg._unit, outerScope.Class.SelfSet, 0);
                    } else {
                        newScope.AddParameterType(ddg._unit, ProjectState._typeObj.SelfSet, 0);
                    }
                }
            } else if (!newScope.IsStatic) {
                // self is always an instance of the class
                // TODO: Check for __new__ (auto static) and
                // @staticmethod and @classmethod and @property
                if (newScope.ParameterTypes.Length > 0) {
                    var classScope = ddg.Scopes[ddg.Scopes.Length - 1] as ClassScope;
                    if (classScope != null) {
                        newScope.AddParameterType(ddg._unit, classScope.Class.Instance, 0);
                    }
                }
            }
        }

        private void EnsureDecoratorCall(FunctionDefinition funcdef) {
            if (_decoratorCall == null) {
                Expression decCall = new NameExpression(funcdef.Name);
                foreach (var d in funcdef.Decorators.Decorators) {
                    if (d != null) {
                        decCall = new CallExpression(
                            d,
                            new[] {
                                    new Arg(decCall)
                                }
                        );
                    }
                }
                _decoratorCall = decCall;
            }
        }

        protected virtual void AnalyzeFunction(DDG ddg, FunctionInfo function, FunctionScope funcScope) {
            // process parameters
            int len = Math.Min(Ast.Parameters.Count, function.ParameterTypes.Length);
            for (int i = 0; i < len; i++) {
                var p = Ast.Parameters[i];
                if (p.DefaultValue != null) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        function.AddParameterType(this, val, i);
                    }
                }
                ddg._eval.EvaluateMaybeNull(p.Annotation);
            }
            ddg._eval.EvaluateMaybeNull(Ast.ReturnAnnotation);

            ddg.SetCurrentUnit(this);
            Ast.Body.Walk(ddg);
        }
    }

    /// <summary>
    /// Provides analysis of a function called with a specific set of arguments.  We analyze each function
    /// with each unique set of arguments (the cartesian product of the arguments).
    /// 
    /// It's possible that we still need to perform the analysis multiple times which can occur 
    /// if we take a dependency on something which later gets updated.
    /// </summary>
    class CartesianProductFunctionAnalysisUnit : FunctionAnalysisUnit {
        private readonly FunctionInfo.CallArgs _callArgs;
        private readonly VariableDef _returnValue;
        private readonly VariableDef[] _newParams;
        private readonly CartesianLocalVariable[] _specializedLocals;

        public CartesianProductFunctionAnalysisUnit(FunctionInfo funcInfo, InterpreterScope[] scopes, AnalysisUnit outerUnit, FunctionInfo.CallArgs callArgs, VariableDef returnValue)
            : base(funcInfo.FunctionDefinition, scopes, outerUnit) {
            _callArgs = callArgs;
            _returnValue = returnValue;
            
            // Set parameters to new empty variables, save the old variables which
            // we'll merge back into.
            var newParams = new VariableDef[Ast.Parameters.Count];
            var oldParams = funcInfo.ParameterTypes;
            int index = 0;
            foreach (var param in Ast.Parameters) {
                VariableDef variable;
                if (param.Kind != ParameterKind.Dictionary) {
                    variable = newParams[index++] = InterpreterScope.MakeParameterDef(param, this, param.Kind, false);
                } else {
                    variable = newParams[index] = oldParams[index++];
                }
            }
            _newParams = newParams;

            var funcScope = scopes[scopes.Length - 1] as FunctionScope;
            if (funcScope._assignedVars != null) {
                var specLocals = new List<CartesianLocalVariable>();
                foreach (var assignedVar in funcScope._assignedVars) {
                    ProcessVariableForScope(funcScope, specLocals, assignedVar);
                }
                _specializedLocals = specLocals.ToArray();
            }
        }

        private static void ProcessVariableForScope(InterpreterScope scope, List<CartesianLocalVariable> specLocals, string assignedVar) {
            VariableDef oldDef;
            if (scope.Variables.TryGetValue(assignedVar, out oldDef)) {
                specLocals.Add(
                    new CartesianLocalVariable(
                        assignedVar,
                        scope,
                        new VariableDef(),
                        oldDef
                    )
                );
            }

            foreach (var childScope in scope.Children) {
                if (childScope is IsInstanceScope || childScope is StatementScope) {
                    ProcessVariableForScope(childScope, specLocals, assignedVar);
                }
            }
        }

        protected override void AnalyzeFunction(DDG ddg, FunctionInfo function, FunctionScope funcScope) {
            var args = _callArgs;            

            // Set parameters to new empty variables, save the old variables which
            // we'll merge back into.
            var oldParams = function.ParameterTypes;
            int index = 0;
            foreach (var param in Ast.Parameters) {
                funcScope.Variables[param.Name] = _newParams[index++];
            }

            // Set the specialized versions of the locals
            if (_specializedLocals != null) {
                foreach (var local in _specializedLocals) {
                    local.DefiningScope.Variables[local.Name] = local.Specialized;
                }
            }

            function.SetParameters(_newParams);
            function.PropagateCall(Ast, args.KeywordArgs, this, args.Args, false);
            var unifiedReturn = function.ReturnValue;
            function.ReturnValue = _returnValue;

            for (int i = 0; i < Ast.Parameters.Count; i++) {
                var param = Ast.Parameters[i];

                // We need to avoid an explosion of types for list/dictionary parameters.  If we create a new SequenceInfo
                // everytime we process these then we break recursive *args calls such as:
                // def f(*args):
                //      f(args)
                // As we need up creating a sequence of a sequence of a sequence ...  forever.
                // So here we uniqify based upon the call arguments, removing any sequences which actually came from
                // *args or **args.  
                if (param.Kind == ParameterKind.List) {
                    var listVar = (ListParameterVariableDef)_newParams[i];
                    ISet<Namespace>[] argTypes = new ISet<Namespace>[listVar.List.IndexTypes.Length];
                    for (int j = 0; j < argTypes.Length; j++) {
                        argTypes[j] = new HashSet<Namespace>(listVar.List.IndexTypes[j].Types.Where(x => !(x is StarArgsSequenceInfo)));
                    }
                    var callArgs = new FunctionInfo.CallArgs(argTypes, ExpressionEvaluator.EmptyNames, false);
                    SequenceInfo seqInfo;
                    if (function._starArgs == null) {
                        function._starArgs = new Dictionary<FunctionInfo.CallArgs, SequenceInfo>();
                    }
                    if (!function._starArgs.TryGetValue(callArgs, out seqInfo)) {
                        function._starArgs[callArgs] = listVar.List;
                    } else {
                        listVar.List = seqInfo;
                    }

                    listVar.AddTypes(this, listVar.List);
                } else if (param.Kind == ParameterKind.Dictionary) {

                }
            }

            try {
                base.AnalyzeFunction(ddg, function, funcScope);
            } finally {
                function.SetParameters(oldParams);
                function.ReturnValue = unifiedReturn;

                // propagate the calculated types into the full variables
                for (int i = 0; i < Ast.Parameters.Count; i++) {
                    funcScope.Variables[Ast.Parameters[i].Name] = oldParams[i];

                    if (_newParams[i] != oldParams[i]) {    // we don't yet copy dict params...
                        CopyTypesTo(_newParams[i], oldParams[i]);
                    }
                }

                // restore the locals, merging types back into the shared...
                if (_specializedLocals != null) {
                    foreach (var variable in _specializedLocals) {
                        var newVar = variable.Specialized;
                        var oldVar = variable.Shared;

                        CopyTypesTo(newVar, oldVar);

                        variable.DefiningScope.Variables[variable.Name] = oldVar;
                    }
                }
            }
        }

        /// <summary>
        /// A pair of variable defs - the old one, and the new one.
        /// </summary>
        struct CartesianLocalVariable {
            /// <summary>
            /// The specialized variable which is used for each individual analysis.
            /// </summary>
            public readonly VariableDef Specialized;
            /// <summary>
            /// The shared variable which has the merged locals from all of the analysis.
            /// </summary>
            public readonly VariableDef Shared;
            public readonly string Name;
            public readonly InterpreterScope DefiningScope;

            public CartesianLocalVariable(string name, InterpreterScope definingScope, VariableDef specialized, VariableDef shared) {
                Specialized = specialized;
                Shared = shared;
                DefiningScope = definingScope;
                Name = name;
            }
        }

        private static void CopyTypesTo(VariableDef from, VariableDef to) {
            Debug.Assert(from != to);
            foreach (var keyValue in from._dependencies) {
                var projEntry = keyValue.Key;
                var dependencies = keyValue.Value;

                to.AddTypes(projEntry, dependencies.Types);
            }
        }
    }

    /// <summary>
    /// Handles the re-evaluation of a base class when we have a deferred variable lookup.
    /// 
    /// For each base class which a class inherits from we create a new ClassBaseAnalysisUnit.  
    /// 
    /// We use this AnalysisUnit for the evaulation of the base class.  The ClassBaseAU is setup
    /// to have the same set of scopes as the classes outer (defining) analysis unit.  We then
    /// evaluate the base class inside of this unit.  If any of our dependencies change we will 
    /// then re-evaluate our base class, and we will also re-evaluate our outer unit as well.
    /// </summary>
    class ClassBaseAnalysisUnit : AnalysisUnit {
        private readonly Expression _baseClassNode;
        private readonly AnalysisUnit _outerUnit;
        public ClassBaseAnalysisUnit(ClassDefinition node, InterpreterScope[] scopes, Expression baseClassNode, AnalysisUnit outerUnit)
            : base(node, scopes) {
            _outerUnit = outerUnit;
            _baseClassNode = baseClassNode;
        }

        protected override void AnalyzeWorker(DDG ddg) {
            ddg.SetCurrentUnit(this);

            InterpreterScope scope;
            if (!DeclaringModule.NodeScopes.TryGetValue(Ast, out scope)) {
                return;
            }

            ClassAnalysisUnit.EvaluateBaseClass(
                ddg,
                ((ClassScope)scope).Class,
                _baseClassNode
            );

            _outerUnit.Enqueue();
        }
    }

    class ClassAnalysisUnit : AnalysisUnit {
        private readonly AnalysisUnit _outerUnit;
        private readonly ClassBaseAnalysisUnit[] _baseEvals;
        public ClassAnalysisUnit(ClassDefinition node, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, scopes) {
            _outerUnit = outerUnit;
            _baseEvals = new ClassBaseAnalysisUnit[node.Bases.Count];
            for (int i = 0; i < node.Bases.Count; i++) {
                _baseEvals[i] = new ClassBaseAnalysisUnit(node, outerUnit.Scopes, node.Bases[i].Expression, this);
            }
        }

        public new ClassDefinition Ast {
            get {
                return (ClassDefinition)base.Ast;
            }
        }

        protected override void AnalyzeWorker(DDG ddg) {
            InterpreterScope scope;
            if (!DeclaringModule.NodeScopes.TryGetValue(Ast, out scope)) {
                return;
            }
            
            var newScope = (scope as ClassScope).Class;

            newScope.ClearBases();
            if (Ast.Bases.Count == 0) {
                if (ddg.ProjectState.LanguageVersion.Is3x()) {
                    // 3.x all classes inherit from object by default
                    newScope.AddBase(ddg.ProjectState._objectSet);
                }
            } else {
                // Process base classes
                for (int i = 0; i < Ast.Bases.Count; i++) {
                    var baseClassArg = Ast.Bases[i];

                    ddg.SetCurrentUnit(_baseEvals[i]);

                    if (baseClassArg.Name != null) {
                        if (baseClassArg.Name == "metaclass") {
                            var metaClass = baseClassArg.Expression;
                            metaClass.Walk(ddg);
                            var metaClassValue = ddg._eval.Evaluate(metaClass);
                            if (metaClassValue.Count > 0) {
                                newScope.GetOrCreateMetaclassVariable().AddTypes(_outerUnit, metaClassValue);
                            }
                        }
                        continue;
                    }

                    var baseClass = baseClassArg.Expression;

                    EvaluateBaseClass(ddg, newScope, baseClass);
                }
            }

            ddg.SetCurrentUnit(this);
            ddg.WalkBody(Ast.Body, newScope._analysisUnit);
        }

        internal static void EvaluateBaseClass(DDG ddg, ClassInfo newScope, Expression baseClass) {
            baseClass.Walk(ddg);
            var bases = ddg._eval.Evaluate(baseClass);
            newScope.AddBase(bases);

            foreach (var baseValue in bases) {
                ClassInfo ci = baseValue as ClassInfo;
                if (ci != null) {
                    ci.SubClasses.AddTypes(newScope._analysisUnit, new[] { newScope });
                }
            }
        }
    }
    
    class ComprehensionAnalysisUnit : AnalysisUnit {
        private readonly PythonAst _parent;
        private readonly AnalysisUnit _outerUnit;

        public ComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, scopes) {
            _outerUnit = outerUnit;
            _parent = parent;
        }

        protected override void AnalyzeWorker(DDG ddg) {
            ddg.SetCurrentUnit(this);

            ExpressionEvaluator.WalkComprehension(ddg._eval, (Comprehension)Ast);
        }

        public override PythonAst Tree {
            get {
                return _parent;
            }
        }
    }

    class GeneratorComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public GeneratorComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, parent, scopes, outerUnit) {
        }

        protected override void AnalyzeWorker(DDG ddg) {
            base.AnalyzeWorker(ddg);

            var generator = (GeneratorInfo)((ComprehensionScope)Scopes[Scopes.Length - 1]).Namespace;

            var node = (GeneratorExpression)Ast;
            generator.AddYield(ddg._eval.Evaluate(node.Item));
        }
    }

    class SetComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public SetComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, parent, scopes, outerUnit) {
        }

        protected override void AnalyzeWorker(DDG ddg) {
            base.AnalyzeWorker(ddg);

            var set = (SetInfo)((ComprehensionScope)Scopes[Scopes.Length - 1]).Namespace;

            var node = (SetComprehension)Ast;

            set.AddTypes(this, ddg._eval.Evaluate(node.Item));
        }
    }


    class DictionaryComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public DictionaryComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, parent, scopes, outerUnit) {
        }

        protected override void AnalyzeWorker(DDG ddg) {
            base.AnalyzeWorker(ddg);

            var dict = (DictionaryInfo)((ComprehensionScope)Scopes[Scopes.Length - 1]).Namespace;

            var node = (DictionaryComprehension)Ast;

            dict.SetIndex(node, this, ddg._eval.Evaluate(node.Key), ddg._eval.Evaluate(node.Value));
        }
    }

    class ListComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public ListComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope[] scopes, AnalysisUnit outerUnit)
            : base(node, parent, scopes, outerUnit) {
        }

        protected override void AnalyzeWorker(DDG ddg) {
            base.AnalyzeWorker(ddg);

            var list = (ListInfo)((ComprehensionScope)Scopes[Scopes.Length - 1]).Namespace;
            var node = (ListComprehension)Ast;

            list.AddTypes(this, new[] { ddg._eval.Evaluate(node.Item) });
        }
    }
}
