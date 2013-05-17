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
using System.Diagnostics;

namespace Microsoft.PythonTools.Analysis {
    // Very light wrappers that are intended to discourage direct modification
    // of the wrapped collections. They do not enforce thread-safety or prevent
    // the underlying collection from being modified. Hopefully the JIT
    // optimizer will learn how to completely bypass the wrappers one day.

    [DebuggerDisplay("Count = {Count}")]
    struct ReadOnlyDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly IDictionary<TKey, TValue> dictionary;

        public ReadOnlyDictionary(IDictionary<TKey, TValue> obj) {
            dictionary = obj;
        }

        public TValue this[TKey key] {
            get {
                return dictionary[key];
            }
        }

        public bool TryGetValue(TKey key, out TValue value) {
            return dictionary.TryGetValue(key, out value);
        }

        public int Count {
            get {
                return dictionary.Count;
            }
        }

        public ICollection<TKey> Keys {
            get {
                return dictionary.Keys;
            }
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            return dictionary.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return dictionary.GetEnumerator();
        }

        public bool ContainsKey(TKey key) {
            return dictionary.ContainsKey(key);
        }
    }

    [DebuggerDisplay("Count = {Count}")]
    struct ReadOnlyList<T> : IEnumerable<T> {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly IList<T> list;

        public ReadOnlyList(IList<T> obj) {
            list = obj;
        }

        public T this[int index] {
            get {
                return list[index];
            }
        }

        public int Count {
            get {
                return list.Count;
            }
        }

        public IEnumerator<T> GetEnumerator() {
            return list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return list.GetEnumerator();
        }
    }

    [DebuggerDisplay("Count = {Count}")]
    struct ReadOnlySet<T> : IEnumerable<T> {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private readonly ISet<T> set;

        public ReadOnlySet(ISet<T> obj) {
            set = obj;
        }

        public bool Contains(T item) {
            return set.Contains(item);
        }

        public int Count {
            get {
                return set.Count;
            }
        }

        public IEnumerator<T> GetEnumerator() {
            return set.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return set.GetEnumerator();
        }
    }
}
