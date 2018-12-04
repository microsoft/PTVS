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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// A simple dictionary like object which has efficient storage when there's only a single item in the dictionary.
    /// </summary>
    [DebuggerDisplay("Count = {Count}")]
    struct SingleDict<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
        where TKey : class
        where TValue : class
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
        internal object _data; // AnalysisDictionary<TKey, TValue>, SingleEntry<TKey, TValue> or IEqualityComparer<TKey>

        public SingleDict(IEqualityComparer<TKey> comparer) {
            _data = comparer;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        private KeyValuePair<TKey, TValue>[] AllItems {
            get {
                var single = _data as SingleDependency;
                if (single != null) {
                    return new[] { new KeyValuePair<TKey, TValue>(single.Key, single.Value) };
                }

                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict != null) {
                    return dict.ToArray();
                }

                return new KeyValuePair<TKey, TValue>[0];
            }
        }

        public IEqualityComparer<TKey> Comparer {
            get {
                var single = _data as SingleDependency;
                if (single != null) {
                    return single.Comparer;
                }

                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict != null) {
                    return dict.Comparer;
                }

                return (_data as IEqualityComparer<TKey>) ?? EqualityComparer<TKey>.Default;
            }
        }

        internal sealed class SingleDependency {
            public readonly TKey Key;
            public TValue Value;
            public readonly IEqualityComparer<TKey> Comparer;

            public SingleDependency(TKey key, TValue value, IEqualityComparer<TKey> comparer) {
                Key = key;
                Value = value;
                Comparer = comparer;
            }
        }


        public bool ContainsKey(TKey key) {
            var single = _data as SingleDependency;
            if (single != null) {
                return single.Comparer.Equals(single.Key, key);
            }
            var dict = _data as AnalysisDictionary<TKey, TValue>;
            if (dict != null) {
                return dict.ContainsKey(key);
            }
            return false;
        }

        public bool TryGetValue(TKey key, out TValue value) {
            SingleDependency single = _data as SingleDependency;
            if (single != null) {
                if (single.Comparer.Equals(single.Key, key)) {
                    value = single.Value;
                    return true;
                }
                value = default(TValue);
                return false;
            }

            var dict = _data as AnalysisDictionary<TKey, TValue>;
            if (dict != null) {
                return dict.TryGetValue(key, out value);
            }

            value = default(TValue);
            return false;
        }

        public TValue this[TKey key] {
            get {
                TValue res;
                if (TryGetValue(key, out res)) {
                    return res;
                }

                throw new KeyNotFoundException();
            }
            set {
                IEqualityComparer<TKey> comparer = null;
                if (_data == null || (comparer = _data as IEqualityComparer<TKey>) != null) {
                    _data = new SingleDependency(key, value, comparer ?? EqualityComparer<TKey>.Default);
                    return;
                }

                var single = _data as SingleDependency;
                if (single != null) {
                    if (single.Comparer.Equals(single.Key, key)) {
                        single.Value = value;
                        return;
                    }

                    var data = new AnalysisDictionary<TKey, TValue>(single.Comparer);
                    data[single.Key] = single.Value;
                    data[key] = value;
                    _data = data;
                    return;
                }

                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict == null) {
                    _data = dict = new AnalysisDictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
                }
                dict[key] = value;
            }
        }

        internal void Remove(TKey fromModule) {
            var single = _data as SingleDependency;
            if (single != null) {
                if (single.Comparer.Equals(single.Key, fromModule)) {
                    _data = single.Comparer;
                }
                return;
            }

            var dict = _data as AnalysisDictionary<TKey, TValue>;
            if (dict != null) {
                dict.Remove(fromModule);
            }
        }

        public bool TryGetSingleValue(out TValue value) {
            var single = _data as SingleDependency;
            if (single != null) {
                value = single.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        public bool TryGetSingleDependency(out SingleDependency dependency) {
            dependency = _data as SingleDependency;
            return dependency != null;
        }


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IEnumerable<TValue> DictValues {
            get {
                Debug.Assert(_data is AnalysisDictionary<TKey, TValue>);

                return ((AnalysisDictionary<TKey, TValue>)_data).EnumerateValues;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IEnumerable<TValue> Values {
            get {
                SingleDependency single;
                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict != null) {
                    return dict.Values;
                } else if ((single = _data as SingleDependency) != null) {
                    return new[] { single.Value };
                }
                return new TValue[0];
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public IEnumerable<TKey> Keys {
            get {
                var single = _data as SingleDependency;
                if (single != null) {
                    yield return single.Key;
                }

                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict != null) {
                    foreach (var value in dict.Keys) {
                        yield return value;
                    }
                }
            }
        }

        public int Count {
            get {
                var single = _data as SingleDependency;
                if (single != null) {
                    return 1;
                }

                var dict = _data as AnalysisDictionary<TKey, TValue>;
                if (dict != null) {
                    return dict.Count;
                }

                return 0;
            }
        }

        #region IEnumerable<KeyValuePair<TKey,TValue>> Members

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            var single = _data as SingleDependency;
            if (single != null) {
                yield return new KeyValuePair<TKey, TValue>(single.Key, single.Value);
            }

            var dict = _data as AnalysisDictionary<TKey, TValue>;
            if (dict != null) {
                foreach (var keyValue in dict) {
                    yield return keyValue;
                }
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #endregion

    }

}
