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
    [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
    [Serializable]
    internal sealed class AnalysisHashSet : IAnalysisSet {

        [NonSerialized]
        private BucketSet _buckets;
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
            Buckets = new BucketSet(AnalysisDictionary<object, object>.GetPrime((int)(count / Load + 2)));
            _comparer = ObjectComparer.Instance;
        }

        public AnalysisHashSet(IEqualityComparer<AnalysisValue> comparer) {
            _comparer = comparer;
        }

        public AnalysisHashSet(int count, IEqualityComparer<AnalysisValue> comparer) {
            Buckets = new BucketSet(AnalysisDictionary<object, object>.GetPrime((int)(count / Load + 2)));
            _comparer = comparer;
        }

        public AnalysisHashSet(IEnumerable<AnalysisValue> enumerable, IEqualityComparer<AnalysisValue> comparer)
            : this(comparer) {
            using (var e = enumerable.GetEnumerator()) {
                AddFromEnumerator(e);
            }
        }

        public AnalysisHashSet(IEnumerable<AnalysisValue> enumerable) : this() {
            using (var e = enumerable.GetEnumerator()) {
                AddFromEnumerator(e);
            }
        }

        private BucketSet Buckets {
            get {
                lock (this) {
                    return _buckets;
                }
            }
            set {
                lock (this) {
                    _buckets = value;
                }
            }
        }

        public IEqualityComparer<AnalysisValue> Comparer {
            get {
                return _comparer;
            }
        }

        public override string ToString() {
            return DebugViewProxy.ToString(this);
        }

        public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
            if (!canMutate) {
                if (Contains(item)) {
                    return this;
                }
                return Clone().Add(item, true);
            }
            AddOne(item);
            return this;
        }

        public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
#if FULL_VALIDATION
            var r = AddWorker(item, out wasChanged, canMutate);
            Validation.Assert(r.Count == r.GetTrueCount(), $"Set count is incorrect. Expected {r.GetTrueCount()}. Actual {r.Count}");
            return r;
        }

        private IAnalysisSet AddWorker(AnalysisValue item, out bool wasChanged, bool canMutate) {
#endif
            if (!canMutate) {
                var buckets = Buckets;
                var i = Contains(buckets.Buckets, item);
                if (i >= 0) {
                    var existing = buckets.Buckets[i].Key;
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
                    return ((AnalysisHashSet)Clone()).Remove(existing).Add(item, out wasChanged, true);
                }
                wasChanged = true;
                return Clone().Add(item, true);
            }
            wasChanged = AddOne(item);
            return this;
        }

        public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
            bool wasChanged;
            return Union(items, out wasChanged, canMutate);
        }


        public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
#if FULL_VALIDATION
            var r = UnionWorker(items, out wasChanged, canMutate);
            Validation.Assert(r.Count == r.GetTrueCount(), $"Set count is incorrect. Expected {r.GetTrueCount()}. Actual {r.Count}");
            return r;
        }

        private IAnalysisSet UnionWorker(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate) {
#endif
            if (!canMutate) {
                // Return ourselves if we aren't adding any new items
                using (var e = items.GetEnumerator()) {
                    while (e.MoveNext()) {
                        if (!Contains(e.Current)) {
                            // Have to add an item, so clone and finish
                            // enumerating
                            var res = (AnalysisHashSet)Clone();
                            res.AddOne(e.Current);
                            var b = res.Buckets;
                            AddFromEnumerator(ref b, e, res.Comparer);
                            res.Buckets = b;
                            wasChanged = true;
                            return res;
                        }
                    }
                }
                wasChanged = false;
                return this;
            }

            // Faster path if we are allowed to mutate ourselves
            var buckets = Buckets;
            AnalysisHashSet otherHc = items as AnalysisHashSet;
            if (otherHc != null && Comparer == otherHc.Comparer) {
                var otherBuckets = otherHc.Buckets;
                bool anyChanged = false;

                if (otherBuckets.Capacity != 0) {
                    // do a fast copy from the other hash set...
                    for (int i = 0; i < otherBuckets.Capacity; i++) {
                        var key = otherBuckets.Buckets[i].Key;
                        if (key != null && key != AnalysisDictionaryRemovedValue.Instance) {
                            anyChanged |= AddOne(ref buckets, key, _comparer);
                        }
                    }
                    Buckets = buckets;
                }
                wasChanged = anyChanged;
                return this;
            }

            // some other set, copy it the slow way...
            using (var e = items.GetEnumerator()) {
                wasChanged = AddFromEnumerator(ref buckets, e, Comparer);
            }
            Buckets = buckets;
            return this;
        }

        internal AnalysisHashSet AddFromEnumerator(IEnumerator<AnalysisValue> items) {
            var buckets = Buckets;
            AddFromEnumerator(ref buckets, items, Comparer);
            Buckets = buckets;
            return this;
        }

        private static bool AddFromEnumerator(ref BucketSet buckets, IEnumerator<AnalysisValue> items, IEqualityComparer<AnalysisValue> comparer) {
            bool wasChanged = false;
            while (items.MoveNext()) {
                wasChanged |= AddOne(ref buckets, items.Current, comparer);
            }
            return wasChanged;
        }

        public AnalysisHashSet Remove(AnalysisValue key) => Remove(key, out _);

        public AnalysisHashSet Remove(AnalysisValue key, out bool wasChanged) {
            var buckets = Buckets;
            int i = Contains(buckets.Buckets, key);
            if (i < 0) {
                wasChanged = false;
                return this;
            }

            buckets.Buckets[i].Key = _removed;
            buckets.Count -= 1;

            EnsureSize(ref buckets, buckets.Count, Comparer);

            Buckets = buckets;
            wasChanged = true;
            return this;
        }

        public IAnalysisSet Clone() {
            var res = new AnalysisHashSet(Comparer);
            using (var e = GetEnumerator()) {
                res.AddFromEnumerator(e);
            }
            return res;
        }

        public bool SetEquals(IAnalysisSet other) {
            if (ReferenceEquals(this, other)) {
                return true;
            }

            if (Comparer == other.Comparer) {
                // Quick check for any unmatched hashcodes.
                // This can conclusively prove the sets are not equal, but cannot
                // prove equality.
                var lBuckets = Buckets.Buckets;
                var rBuckets = (other as AnalysisHashSet)?.Buckets.Buckets;
                if (lBuckets != null && rBuckets != null) {
                    var rKeys = new HashSet<int>(rBuckets.Select(b => b.HashCode));
                    rKeys.ExceptWith(lBuckets.Select(b => b.HashCode));
                    if (rKeys.Any()) {
                        return false;
                    }
                }
            }

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
            if (!key.IsAlive) {
                return false;
            }

            var buckets = Buckets;
            var res = AddOne(ref buckets, key, _comparer);
            Buckets = buckets;
            return res;
        }

        private static bool AddOne(ref BucketSet buckets, AnalysisValue key, IEqualityComparer<AnalysisValue> comparer) {
            if (buckets.Buckets == null) {
                buckets = new BucketSet(InitialBucketSize);
            }

            int hc = comparer.GetHashCode(key) & Int32.MaxValue;
            if (AddOne(ref buckets, key, hc, comparer)) {
                CheckGrow(ref buckets, comparer);
                return true;
            }
            return false;
        }

        private static void CheckGrow(ref BucketSet buckets, IEqualityComparer<AnalysisValue> comparer) {
            if (buckets.Capacity == 0) {
                return;
            }
            if (buckets.Count >= (buckets.Capacity * Load)) {
                // grow the hash table
                EnsureSize(ref buckets, (int)(buckets.Capacity / Load) * ResizeMultiplier, comparer);
            }
        }

        private static void EnsureSize(ref BucketSet buckets, int newSize, IEqualityComparer<AnalysisValue> comparer) {
            // see if we can reclaim collected buckets before growing...
            if (buckets.Capacity == 0) {
                buckets = new BucketSet(newSize);
                return;
            }

            for (int i = 0; i < buckets.Capacity; i++) {
                var key = buckets.Buckets[i].Key;
                if (key != null && !key.IsAlive) {
                    buckets.Buckets[i].Key = _removed;
                    newSize--;
                    buckets.Count--;
                }
            }

            if (newSize > buckets.Buckets.Length || newSize < buckets.Buckets.Length / 4) {
                newSize = AnalysisDictionary<object, object>.GetPrime(newSize);

                var newBuckets = new BucketSet(newSize);

                for (int i = 0; i < buckets.Buckets.Length; i++) {
                    var curBucket = buckets.Buckets[i];
                    if (curBucket.Key != null && curBucket.Key != _removed && curBucket.Key.IsAlive) {
                        AddOne(ref newBuckets, curBucket.Key, curBucket.HashCode, comparer);
                    }
                }

                buckets = newBuckets;
            }
        }

        /// <summary>
        /// Initializes the buckets to their initial capacity, the caller
        /// must check if the buckets are empty first.
        /// </summary>
        private static BucketSet Initialize() {
            return new BucketSet(InitialBucketSize);
        }

        /// <summary>
        /// Add helper which adds the given key/value (where the key is not null) with
        /// a pre-computed hash code.
        /// </summary>
        private static bool AddOne(ref BucketSet buckets, AnalysisValue/*!*/ key, int hc, IEqualityComparer<AnalysisValue> comparer) {
            Debug.Assert(key != null);

            int index = hc % buckets.Capacity;
            int startIndex = index;
            int addIndex = -1;

            for (; ;) {
                Bucket cur = buckets.Buckets[index];
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
                } else if (cur.HashCode == hc && comparer.Equals(key, existingKey)) {
                    var uc = comparer as UnionComparer;
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
                    var newHc = comparer.GetHashCode(newKey) & Int32.MaxValue;
                    if (newHc != buckets.Buckets[index].HashCode) {
                        // The hash code should not change, but if it does, we
                        // need to keep things consistent
                        Debug.Fail("Hash code changed when merging AnalysisValues");
                    }
                    Thread.MemoryBarrier();
                    buckets.Buckets[index].Key = _removed;
                    buckets.Count -= 1;
                    return AddOne(ref buckets, newKey, newHc, comparer);
                }

                index = ProbeNext(buckets.Buckets, index);

                if (index == startIndex) {
                    break;
                }
            }

            if (buckets.Buckets[addIndex].Key == null || buckets.Buckets[addIndex].Key == _removed) {
                // Removal has been counted already
                buckets.Count += 1;
            } else if (!buckets.Buckets[addIndex].Key.IsAlive) {
                // Remove/add means no change te count
            } else {
                buckets.Count += 1;
            }
            buckets.Buckets[addIndex].HashCode = hc;
            Thread.MemoryBarrier();
            // we write the key last so that we can check for null to
            // determine if a bucket is available.
            buckets.Buckets[addIndex].Key = key;

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
            return Contains(Buckets.Buckets, key) >= 0;
        }

        /// <summary>
        /// Static helper to try and get the value from the dictionary.
        /// 
        /// Used so the value lookup can run against a buckets while a writer
        /// replaces the buckets.
        /// </summary>
        private int Contains(Bucket[] buckets, AnalysisValue/*!*/ key) {
            Debug.Assert(key != null);

            if (buckets != null) {
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
        public int Count => Buckets.Count;

#if FULL_VALIDATION || DEBUG
        public int GetTrueCount() => Buckets.Buckets?.Count(b => b.Key != null && b.Key != _removed) ?? 0;
#endif


        public void Clear() {
            Buckets = default(BucketSet);
        }

        public IEnumerator<AnalysisValue> GetEnumerator() {
            var buckets = Buckets;
            if (buckets.Capacity > 0) {
                for (int i = 0; i < buckets.Capacity; i++) {
                    var key = buckets.Buckets[i].Key;
                    if (key != null && key != _removed && key.IsAlive) {
                        yield return key;
                    }
                }
            }
        }

        public override bool Equals(object obj) => obj is IAnalysisSet set && SetEquals(set);
        public override int GetHashCode() => ((IEqualityComparer<IAnalysisSet>)Comparer).GetHashCode(this);

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

        struct BucketSet {
            public BucketSet(int capacity, int count = 0) {
                Buckets = capacity > 0 ? new Bucket[capacity] : Array.Empty<Bucket>();
                Count = count;
                if (count > capacity) {
                    throw new ArgumentOutOfRangeException(nameof(count));
                }
            }

            public readonly Bucket[] Buckets;
            public int Count;
            public int Capacity => Buckets?.Length ?? 0;
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}

