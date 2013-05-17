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

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Lightweight ISet object which always holds onto 2 values for reduced memory usage (we have lots of sets)
    /// </summary>
    /// <typeparam name="T"></typeparam>
    sealed class SetOfTwo<T> : ISet<T> {
        private readonly T _value1, _value2;

        public SetOfTwo(T value1, T value2) {
            Debug.Assert(!Object.ReferenceEquals(value1, value2));
            _value1 = value1;
            _value2 = value2;
        }

        public T Value1 {
            get {
                return _value1;
            }
        }

        public T Value2 {
            get {
                return _value2;
            }
        }

        public bool Add(T item) {
            throw new NotImplementedException();
        }

        public void ExceptWith(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public void IntersectWith(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool IsProperSupersetOf(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool IsSupersetOf(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool Overlaps(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public bool SetEquals(IEnumerable<T> other) {
            var enumerator = other.GetEnumerator();
            if (enumerator.MoveNext()) {
                if (EqualityComparer<T>.Default.Equals(enumerator.Current, _value1)) {
                    if (enumerator.MoveNext() && EqualityComparer<T>.Default.Equals(enumerator.Current, _value2)) {
                        return !enumerator.MoveNext();
                    }
                }else if(EqualityComparer<T>.Default.Equals(enumerator.Current, _value2)) {
                    if (enumerator.MoveNext() && EqualityComparer<T>.Default.Equals(enumerator.Current, _value1)) {
                        return !enumerator.MoveNext();
                    }
                }
            }
            return false;
        }

        public void SymmetricExceptWith(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        public void UnionWith(IEnumerable<T> other) {
            throw new NotImplementedException();
        }

        void ICollection<T>.Add(T item) {
            throw new NotImplementedException();
        }

        public void Clear() {
            throw new NotImplementedException();
        }

        public bool Contains(T item) {

            return EqualityComparer<T>.Default.Equals(item, _value1) || 
                EqualityComparer<T>.Default.Equals(item, _value2);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            array[arrayIndex++] = _value1;
            array[arrayIndex++] = _value2;
        }

        public int Count {
            get { return 2; }
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public bool Remove(T item) {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator() {
            yield return _value1;
            yield return _value2;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            yield return _value1;
            yield return _value2;
        }
    }
}
