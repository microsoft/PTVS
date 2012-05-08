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
    internal class Namespace : AnalysisValue, ISet<Namespace>, IAnalysisValue {
        [ThreadStatic] private static HashSet<Namespace> _processing;

        public Namespace() { }

        /// <summary>
        /// Returns an immutable set which contains just this Namespace.
        /// 
        /// Currently implemented as returning the Namespace object directly which implements ISet{Namespace}.
        /// </summary>
        public ISet<Namespace> SelfSet {
            get { return this; }
        }

        #region Namespace Information
        /*
        public LocationInfo Location {
            get {
            }
        }*/

        public LocationInfo Location {
            get {
                return Locations.FirstOrDefault();
            }
        }

        private static OverloadResult[] EmptyOverloadResult = new OverloadResult[0];
        public virtual ICollection<OverloadResult> Overloads {
            get {
                return EmptyOverloadResult;
            }
        }

        public virtual string Documentation {
            get {
                return String.Empty;
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

        public virtual PythonMemberType ResultType {
            get { return PythonMemberType.Unknown; }
        }

        public virtual IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return new Dictionary<string, ISet<Namespace>>();
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

        #endregion

        #region Dynamic Operations

        /// <summary>
        /// Attempts to call this object and returns the set of possible types it can return.
        /// </summary>
        /// <param name="node">The node which is triggering the call, for reference tracking</param>
        /// <param name="unit">The analysis unit performing the analysis</param>
        /// <param name="args">The arguments being passed to the function</param>
        /// <param name="keywordArgNames">Keyword argument names, * and ** are included in here for splatting calls</param>
        public virtual ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            return EmptySet<Namespace>.Instance;
        }

        public virtual ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return EmptySet<Namespace>.Instance;
        }

        public virtual void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
        }

        public virtual void DeleteMember(Node node, AnalysisUnit unit, string name) {
        }

        public virtual void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, ISet<Namespace> value) {
        }

        public virtual ISet<Namespace> BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {
            switch (operation) {
                case PythonOperator.Is:
                case PythonOperator.IsNot:
                    return unit.DeclaringModule.ProjectEntry.ProjectState._boolType.Instance;
                default:
                    ISet<Namespace> res = EmptySet<Namespace>.Instance;
                    bool madeSet = false;
                    foreach (var value in rhs) {
                        res = res.Union(value.ReverseBinaryOperation(node, unit, operation, SelfSet), ref madeSet);
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
        public virtual ISet<Namespace> ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, ISet<Namespace> rhs) {                        
            return EmptySet<Namespace>.Instance;
        }

        public virtual ISet<Namespace> UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return this.SelfSet;
        }

        /// <summary>
        /// Returns the length of the object if it's known, or null if it's not a fixed size object.
        /// </summary>
        /// <returns></returns>
        public virtual int? GetLength() {
            return null;
        }

        public virtual ISet<Namespace> GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            // TODO: need more than constant 0...
            //index = (VariableRef(ConstantInfo(0, self.ProjectState, False)), )
            //self.AssignTo(self._state.IndexInto(listRefs, index), node, node.Left)
            return GetIndex(node, unit, unit.ProjectState._intType.SelfSet);
        }

        public virtual ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            return GetMember(node, unit, "__getitem__").Call(node, unit, new[] { index }, ExpressionEvaluator.EmptyNames);
        }

        public virtual void SetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index, ISet<Namespace> value) {
        }

        public virtual ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            return SelfSet;
        }

        public virtual ISet<Namespace> GetStaticDescriptor(AnalysisUnit unit) {
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

        public virtual bool UnionEquals(Namespace ns) {
            return Equals(ns);
        }

        public virtual int UnionHashCode() {
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

        #region SelfSet

        #region ISet<Namespace> Members

        bool ISet<Namespace>.Add(Namespace item) {
            throw new InvalidOperationException();
        }

        void ISet<Namespace>.ExceptWith(IEnumerable<Namespace> other) {
            throw new InvalidOperationException();
        }

        void ISet<Namespace>.IntersectWith(IEnumerable<Namespace> other) {
            throw new InvalidOperationException();
        }

        bool ISet<Namespace>.IsProperSubsetOf(IEnumerable<Namespace> other) {
            throw new NotImplementedException();
        }

        bool ISet<Namespace>.IsProperSupersetOf(IEnumerable<Namespace> other) {
            throw new NotImplementedException();
        }

        bool ISet<Namespace>.IsSubsetOf(IEnumerable<Namespace> other) {
            throw new NotImplementedException();
        }

        bool ISet<Namespace>.IsSupersetOf(IEnumerable<Namespace> other) {
            throw new NotImplementedException();
        }

        bool ISet<Namespace>.Overlaps(IEnumerable<Namespace> other) {
            throw new NotImplementedException();
        }

        bool ISet<Namespace>.SetEquals(IEnumerable<Namespace> other) {
            var enumerator = other.GetEnumerator();
            if (enumerator.MoveNext()) {
                if (((ISet<Namespace>)this).Contains(enumerator.Current)) {
                    return !enumerator.MoveNext();
                }
            }
            return false;
        }

        void ISet<Namespace>.SymmetricExceptWith(IEnumerable<Namespace> other) {
            throw new InvalidOperationException();
        }

        void ISet<Namespace>.UnionWith(IEnumerable<Namespace> other) {
            throw new InvalidOperationException();
        }

        #endregion

        #region ICollection<Namespace> Members

        void ICollection<Namespace>.Add(Namespace item) {
            throw new NotImplementedException();
        }

        void ICollection<Namespace>.Clear() {
            throw new InvalidOperationException();
        }

        bool ICollection<Namespace>.Contains(Namespace item) {
            return EqualityComparer<Namespace>.Default.Equals(item, this);
        }

        void ICollection<Namespace>.CopyTo(Namespace[] array, int arrayIndex) {
            array[arrayIndex] = this;
        }

        int ICollection<Namespace>.Count {
            get { return 1; }
        }

        bool ICollection<Namespace>.IsReadOnly {
            get { return true; }
        }

        bool ICollection<Namespace>.Remove(Namespace item) {
            throw new InvalidOperationException();
        }

        #endregion

        #region IEnumerable<Namespace> Members

        IEnumerator<Namespace> IEnumerable<Namespace>.GetEnumerator() {
            return new SetOfOneEnumerator<Namespace>(this);
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { 
            yield return this; 
        }

        #endregion

        #endregion

        internal virtual void AddReference(Node node, AnalysisUnit analysisUnit) {
        }

        public virtual IEnumerable<LocationInfo> References {
            get {
                yield break;
            }
        }
    }
}
