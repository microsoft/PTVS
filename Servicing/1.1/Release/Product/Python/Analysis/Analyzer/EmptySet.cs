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
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.PythonTools.Analysis {
    sealed class EmptySet<T> : ISet<T> {
        private static readonly IEnumerator<T> EmptyEnum = new EmptyEnumerator();
        public static readonly EmptySet<T> Instance = new EmptySet<T>();

        private EmptySet() {
        }

        #region ISet<T> Members

        public bool Add(T item) {
            throw new InvalidOperationException();
        }

        public void ExceptWith(IEnumerable<T> other) {
            throw new InvalidOperationException();
        }

        public void IntersectWith(IEnumerable<T> other) {
            throw new InvalidOperationException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) {
            return true;
        }

        public bool IsProperSupersetOf(IEnumerable<T> other) {
            return false;
        }

        public bool IsSubsetOf(IEnumerable<T> other) {
            return true;
        }

        public bool IsSupersetOf(IEnumerable<T> other) {
            return false;
        }

        public bool Overlaps(IEnumerable<T> other) {
            return false;
        }

        public bool SetEquals(IEnumerable<T> other) {
            foreach (T x in other) {
                return false;
            }
            return true;
        }

        public void SymmetricExceptWith(IEnumerable<T> other) {
            throw new InvalidOperationException();
        }

        public void UnionWith(IEnumerable<T> other) {
            throw new InvalidOperationException();
        }

        #endregion

        #region ICollection<T> Members

        void ICollection<T>.Add(T item) {
            throw new InvalidOperationException();
        }

        public void Clear() {
        }

        public bool Contains(T item) {
            return false;
        }

        public void CopyTo(T[] array, int arrayIndex) {
        }

        public int Count {
            get { return 0; }
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public bool Remove(T item) {
            throw new InvalidOperationException();
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator() {
            return EmptyEnum;
        }

        #endregion

        #region IEnumerable Members

        IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return EmptyEnum;
        }

        #endregion

        class EmptyEnumerator : IEnumerator<T> {
            #region IEnumerator<T> Members

            public T Current {
                get { throw new NotImplementedException(); }
            }

            #endregion

            #region IDisposable Members

            public void Dispose() {
            }

            #endregion

            #region IEnumerator Members

            object IEnumerator.Current {
                get { throw new InvalidOperationException(); }
            }

            public bool MoveNext() {
                return false;
            }

            public void Reset() {
            }

            #endregion
        }

    }
}
