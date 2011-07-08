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
using System.Text;
using System.Collections;

namespace Microsoft.PythonTools.Analysis.Interpreter {
    /// <summary>
    /// A simple dictionary like object which has efficient storage when there's only a single item in the dictionary.
    /// </summary>
    struct SingleDict<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> {
        private object _data; // Dictionary<TKey, TValue> or SingleEntry<TKey, TValue>

        sealed class SingleDependency {
            public readonly TKey Key;
            public TValue Value;

            public SingleDependency(TKey key, TValue value) {
                Key = key;
                Value = value;
            }
        }

        internal bool TryGetValue(TKey fromModule, out TValue deps) {
            SingleDependency single = _data as SingleDependency;
            if (single != null) {
                if (single.Key.Equals(fromModule)) {
                    deps = single.Value;
                    return true;
                }
                deps = default(TValue);
                return false;
            }

            Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
            if (_data != null) {
                return dict.TryGetValue(fromModule, out deps);
            }

            deps = default(TValue);
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
                if (_data == null) {
                    _data = new SingleDependency(key, value);
                    return;
                }

                SingleDependency single = _data as SingleDependency;
                if (single != null) {
                    if (single.Key.Equals(key)) {
                        single.Value = value;
                        return;
                    }

                    var data = new Dictionary<TKey, TValue>();
                    data[single.Key] = single.Value;
                    _data = data;
                }

                Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
                if (dict == null) {
                    _data = dict = new Dictionary<TKey, TValue>();
                }
                dict[key] = value;
            }
        }

        internal void Remove(TKey fromModule) {
            SingleDependency single = _data as SingleDependency;
            if (single != null) {
                if (single.Key.Equals(fromModule)) {
                    _data = null;
                }
                return;
            }

            Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
            if (_data != null) {
                dict.Remove(fromModule);
            }
        }

        public IEnumerable<TValue> Values {
            get {
                SingleDependency single = _data as SingleDependency;
                if (single != null) {
                    yield return single.Value;
                }

                Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
                if (dict != null) {
                    foreach (var value in dict.Values) {
                        yield return value;
                    }
                }
            }
        }

        public IEnumerable<TKey> Keys {
            get {
                SingleDependency single = _data as SingleDependency;
                if (single != null) {
                    yield return single.Key;
                }

                Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
                if (dict != null) {
                    foreach (var value in dict.Keys) {
                        yield return value;
                    }
                }
            }
        }

        public int Count {
            get {
                SingleDependency single = _data as SingleDependency;
                if (single != null) {
                    return 1;
                }

                Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
                if (dict != null) {
                    return dict.Count;
                }

                return 0;
            }
        }

        #region IEnumerable<KeyValuePair<TKey,TValue>> Members

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
            SingleDependency single = _data as SingleDependency;
            if (single != null) {
                yield return new KeyValuePair<TKey, TValue>(single.Key, single.Value);
            }

            Dictionary<TKey, TValue> dict = _data as Dictionary<TKey, TValue>;
            if (dict != null) {
                foreach (var keyValue in dict) {
                    yield return keyValue;
                }
            }
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion
    }

}
