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

        public ICollection<TValue> Values {
            get {
                return dictionary.Values;
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
