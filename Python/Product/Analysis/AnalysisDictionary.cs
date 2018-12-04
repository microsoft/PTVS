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
using System.Threading;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Dictionary used in analysis engine.
    /// 
    /// This dictionary storage is thread safe for a single writer and multiple
    /// readers.  
    /// 
    /// 
    /// Reads and writes on the dictionary are all lock free, but have memory
    /// barriers in place.  The key is used to indicate the current state of a bucket.
    /// When adding a bucket the key is updated last after all other values
    /// have been added.  When removing a bucket the key is cleared first.  Memory
    /// barriers are used to ensure that the writes to the key bucket are not
    /// re-ordered.
    /// 
    /// When resizing the dictionary the buckets are replaced atomically so that the reader
    /// sees the new buckets or the old buckets.  When reading the reader first reads
    /// the buckets and then calls a static helper function to do the read from the bucket
    /// array to ensure that readers are not seeing multiple bucket arrays.
    /// </summary>
    /// <remarks>
    /// This class differs from ConcurrentDictionary in that it's not thread safe
    /// for multiple writers.  That works fine in our analysis engine where we have
    /// a single analysis thread running and we have the UI thread which is reading
    /// the results of the analysis potentially while it's running.  This class also
    /// typically has better working set and is much faster because it runs lock
    /// free.
    /// </remarks>
    [Serializable]
    internal sealed class AnalysisDictionary<TKey, TValue> : IDictionary<TKey, TValue>
        where TKey : class
        where TValue : class {

        [NonSerialized]
        private Bucket[] _buckets;
        private int _count;
        private IEqualityComparer<TKey> _comparer;

        private const int InitialBucketSize = 3;
        private const int ResizeMultiplier = 2;
        private const double Load = .9;

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public AnalysisDictionary() {
            _comparer = EqualityComparer<TKey>.Default;
        }

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public AnalysisDictionary(int count) {
            _buckets = new Bucket[GetPrime((int)(count / Load + 2))];
            _comparer = EqualityComparer<TKey>.Default;
        }

        public AnalysisDictionary(IEqualityComparer<TKey> comparer) {
            _comparer = comparer;
        }

        public AnalysisDictionary(int count, IEqualityComparer<TKey> comparer) {
            _buckets = new Bucket[GetPrime((int)(count / Load + 2))];
            _comparer = comparer;
        }

        public IEqualityComparer<TKey> Comparer {
            get {
                return _comparer;
            }
        }

        /// <summary>
        /// Adds a new item to the dictionary, replacing an existing one if it already exists.
        /// </summary>
        public void Add(TKey key, TValue value) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }

            if (_buckets == null) {
                Initialize();
            }

            AddOne(key, value, true);
        }

        private void AddOne(TKey key, TValue value, bool throwIfExists = false) {
            if (Add(_buckets, key, value, throwIfExists)) {
                _count++;

                if (_count >= (_buckets.Length * Load)) {
                    // grow the hash table
                    EnsureSize((int)(_buckets.Length / Load) * ResizeMultiplier);
                }
            }
        }

        private static readonly int[] _primes = {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369};


        private static bool IsPrime(int candidate) {
            if ((candidate & 1) != 0) {
                int limit = (int)Math.Sqrt(candidate);
                for (int divisor = 3; divisor <= limit; divisor += 2) {
                    if ((candidate % divisor) == 0)
                        return false;
                }
                return true;
            }
            return (candidate == 2);
        }

        private const Int32 HashPrime = 101;
        internal static int GetPrime(int min) {
            for (int i = 0; i < _primes.Length; i++) {
                int prime = _primes[i];
                if (prime >= min) return prime;
            }

            //outside of our predefined table. 
            //compute the hard way. 
            for (int i = (min | 1); i < Int32.MaxValue; i += 2) {
                if (IsPrime(i) && ((i - 1) % HashPrime != 0))
                    return i;
            }
            return min;
        }

        private void EnsureSize(int newSize) {
            newSize = GetPrime(newSize);

            var oldBuckets = _buckets;
            var newBuckets = new Bucket[newSize];

            for (int i = 0; i < oldBuckets.Length; i++) {
                var curBucket = oldBuckets[i];
                if (curBucket.Key != null && curBucket.Key != AnalysisDictionaryRemovedValue.Instance) {
                    AddWorker(newBuckets, (TKey)curBucket.Key, curBucket.Value, curBucket.HashCode);
                }
            }

            _buckets = newBuckets;
        }

        /// <summary>
        /// Initializes the buckets to their initial capacity, the caller
        /// must check if the buckets are empty first.
        /// </summary>
        private void Initialize() {
            _buckets = new Bucket[InitialBucketSize];
        }

        /// <summary>
        /// Add helper that works over a single set of buckets.  Used for
        /// both the normal add case as well as the resize case.
        /// </summary>
        private bool Add(Bucket[] buckets, TKey key, TValue value, bool throwIfExists = false) {
            int hc = _comparer.GetHashCode(key) & Int32.MaxValue;

            return AddWorker(buckets, key, value, hc, throwIfExists);
        }

        /// <summary>
        /// Add helper which adds the given key/value (where the key is not null) with
        /// a pre-computed hash code.
        /// </summary>
        private bool AddWorker(Bucket[] buckets, TKey/*!*/ key, TValue value, int hc, bool throwIfExists = false) {
            Debug.Assert(key != null);

            Debug.Assert(_count < buckets.Length);
            int index = hc % buckets.Length;
            int startIndex = index;
            int addIndex = -1;

            for (; ;) {
                Bucket cur = buckets[index];
                var existingKey = cur.Key;
                if (existingKey == null || existingKey == AnalysisDictionaryRemovedValue.Instance) {
                    if (addIndex == -1) {
                        addIndex = index;
                    }
                    if (cur.Key == null) {
                        break;
                    }
                } else if (Object.ReferenceEquals(key, existingKey) ||
                    (cur.HashCode == hc && _comparer.Equals(key, (TKey)existingKey))) {
                    if (throwIfExists) {
                        throw new ArgumentException();
                    }
                    buckets[index].Value = value;
                    return false;
                }

                index = ProbeNext(buckets, index);

                if (index == startIndex) {
                    break;
                }
            }

            Debug.Assert(
                addIndex >= 0,
                "addIndex was not found while scanning buckets. This normally indicates a race condition - " +
                "AnalysisDictionary does not support simultaneous mutations."
            );

            buckets[addIndex].HashCode = hc;
            buckets[addIndex].Value = value;
            Thread.MemoryBarrier();
            // we write the key last so that we can check for null to
            // determine if a bucket is available.
            buckets[addIndex].Key = key;

            return true;
        }

        private static int ProbeNext(Bucket[] buckets, int index) {
            // probe to next bucket    
            return (index + ((buckets.Length - 1) / 2)) % buckets.Length;
        }

        public bool Remove(TKey key) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }

            if (_buckets == null || _count == 0) {
                return false;
            }

            int hc = _comparer.GetHashCode(key) & Int32.MaxValue;

            Debug.Assert(key != null);

            int index = hc % _buckets.Length;
            int startIndex = index;
            do {
                Bucket bucket = _buckets[index];
                var existingKey = bucket.Key;
                if (existingKey == null) {
                    break;
                } else if (
                    Object.ReferenceEquals(key, existingKey) ||
                    (existingKey != AnalysisDictionaryRemovedValue.Instance &&
                    bucket.HashCode == hc &&
                    _comparer.Equals(key, (TKey)existingKey))) {
                    // clear the key first so readers won't see it.
                    _buckets[index].Key = AnalysisDictionaryRemovedValue.Instance;
                    Thread.MemoryBarrier();
                    _buckets[index].Value = null;
                    _count--;

                    return true;
                }

                index = ProbeNext(_buckets, index);
            } while (index != startIndex);

            return false;
        }

        /// <summary>
        /// Checks to see if the key exists in the dictionary.
        /// </summary>
        public bool ContainsKey(TKey key) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }
            TValue dummy;
            return TryGetValue(key, out dummy);
        }

        /// <summary>
        /// Trys to get the value associated with the given key and returns true
        /// if it's found or false if it's not present.
        /// </summary>
        public bool TryGetValue(TKey key, out TValue value) {
            if (key != null) {
                return TryGetValue(_buckets, key, out value);
            }

            throw new ArgumentNullException("key");
        }

        /// <summary>
        /// Static helper to try and get the value from the dictionary.
        /// 
        /// Used so the value lookup can run against a buckets while a writer
        /// replaces the buckets.
        /// </summary>
        private bool TryGetValue(Bucket[] buckets, TKey/*!*/ key, out TValue value) {
            Debug.Assert(key != null);

            if (_count > 0 && buckets != null) {
                int hc = _comparer.GetHashCode(key) & Int32.MaxValue;

                return TryGetValue(buckets, key, hc, out value);
            }

            value = null;
            return false;
        }

        private bool TryGetValue(Bucket[] buckets, TKey key, int hc, out TValue value) {
            int index = hc % buckets.Length;
            int startIndex = index;
            do {
                var existingKey = buckets[index].Key;
                if (existingKey == null) {
                    break;
                } else {
                    while (Object.ReferenceEquals(key, existingKey) ||
                        (existingKey != AnalysisDictionaryRemovedValue.Instance &&
                        buckets[index].HashCode == hc &&
                        _comparer.Equals(key, (TKey)existingKey))) {

                        value = buckets[index].Value;

                        Thread.MemoryBarrier();
                        // make sure the bucket hasn't changed so
                        // we know that value is still valid.
                        if (Object.ReferenceEquals(buckets[index].Key, existingKey)) {
                            return true;
                        }

                        // loop around, we may have an updated key which 
                        // replaced us in which case we'll try again or we
                        // may have been removed and replaced with a different
                        // key in which case the loop will terminate.
                        existingKey = buckets[index].Key;
                    }
                }

                index = ProbeNext(buckets, index);
            } while (startIndex != index);

            value = null;
            return false;
        }

        /// <summary>
        /// Returns the number of key/value pairs currently in the dictionary.
        /// </summary>
        public int Count {
            get {
                return _count;
            }
        }

        public void Clear() {
            if (_buckets != null && _count != 0) {
                _buckets = new Bucket[InitialBucketSize];
                _count = 0;
            }
        }

        //public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() {
        //    var buckets = _buckets;
        //    if (buckets != null) {
        //        for (int i = 0; i < buckets.Length; i++) {
        //            var key = buckets[i].Key;
        //            while (key != null && key != AnalysisDictionaryRemovedValue.Instance) {
        //                var value = buckets[i].Value;

        //                Thread.MemoryBarrier();

        //                // check the key again, if it's changed we need to re-read
        //                // the value as we could be racing with a replacement...
        //                if (Object.ReferenceEquals(key, buckets[i].Key)) {
        //                    yield return new KeyValuePair<TKey, TValue>((TKey)key, value);
        //                    break;
        //                }

        //                key = buckets[i].Key;
        //            }
        //        }
        //    }
        //}


        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return GetEnumerator();
        }

        public AnalysisDictionaryEnumerator GetEnumerator() {
            return new AnalysisDictionaryEnumerator(this);
        }

        internal struct AnalysisDictionaryEnumerator : IEnumerator<KeyValuePair<TKey, TValue>> {
            private readonly Bucket[] _buckets;
            private int _curBucket;
            private KeyValuePair<TKey, TValue> _curValue;

            internal AnalysisDictionaryEnumerator(AnalysisDictionary<TKey, TValue> dict) {
                _buckets = dict._buckets;
                _curValue = default(KeyValuePair<TKey, TValue>);
                _curBucket = 0;
            }

            public KeyValuePair<TKey, TValue> Current {
                get {
                    return _curValue;
                }
            }

            public void Dispose() {
            }

            object IEnumerator.Current {
                get { return _curValue; }
            }

            public bool MoveNext() {
                if (_buckets != null) {
                    for (; _curBucket < _buckets.Length; _curBucket++) {
                        var key = _buckets[_curBucket].Key;
                        while (key != null && key != AnalysisDictionaryRemovedValue.Instance) {
                            var value = _buckets[_curBucket].Value;
                            Thread.MemoryBarrier();

                            // check the key again, if it's changed we need to re-read
                            // the value as we could be racing with a replacement...
                            if (Object.ReferenceEquals(key, _buckets[_curBucket].Key)) {
                                _curBucket++;
                                _curValue = new KeyValuePair<TKey, TValue>((TKey)key, value);
                                return true;
                            }

                            key = _buckets[_curBucket].Key;
                        }
                    }
                }
                return false;
            }

            public void Reset() {
            }
        }

        /// <summary>
        /// Used to store a single hashed key/value.
        /// 
        /// Bucket is not serializable because it stores the computed hash
        /// code which could change between serialization and deserialization.
        /// </summary>
        private struct Bucket {
            public object Key;          // the key to be hashed (not strongly typed because we need our remove marker)
            public TValue Value;        // the value associated with the key
            public int HashCode;        // the hash code of the contained key.
        }

        public ICollection<TKey> Keys {
            get {
                return this.Select(x => x.Key).ToArray();
            }
        }

        public ICollection<TValue> Values {
            get {
                return this.Select(x => x.Value).ToArray();
            }
        }

        /// <summary>
        /// Enumerates the values (this is rather perf sensitive so it's not implemented
        /// as a select of our normal enumerator)
        /// </summary>
        public IEnumerable<TValue> EnumerateValues {
            get {
                var buckets = _buckets;
                if (buckets != null) {
                    for (int i = 0; i < buckets.Length; i++) {
                        var key = buckets[i].Key;
                        while (key != null && key != AnalysisDictionaryRemovedValue.Instance) {
                            var value = buckets[i].Value;

                            Thread.MemoryBarrier();

                            // check the key again, if it's changed we need to re-read
                            // the value as we could be racing with a replacement...
                            if (Object.ReferenceEquals(key, buckets[i].Key)) {
                                yield return value;
                                break;
                            }

                            key = buckets[i].Key;
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Enumerates the keys
        /// </summary>
        public IEnumerable<TKey> KeysNoCopy {
            get {
                return this.Select(x => x.Key);
            }
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
                if (key == null) {
                    throw new ArgumentNullException("key");
                }

                if (_buckets == null) {
                    Initialize();
                }

                AddOne(key, value);
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item) {
            Add(item.Key, item.Value);
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) {
            TValue value;
            return TryGetValue(item.Key, out value) &&
                EqualityComparer<TValue>.Default.Equals(value, item.Value);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) {
            var items = new List<KeyValuePair<TKey, TValue>>();
            foreach (var item in this) {
                items.Add(item);
            }
            items.CopyTo(array, arrayIndex);
        }

        public bool IsReadOnly {
            get { return false; }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) {
            if (Contains(item)) {
                Remove(item.Key);
                return true;
            }
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }

    /// <summary>
    /// Holdes the marker object for values removed from our AnalysisDictionary.
    /// Hoisted to a non-generic class for perf reasons.
    /// </summary>
    internal class AnalysisDictionaryRemovedValue {
        public static readonly object Instance = new object();
    }
}

