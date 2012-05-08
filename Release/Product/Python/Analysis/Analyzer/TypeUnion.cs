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

namespace Microsoft.PythonTools.Analysis.Interpreter {
    class TypeUnion<T> : ICollection<T> where T : Namespace {
        private ISet<T> _ns;
        private const int MaxUniqueNamespaces = 10;

        private static IEqualityComparer<T> ObjectComparer = EqualityComparer<T>.Default;
        public static IEqualityComparer<T> UnionComparer = new UnionEqualityComparer();
        private static HashSet<T> Empty = new HashSet<T>();

        public TypeUnion() {
        }

        public bool Add(T ns) {
            if (_ns == Empty) {
                return false;
            }
            SetOfOne<T> one;
            if (_ns == null) {
                _ns = new SetOfOne<T>(ns);
                return true;
            } else if ((one = _ns as SetOfOne<T>) != null) {
                if (!one.Contains(ns)) {
                    _ns = new SetOfTwo<T>(one.Value, ns);
                    return true;
                }
                return false;
            } else if (_ns is SetOfTwo<T>) {
                if (_ns.Contains(ns)) {
                    return false;
                }
                _ns = new HashSet<T>(_ns);
            }

            if (_ns.Add(ns)) {
                if (_ns.Count > MaxUniqueNamespaces) {
                    if (((HashSet<T>)_ns).Comparer == ObjectComparer) {
                        _ns = new HashSet<T>(_ns, UnionComparer);
                    }
                }
                return true;
            }

            return false;
        }

        public int Count {
            get {
                if (_ns == null) {
                    return 0;
                }
                return _ns.Count;
            }
        }

        public bool Contains(T ns) {
            if (_ns != null) {
                return _ns.Contains(ns);
            }
            return false;
        }

        public ISet<T> ToSet() {
            if (_ns == null) {
                return EmptySet<T>.Instance;
            } else if (_ns is SetOfOne<T> || _ns is SetOfTwo<T>) {
                return _ns;
            }

            return new HashSet<T>(_ns);
        }

        public ISet<T> ToSetNoCopy() {
            if (Count == 0) {
                return EmptySet<T>.Instance;
            }

            return _ns;
        }

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator() {
            if (_ns == null) {
                return EmptySet<T>.EmptyEnum;
            }
            return _ns.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            if (_ns == null) {
                return EmptySet<T>.EmptyEnum;
            }
            return _ns.GetEnumerator();
        }

        #endregion

        class UnionEqualityComparer : IEqualityComparer<T> {
            #region IEqualityComparer<T> Members

            public bool Equals(T x, T y) {
                return x.UnionEquals(y);
            }

            public int GetHashCode(T obj) {
                return obj.UnionHashCode();
            }

            #endregion
        }

        #region ICollection<T> Members

        void ICollection<T>.Add(T item) {
            throw new InvalidOperationException();
        }

        public void Clear() {
            throw new InvalidOperationException();
        }

        public void CopyTo(T[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public bool Remove(T item) {
            throw new InvalidOperationException();
        }

        #endregion
    }
}
