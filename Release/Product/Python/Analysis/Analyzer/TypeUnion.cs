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
    class TypeUnion : ICollection<Namespace> {
        private ISet<Namespace> _ns;
        public const int MaxUniqueNamespaces = 10;

        public static readonly IEqualityComparer<Namespace> ObjectComparer = EqualityComparer<Namespace>.Default;
        public static readonly IEqualityComparer<Namespace> UnionComparer = new UnionEqualityComparer();

        public bool Add(Namespace ns) {
            return Add(ref _ns, ns);
        }

        internal static bool Add(ref ISet<Namespace> set, Namespace ns) {
            Namespace one;
            if (set == null) {
                set = ns;
                return true;
            } else if ((one = set as Namespace) != null) {
                if (!one.Contains(ns)) {
                    set = new SetOfTwo<Namespace>(one, ns);
                    return true;
                }
            } else if (set is SetOfTwo<Namespace>) {
                if (set.Contains(ns)) {
                    return false;
                }
                set = new HashSet<Namespace>(set);
                set.Add(ns);
                return true;
            } else if (set.Add(ns)) {
                if (set.Count > MaxUniqueNamespaces) {
                    if (((HashSet<Namespace>)set).Comparer == ObjectComparer) {
                        set = new HashSet<Namespace>(set, UnionComparer);
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

        public bool Contains(Namespace ns) {
            if (_ns != null) {
                return _ns.Contains(ns);
            }
            return false;
        }

        public ISet<Namespace> ToSet() {
            if (_ns == null) {
                return EmptySet<Namespace>.Instance;
            } else if (_ns is SetOfOne<Namespace> || _ns is SetOfTwo<Namespace>) {
                return _ns;
            }

            return new HashSet<Namespace>(_ns);
        }

        public ISet<Namespace> ToSetNoCopy() {
            if (Count == 0) {
                return EmptySet<Namespace>.Instance;
            }

            return _ns;
        }

        #region IEnumerable<Namespace> Members

        public IEnumerator<Namespace> GetEnumerator() {
            if (_ns == null) {
                return EmptySet<Namespace>.EmptyEnum;
            }
            return _ns.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            if (_ns == null) {
                return EmptySet<Namespace>.EmptyEnum;
            }
            return _ns.GetEnumerator();
        }

        #endregion

        class UnionEqualityComparer : IEqualityComparer<Namespace> {
            #region IEqualityComparer<Namespace> Members

            public bool Equals(Namespace x, Namespace y) {
                return x.UnionEquals(y);
            }

            public int GetHashCode(Namespace obj) {
                return obj.UnionHashCode();
            }

            #endregion
        }

        #region ICollection<Namespace> Members

        void ICollection<Namespace>.Add(Namespace item) {
            throw new InvalidOperationException();
        }

        public void Clear() {
            throw new InvalidOperationException();
        }

        public void CopyTo(Namespace[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public bool Remove(Namespace item) {
            throw new InvalidOperationException();
        }

        #endregion
    }
}
