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
using System.Text;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    /// <summary>
    /// Encapsulates a single piece of code which can be analyzed.  Currently this could be a top-level module, a class definition, or
    /// a function definition.  AnalysisUnit holds onto both the AST of the code which is to be analyzed along with
    /// the scope in which the object is declared.
    /// </summary>
    internal class AnalysisUnit {
        private readonly ScopeStatement _ast;
        private readonly InterpreterScope[] _scopes;
        private bool _inQueue, _forEval;
#if DEBUG
        private long _analysisTime;
        private static Stopwatch _sw = new Stopwatch();

        static AnalysisUnit() {
            _sw.Start();
        }
#endif

        public AnalysisUnit(ScopeStatement node, InterpreterScope[] scopes) {
            _ast = node;
            _scopes = scopes;
        }

        private AnalysisUnit(ScopeStatement ast, InterpreterScope[] scopes, bool forEval) {
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
        public ScopeStatement Ast {
            get { return _ast; }
        }

        public  void Analyze(DDG ddg) {
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
            Ast.Walk(ddg);
        }

        /// <summary>
        /// The chain of scopes in which this analysis is defined.
        /// </summary>
        public InterpreterScope[] Scopes {
            get { return _scopes; }
        }

        public override string ToString() {
            return String.Format(
                "<_AnalysisUnit: Name={0}, NodeType={1}, ScopeName={2}>",
                FullName,
                _ast.GetType().Name,
                Ast.Name
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

    class FunctionAnalysisUnit : AnalysisUnit {
        public FunctionAnalysisUnit(FunctionDefinition node, InterpreterScope[] scopes)
            : base(node, scopes) {
        }

        public new FunctionDefinition Ast {
            get {
                return (FunctionDefinition)base.Ast;
            }
        }

        protected override void AnalyzeWorker(DDG ddg) {
            var newScope = (DeclaringModule.NodeScopes[Ast] as FunctionScope).Function;
            Debug.Assert(newScope != null);

            // TODO: __new__ in class should assign returnValue

            ClassScope curClass = null;
            for (int i = Scopes.Length - 1; i >= 0; i--) {
                if (Scopes[i] is ClassScope) {
                    curClass = (ClassScope)Scopes[i];
                    break;
                }
            }
            
            if (curClass != null) {
                // wire up information about the class
                // TODO: Should follow MRO
                var bases = ddg.LookupBaseMethods(Ast.Name, curClass.Class.Bases, Ast, this);
                foreach (var ns in bases) {
                    if (ns is BuiltinMethodInfo) {
                        ddg.PropagateBaseParams(newScope, ns);
                    }
                }
            }

            ddg.ProcessFunctionDecorators(Ast, newScope);

            // process parameters
            int len = Math.Min(Ast.Parameters.Count, newScope.ParameterTypes.Length);
            for (int i = 0; i < len; i++) {
                var p = Ast.Parameters[i];
                var v = newScope.ParameterTypes[i];
                if (p.DefaultValue != null) {
                    var val = ddg._eval.Evaluate(p.DefaultValue);
                    if (val != null) {
                        v.AddTypes(p, this, val);
                    }
                }
            }
            var oldUnit = ddg._unit;
            ddg._unit = this;
            try {
                Ast.Body.Walk(ddg);
            } finally {
                ddg._unit = oldUnit;
            }
        }
    }

    class ClassAnalysisUnit : AnalysisUnit {
        public ClassAnalysisUnit(ClassDefinition node, InterpreterScope[] scopes)
            : base(node, scopes) {
        }

        public new ClassDefinition Ast {
            get {
                return (ClassDefinition)base.Ast;
            }
        }


        protected override void AnalyzeWorker(DDG ddg) {
            var newScope = (DeclaringModule.NodeScopes[Ast] as ClassScope).Class;

            newScope.Bases.Clear();
            // Process base classes
            foreach (var baseClassArg in Ast.Bases) {
                if (baseClassArg.Name != null) {
                    // TODO: support namaed args to user defined meta classes and metaclass arg.
                    continue;
                }
                var baseClass = baseClassArg.Expression;

                baseClass.Walk(ddg);
                var bases = ddg._eval.Evaluate(baseClass);
                newScope.Bases.Add(bases);

                foreach (var baseValue in bases) {
                    ClassInfo ci = baseValue as ClassInfo;
                    if (ci != null) {
                        ci.SubClasses.AddTypes(Ast, newScope._analysisUnit, new[] { newScope });
                    }
                }
            }

            ddg.WalkBody(Ast.Body, newScope._analysisUnit);
        }
    }
}
