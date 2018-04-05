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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis {
    /// <summary>
    /// Represents an unordered collection of <see cref="AnalysisValue" /> objects;
    /// in effect, a set of Python types. There are multiple implementing
    /// classes, including <see cref="AnalysisValue" /> itself, to improve memory
    /// usage for small sets.
    /// 
    /// <see cref="AnalysisSet" /> does not implement this interface, but
    /// provides factory and extension methods.
    /// </summary>
    public interface IAnalysisSet : IEnumerable<AnalysisValue> {
        IAnalysisSet Add(AnalysisValue item, bool canMutate = false);
        IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false);
        IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false);
        IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false);
        IAnalysisSet Clone();

        bool Contains(AnalysisValue item);
        bool SetEquals(IAnalysisSet other);

        int Count { get; }
        IEqualityComparer<AnalysisValue> Comparer { get; }
    }

    /// <summary>
    /// Marker interface to indicate that an analysis set is a read-only copy on write
    /// analysis set.
    /// </summary>
    internal interface IImmutableAnalysisSet : IAnalysisSet {
    }

    /// <summary>
    /// Provides factory and extension methods for objects implementing
    /// <see cref="IAnalysisSet" />.
    /// </summary>
    public static class AnalysisSet {
        /// <summary>
        /// An empty set that does not combine types. This object is immutable
        /// and can be used without cloning.
        /// </summary>
        public static readonly IAnalysisSet Empty = Create();
        /// <summary>
        /// An empty set that combines types with a strength of zero. This
        /// object is immutable and can be used without cloning.
        /// </summary>
        public static readonly IAnalysisSet EmptyUnion = CreateUnion(UnionComparer.Instances[0]);

        #region Constructors

        /// <summary>
        /// Returns an empty set that does not combine types. This is exactly
        /// equivalent to accessing <see cref="Empty" />.
        /// </summary>
        public static IAnalysisSet Create() {
            return AnalysisSetDetails.AnalysisSetEmptyObject.Instance;
        }

        /// <summary>
        /// Returns a set containing only <paramref name="ns" />. This is
        /// exactly equivalent to casting <paramref name="ns" /> to <see
        /// cref="IAnalysisSet" />.
        /// </summary>
        /// <param name="ns">The namespace to contain in the set.</param>
        public static IAnalysisSet Create(AnalysisValue ns) {
            return ns;
        }

        /// <summary>
        /// Returns a set containing all the types in <paramref name="ns" />.
        /// This is the usual way of creating a new set from any sequence.
        /// </summary>
        /// <param name="ns">The namespaces to contain in the set.</param>
        public static IAnalysisSet Create(IEnumerable<AnalysisValue> ns) {
            // TODO: Replace Trim() call with more efficient enumeration.
            if (ns is IReadOnlyList<AnalysisValue> lst) {
                if (lst.Count == 0) {
                    return AnalysisSet.Empty;
                } else if (lst.Count == 1) {
                    return lst[0];
                } else if (lst.Count == 2) {
                    if (ObjectComparer.Instance.Equals(lst[0], lst[1])) {
                        return lst[0];
                    }
                    return new AnalysisSetDetails.AnalysisSetTwoObject(lst[0], lst[1]);
                }
            }
            return new AnalysisSetDetails.AnalysisHashSet(ns, ObjectComparer.Instance).Trim();
        }

        /// <summary>
        /// Returns a set containing all the types in <paramref name="ns" />
        /// with the specified comparer. This function uses the type of
        /// <paramref name="comparer" /> to determine which factory method
        /// should be used.
        /// 
        /// If <paramref name="ns" /> is a set with the same comparer as
        /// <paramref name="comparer"/>, it may be returned without
        /// modification.
        /// </summary>
        /// <param name="ns">The namespaces to contain in the set.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        /// <exception name="InvalidOperationException"><paramref
        /// name="comparer" /> is not an instance of <see cref="ObjectComparer"
        /// /> or <see cref="UnionComparer" />.</exception>
        public static IAnalysisSet Create(IEnumerable<AnalysisValue> ns, IEqualityComparer<AnalysisValue> comparer) {
            var set = ns as IAnalysisSet;
            if (set == null) {
                UnionComparer uc;
                if (comparer is ObjectComparer) {
                    return ns == null ? Create() : Create(ns);
                } else if ((uc = comparer as UnionComparer) != null) {
                    return ns == null ? CreateUnion(uc) : CreateUnion(ns, uc);
                }
            } else if (comparer == set.Comparer) {
                return set;
            } else if (comparer != null && comparer.GetType() == set.Comparer.GetType()) {
                return set;
            } else if (comparer is ObjectComparer) {
                return Create(set);
            } else if (comparer is UnionComparer) {
                return set.AsUnion((UnionComparer)comparer, out _);
            }

            throw new InvalidOperationException("cannot use {0} as a comparer".FormatInvariant(comparer));
        }

        /// <summary>
        /// Returns an empty set that uses a <see cref="UnionComparer" /> with
        /// the specified strength.
        /// </summary>
        /// <param name="strength">The strength to use for the comparer.
        /// </param>
        public static IAnalysisSet CreateUnion(int strength) {
            if (strength < 0) {
                strength = 0;
            } else if (strength > UnionComparer.MAX_STRENGTH) {
                strength = UnionComparer.MAX_STRENGTH;
            }
            return AnalysisSetDetails.AnalysisSetEmptyUnion.Instances[strength];
        }

        /// <summary>
        /// Returns an empty set that uses the specified <see
        /// cref="UnionComparer" />.
        /// </summary>
        /// <param name="comparer">The comparer to use for the set.</param>
        internal static IAnalysisSet CreateUnion(UnionComparer comparer) {
            return AnalysisSetDetails.AnalysisSetEmptyUnion.Instances[comparer.Strength];
        }

        /// <summary>
        /// Returns a set containing only <paramref name="ns" /> that uses the
        /// specified <see cref="UnionComparer" />.
        /// 
        /// This is different to casting from <see cref="AnalysisValue" /> to <see
        /// cref="IAnalysisSet" />, because the implementation in <see
        /// cref="AnalysisValue" /> always uses <see cref="ObjectComparer" />.
        /// </summary>
        /// <param name="ns">The namespace to contain in the set.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        internal static IAnalysisSet CreateUnion(AnalysisValue ns, UnionComparer comparer) {
            return new AnalysisSetDetails.AnalysisSetOneUnion(ns, comparer);
        }

        /// <summary>
        /// Returns a set containing all the types in <paramref name="ns" />
        /// after merging using the specified <see cref="UnionComparer" />. For
        /// large sets, this operation may require significant time and memory.
        /// The returned set is always a copy of the original.
        /// </summary>
        /// <param name="ns">The namespaces to contain in the set.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        internal static IAnalysisSet CreateUnion(IEnumerable<AnalysisValue> ns, UnionComparer comparer) {
            // TODO: Replace Trim() call with more intelligent enumeration.
            return new AnalysisSetDetails.AnalysisHashSet(ns.UnionIter(comparer, out _), comparer).Trim();
        }

        /// <summary>
        /// Returns a set containing all types in all the sets in <paramref
        /// name="sets" />.
        /// </summary>
        /// <param name="sets">The sets to contain in the set.</param>
        /// <param name="canMutate">True if sets in <paramref name="sets"/> may
        /// be modified.</param>
        public static IAnalysisSet UnionAll(IEnumerable<IAnalysisSet> sets, bool canMutate = false) {
            return Empty.UnionAll(sets, canMutate);
        }

        /// <summary>
        /// Returns a set containing all types in all the sets in <paramref
        /// name="sets" />.
        /// </summary>
        /// <param name="sets">The sets to contain in the set.</param>
        /// <param name="wasChanged">Returns True if the result is not an empty
        /// set.</param>
        /// <param name="canMutate">True if sets in <paramref name="sets"/> may
        /// be modified.</param>
        public static IAnalysisSet UnionAll(IEnumerable<IAnalysisSet> sets, out bool wasChanged, bool canMutate = false) {
            return Empty.UnionAll(sets, out wasChanged, canMutate);
        }

        #endregion

        #region Extension Methods

        /// <summary>
        /// Returns <paramref name="set"/> with a comparer with increased
        /// strength. If the strength cannot be increased, <paramref
        /// name="set"/> is returned unmodified.
        /// </summary>
        /// <param name="set">The set to increase the strength of.</param>
        public static IAnalysisSet AsStrongerUnion(this IAnalysisSet set) {
            var comparer = set.Comparer as UnionComparer;
            if (comparer != null) {
                return set.AsUnion(comparer.Strength + 1);
            } else {
                return set.AsUnion(0);
            }
        }

        /// <summary>
        /// Returns <paramref name="set"/> with a comparer with the specified
        /// strength. If the strength does not need to be changed, <paramref
        /// name="set"/> is returned unmodified.
        /// </summary>
        /// <param name="set">The set to convert to a union.</param>
        /// <param name="strength">The strength of the union.</param>
        public static IAnalysisSet AsUnion(this IAnalysisSet set, int strength) {
            return set.AsUnion(strength, out _);
        }

        /// <summary>
        /// Returns <paramref name="set"/> with a comparer with the specified
        /// strength. If the strength does not need to be changed, <paramref
        /// name="set"/> is returned unmodified.
        /// </summary>
        /// <param name="set">The set to convert to a union.</param>
        /// <param name="strength">The strength of the union.</param>
        /// <param name="wasChanged">Returns True if the contents of the
        /// returned set are different to <paramref name="set"/>.</param>
        public static IAnalysisSet AsUnion(this IAnalysisSet set, int strength, out bool wasChanged) {
            if (strength > UnionComparer.MAX_STRENGTH) {
                strength = UnionComparer.MAX_STRENGTH;
            } else if (strength < 0) {
                strength = 0;
            }
            var comparer = UnionComparer.Instances[strength];
            return AsUnion(set, comparer, out wasChanged);
        }

        /// <summary>
        /// Returns <paramref name="set"/> with the specified comparer. If the
        /// comparer does not need to be changed, <paramref name="set"/> is
        /// returned unmodified.
        /// </summary>
        /// <param name="set">The set to convert to a union.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        /// <param name="wasChanged">Returns True if the contents of the
        /// returned set are different to <paramref name="set"/>.</param>
        internal static IAnalysisSet AsUnion(this IAnalysisSet set, UnionComparer comparer, out bool wasChanged) {
            if ((set is AnalysisSetDetails.AnalysisSetOneUnion ||
                set is AnalysisSetDetails.AnalysisSetTwoUnion ||
                set is AnalysisSetDetails.AnalysisSetEmptyUnion ||
                set is AnalysisSetDetails.AnalysisHashSet) &&
                set.Comparer == comparer) {
                wasChanged = false;
                return set;
            }

            wasChanged = true;

            var ns = set as AnalysisValue;
            if (ns != null) {
                return CreateUnion(ns, comparer);
            }
            var ns1 = set as AnalysisSetDetails.AnalysisSetOneObject;
            if (ns1 != null) {
                return CreateUnion(ns1.Value, comparer);
            }
            var ns2 = set as AnalysisSetDetails.AnalysisSetTwoObject;
            if (ns2 != null) {
                if (comparer.Equals(ns2.Value1, ns2.Value2)) {
                    return new AnalysisSetDetails.AnalysisSetOneUnion(comparer.MergeTypes(ns2.Value1, ns2.Value2, out _), comparer);
                } else {
                    return new AnalysisSetDetails.AnalysisSetTwoUnion(ns2.Value1, ns2.Value2, comparer);
                }
            }

            return new AnalysisSetDetails.AnalysisHashSet(set, comparer);
        }

        /// <summary>
        /// Merges the provided sequence using the specified <see
        /// cref="UnionComparer"/>.
        /// </summary>
#if FULL_VALIDATION
        internal static IEnumerable<AnalysisValue> UnionIter(this IEnumerable<AnalysisValue> items, UnionComparer comparer, out bool wasChanged) {
            var originalItems = items.ToList();
            var newItems = UnionIterInternal(items, comparer, out wasChanged).ToList();

            Validation.Assert(newItems.Count <= originalItems.Count);
            if (wasChanged) {
                Validation.Assert(newItems.Count < originalItems.Count);
                foreach (var x in newItems) {
                    foreach (var y in newItems) {
                        if (object.ReferenceEquals(x, y)) continue;

                        Validation.Assert(!comparer.Equals(x, y), $"Failed {comparer}.Equals({x}, {y})");
                        Validation.Assert(!comparer.Equals(y, x), $"Failed {comparer}.Equals({y}, {x})");
                    }
                }
            }

            return newItems;
        }

        private static IEnumerable<AnalysisValue> UnionIterInternal(IEnumerable<AnalysisValue> items, UnionComparer comparer, out bool wasChanged) {
#else
        internal static IEnumerable<AnalysisValue> UnionIter(this IEnumerable<AnalysisValue> items, UnionComparer comparer, out bool wasChanged) {
#endif
            wasChanged = false;

            var asSet = items as IAnalysisSet;
            if (asSet != null && asSet.Comparer == comparer) {
                return items;
            }

            var newItems = new List<AnalysisValue>();
            var anyMerged = true;

            while (anyMerged) {
                anyMerged = false;
                var matches = new Dictionary<AnalysisValue, List<AnalysisValue>>(comparer);

                foreach (var ns in items) {
                    List<AnalysisValue> list;
                    if (matches.TryGetValue(ns, out list)) {
                        if (list == null) {
                            matches[ns] = list = new List<AnalysisValue>();
                        }
                        list.Add(ns);
                    } else {
                        matches[ns] = null;
                    }
                }

                newItems.Clear();

                foreach (var keyValue in matches) {
                    var item = keyValue.Key;
                    if (keyValue.Value != null) {
                        foreach (var other in keyValue.Value) {
                            bool merged;
#if FULL_VALIDATION
                            Validation.Assert(comparer.Equals(item, other), $"Merging non-equal items {item} and {other}");
#endif
                            item = comparer.MergeTypes(item, other, out merged);
                            if (merged) {
                                anyMerged = true;
                                wasChanged = true;
                            }
                        }
                    }
                    newItems.Add(item);
                }
                items = newItems;
            }

            return items;
        }

#if FULL_VALIDATION || DEBUG
        public static int GetTrueCount(this IAnalysisSet set) {
            if (set is AnalysisSetDetails.AnalysisSetOneObject as1o) {
                return as1o.Value == null ? 0 : 1;
            } else if (set is AnalysisSetDetails.AnalysisSetOneUnion as1u) {
                return as1u.Value == null ? 0 : 1;
            } else if (set is AnalysisSetDetails.AnalysisSetTwoObject as2o) {
                return (as2o.Value1 == null ? 0 : 1) + (as2o.Value2 == null ? 0 : 1);
            } else if (set is AnalysisSetDetails.AnalysisSetTwoUnion as2u) {
                return (as2u.Value1 == null ? 0 : 1) + (as2u.Value2 == null ? 0 : 1);
            } else if (set is AnalysisSetDetails.AnalysisSetEmptyObject || set is AnalysisSetDetails.AnalysisSetEmptyUnion) {
                return 0;
            } else if (set is AnalysisSetDetails.AnalysisHashSet hashSet) {
                return hashSet.GetTrueCount();
            } else {
                return set?.Count() ?? 0;
            }
        }
#endif

        /// <summary>
        /// Removes excess capacity from <paramref name="set"/>.
        /// </summary>
        public static IAnalysisSet Trim(this IAnalysisSet set) {
            if (!(set is AnalysisSetDetails.AnalysisHashSet)) {
                return set;
            }

            var uc = set.Comparer as UnionComparer;
            AnalysisValue first = null, second = null;
            using (var e = set.GetEnumerator()) {
                if (e.MoveNext()) {
                    first = e.Current;
                    if (e.MoveNext()) {
                        second = e.Current;
                        if (e.MoveNext()) {
                            // More than two items, so return unchanged
                            return set;
                        } else if (uc != null) {
                            return new AnalysisSetDetails.AnalysisSetTwoUnion(first, second, uc);
                        } else {
                            return new AnalysisSetDetails.AnalysisSetTwoObject(first, second);
                        }
                    } else if (uc != null) {
                        return new AnalysisSetDetails.AnalysisSetOneUnion(first, uc);
                    } else {
                        return first;
                    }
                } else if (uc != null) {
                    return AnalysisSetDetails.AnalysisSetEmptyUnion.Instances[uc.Strength];
                } else {
                    return AnalysisSetDetails.AnalysisSetEmptyObject.Instance;
                }
            }
        }

        /// <summary>
        /// Merges all the types in <paramref name="sets" /> into this set.
        /// </summary>
        /// <param name="sets">The sets to merge into this set.</param>
        /// <param name="canMutate">True if this set may be modified.</param>
        public static IAnalysisSet UnionAll(this IAnalysisSet set, IEnumerable<IAnalysisSet> sets, bool canMutate = false) {
            return set.UnionAll(sets, out _, canMutate);
        }

        /// <summary>
        /// Merges all the types in <paramref name="sets" /> into this set.
        /// </summary>
        /// <param name="sets">The sets to merge into this set.</param>
        /// <param name="wasChanged">Returns True if the contents of the
        /// returned set are different to the original set.</param>
        /// <param name="canMutate">True if this set may be modified.</param>
        public static IAnalysisSet UnionAll(this IAnalysisSet set, IEnumerable<IAnalysisSet> sets, out bool wasChanged, bool canMutate = false) {
            bool changed;
            wasChanged = false;
            foreach (var s in sets) {
                var newSet = set.Union(s, out changed, canMutate);
                if (changed) {
                    wasChanged = true;
                }
                set = newSet;
            }
            return set;
        }

        /// <summary>
        /// Splits values in the set according to the predicate. Returns true
        /// if there are any values in <paramref name="trueSet"/> on return.
        /// </summary>
        public static bool Split(this IAnalysisSet set, Func<AnalysisValue, bool> predicate, out IAnalysisSet trueSet, out IAnalysisSet falseSet) {
            if (predicate == null) {
                throw new ArgumentNullException(nameof(predicate));
            }

            IAnalysisSet empty;
            if (set.Comparer is UnionComparer uc) {
                empty = CreateUnion(uc);
            } else {
                empty = Create();
            }

            if (set.Count == 0) {
                trueSet = falseSet = empty;
                return false;
            } else if (set is AnalysisValue av) {
                if (predicate(av)) {
                    trueSet = av;
                    falseSet = empty;
                    return true;
                }
                trueSet = empty;
                falseSet = av;
                return false;
            }

            trueSet = empty.Union(set.Where(predicate), out bool res);
            falseSet = empty.Union(set.Where(v => !predicate(v)));
            return res;
        }

        /// <summary>
        /// Splits values in the set according to type. Returns true if there are
        /// any values of type T in <paramref name="ofType"/>.
        /// </summary>
        public static bool Split<T>(this IAnalysisSet set, out IReadOnlyList<T> ofType, out IAnalysisSet rest) {
            IAnalysisSet empty;
            if (set.Comparer is UnionComparer uc) {
                empty = CreateUnion(uc);
            } else {
                empty = Create();
            }

            if (set is T t) {
                ofType = new[] { t };
                rest = empty;
                return true;
            } else if (set is AnalysisValue) {
                ofType = Array.Empty<T>();
                rest = set;
                return false;
            }

            ofType = set.OfType<T>().ToArray();
            if (!ofType.Any()) {
                rest = set;
                return false;
            }

            rest = Create(set.Where(av => !(av is T)), set.Comparer);
            return true;
        }

        /// <summary>
        /// Determines whether there is any overlap between two sets.
        /// </summary>
        public static bool ContainsAny(this IAnalysisSet set, IAnalysisSet values) {
            // TODO: This can be optimised for specific set types
            return set.Intersect(values, set.Comparer).Any();
        }

        #endregion
    }

    sealed class ObjectComparer : IEqualityComparer<AnalysisValue>, IEqualityComparer<IAnalysisSet> {
        public static readonly ObjectComparer Instance = new ObjectComparer();

        public bool Equals(AnalysisValue x, AnalysisValue y) {
#if FULL_VALIDATION
            if (x != null && y != null) {
                Validation.Assert(x.Equals(y) == y.Equals(x), $"Non-commutative equality: {x} == {y}");
                if (x.Equals(y)) {
                    Validation.Assert(x.GetHashCode() == y.GetHashCode(), $"Mismatched hash code for {x} ({x.GetHashCode()}) == {y} ({y.GetHashCode()})");
                }
            }
#endif
            return (x == null) ? (y == null) : x.Equals(y);
        }

        public int GetHashCode(AnalysisValue obj) {
            return (obj == null) ? 0 : obj.GetHashCode();
        }

        public bool Equals(IAnalysisSet set1, IAnalysisSet set2) {
            if (set1.Comparer == this) {
                return set1.SetEquals(set2);
            } else if (set2.Comparer == this) {
                return set2.SetEquals(set1);
            } else if (set1.Count == set2.Count) {
                return set1.All(ns => set2.Contains(ns, this)) &&
                       set2.All(ns => set1.Contains(ns, this));
            }
            return false;
        }

        public int GetHashCode(IAnalysisSet obj) {
            return obj.Aggregate(GetHashCode(), (hash, ns) => hash ^ GetHashCode(ns));
        }
    }

    sealed class UnionComparer : IEqualityComparer<AnalysisValue>, IEqualityComparer<IAnalysisSet> {
        public const int MAX_STRENGTH = 3;
        public static readonly UnionComparer[] Instances = Enumerable.Range(0, MAX_STRENGTH + 1).Select(i => new UnionComparer(i)).ToArray();


        public readonly int Strength;

        public UnionComparer(int strength = 0) {
            Strength = strength;
        }

        public bool Equals(AnalysisValue x, AnalysisValue y) {
#if FULL_VALIDATION
            if (x != null && y != null) {
                Validation.Assert(x.UnionEquals(y, Strength) == y.UnionEquals(x, Strength), $"{Strength}\n{x}\n{y}");
                if (x.UnionEquals(y, Strength)) {
                    Validation.Assert(x.UnionHashCode(Strength) == y.UnionHashCode(Strength), $"Strength:{Strength}\n{x} - {x.UnionHashCode(Strength)}\n{y} - {y.UnionHashCode(Strength)}");
                }
            }
#endif
            if (Object.ReferenceEquals(x, y)) {
                return true;
            }
            return (x == null) ? (y == null) : x.UnionEquals(y, Strength);
        }

        public int GetHashCode(AnalysisValue obj) {
            return (obj == null) ? 0 : obj.UnionHashCode(Strength);
        }

        public AnalysisValue MergeTypes(AnalysisValue x, AnalysisValue y, out bool wasChanged) {
            if (Object.ReferenceEquals(x, y)) {
                wasChanged = false;
                return x;
            }
            var z = x.UnionMergeTypes(y, Strength);
            wasChanged = !Object.ReferenceEquals(x, z);
#if FULL_VALIDATION
            var z2 = y.UnionMergeTypes(x, Strength);
            if (!object.ReferenceEquals(z, z2)) {
                Validation.Assert(z.UnionEquals(z2, Strength), $"{Strength}\n{x} + {y} => {z}\n{y} + {x} => {z2}");
                Validation.Assert(z2.UnionEquals(z, Strength), $"{Strength}\n{y} + {x} => {z2}\n{x} + {y} => {z}");
            }
            Validation.Assert(x.UnionEquals(z, Strength), $"{Strength}\n{x} != {z}");
            Validation.Assert(y.UnionEquals(z, Strength), $"{Strength}\n{y} != {z}");
#endif
            return z;
        }

        public bool Equals(IAnalysisSet set1, IAnalysisSet set2) {
            if (set1.Comparer == this) {
                return set1.SetEquals(set2);
            } else if (set2.Comparer == this) {
                return set2.SetEquals(set1);
            } else {
                return set1.All(ns => set2.Contains(ns, this)) &&
                    (set2.Comparer == set1.Comparer || set2.All(ns => set1.Contains(ns, this)));
            }
        }

        public int GetHashCode(IAnalysisSet obj) {
            return obj.Aggregate(GetHashCode(), (hash, ns) => hash ^ GetHashCode(ns));
        }
    }



    namespace AnalysisSetDetails {
        sealed class DebugViewProxy {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public const string DisplayString = "{this}, {Comparer.GetType().Name,nq}";

            public DebugViewProxy(IAnalysisSet source) {
                Data = source.ToArray();
                Comparer = source.Comparer;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public AnalysisValue[] Data;

            public override string ToString() {
                return ToString(Data);
            }

            public static string ToString(IAnalysisSet source) {
                return ToString(source.ToArray());
            }

            public static string ToString(AnalysisValue[] source) {
                var data = source.ToArray();
                if (data.Length == 0) {
                    return "{}";
                } else if (data.Length < 5) {
                    return "{" + string.Join(", ", data.AsEnumerable()) + "}";
                } else {
                    return $"{{Size = {data.Length}}}";
                }
            }

            public IEqualityComparer<AnalysisValue> Comparer {
                get;
                private set;
            }

            public int Size {
                get { return Data.Length; }
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetEmptyObject : IAnalysisSet, IImmutableAnalysisSet {
            public static readonly IAnalysisSet Instance = new AnalysisSetEmptyObject();

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                return item;
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                wasChanged = true;
                return item;
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                if (items == null || items is AnalysisSetEmptyObject || items is AnalysisSetEmptyUnion) {
                    return this;
                }
                if (items is AnalysisValue || items is AnalysisSetOneObject || items is AnalysisSetTwoObject) {
                    return (IAnalysisSet)items;
                }
                return items.Any() ? AnalysisSet.Create(items) : this;
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                if (items == null || items is AnalysisSetEmptyObject || items is AnalysisSetEmptyUnion) {
                    wasChanged = false;
                    return this;
                }
                if (items is AnalysisValue || items is AnalysisSetOneObject || items is AnalysisSetTwoObject) {
                    wasChanged = true;
                    return (IAnalysisSet)items;
                }
                wasChanged = items.Any();
                return wasChanged ? AnalysisSet.Create(items) : this;
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return false;
            }

            public bool SetEquals(IAnalysisSet other) {
                return other != null && other.Count == 0;
            }

            public int Count {
                get { return 0; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<AnalysisValue> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => ((IEqualityComparer<IAnalysisSet>)Comparer).GetHashCode(this);
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetOneObject : IAnalysisSet, IImmutableAnalysisSet {
            public readonly AnalysisValue Value;

            public AnalysisSetOneObject(AnalysisValue value) {
                Value = value;
            }

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                if (ObjectComparer.Instance.Equals(Value, item)) {
                    return this;
                } else {
                    return new AnalysisSetTwoObject(Value, item);
                }
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                if (ObjectComparer.Instance.Equals(Value, item)) {
                    wasChanged = false;
                    return this;
                } else {
                    wasChanged = true;
                    return new AnalysisSetTwoObject(Value, item);
                }
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                bool wasChanged;
                return Union(items, out wasChanged, canMutate);
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                AnalysisSetOneObject ns1;
                AnalysisSetTwoObject ns2;
                if (items == null) {
                    wasChanged = false;
                    return this;
                } else if ((ns1 = items as AnalysisSetOneObject) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else if ((ns2 = items as AnalysisSetTwoObject) != null) {
                    if (ns2.Contains(Value)) {
                        wasChanged = false;
                        return ns2;
                    }
                    wasChanged = true;
                    return new AnalysisHashSet(3, ObjectComparer.Instance).Add(ns2.Value1, canMutate: true).Add(ns2.Value2, canMutate: true).Add(Value, canMutate: true);
                } else {
                    return new AnalysisHashSet(items, ObjectComparer.Instance).Add(Value, out wasChanged, canMutate: true);
                }
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return ObjectComparer.Instance.Equals(Value, item);
            }

            public bool SetEquals(IAnalysisSet other) {
                AnalysisValue ns;
                AnalysisSetOneObject ns1o;
                AnalysisSetOneUnion ns1u;
                if ((ns = other as AnalysisValue) != null) {
                    return ObjectComparer.Instance.Equals(Value, ns);
                } else if ((ns1o = other as AnalysisSetOneObject) != null) {
                    return ObjectComparer.Instance.Equals(Value, ns1o.Value);
                } else if ((ns1u = other as AnalysisSetOneUnion) != null) {
                    return ObjectComparer.Instance.Equals(Value, ns1u.Value);
                } else if (other != null && other.Count == 1) {
                    return ObjectComparer.Instance.Equals(Value, other.First());
                } else {
                    return false;
                }
            }

            public int Count {
                get { return 1; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                return new SetOfOneEnumerator<AnalysisValue>(Value);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<AnalysisValue> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => ((IEqualityComparer<IAnalysisSet>)Comparer).GetHashCode(this);
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetTwoObject : IAnalysisSet, IImmutableAnalysisSet {
            public readonly AnalysisValue Value1, Value2;

            public AnalysisSetTwoObject(AnalysisValue value1, AnalysisValue value2) {
                Value1 = value1;
                Value2 = value2;
            }

            public AnalysisSetTwoObject(IEnumerable<AnalysisValue> set) {
                using (var e = set.GetEnumerator()) {
                    if (!e.MoveNext()) {
                        throw new InvalidOperationException("Sequence requires exactly two values");
                    }
                    Value1 = e.Current;
                    if (!e.MoveNext() && !ObjectComparer.Instance.Equals(e.Current, Value1)) {
                        throw new InvalidOperationException("Sequence requires exactly two values");
                    }
                    Value2 = e.Current;
                    if (e.MoveNext()) {
                        throw new InvalidOperationException("Sequence requires exactly two values");
                    }
                }
            }

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                bool wasChanged;
                return Add(item, out wasChanged, canMutate);
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                if (ObjectComparer.Instance.Equals(Value1, item) || ObjectComparer.Instance.Equals(Value2, item)) {
                    wasChanged = false;
                    return this;
                }
                wasChanged = true;
                return new AnalysisHashSet(3, ObjectComparer.Instance).Add(Value1).Add(Value2).Add(item);
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneObject ns1;
                if (items == null) {
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1 = items as AnalysisSetOneObject) != null) {
                    return Add(ns1.Value, canMutate);
                } else {
                    return new AnalysisHashSet(items, ObjectComparer.Instance).Add(Value1).Add(Value2);
                }
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneObject ns1;
                if (items == null) {
                    wasChanged = false;
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1 = items as AnalysisSetOneObject) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else {
                    bool wasChanged1, wasChanged2;
                    var set = new AnalysisHashSet(items, ObjectComparer.Instance)
                        .Add(Value1, out wasChanged1)
                        .Add(Value2, out wasChanged2);
                    wasChanged = wasChanged1 || wasChanged2;
                    return set;
                }
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return ObjectComparer.Instance.Equals(Value1, item) || ObjectComparer.Instance.Equals(Value2, item);
            }

            public bool SetEquals(IAnalysisSet other) {
                var ns2 = other as AnalysisSetTwoObject;
                if (ns2 != null) {
                    return ObjectComparer.Instance.Equals(Value1, ns2.Value1) && ObjectComparer.Instance.Equals(Value2, ns2.Value2) ||
                        ObjectComparer.Instance.Equals(Value1, ns2.Value2) && ObjectComparer.Instance.Equals(Value2, ns2.Value1);
                } else if (other != null && other.Count == 2) {
                    foreach (var ns in other) {
                        if (!ObjectComparer.Instance.Equals(Value1, ns) && !ObjectComparer.Instance.Equals(Value2, ns)) {
                            return false;
                        }
                    }
                    return true;
                } else {
                    return false;
                }
            }

            public int Count {
                get { return 2; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                yield return Value1;
                yield return Value2;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<AnalysisValue> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => ((IEqualityComparer<IAnalysisSet>)Comparer).GetHashCode(this);
        }




        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetEmptyUnion : IAnalysisSet, IImmutableAnalysisSet {
            public static readonly IAnalysisSet[] Instances = UnionComparer.Instances.Select(cmp => new AnalysisSetEmptyUnion(cmp)).ToArray();

            private readonly UnionComparer _comparer;

            public AnalysisSetEmptyUnion(UnionComparer comparer) {
                _comparer = comparer;
            }

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                return new AnalysisSetOneUnion(item, Comparer);
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                wasChanged = true;
                return new AnalysisSetOneUnion(item, Comparer);
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                if (items == null) {
                    return this;
                } else if (items is AnalysisSetOneUnion || items is AnalysisSetTwoUnion || items is AnalysisSetEmptyUnion) {
                    return (IAnalysisSet)items;
                }
                return items.Any() ? AnalysisSet.CreateUnion(items, Comparer) : this;
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                if (items == null || items is AnalysisSetEmptyObject || items is AnalysisSetEmptyUnion) {
                    wasChanged = false;
                    return this;
                }
                if (items is AnalysisSetOneUnion || items is AnalysisSetTwoUnion) {
                    wasChanged = true;
                    return (IAnalysisSet)items;
                }
                wasChanged = items.Any();
                return wasChanged ? AnalysisSet.CreateUnion(items, Comparer) : this;
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return false;
            }

            public bool SetEquals(IAnalysisSet other) {
                return other != null && other.Count == 0;
            }

            public int Count {
                get { return 0; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<AnalysisValue> IAnalysisSet.Comparer {
                get { return ((AnalysisSetEmptyUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => Comparer.GetHashCode(this);
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetOneUnion : IAnalysisSet, IImmutableAnalysisSet {
            public readonly AnalysisValue Value;
            private readonly UnionComparer _comparer;

            public AnalysisSetOneUnion(AnalysisValue value, UnionComparer comparer) {
                Value = value;
                _comparer = comparer;
            }

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                if (Object.ReferenceEquals(Value, item)) {
                    return this;
                } else if (Comparer.Equals(Value, item)) {
                    bool wasChanged;
                    var newItem = Comparer.MergeTypes(Value, item, out wasChanged);
                    return wasChanged ? new AnalysisSetOneUnion(newItem, Comparer) : this;
                } else {
                    return new AnalysisSetTwoUnion(Value, item, Comparer);
                }
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                if (Object.ReferenceEquals(Value, item)) {
                    wasChanged = false;
                    return this;
                } else if (Comparer.Equals(Value, item)) {
                    var newItem = Comparer.MergeTypes(Value, item, out wasChanged);
                    return wasChanged ? new AnalysisSetOneUnion(newItem, Comparer) : this;
                } else {
                    wasChanged = true;
                    return new AnalysisSetTwoUnion(Value, item, Comparer);
                }
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneUnion ns1;
                AnalysisSetOneObject nsO1;
                AnalysisSetTwoUnion ns2;
                if (items == null) {
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1 = items as AnalysisSetOneUnion) != null) {
                    return Add(ns1.Value, canMutate);
                } else if ((nsO1 = items as AnalysisSetOneObject) != null) {
                    return Add(nsO1.Value, canMutate);
                } else if ((ns2 = items as AnalysisSetTwoUnion) != null && ns2.Comparer == Comparer) {
                    return ns2.Add(Value, false);
                } else {
                    return new AnalysisHashSet(items, Comparer).Add(Value).Trim();
                }
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneUnion ns1;
                AnalysisSetOneObject nsO1;
                AnalysisSetTwoUnion ns2;
                if (items == null) {
                    wasChanged = false;
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1 = items as AnalysisSetOneUnion) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else if ((nsO1 = items as AnalysisSetOneObject) != null) {
                    return Add(nsO1.Value, out wasChanged, canMutate);
                } else if ((ns2 = items as AnalysisSetTwoUnion) != null && ns2.Comparer == Comparer) {
                    return ns2.Add(Value, out wasChanged, false);
                } else {
                    return new AnalysisHashSet(Value, Comparer).Union(items, out wasChanged).Trim();
                }
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return Comparer.Equals(Value, item);
            }

            public bool SetEquals(IAnalysisSet other) {
                var ns1 = other as AnalysisSetOneUnion;
                if (ns1 != null) {
                    return Comparer.Equals(Value, ns1.Value);
                } else if (other == null || other.Count == 0) {
                    return false;
                } else if (other.Count == 1) {
                    return Comparer.Equals(Value, other.First());
                } else {
                    return other.All(ns => Comparer.Equals(ns, Value));
                }
            }

            public int Count {
                get { return 1; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                return new SetOfOneEnumerator<AnalysisValue>(Value);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<AnalysisValue> IAnalysisSet.Comparer {
                get { return ((AnalysisSetOneUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => Comparer.GetHashCode(this);
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class AnalysisSetTwoUnion : IAnalysisSet, IImmutableAnalysisSet {
            public readonly AnalysisValue Value1, Value2;
            private readonly UnionComparer _comparer;

            public AnalysisSetTwoUnion(AnalysisValue value1, AnalysisValue value2, UnionComparer comparer) {
                Debug.Assert(!comparer.Equals(value1, value2));
                Value1 = value1;
                Value2 = value2;
                _comparer = comparer;
            }

            internal static Tuple<AnalysisValue, AnalysisValue> FromEnumerable(IEnumerable<AnalysisValue> set, UnionComparer comparer) {
                using (var e = set.GetEnumerator()) {
                    if (!e.MoveNext()) {
                        return new Tuple<AnalysisValue, AnalysisValue>(null, null);
                    }
                    var value1 = e.Current;
                    if (!e.MoveNext()) {
                        return new Tuple<AnalysisValue, AnalysisValue>(value1, null);
                    }
                    var value2 = e.Current;
                    if (comparer.Equals(e.Current, value1)) {
                        return new Tuple<AnalysisValue, AnalysisValue>(comparer.MergeTypes(value1, value2, out _), null);
                    }
                    if (e.MoveNext()) {
                        return null;
                    }
                    return new Tuple<AnalysisValue, AnalysisValue>(value1, value2);
                }
            }

            public AnalysisSetTwoUnion(IEnumerable<AnalysisValue> set, UnionComparer comparer) {
                _comparer = comparer;
                var tup = FromEnumerable(set, comparer);
                if (tup == null || tup.Item2 == null) {
                    throw new InvalidOperationException("Sequence requires exactly two values");
                }
                Value1 = tup.Item1;
                Value2 = tup.Item2;
            }

            public IAnalysisSet Add(AnalysisValue item, bool canMutate = false) {
                return Add(item, out _, canMutate);
            }

            public IAnalysisSet Add(AnalysisValue item, out bool wasChanged, bool canMutate = false) {
                if (Object.ReferenceEquals(Value1, item) || Object.ReferenceEquals(Value2, item)) {
                    wasChanged = false;
                    return this;
                } else if (Comparer.Equals(Value1, item)) {
                    var newValue = Comparer.MergeTypes(Value1, item, out wasChanged);
                    if (!wasChanged) {
                        return this;
                    }
                    if (Comparer.Equals(Value2, newValue)) {
                        return new AnalysisSetOneUnion(Comparer.MergeTypes(Value2, newValue, out _), Comparer);
                    } else {
                        return new AnalysisSetTwoUnion(newValue, Value2, Comparer);
                    }
                } else if (Comparer.Equals(Value2, item)) {
                    var newValue = Comparer.MergeTypes(Value2, item, out wasChanged);
                    if (!wasChanged) {
                        return this;
                    }
                    if (Comparer.Equals(Value1, newValue)) {
                        return new AnalysisSetOneUnion(Comparer.MergeTypes(Value1, newValue, out _), Comparer);
                    } else {
                        return new AnalysisSetTwoUnion(Value1, newValue, Comparer);
                    }
                }
                wasChanged = true;
                return new AnalysisHashSet(3, Comparer).Add(Value1).Add(Value2).Add(item);
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneObject ns1o;
                AnalysisSetOneUnion ns1u;
                if (items == null) {
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1o = items as AnalysisSetOneObject) != null) {
                    return Add(ns1o.Value, canMutate);
                } else if ((ns1u = items as AnalysisSetOneUnion) != null) {
                    return Add(ns1u.Value, canMutate);
                } else {
                    return new AnalysisHashSet(this, Comparer).Union(items);
                }
            }

            public IAnalysisSet Union(IEnumerable<AnalysisValue> items, out bool wasChanged, bool canMutate = false) {
                AnalysisValue ns;
                AnalysisSetOneObject ns1o;
                AnalysisSetOneUnion ns1u;
                if (items == null) {
                    wasChanged = false;
                    return this;
                } else if ((ns = items as AnalysisValue) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1o = items as AnalysisSetOneObject) != null) {
                    return Add(ns1o.Value, out wasChanged, canMutate);
                } else if ((ns1u = items as AnalysisSetOneUnion) != null) {
                    return Add(ns1u.Value, out wasChanged, canMutate);
                } else {
                    return new AnalysisHashSet(this, Comparer).Union(items, out wasChanged);
                }
            }

            public IAnalysisSet Clone() {
                return this;
            }

            public bool Contains(AnalysisValue item) {
                return Comparer.Equals(Value1, item) || Comparer.Equals(Value2, item);
            }

            public bool SetEquals(IAnalysisSet other) {
                var ns2 = other as AnalysisSetTwoUnion;
                if (ns2 != null) {
                    return Comparer.Equals(Value1, ns2.Value1) && Comparer.Equals(Value2, ns2.Value2) ||
                        Comparer.Equals(Value1, ns2.Value2) && Comparer.Equals(Value2, ns2.Value1);
                } else if (other != null && other.Count > 0) {
                    return other.All(ns => Comparer.Equals(Value1) || Comparer.Equals(Value2));
                } else {
                    return false;
                }
            }

            public int Count {
                get { return 2; }
            }

            public IEnumerator<AnalysisValue> GetEnumerator() {
                yield return Value1;
                yield return Value2;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<AnalysisValue> IAnalysisSet.Comparer {
                get { return ((AnalysisSetTwoUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }

            public override bool Equals(object obj) => (obj is IAnalysisSet s) && SetEquals(s);
            public override int GetHashCode() => Comparer.GetHashCode(this);
        }

    }

}
