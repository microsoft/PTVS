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
using System.Threading;

namespace Microsoft.PythonTools.Analysis.AnalysisSetDetails {
    /// <summary>
    /// HashSet used in analysis engine.
    /// 
    /// This set is thread safe for a single writer and multiple readers.  
    /// 
    /// Reads and writes on the dictionary are all lock free, but have memory
    /// barriers in place.  The key is used to indicate the current state of a bucket.
    /// When adding a bucket the key is updated last after all other values
    /// have been added.  When removing a bucket the key is cleared first.  Memory
    /// barriers are used to ensure that the writes to the key bucket are not
    /// re-ordered.
    /// 
    /// When resizing the set the buckets are replaced atomically so that the reader
    /// sees the new buckets or the old buckets.  When reading the reader first reads
    /// the buckets and then calls a static helper function to do the read from the bucket
    /// array to ensure that readers are not seeing multiple bucket arrays.
    /// </summary>
    [DebuggerDisplay(AnalysisSetDetails.DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(AnalysisSetDetails.DebugViewProxy))]
    [Serializable]
    internal sealed class AnalysisHashSet : IAnalysisSet {

        private Bucket[] _buckets;
        private int _count;
        private readonly IEqualityComparer<AnalysisValue> _comparer;

        private const int InitialBucketSize = 3;
        private const int ResizeMultiplier = 2;
        private const double Load = .9;

        // Marker object used to indicate we have a removed value
        private class RemovedAnalysisValue : AnalysisValue { public RemovedAnalysisValue() { } }
        private static readonly AnalysisValue _removed = new RemovedAnalysisValue();

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public AnalysisHashSet() {
            _comparer = ObjectComparer.Instance;
        }

        /// <summary>
        /// Creates a new dictionary storage with no buckets
        /// </summary>
        public AnalysisHashSet(int count) {
            _buckets = new Bucket[AnalysisDictionary<object, object>.GetPrime((int)(count / Load + 2))];
            _comparer = ObjectComparer.Instance;
        }

        public AnalysisHashSet(IEqualityComparer<AnalysisValue> comparer) {
            _comparer = comparer;
        }

        public AnalysisHashSet(int count, IEqualityComparer<AnalysisValue> comparer) {
            _buckets = new Bucket[AnalysisDictionary<object, object>.GetPrime((int)(count / Load + 2))];
            _comparer = comparer;
        }

        public AnalysisHashSet(IEnumerable<AnalysisValue> enumerable, IEqualityComparer<AnalysisValue> comparer)
            : this(comparer) {
            Union(enumerable);
        }

        public AnalysisHashSet(IEnumerable<AnalysisValue> enumerable) : this() {
            Union(enumerable);
        }

        public IEqualityComparer<AnalysisValue> Comparer {
            get {
                return _comparer;
            }
        }

        public IAnalysisSet Add(AnalysisValue item, bool canMutate = true) {
            if (!canMutate) {
                if (Contains(item)) {
                    return this;
                }
                return Clone().Add(item, true);
            }
            AddOne(item);
            return this;
        }

        public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = true) {
            if (!canMutate) {
                var buckets = _buckets;
                var i = Contains(buckets, item);
                if (i >= 0) {
                    var existing = buckets[i].Key;
                    if (object.ReferenceEquals(existing, item)) {
                        wasChanged = false;
                        return this;
                    }
                    var uc = _comparer as UnionComparer;
                    if (uc == null) {
                        wasChanged = false;
                        return this;
                    }
                    item = uc.MergeTypes(existing, item, out wasChanged);
                    if (!wasChanged) {
                        return this;
                    }
                    return ((AnalysisHashSet)Clone()).Remove(existing).Add(item, true);
                }
                wasChanged = true;
                return Clone().Add(item, true);
            }
            wasChanged = AddOne(item);
            return this;
        }

        public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = true) {
            bool wasChanged;
            return Union(items, out wasChanged, canMutate);
        }

        public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = true) {
            if (!canMutate) {
                // Return ourselves if we aren't adding any new items
                using (var e = items.GetEnumerator()) {
                    while (e.MoveNext()) {
                        if (!Contains(e.Current)) {
                            // Have to add an item, so clone and finish
                            // enumerating
                            var res = (AnalysisHashSet)Clone();
                            res.AddOne(e.Current);
                            res.AddFromEnumerator(e);
                            wasChanged = true;
                            return res;
                        }
                    }
                }
                wasChanged = false;
                return this;
            }

            // Faster path if we are allowed to mutate ourselves
            AnalysisHashSet otherHc = items as AnalysisHashSet;
            if (otherHc != null) {
                bool anyChanged = false;

                if (otherHc._count != 0) {
                    // do a fast copy from the other hash set...
                    var buckets = otherHc._buckets;
                    for (int i = 0; i < buckets.Length; i++) {
                        var key = buckets[i].Key;
                        if (key != null && key != AnalysisDictionaryRemovedValue.Instance) {
                            anyChanged |= AddOne(key);
                        }
                    }
                }
                wasChanged = anyChanged;
                return this;
            }

            // some other set, copy it the slow way...
            using (var e = items.GetEnumerator()) {
                wasChanged = AddFromEnumerator(e);
            }
            return this;
        }

        private bool AddFromEnumerator(IEnumerator<AnalysisValue> items) {
            bool wasChanged = false;
            while (items.MoveNext()) {
                wasChanged |= AddOne(items.Current);
            }
            return wasChanged;
        }

        public AnalysisHashSet Remove(AnalysisValue key) {
            bool dummy;
            return Remove(key, out dummy);
        }

        public AnalysisHashSet Remove(AnalysisValue key, out bool wasChanged) {
            var buckets = _buckets;
            int i = Contains(buckets, key);
            if (i < 0) {
                wasChanged = false;
                return this;
            }

            _buckets[i].Key = _removed;
            _count--;
            wasChanged = true;
            return this;
        }

        public IAnalysisSet Clone() {
            var buckets = _buckets;
            var count = _count;
            if (buckets != null) {
                var newBuckets = new Bucket[buckets.Length];
                Array.Copy(buckets, newBuckets, buckets.Length);
                buckets = newBuckets;
            }
            var res = new AnalysisHashSet(Comparer);
            res._buckets = buckets;
            res._count = count;
            return res;
        }

        public bool SetEquals(IAnalysisSet other) {
            var otherHc = new HashSet<AnalysisValue>(other, _comparer);
            foreach (var key in this) {
                if (!otherHc.Remove(key)) {
                    return false;
                }
            }
            if (otherHc.Any()) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Adds a new item to the dictionary, replacing an existing one if it already exists.
        /// </summary>
        private bool AddOne(AnalysisValue key) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }
            if (key.IsAlive) {
                if (_buckets == null) {
                    Initialize();
                }

                if (Add(_buckets, key)) {
                    _count++;

                    CheckGrow();
                    return true;
                }
            }
            return false;
        }

        private void CheckGrow() {
            if (_count >= (_buckets.Length * Load)) {
                // grow the hash table
                EnsureSize((int)(_buckets.Length / Load) * ResizeMultiplier);
            }
        }

        private void EnsureSize(int newSize) {
            // see if we can reclaim collected buckets before growing...
            var oldBuckets = _buckets;
            if (_buckets == null) {
                _buckets = new Bucket[newSize];
                return;
            }

            if (oldBuckets != null) {
                for (int i = 0; i < oldBuckets.Length; i++) {
                    var curBucket = oldBuckets[i];
                    if (curBucket.Key != null && !curBucket.Key.IsAlive) {
                        oldBuckets[i].Key = _removed;
                        newSize--;
                        _count--;
                    }
                }
            }

            if (newSize > oldBuckets.Length) {
                newSize = AnalysisDictionary<object, object>.GetPrime(newSize);

                var newBuckets = new Bucket[newSize];

                for (int i = 0; i < oldBuckets.Length; i++) {
                    var curBucket = oldBuckets[i];
                    if (curBucket.Key != null &&
                        curBucket.Key != _removed) {
                        AddOne(newBuckets, curBucket.Key, curBucket.HashCode);
                    }
                }

                _buckets = newBuckets;
            }
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
        private bool Add(Bucket[] buckets, AnalysisValue key) {
            int hc = _comparer.GetHashCode(key) & Int32.MaxValue;

            return AddOne(buckets, key, hc);
        }

        /// <summary>
        /// Add helper which adds the given key/value (where the key is not null) with
        /// a pre-computed hash code.
        /// </summary>
        private bool AddOne(Bucket[] buckets, AnalysisValue/*!*/ key, int hc) {
            Debug.Assert(key != null);

            Debug.Assert(_count < buckets.Length);
            int index = hc % buckets.Length;
            int startIndex = index;
            int addIndex = -1;

            for (; ;) {
                Bucket cur = buckets[index];
                var existingKey = cur.Key;
                if (existingKey == null || existingKey == _removed || !existingKey.IsAlive) {
                    if (addIndex == -1) {
                        addIndex = index;
                    }
                    if (cur.Key == null) {
                        break;
                    }
                } else if (Object.ReferenceEquals(key, existingKey)) {
                    return false;
                } else if (cur.HashCode == hc && _comparer.Equals(key, existingKey)) {
                    var uc = _comparer as UnionComparer;
                    if (uc == null) {
                        return false;
                    }
                    bool changed;
                    var newKey = uc.MergeTypes(existingKey, key, out changed);
                    if (!changed) {
                        return false;
                    }
                    // merging values has changed the one we should store, so
                    // replace it.
                    var newHc = _comparer.GetHashCode(newKey) & Int32.MaxValue;
                    if (newHc != buckets[index].HashCode) {
                        // The hash code should not change, but if it does, we
                        // need to keep things consistent
                        Debug.Fail("Hash code changed when merging AnalysisValues");
                        Thread.MemoryBarrier();
                        buckets[index].Key = _removed;
                        AddOne(buckets, newKey, newHc);
                        return true;
                    }
                    Thread.MemoryBarrier();
                    buckets[index].Key = newKey;
                    return true;
                }

                index = ProbeNext(buckets, index);

                if (index == startIndex) {
                    break;
                }
            }

            if (buckets[addIndex].Key != null &&
                buckets[addIndex].Key != _removed &&
                !buckets[addIndex].Key.IsAlive) {
                _count--;
            }
            buckets[addIndex].HashCode = hc;
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

        /// <summary>
        /// Checks to see if the key exists in the dictionary.
        /// </summary>
        public bool Contains(AnalysisValue key) {
            if (key == null) {
                throw new ArgumentNullException("key");
            }
            return Contains(_buckets, key) >= 0;
        }

        /// <summary>
        /// Static helper to try and get the value from the dictionary.
        /// 
        /// Used so the value lookup can run against a buckets while a writer
        /// replaces the buckets.
        /// </summary>
        private int Contains(Bucket[] buckets, AnalysisValue/*!*/ key) {
            Debug.Assert(key != null);

            if (_count > 0 && buckets != null) {
                int hc = _comparer.GetHashCode(key) & Int32.MaxValue;

                return Contains(buckets, key, hc);
            }

            return -1;
        }

        private int Contains(Bucket[] buckets, AnalysisValue key, int hc) {
            int index = hc % buckets.Length;
            int startIndex = index;
            do {
                var existingKey = buckets[index].Key;
                if (existingKey == null) {
                    break;
                } else {
                    if (Object.ReferenceEquals(key, existingKey) ||
                        (existingKey != _removed &&
                        buckets[index].HashCode == hc &&
                        _comparer.Equals(key, (AnalysisValue)existingKey))) {

                        return index;
                    }
                }

                index = ProbeNext(buckets, index);
            } while (startIndex != index);

            return -1;
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

        public IEnumerator<AnalysisValue> GetEnumerator() {
            var buckets = _buckets;
            if (buckets != null) {
                for (int i = 0; i < buckets.Length; i++) {
                    var key = buckets[i].Key;
                    if (key != null && key != _removed && key.IsAlive) {
                        yield return key;
                    }
                }
            }
        }

        /// <summary>
        /// Used to store a single hashed key/value.
        /// 
        /// Bucket is not serializable because it stores the computed hash
        /// code which could change between serialization and deserialization.
        /// </summary>
        struct Bucket {
            public AnalysisValue Key;          // the key to be hashed
            public int HashCode;        // the hash code of the contained key.
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}

