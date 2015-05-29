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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// An analysis value represents a set of variables and code.  Examples of 
    /// analysis values include top-level code, classes, and functions.
    /// </summary>
    public class AnalysisValue : IAnalysisSet {
        [ThreadStatic]
        private static HashSet<AnalysisValue> _processing;

        protected AnalysisValue() { }


        internal bool IsAlive {
            get {
                return true;
            }
        }

        /// <summary>
        /// Returns an immutable set which contains just this AnalysisValue.
        /// 
        /// Currently implemented as returning the AnalysisValue object directly which implements ISet{AnalysisValue}.
        /// </summary>
        public IAnalysisSet SelfSet {
            get { return this; }
        }

        /// <summary>
        /// Gets the name of the value if it has one, or null if it's a non-named item.
        /// 
        /// The name property here is typically the same value you'd get by accessing __name__
        /// on the real Python object.
        /// </summary>
        public virtual string Name {
            get {
                return null;
            }
        }

        /// <summary>
        /// Gets the documentation of the value.
        /// </summary>
        public virtual string Documentation {
            get {
                return null;
            }
        }

        /// <summary>
        /// Gets a list of locations where this value is defined.
        /// </summary>
        public virtual IEnumerable<LocationInfo> Locations {
            get { return LocationInfo.Empty; }
        }

        public virtual IEnumerable<OverloadResult> Overloads {
            get {
                return Enumerable.Empty<OverloadResult>();
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

        /// <summary>
        /// Returns the member type of the analysis value, or PythonMemberType.Unknown if it's unknown.
        /// </summary>
        public virtual PythonMemberType MemberType {
            get {
                return PythonMemberType.Unknown;
            }
        }

        public virtual IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
            return new Dictionary<string, IAnalysisSet>();
        }

        /// <summary>
        /// Gets the constant value that this object represents, if it's a constant.
        /// 
        /// Returns Type.Missing if the value is not constant (because it returns null
        /// if the type is None).
        /// </summary>
        /// <returns></returns>
        public virtual object GetConstantValue() {
            return Type.Missing;
        }

        /// <summary>
        /// Returns the constant value as a string.  This returns a string if the constant
        /// value is either a unicode or ASCII string.
        /// </summary>
        public string GetConstantValueAsString() {
            var constName = GetConstantValue();
            if (constName != null) {
                string unicodeName = constName as string;
                AsciiString asciiName;
                if (unicodeName != null) {
                    return unicodeName;
                } else if ((asciiName = constName as AsciiString) != null) {
                    return asciiName.String;
                }
            }
            return null;
        }

        public virtual IPythonType PythonType {
            get { return null; }
        }

        public virtual IPythonProjectEntry DeclaringModule {
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

        public virtual IEnumerable<IAnalysisSet> Mro {
            get {
                yield break;
            }
        }

        public virtual AnalysisUnit AnalysisUnit {
            get { return null; }
        }

        #region Dynamic Operations

        /// <summary>
        /// Attempts to call this object and returns the set of possible types it can return.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="keywordArgNames">Keyword argument names, * and ** are included in here for splatting calls</param>
        public virtual IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return AnalysisSet.Empty;
        }

        /// <summary>
        /// Attempts to get a member from this object with the specified name.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="name">The name of the member.</param>
        /// <remarks>
        /// Overrides of this method must unconditionally call the base
        /// implementation, even if the return value is ignored.
        /// </remarks>
        public virtual IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.Empty;
        }

        public virtual void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
        }

        public virtual void DeleteMember(Node node, AnalysisUnit unit, string name) {
        }

        public virtual void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
        }

        public virtual IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return CallReverseBinaryOp(node, unit, operation, rhs);
        }

        internal IAnalysisSet CallReverseBinaryOp(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            switch (operation) {
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                case PythonOperator.In:
                case PythonOperator.NotIn:
                    return unit.DeclaringModule.ProjectEntry.ProjectState.ClassInfos[BuiltinTypeId.Bool].Instance;
                default:
                    var res = AnalysisSet.Empty;
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
        public virtual IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return AnalysisSet.Empty;
        }

        public virtual IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return this.SelfSet;
        }

        /// <summary>
        /// Returns the length of the object if it's known, or null if it's not a fixed size object.
        /// </summary>
        /// <returns></returns>
        public virtual int? GetLength() {
            return null;
        }

        public virtual IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            // TODO: need more than constant 0...
            //index = (VariableRef(ConstantInfo(0, self.ProjectState, False)), )
            //self.AssignTo(self._state.IndexInto(listRefs, index), node, node.Left)
            return GetIndex(node, unit, unit.ProjectState.ClassInfos[BuiltinTypeId.Int].SelfSet);
        }

        public virtual IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) {
            return AnalysisSet.Empty;
        }

        public virtual IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return GetMember(node, unit, "__iter__").Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);
        }

        public virtual IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) {
            return GetMember(node, unit, "__aiter__")
                .Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames)
                .Await(node, unit);
        }

        public virtual IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return GetMember(node, unit, "__getitem__").Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);
        }

        /// <summary>
        /// Returns a list of key/value pairs stored in the this object which are retrivable using
        /// indexing.  For lists the key values will be integers (potentially constant, potentially not), 
        /// for dicts the key values will be arbitrary analysis values.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            yield break;
        }

        public virtual void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
        }

        public virtual IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            return SelfSet;
        }

        public virtual IAnalysisSet GetStaticDescriptor(AnalysisUnit unit) {
            return SelfSet;
        }

        public virtual IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) {
            return SelfSet;
        }

        public virtual IAnalysisSet GetInstanceType() {
            return SelfSet;
        }

        public virtual IAnalysisSet Await(Node node, AnalysisUnit unit) {
            return AnalysisSet.Empty;
        }

        internal virtual bool IsOfType(IAnalysisSet klass) {
            return false;
        }

        internal virtual BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Unknown;
            }
        }

        #endregion

        #region Union Equality

        /// <summary>
        /// Returns an analysis value representative of both this and another
        /// analysis value. This should only be called when
        /// <see cref="UnionEquals"/> returns true for the two values.
        /// </summary>
        /// <param name="av">The value to merge with.</param>
        /// <param name="strength">A value matching that passed to
        /// <see cref="UnionEquals"/>.</param>
        /// <returns>A merged analysis value.</returns>
        /// <remarks>
        /// <para>Calling this function when <see cref="UnionEquals"/> returns
        /// false for the same parameters is undefined.</para>
        /// 
        /// <para>Where there is no analysis value representative of those
        /// provided, it is preferable to return this rather than
        /// <paramref name="av"/>.</para>
        /// 
        /// <para>
        /// <paramref name="strength"/> is used as a key in this function and must
        /// match the value used in <see cref="UnionEquals"/>.
        /// </para>
        /// </remarks>
        internal virtual AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            return this;
        }

        /// <summary>
        /// Determines whether two analysis values are effectively equivalent.
        /// </summary>
        /// <remarks>
        /// The intent of <paramref name="strength"/> is to allow different
        /// types to merge more aggressively. For example, string constants
        /// may merge into a non-specific string instance at a low strength,
        /// while distinct user-defined types may merge into <c>object</c> only
        /// at higher strengths. There is no defined maximum value.
        /// </remarks>
        internal virtual bool UnionEquals(AnalysisValue av, int strength) {
            return Equals(av);
        }

        /// <summary>
        /// Returns a hash code for this analysis value for the given strength.
        /// </summary>
        /// <remarks>
        /// <paramref name="strength"/> must match the value that will be
        /// passed to <see cref="UnionEquals"/> and
        /// <see cref="UnionMergeTypes"/> to ensure valid results.
        /// </remarks>
        internal virtual int UnionHashCode(int strength) {
            return GetHashCode();
        }

        #endregion

        #region Recursion Tracking

        /// <summary>
        /// Tracks whether or not we're currently processing this value to
        /// prevent stack overflows. Returns true if the the variable should be
        /// processed.
        /// </summary>
        /// <returns>
        /// True if the variable should be processed. False if it should be
        /// skipped.
        /// </returns>
        public bool Push() {
            if (_processing == null) {
                _processing = new HashSet<AnalysisValue>();
            }

            return _processing.Add(this);
        }

        public void Pop() {
            bool wasRemoved = _processing.Remove(this);
            Debug.Assert(wasRemoved, string.Format("Popped {0} but it wasn't pushed", GetType().FullName));
        }

        #endregion



        internal virtual void AddReference(Node node, AnalysisUnit analysisUnit) {
        }

        internal virtual IEnumerable<LocationInfo> References {
            get {
                yield break;
            }
        }

        public override string ToString() {
            return ShortDescription;
        }

        IAnalysisSet IAnalysisSet.Add(AnalysisValue item, bool canMutate) {
            if (((IAnalysisSet)this).Comparer.Equals(this, item)) {
                return this;
            }
            return new AnalysisSetDetails.AnalysisSetTwoObject(this, item);
        }

        IAnalysisSet IAnalysisSet.Add(AnalysisValue item, out bool wasChanged, bool canMutate) {
            if (((IAnalysisSet)this).Comparer.Equals(this, item)) {
                wasChanged = false;
                return this;
            }
            wasChanged = true;
            return new AnalysisSetDetails.AnalysisSetTwoObject(this, item);
        }

        IAnalysisSet IAnalysisSet.Union(IEnumerable<AnalysisValue> items, bool canMutate) {
            if (items == null || items.All(av => ((IAnalysisSet)this).Comparer.Equals(this, av))) {
                return this;
            }
            return AnalysisSet.Create(items).Add(this, false);
        }

        IAnalysisSet IAnalysisSet.Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate) {
            if (items.All(av => ((IAnalysisSet)this).Comparer.Equals(this, av))) {
                wasChanged = false;
                return this;
            }
            wasChanged = true;
            return AnalysisSet.Create(items).Add(this, false);
        }

        IAnalysisSet IAnalysisSet.Clone() {
            return this;
        }

        bool IAnalysisSet.Contains(AnalysisValue item) {
            return ((IAnalysisSet)this).Comparer.Equals(this, item);
        }

        bool IAnalysisSet.SetEquals(IAnalysisSet other) {
            if (other.Count != 1) {
                return false;
            }
            var av = other as AnalysisValue;
            if (av != null) {
                return ((IAnalysisSet)this).Comparer.Equals(this, av);
            }

            return ((IAnalysisSet)this).Comparer.Equals(this, other.First());
        }

        int IAnalysisSet.Count {
            get { return 1; }
        }

        IEqualityComparer<AnalysisValue> IAnalysisSet.Comparer {
            get { return ObjectComparer.Instance; }
        }

        IEnumerator<AnalysisValue> IEnumerable<AnalysisValue>.GetEnumerator() {
            yield return this;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return ((IEnumerable<AnalysisValue>)this).GetEnumerator();
        }
    }
}
