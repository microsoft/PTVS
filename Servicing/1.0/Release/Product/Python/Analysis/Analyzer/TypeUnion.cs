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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis.Values;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    class TypeUnion<T> : IEnumerable<T> where T : Namespace {
        private HashSet<T> _ns;
        private const int MaxUniqueNamespaces = 10;

        private static IEqualityComparer<T> ObjectComparer = EqualityComparer<T>.Default;
        public static IEqualityComparer<T> UnionComparer = new UnionEqualityComparer();
        private static HashSet<T> Empty = new HashSet<T>();

        public bool Add(T ns, PythonAnalyzer state) {
            if (_ns == Empty) {
                return false;
            }
            if (_ns == null) {
                _ns = new HashSet<T>(ObjectComparer);
            }

            if (_ns.Add(ns)) {
                if (_ns.Count > MaxUniqueNamespaces) {
                    if (_ns.Comparer == ObjectComparer) {
                        _ns = new HashSet<T>(_ns, UnionComparer);
                    } else {
                        // TODO: We should warn here in debug builds so see if we can improve tracking
                        _ns = Empty;
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
            if (Count == 0) {
                return EmptySet<T>.Instance;
            }

            return new HashSet<T>(this);
        }

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator() {
            if (_ns == null) {
                return EmptySet();
            }
            return _ns.GetEnumerator();
        }

        private IEnumerator<T> EmptySet() {
            yield break;
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
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
    }

    class TypeUnion : TypeUnion<Namespace> {
    }
}
