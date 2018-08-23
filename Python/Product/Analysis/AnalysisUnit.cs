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
using System.Threading;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Encapsulates a single piece of code which can be analyzed.  Currently this could be a top-level module, a class definition, 
    /// a function definition, or a comprehension scope (generator, dict, set, or list on 3.x).  AnalysisUnit holds onto both the 
    /// AST of the code which is to be analyzed along with the scope in which the object is declared.
    /// 
    /// Our dependency tracking scheme works by tracking analysis units - when we add a dependency it is the current
    /// AnalysisUnit which is dependent upon the variable.  If the value of a variable changes then all of the dependent
    /// AnalysisUnit's will be re-enqueued.  This proceeds until we reach a fixed point.
    /// </summary>
    public class AnalysisUnit : ISet<AnalysisUnit>, ILocationResolver, ICanExpire {
        internal InterpreterScope _scope;
        private ModuleInfo _declaringModule;
        private bool _suppressEnqueue;
#if DEBUG
        private long _analysisTime;
        private long _analysisCount;
        private static Stopwatch _sw = new Stopwatch();

        static AnalysisUnit() {
            _sw.Start();
        }
#endif

        internal static AnalysisUnit EvalUnit = new AnalysisUnit(null, null, null, true);

        internal AnalysisUnit(ScopeStatement ast, InterpreterScope scope)
            : this(ast, ast?.GlobalParent, scope, false) {
        }

        internal AnalysisUnit(Node ast, PythonAst tree, InterpreterScope scope, bool forEval) {
            Ast = ast;
            Tree = tree;
            _scope = scope;
            ForEval = forEval;
        }

        /// <summary>
        /// True if this analysis unit is currently in the queue.
        /// </summary>
        internal bool IsInQueue;

        /// <summary>
        /// True if this analysis unit is being used to evaluate the result of the analysis.  In this
        /// mode we don't track references or re-queue items.
        /// </summary>
        internal readonly bool ForEval;

        public virtual bool IsAlive => _scope?.AnalysisValue?.IsAlive ?? true;

        internal virtual ModuleInfo GetDeclaringModule() {
            if (_scope != null) {
                var moduleScope = _scope.EnumerateTowardsGlobal.OfType<ModuleScope>().FirstOrDefault();
                if (moduleScope != null) {
                    return moduleScope.Module;
                }
            }
            return null;
        }

        /// <summary>
        /// The global scope that the code associated with this analysis unit is declared within.
        /// </summary>
        internal ModuleInfo DeclaringModule {
            get {
                if (_declaringModule == null) {
                    _declaringModule = GetDeclaringModule();
                }
                return _declaringModule;
            }
        }

        /// <summary>
        /// Returns the project entry which this analysis unit analyzes.
        /// </summary>
        public IPythonProjectEntry Entry {
            get {
                return ProjectEntry;
            }
        }

        public virtual IVersioned DependencyProject {
            get {
                return ProjectEntry;
            }
        }

        internal ProjectEntry ProjectEntry {
            get { return DeclaringModule.ProjectEntry; }
        }

        public PythonAnalyzer State {
            get { return DeclaringModule.ProjectEntry.ProjectState; }
        }

        internal AnalysisUnit CopyForEval() {
            return new AnalysisUnit(Ast, Tree, _scope, true);
        }

        internal void Enqueue() {
            if (!ForEval && !IsInQueue && !_suppressEnqueue) {
                State.Queue.Append(this);
                AnalysisLog.Enqueue(State.Queue, this);
                IsInQueue = true;

                if (DeclaringModule?.Scope == Scope) {
                    DeclaringModule.ModuleDefinition.EnqueueDependents();
                }
            }
        }

        /// <summary>
        /// The AST which will be analyzed when this node is analyzed
        /// </summary>
        internal readonly Node Ast;

        internal readonly PythonAst Tree;

        internal void Analyze(DDG ddg, CancellationToken cancel) {
#if DEBUG
            long startTime = _sw.ElapsedMilliseconds;
            try {
                _analysisCount += 1;
#endif
                if (cancel.IsCancellationRequested) {
                    return;
                }
                AnalyzeWorker(ddg, cancel);
#if DEBUG
            } finally {
                long endTime = _sw.ElapsedMilliseconds;
                var thisTime = endTime - startTime;
                _analysisTime += thisTime;
                if (thisTime >= 500 || (_analysisTime / _analysisCount) > 500) {
                    Trace.TraceWarning("Analyzed: {0} {1} ({2} count, {3}ms total, {4}ms mean)", this, thisTime, _analysisCount, _analysisTime, (double)_analysisTime / _analysisCount);
                }
            }
#endif
        }

        internal virtual void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            ddg.SetCurrentUnit(this);
            Ast.Walk(ddg);

            List<KeyValuePair<string, VariableDef>> toRemove = null;

            foreach (var variableInfo in DeclaringModule.Scope.AllVariables) {
                variableInfo.Value.ClearOldValues(ProjectEntry);
                if (!variableInfo.Value.HasTypes && !variableInfo.Value.IsAssigned && !variableInfo.Value.IsEphemeral) {
                    toRemove = toRemove ?? new List<KeyValuePair<string, VariableDef>>();
                    toRemove.Add(variableInfo);
                }
            }

            if (toRemove != null) {
                // Do not allow variable removal to re-enqueue our unit
                _suppressEnqueue = true;
                try {
                    foreach (var nameValue in toRemove) {
                        DeclaringModule.Scope.RemoveVariable(nameValue.Key);
                        // if anyone read this value it could now be gone (e.g. user 
                        // deletes a class definition) so anyone dependent upon it
                        // needs to be updated.
                        nameValue.Value.EnqueueDependents();
                    }
                } finally {
                    _suppressEnqueue = false;
                }
            }
        }

        /// <summary>
        /// The chain of scopes in which this analysis is defined.
        /// </summary>
        internal InterpreterScope Scope {
            get { return _scope; }
        }

        public override string ToString() {
            return "{0}: Name={1} ({2}), NodeType={3}".FormatInvariant(
                GetType().Name, FullName, GetHashCode(), Ast?.GetType().Name ?? "<unknown>");
        }

        /// <summary>
        /// Returns the fully qualified name of the analysis unit's scope
        /// including all outer scopes.
        /// </summary>
        internal string FullName {
            get {
                if (Scope != null) {
                    return string.Join(".", Scope.EnumerateFromGlobal.Select(s => s.Name));
                } else {
                    return "<Unnamed unit>";
                }
            }
        }

        #region SelfSet

        bool ISet<AnalysisUnit>.Add(AnalysisUnit item) {
            throw new NotImplementedException();
        }

        void ISet<AnalysisUnit>.ExceptWith(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        void ISet<AnalysisUnit>.IntersectWith(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.IsProperSubsetOf(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.IsProperSupersetOf(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.IsSubsetOf(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.IsSupersetOf(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.Overlaps(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        bool ISet<AnalysisUnit>.SetEquals(IEnumerable<AnalysisUnit> other) {
            var enumerator = other.GetEnumerator();
            if (enumerator.MoveNext()) {
                if (((ISet<AnalysisUnit>)this).Contains(enumerator.Current)) {
                    return !enumerator.MoveNext();
                }
            }
            return false;
        }

        void ISet<AnalysisUnit>.SymmetricExceptWith(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        void ISet<AnalysisUnit>.UnionWith(IEnumerable<AnalysisUnit> other) {
            throw new NotImplementedException();
        }

        void ICollection<AnalysisUnit>.Add(AnalysisUnit item) {
            throw new InvalidOperationException();
        }

        void ICollection<AnalysisUnit>.Clear() {
            throw new InvalidOperationException();
        }

        bool ICollection<AnalysisUnit>.Contains(AnalysisUnit item) {
            return item == this;
        }

        void ICollection<AnalysisUnit>.CopyTo(AnalysisUnit[] array, int arrayIndex) {
            throw new InvalidOperationException();
        }

        int ICollection<AnalysisUnit>.Count {
            get { return 1; }
        }

        bool ICollection<AnalysisUnit>.IsReadOnly {
            get { return true; }
        }

        bool ICollection<AnalysisUnit>.Remove(AnalysisUnit item) {
            throw new InvalidOperationException();
        }

        IEnumerator<AnalysisUnit> IEnumerable<AnalysisUnit>.GetEnumerator() {
            return new SetOfOneEnumerator<AnalysisUnit>(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            yield return this;
        }

        #endregion

        /// <summary>
        /// Looks up a sequence of types associated with the name using the
        /// normal Python semantics.
        /// 
        /// This function is only safe to call during analysis. After analysis
        /// has completed, use a <see cref="ModuleAnalysis"/> instance.
        /// </summary>
        /// <param name="node">The node to associate with the lookup.</param>
        /// <param name="name">The full name of the value to find.</param>
        /// <returns>
        /// All values matching the provided name, or null if the name could not
        /// be resolved.
        /// 
        /// An empty sequence is returned if the name is found but currently has
        /// no values.
        /// </returns>
        /// <remarks>
        /// Calling this function will associate this unit with the requested
        /// variable. Future updates to the variable may result in the unit
        /// being reanalyzed.
        /// </remarks>
        public IAnalysisSet FindAnalysisValueByName(Node node, string name) {
            foreach (var scope in Scope.EnumerateTowardsGlobal) {
                if (scope == Scope || scope.VisibleToChildren) {
                    var refs = scope.GetVariable(node, this, name, true);
                    if (refs != null) {
                        scope.AddReferenceToLinkedVariables(node, this, name);
                        return refs.Types;
                    }
                }
            }

            return State.BuiltinModule.GetMember(node, this, name);
        }

        public LocationInfo ResolveLocation(object location) {
            Node node = (Node)location;
            var span = (node as MemberExpression)?.GetNameSpan(Tree)
                ?? (node as Parameter)?.NameExpression?.GetSpan(Tree)
                ?? node.GetSpan(Tree);

            return new LocationInfo(ProjectEntry.FilePath, Entry.DocumentUri, span.Start.Line, span.Start.Column, span.End.Line, span.End.Column);
        }

        internal virtual ILocationResolver AlternateResolver => null;

        ILocationResolver ILocationResolver.GetAlternateResolver() => AlternateResolver;
    }

    class ClassAnalysisUnit : AnalysisUnit {
        private readonly AnalysisUnit _outerUnit;
        private readonly Dictionary<Node, Expression> _decoratorCalls;

        public ClassAnalysisUnit(ClassDefinition node, InterpreterScope declScope, AnalysisUnit outerUnit)
            : base(node, new ClassScope(new ClassInfo(node, outerUnit), node, declScope)) {

            ((ClassScope)Scope).Class.SetAnalysisUnit(this);
            _outerUnit = outerUnit;
            _decoratorCalls = new Dictionary<Node, Expression>();

            AnalysisLog.NewUnit(this);
        }

        public new ClassDefinition Ast {
            get {
                return (ClassDefinition)base.Ast;
            }
        }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            InterpreterScope scope;
            if (!ddg.Scope.TryGetNodeScope(Ast, out scope)) {
                return;
            }

            var classInfo = ((ClassScope)Scope).Class;
            var bases = new List<IAnalysisSet>();

            if (Ast.BasesInternal.Length == 0) {
                if (ddg.ProjectState.LanguageVersion.Is3x()) {
                    // 3.x all classes inherit from object by default
                    bases.Add(ddg.ProjectState.ClassInfos[BuiltinTypeId.Object]);
                }
            } else {
                // Process base classes
                for (int i = 0; i < Ast.BasesInternal.Length; i++) {
                    var baseClassArg = Ast.BasesInternal[i];

                    if (baseClassArg.Name == null) {
                        bases.Add(EvaluateBaseClass(ddg, classInfo, i, baseClassArg.Expression));

                    } else if (baseClassArg.Name == "metaclass") {
                        var metaClass = baseClassArg.Expression;
                        metaClass.Walk(ddg);
                        var metaClassValue = ddg._eval.Evaluate(metaClass);
                        if (metaClassValue.Count > 0) {
                            classInfo.GetOrCreateMetaclassVariable().AddTypes(_outerUnit, metaClassValue);
                        }
                    }
                }
            }

            classInfo.SetBases(bases);

            foreach (var baseSet in bases) {
                foreach (var baseClassInfo in baseSet.OfType<ClassInfo>()) {
                    baseClassInfo._mro.AddDependency(this);
                }
            }


            ddg.SetCurrentUnit(this);
            ddg.WalkBody(Ast.Body, classInfo.AnalysisUnit);

            ddg.SetCurrentUnit(_outerUnit);
            _outerUnit.Scope.AddLocatedVariable(Ast.Name, Ast.NameExpression, this);
            _outerUnit.Scope.AssignVariable(Ast.Name, Ast.NameExpression, this, ProcessClassDecorators(ddg, classInfo));
        }

        private IAnalysisSet ProcessClassDecorators(DDG ddg, ClassInfo classInfo) {
            var types = classInfo.SelfSet;
            if (Ast.Decorators != null) {
                Expression expr = Ast.NameExpression;

                foreach (var d in Ast.Decorators.DecoratorsInternal) {
                    if (d != null) {
                        var decorator = ddg._eval.Evaluate(d);

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
            return types;
        }

        internal static IAnalysisSet EvaluateBaseClass(DDG ddg, ClassInfo newClass, int indexInBaseList, Expression baseClass) {
            baseClass.Walk(ddg);
            var bases = ddg._eval.Evaluate(baseClass);

            foreach (var baseValue in bases) {
                ClassInfo ci = baseValue as ClassInfo;
                if (ci != null) {
                    if (!ci._mro.IsValid) {
                        ci._mro.Recompute();
                    }
                    ci.SubClasses.AddTypes(newClass.AnalysisUnit, newClass);
                }
            }

            return bases;
        }
    }

    class ComprehensionAnalysisUnit : AnalysisUnit {
        private readonly PythonAst _parent;
        private readonly AnalysisUnit _outerUnit;

        public ComprehensionAnalysisUnit(Comprehension node, PythonAst parent, InterpreterScope scope, AnalysisUnit outerUnit)
            : base(node, parent, scope, false) {
            _outerUnit = outerUnit;
            _parent = parent;

            AnalysisLog.NewUnit(this);
        }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            var comp = (Comprehension)Ast;
            var forComp = comp.Iterators[0] as ComprehensionFor;

            if (forComp != null) {
                // evaluate the 1st iterator in the outer scope
                ddg.Scope = _outerUnit.Scope;
                var listTypes = ddg._eval.Evaluate(forComp.List);
                ddg.Scope = Scope;

                ddg._eval.AssignTo(comp, forComp.Left, listTypes.GetEnumeratorTypes(comp, this));
            }

            ExpressionEvaluator.WalkComprehension(ddg._eval, (Comprehension)Ast);
        }
    }

    class GeneratorComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public GeneratorComprehensionAnalysisUnit(Comprehension node, PythonAst parent, AnalysisUnit outerUnit, InterpreterScope outerScope)
            : base(node, parent,
                new ComprehensionScope(
                    new GeneratorInfo(
                        outerUnit.State,
                        outerUnit.ProjectEntry
                    ),
                    node,
                    outerScope
                ),
                outerUnit
            ) { }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            base.AnalyzeWorker(ddg, cancel);

            var generator = (GeneratorInfo)Scope.AnalysisValue;
            var node = (GeneratorExpression)Ast;

            generator.AddYield(node, this, ddg._eval.Evaluate(node.Item));
        }
    }

    class SetComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public SetComprehensionAnalysisUnit(Comprehension node, PythonAst parent, AnalysisUnit outerUnit, InterpreterScope outerScope)
            : base(node, parent,
            new ComprehensionScope(new SetInfo(outerUnit.State, node, outerUnit.ProjectEntry), node, outerScope),
            outerUnit) { }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            base.AnalyzeWorker(ddg, cancel);

            var set = (SetInfo)Scope.AnalysisValue;
            var node = (SetComprehension)Ast;

            set.AddTypes(this, ddg._eval.Evaluate(node.Item));
        }
    }


    class DictionaryComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public DictionaryComprehensionAnalysisUnit(Comprehension node, PythonAst parent, AnalysisUnit outerUnit, InterpreterScope outerScope)
            : base(node, parent,
            new ComprehensionScope(new DictionaryInfo(outerUnit.ProjectEntry, node), node, outerScope),
            outerUnit) { }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            base.AnalyzeWorker(ddg, cancel);

            var dict = (DictionaryInfo)Scope.AnalysisValue;
            var node = (DictionaryComprehension)Ast;

            dict.SetIndex(node, this, ddg._eval.Evaluate(node.Key), ddg._eval.Evaluate(node.Value));
        }
    }

    class ListComprehensionAnalysisUnit : ComprehensionAnalysisUnit {
        public ListComprehensionAnalysisUnit(Comprehension node, PythonAst parent, AnalysisUnit outerUnit, InterpreterScope outerScope)
            : base(node, parent,
            new ComprehensionScope(
                new ListInfo(
                    VariableDef.EmptyArray,
                    outerUnit.State.ClassInfos[BuiltinTypeId.List],
                    node,
                    outerUnit.ProjectEntry
                ), node, outerScope),
            outerUnit) { }

        internal override void AnalyzeWorker(DDG ddg, CancellationToken cancel) {
            base.AnalyzeWorker(ddg, cancel);

            var list = (ListInfo)Scope.AnalysisValue;
            var node = (ListComprehension)Ast;

            list.AddTypes(this, new[] { ddg._eval.Evaluate(node.Item) });
        }
    }
}
