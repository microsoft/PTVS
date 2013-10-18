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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// A namespace represents a set of variables and code.  Examples of 
    /// namespaces include top-level code, classes, and functions.
    /// </summary>
    internal class Namespace : AnalysisValue, INamespaceSet, IAnalysisValue {
        [ThreadStatic]
        private static HashSet<Namespace> _processing;
        private static OverloadResult[] EmptyOverloadResult = new OverloadResult[0];

        public Namespace() { }

        /// <summary>
        /// Returns an immutable set which contains just this Namespace.
        /// 
        /// Currently implemented as returning the Namespace object directly which implements ISet{Namespace}.
        /// </summary>
        public INamespaceSet SelfSet {
            get { return this; }
        }

        #region Namespace Information

        public LocationInfo Location {
            get {
                return Locations.FirstOrDefault();
            }
        }

        public virtual ICollection<OverloadResult> Overloads {
            get {
                return EmptyOverloadResult;
            }
        }

        public virtual string Description {
            get { return null; }
        }

        public virtual string ShortDescription {
            get {
                return Description;
            }
        }

        PythonMemberType IAnalysisValue.ResultType {
            get { return MemberType; }
        }

        public virtual IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            return new Dictionary<string, INamespaceSet>();
        }

        public override IDictionary<string, ISet<AnalysisValue>> GetAllMembers() {
            // TODO: Need to fix the null here
            var members = GetAllMembers(null);
            var res = new Dictionary<string, ISet<AnalysisValue>>();
            foreach (var member in members) {
                res[member.Key] = new HashSet<AnalysisValue>(member.Value);
            }
            return res;
        }

        public virtual IPythonType PythonType {
            get { return null; }
        }

        public virtual ProjectEntry DeclaringModule {
            get {
                return null;
            }
        }

        public virtual int DeclaringVersion {
            get {
                return -1;
            }
        }

        public bool IsCurrent {
            get {
                return DeclaringModule == null || DeclaringVersion == DeclaringModule.AnalysisVersion;
            }
        }

        public virtual AnalysisUnit AnalysisUnit {
            get { return null; }
        }

        #endregion

        #region Dynamic Operations

        /// <summary>
        /// Attempts to call this object and returns the set of possible types it can return.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="keywordArgNames">Keyword argument names, * and ** are included in here for splatting calls</param>
        public virtual INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            return NamespaceSet.Empty;
        }

        public virtual INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            return NamespaceSet.Empty;
        }

        public virtual void SetMember(Node node, AnalysisUnit unit, string name, INamespaceSet value) {
        }

        public virtual void DeleteMember(Node node, AnalysisUnit unit, string name) {
        }

        public virtual void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, INamespaceSet value) {
        }

        public virtual INamespaceSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            switch (operation) {
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                    return unit.DeclaringModule.ProjectEntry.ProjectState.ClassInfos[BuiltinTypeId.Bool].Instance;
                default:
                    var res = NamespaceSet.Empty;
                    foreach (var value in rhs) {
                        res = res.Union(value.ReverseBinaryOperation(node, unit, operation, SelfSet));
                    }

                    return res;
            }
        }

        /// <summary>
        /// Provides implementation of __r*__methods (__radd__, __rsub__, etc...)
        /// 
        /// This is dispatched to when the LHS doesn't understand the RHS.  Unlike normal Python it's currently
        /// the LHS responsibility to dispatch to this.
        /// </summary>
        public virtual INamespaceSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, INamespaceSet rhs) {
            return NamespaceSet.Empty;
        }

        public virtual INamespaceSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return this.SelfSet;
        }

        /// <summary>
        /// Returns the length of the object if it's known, or null if it's not a fixed size object.
        /// </summary>
        /// <returns></returns>
        public virtual int? GetLength() {
            return null;
        }

        public virtual INamespaceSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            // TODO: need more than constant 0...
            //index = (VariableRef(ConstantInfo(0, self.ProjectState, False)), )
            //self.AssignTo(self._state.IndexInto(listRefs, index), node, node.Left)
            return GetIndex(node, unit, unit.ProjectState.ClassInfos[BuiltinTypeId.Int].SelfSet);
        }

        public virtual INamespaceSet GetIterator(Node node, AnalysisUnit unit) {
            return GetMember(node, unit, "__iter__").Call(node, unit, ExpressionEvaluator.EmptyNamespaces, ExpressionEvaluator.EmptyNames);
        }

        public virtual INamespaceSet GetIndex(Node node, AnalysisUnit unit, INamespaceSet index) {
            return GetMember(node, unit, "__getitem__").Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);
        }

        public virtual void SetIndex(Node node, AnalysisUnit unit, INamespaceSet index, INamespaceSet value) {
        }

        public virtual INamespaceSet GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            return SelfSet;
        }

        public virtual INamespaceSet GetStaticDescriptor(AnalysisUnit unit) {
            return SelfSet;
        }

        public virtual INamespaceSet GetDescriptor(PythonAnalyzer projectState, Namespace instance, Namespace context) {
            return SelfSet;
        }

        public virtual INamespaceSet GetInstanceType() {
            return SelfSet;
        }

        public virtual bool IsOfType(BuiltinClassInfo klass) {
            return false;
        }

        public virtual BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Unknown;
            }
        }

        #endregion

        #region Union Equality

        /// <summary>
        /// Returns a namespace representative of both this and another
        /// namespace. This should only be called when
        /// <see cref="UnionEquals"/> returns true for the two namespaces.
        /// </summary>
        /// <param name="ns">The namespace to merge with.</param>
        /// <param name="strength">A value matching that passed to
        /// <see cref="UnionEquals"/>.</param>
        /// <returns>A merged namespace.</returns>
        /// <remarks>
        /// <para>Calling this function when <see cref="UnionEquals"/> returns
        /// false for the same parameters is undefined.</para>
        /// 
        /// <para>Where there is no namespace representative of those provided,
        /// it is preferable to return this rather than <paramref name="ns"/>.
        /// </para>
        /// 
        /// <para>
        /// <paramref name="strength"/> is used as a key in this function and must
        /// match the value used in <see cref="UnionEquals"/>.
        /// </para>
        /// </remarks>
        internal virtual Namespace UnionMergeTypes(Namespace ns, int strength) {
            return this;
        }

        /// <summary>
        /// Determines whether two namespaces are effectively equivalent.
        /// </summary>
        /// <remarks>
        /// The intent of <paramref name="strength"/> is to allow different
        /// types to merge more aggressively. For example, string constants
        /// may merge into a non-specific string instance at a low strength,
        /// while distinct user-defined types may merge into <c>object</c> only
        /// at higher strengths. There is no defined maximum value.
        /// </remarks>
        public virtual bool UnionEquals(Namespace ns, int strength) {
            return Equals(ns);
        }

        /// <summary>
        /// Returns a hash code for this namespace for the given strength.
        /// </summary>
        /// <remarks>
        /// <paramref name="strength"/> must matche the value that will be
        /// passed to <see cref="UnionEquals"/> and
        /// <see cref="UnionMergeTypes"/> to ensure valid results.
        /// </remarks>
        public virtual int UnionHashCode(int strength) {
            return GetHashCode();
        }

        #endregion

        #region Recursion Tracking

        /// <summary>
        /// Tracks whether or not we're currently processing this VariableRef to prevent
        /// stack overflows.  Returns true if the the variable should be processed.
        /// </summary>
        /// <returns></returns>
        public bool Push() {
            if (_processing == null) {
                _processing = new HashSet<Namespace>();
            }

            return _processing.Add(this);
        }

        public void Pop() {
            _processing.Remove(this);
        }

        #endregion



        internal virtual void AddReference(Node node, AnalysisUnit analysisUnit) {
        }

        public virtual IEnumerable<LocationInfo> References {
            get {
                yield break;
            }
        }

        public override string ToString() {
            return ShortDescription;
        }

        #region External value support

        internal override Namespace AsNamespace() {
            return this;
        }

        internal virtual AnalysisValue AsExternal() {
            return this;
        }

        #endregion

        INamespaceSet INamespaceSet.Add(Namespace item, bool canMutate) {
            if (((INamespaceSet)this).Comparer.Equals(this, item)) {
                return this;
            }
            return new NamespaceSetDetails.NamespaceSetTwoObject(this, item);
        }

        INamespaceSet INamespaceSet.Add(Namespace item, out bool wasChanged, bool canMutate) {
            if (((INamespaceSet)this).Comparer.Equals(this, item)) {
                wasChanged = false;
                return this;
            }
            wasChanged = true;
            return new NamespaceSetDetails.NamespaceSetTwoObject(this, item);
        }

        INamespaceSet INamespaceSet.Union(IEnumerable<Namespace> items, bool canMutate) {
            if (items.All(ns => ((INamespaceSet)this).Comparer.Equals(this, ns))) {
                return this;
            }
            return NamespaceSet.Create(items).Add(this, false);
        }

        INamespaceSet INamespaceSet.Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate) {
            if (items.All(ns => ((INamespaceSet)this).Comparer.Equals(this, ns))) {
                wasChanged = false;
                return this;
            }
            wasChanged = true;
            return NamespaceSet.Create(items).Add(this, false);
        }

        INamespaceSet INamespaceSet.Clone() {
            return this;
        }

        bool INamespaceSet.Contains(Namespace item) {
            return ((INamespaceSet)this).Comparer.Equals(this, item);
        }

        bool INamespaceSet.SetEquals(INamespaceSet other) {
            if (other.Count != 1) {
                return false;
            }
            var ns = other as Namespace;
            if (ns != null) {
                return ((INamespaceSet)this).Comparer.Equals(this, ns);
            }

            return ((INamespaceSet)this).Comparer.Equals(this, other.First());
        }

        int INamespaceSet.Count {
            get { return 1; }
        }

        IEqualityComparer<Namespace> INamespaceSet.Comparer {
            get { return ObjectComparer.Instance; }
        }

        IEnumerator<Namespace> IEnumerable<Namespace>.GetEnumerator() {
            yield return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ((IEnumerable<Namespace>)this).GetEnumerator();
        }
    }
}
