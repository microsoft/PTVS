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
        private object _data; // AnalysisDictionary<TKey, TValue>, SingleEntry<TKey, TValue> or IEqualityComparer<TKey>

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

        sealed class SingleDependency {
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


        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public ICollection<TValue> DictValues {
            get {
                Debug.Assert(_data is AnalysisDictionary<TKey, TValue>);

                return ((AnalysisDictionary<TKey, TValue>)_data).Values;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        internal AnalysisDictionary<TKey, TValue> InternalDict {
            get {
                return _data as AnalysisDictionary<TKey, TValue>;
            }
            set {
                if (value.Count == 1) {
                    using (var e = value.GetEnumerator()) {
                        e.MoveNext();
                        _data = new SingleDependency(e.Current.Key, e.Current.Value, value.Comparer);
                    }
                } else {
                    _data = value;
                }
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

        public void Clear() {
            _data = Comparer;
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
