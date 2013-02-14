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

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents an unordered collection of <see cref="Namespace" /> objects;
    /// in effect, a set of Python types. There are multiple implementing
    /// classes, including <see cref="Namespace" /> itself, to improve memory
    /// usage for small sets.
    /// 
    /// <see cref="NamespaceSet" /> does not implement this interface, but
    /// provides factory and extension methods.
    /// </summary>
    internal interface INamespaceSet : IEnumerable<Namespace> {
        INamespaceSet Add(Namespace item, bool canMutate = true);
        INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true);
        INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true);
        INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true);
        INamespaceSet Clone();

        bool Contains(Namespace item);
        bool SetEquals(INamespaceSet other);

        int Count { get; }
        IEqualityComparer<Namespace> Comparer { get; }
    }

    /// <summary>
    /// Provides factory and extension methods for objects implementing
    /// <see cref="INamespaceSet" />.
    /// </summary>
    internal static class NamespaceSet {
        /// <summary>
        /// An empty set that does not combine types. This object is immutable
        /// and can be used without cloning.
        /// </summary>
        public static readonly INamespaceSet Empty = Create();
        /// <summary>
        /// An empty set that combines types with a strength of zero. This
        /// object is immutable and can be used without cloning.
        /// </summary>
        public static readonly INamespaceSet EmptyUnion = CreateUnion(UnionComparer.Instances[0]);

        #region Constructors

        /// <summary>
        /// Returns an empty set that does not combine types. This is exactly
        /// equivalent to accessing <see cref="Empty" />.
        /// </summary>
        public static INamespaceSet Create() {
            return NamespaceSetDetails.NamespaceSetEmptyObject.Instance;
        }

        /// <summary>
        /// Returns a set containing only <paramref name="ns" />. This is
        /// exactly equivalent to casting <paramref name="ns" /> to <see
        /// cref="INamespaceSet" />.
        /// </summary>
        /// <param name="ns">The namespace to contain in the set.</param>
        public static INamespaceSet Create(Namespace ns) {
            return ns;
        }

        /// <summary>
        /// Returns a set containing all the types in <paramref name="ns" />.
        /// This is the usual way of creating a new set from any sequence.
        /// </summary>
        /// <param name="ns">The namespaces to contain in the set.</param>
        public static INamespaceSet Create(IEnumerable<Namespace> ns) {
            // TODO: Replace Trim() call with more efficient enumeration.
            return new NamespaceSetDetails.NamespaceSetManyObject(ns).Trim();
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
        public static INamespaceSet Create(IEnumerable<Namespace> ns, IEqualityComparer<Namespace> comparer) {
            var set = ns as INamespaceSet;
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
                bool dummy;
                return set.AsUnion((UnionComparer)comparer, out dummy);
            }

            throw new InvalidOperationException(string.Format("cannot use {0} as a comparer", comparer));
        }

        /// <summary>
        /// Returns an empty set that uses a <see cref="UnionComparer" /> with
        /// the specified strength.
        /// </summary>
        /// <param name="strength">The strength to use for the comparer.
        /// </param>
        public static INamespaceSet CreateUnion(int strength) {
            if (strength < 0) {
                strength = 0;
            } else if (strength > UnionComparer.MAX_STRENGTH) {
                strength = UnionComparer.MAX_STRENGTH;
            }
            return NamespaceSetDetails.NamespaceSetEmptyUnion.Instances[strength];
        }

        /// <summary>
        /// Returns an empty set that uses the specified <see
        /// cref="UnionComparer" />.
        /// </summary>
        /// <param name="comparer">The comparer to use for the set.</param>
        public static INamespaceSet CreateUnion(UnionComparer comparer) {
            return NamespaceSetDetails.NamespaceSetEmptyUnion.Instances[comparer.Strength];
        }

        /// <summary>
        /// Returns a set containing only <paramref name="ns" /> that uses the
        /// specified <see cref="UnionComparer" />.
        /// 
        /// This is different to casting from <see cref="Namespace" /> to <see
        /// cref="INamespaceSet" />, because the implementation in <see
        /// cref="Namespace" /> always uses <see cref="ObjectComparer" />.
        /// </summary>
        /// <param name="ns">The namespace to contain in the set.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        public static INamespaceSet CreateUnion(Namespace ns, UnionComparer comparer) {
            return new NamespaceSetDetails.NamespaceSetOneUnion(ns, comparer);
        }

        /// <summary>
        /// Returns a set containing all the types in <paramref name="ns" />
        /// after merging using the specified <see cref="UnionComparer" />. For
        /// large sets, this operation may require significant time and memory.
        /// The returned set is always a copy of the original.
        /// </summary>
        /// <param name="ns">The namespaces to contain in the set.</param>
        /// <param name="comparer">The comparer to use for the set.</param>
        public static INamespaceSet CreateUnion(IEnumerable<Namespace> ns, UnionComparer comparer) {
            bool dummy;
            // TODO: Replace Trim() call with more intelligent enumeration.
            return new NamespaceSetDetails.NamespaceSetManyUnion(ns.UnionIter(comparer, out dummy), comparer).Trim();
        }

        /// <summary>
        /// Returns a set containing all types in all the sets in <paramref
        /// name="sets" />.
        /// </summary>
        /// <param name="sets">The sets to contain in the set.</param>
        /// <param name="canMutate">True if sets in <paramref name="sets"/> may
        /// be modified.</param>
        public static INamespaceSet UnionAll(IEnumerable<INamespaceSet> sets, bool canMutate = true) {
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
        public static INamespaceSet UnionAll(IEnumerable<INamespaceSet> sets, out bool wasChanged, bool canMutate = true) {
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
        public static INamespaceSet AsStrongerUnion(this INamespaceSet set) {
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
        public static INamespaceSet AsUnion(this INamespaceSet set, int strength) {
            bool dummy;
            return set.AsUnion(strength, out dummy);
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
        public static INamespaceSet AsUnion(this INamespaceSet set, int strength, out bool wasChanged) {
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
        public static INamespaceSet AsUnion(this INamespaceSet set, UnionComparer comparer, out bool wasChanged) {
            if ((set is NamespaceSetDetails.NamespaceSetOneUnion ||
                set is NamespaceSetDetails.NamespaceSetTwoUnion ||
                set is NamespaceSetDetails.NamespaceSetEmptyUnion ||
                set is NamespaceSetDetails.NamespaceSetManyUnion) &&
                set.Comparer == comparer) {
                wasChanged = false;
                return set;
            }

            wasChanged = true;

            var ns = set as Namespace;
            if (ns != null) {
                return CreateUnion(ns, comparer);
            }
            var ns1 = set as NamespaceSetDetails.NamespaceSetOneObject;
            if (ns1 != null) {
                return CreateUnion(ns1.Value, comparer);
            }
            var ns2 = set as NamespaceSetDetails.NamespaceSetTwoObject;
            if (ns2 != null) {
                if (comparer.Equals(ns2.Value1, ns2.Value2)) {
                    bool dummy;
                    return new NamespaceSetDetails.NamespaceSetOneUnion(comparer.MergeTypes(ns2.Value1, ns2.Value2, out dummy), comparer);
                } else {
                    return new NamespaceSetDetails.NamespaceSetTwoUnion(ns2.Value1, ns2.Value2, comparer);
                }
            }

            return new NamespaceSetDetails.NamespaceSetManyUnion(set, comparer);
        }

        /// <summary>
        /// Merges the provided sequence using the specified <see
        /// cref="UnionComparer"/>.
        /// </summary>
        internal static IEnumerable<Namespace> UnionIter(this IEnumerable<Namespace> items, UnionComparer comparer, out bool wasChanged) {
            wasChanged = false;

            var asSet = items as INamespaceSet;
            if (asSet != null && asSet.Comparer == comparer) {
                return items;
            }

            var newItems = new List<Namespace>();
            var anyMerged = true;

            while (anyMerged) {
                anyMerged = false;
                var matches = new Dictionary<Namespace, List<Namespace>>(comparer);

                foreach (var ns in items) {
                    List<Namespace> list;
                    if (matches.TryGetValue(ns, out list)) {
                        if (list == null) {
                            matches[ns] = list = new List<Namespace>();
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

        /// <summary>
        /// Removes excess capacity from <paramref name="set"/>.
        /// </summary>
        public static INamespaceSet Trim(this INamespaceSet set) {
            if (set is NamespaceSetDetails.NamespaceSetManyObject) {
                switch (set.Count) {
                    case 0:
                        return Empty;
                    case 1:
                        return set.First();
                    case 2:
                        return new NamespaceSetDetails.NamespaceSetTwoObject(set);
                    default:
                        return set;
                }
            } else if (set is NamespaceSetDetails.NamespaceSetManyUnion) {
                switch (set.Count) {
                    case 0:
                        return NamespaceSetDetails.NamespaceSetEmptyUnion.Instances[((UnionComparer)set.Comparer).Strength];
                    case 1:
                        return new NamespaceSetDetails.NamespaceSetOneUnion(set.First(), (UnionComparer)set.Comparer);
                    case 2: {
                            var tup = NamespaceSetDetails.NamespaceSetTwoUnion.FromEnumerable(set, (UnionComparer)set.Comparer);
                            if (tup == null) {
                                return set;
                            } else if (tup.Item1 == null && tup.Item2 == null) {
                                return NamespaceSetDetails.NamespaceSetEmptyUnion.Instances[((UnionComparer)set.Comparer).Strength];
                            } else if (tup.Item2 == null) {
                                return new NamespaceSetDetails.NamespaceSetOneUnion(tup.Item1, (UnionComparer)set.Comparer);
                            } else {
                                return new NamespaceSetDetails.NamespaceSetTwoUnion(tup.Item1, tup.Item2, (UnionComparer)set.Comparer);
                            }
                        }
                    default:
                        return set;
                }
            } else {
                return set;
            }
        }

        /// <summary>
        /// Merges all the types in <paramref name="sets" /> into this set.
        /// </summary>
        /// <param name="sets">The sets to merge into this set.</param>
        /// <param name="canMutate">True if this set may be modified.</param>
        public static INamespaceSet UnionAll(this INamespaceSet set, IEnumerable<INamespaceSet> sets, bool canMutate = true) {
            bool dummy;
            return set.UnionAll(sets, out dummy, canMutate);
        }

        /// <summary>
        /// Merges all the types in <paramref name="sets" /> into this set.
        /// </summary>
        /// <param name="sets">The sets to merge into this set.</param>
        /// <param name="wasChanged">Returns True if the contents of the
        /// returned set are different to the original set.</param>
        /// <param name="canMutate">True if this set may be modified.</param>
        public static INamespaceSet UnionAll(this INamespaceSet set, IEnumerable<INamespaceSet> sets, out bool wasChanged, bool canMutate = true) {
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

        #endregion
    }

    sealed class ObjectComparer : IEqualityComparer<Namespace>, IEqualityComparer<INamespaceSet> {
        public static readonly ObjectComparer Instance = new ObjectComparer();

        public bool Equals(Namespace x, Namespace y) {
            return (x == null) ? (y == null) : x.Equals(y);
        }

        public int GetHashCode(Namespace obj) {
            return (obj == null) ? 0 : obj.GetHashCode();
        }

        public bool Equals(INamespaceSet set1, INamespaceSet set2) {
            if (set1.Comparer == this) {
                return set1.SetEquals(set2);
            } else if (set2.Comparer == this) {
                return set2.SetEquals(set1);
            } else {
                return set1.All(ns => set2.Contains(ns, this)) &&
                    (set2.Comparer == set1.Comparer || set2.All(ns => set1.Contains(ns, this)));
            }
        }

        public int GetHashCode(INamespaceSet obj) {
            return obj.Aggregate(GetHashCode(), (hash, ns) => hash ^ GetHashCode(ns));
        }
    }

    sealed class UnionComparer : IEqualityComparer<Namespace>, IEqualityComparer<INamespaceSet> {
        public const int MAX_STRENGTH = 3;
        public static readonly UnionComparer[] Instances = Enumerable.Range(0, MAX_STRENGTH + 1).Select(i => new UnionComparer(i)).ToArray();


        public readonly int Strength;

        public UnionComparer(int strength = 0) {
            Strength = strength;
        }

        public bool Equals(Namespace x, Namespace y) {
            return (x == null) ? (y == null) : x.UnionEquals(y, Strength);
        }

        public int GetHashCode(Namespace obj) {
            return (obj == null) ? 0 : obj.UnionHashCode(Strength);
        }

        public Namespace MergeTypes(Namespace x, Namespace y, out bool wasChanged) {
            var z = x.UnionMergeTypes(y, Strength);
            wasChanged = !Object.ReferenceEquals(x, z);
            return z;
        }

        public bool Equals(INamespaceSet set1, INamespaceSet set2) {
            if (set1.Comparer == this) {
                return set1.SetEquals(set2);
            } else if (set2.Comparer == this) {
                return set2.SetEquals(set1);
            } else {
                return set1.All(ns => set2.Contains(ns, this)) &&
                    (set2.Comparer == set1.Comparer || set2.All(ns => set1.Contains(ns, this)));
            }
        }

        public int GetHashCode(INamespaceSet obj) {
            return obj.Aggregate(GetHashCode(), (hash, ns) => hash ^ GetHashCode(ns));
        }
    }



    namespace NamespaceSetDetails {
        sealed class DebugViewProxy {
            [DebuggerBrowsable(DebuggerBrowsableState.Never)]
            public const string DisplayString = "{this}, {Comparer.GetType().Name,nq}";

            public DebugViewProxy(INamespaceSet source) {
                Data = source.ToArray();
                Comparer = source.Comparer;
            }

            [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
            public Namespace[] Data;

            public override string ToString() {
                return ToString(Data);
            }

            public static string ToString(INamespaceSet source) {
                return ToString(source.ToArray());
            }

            public static string ToString(Namespace[] source) {
                var data = source.ToArray();
                if (data.Length == 0) {
                    return "{}";
                } else if (data.Length < 5) {
                    return "{" + string.Join(", ", data.AsEnumerable()) + "}";
                } else {
                    return string.Format("{{Size = {0}}}", data.Length);
                }
            }

            public IEqualityComparer<Namespace> Comparer {
                get;
                private set;
            }

            public int Size {
                get { return Data.Length; }
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetManyObject : INamespaceSet {
            public readonly HashSet<Namespace> Set;

            public NamespaceSetManyObject(IEnumerable<Namespace> items) {
                Set = new HashSet<Namespace>(items, ObjectComparer.Instance);
            }

            internal NamespaceSetManyObject(NamespaceSetTwoObject firstTwo, Namespace third) {
                Set = new HashSet<Namespace>(ObjectComparer.Instance);
                Set.Add(firstTwo.Value1);
                Set.Add(firstTwo.Value2);
                Set.Add(third);
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                if (Set.Contains(item)) {
                    return this;
                }
                var set = canMutate ? this : new NamespaceSetManyObject(this.Set);
                set.Set.Add(item);
                return set;
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                if (Set.Contains(item)) {
                    wasChanged = false;
                    return this;
                }
                var set = canMutate ? this : new NamespaceSetManyObject(this.Set);
                wasChanged = set.Set.Add(item);
                return set;
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                if (Set.IsSupersetOf(items)) {
                    return this;
                }
                var set = canMutate ? this : new NamespaceSetManyObject(this.Set);
                set.Set.UnionWith(items);
                return set;
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                if (Set.IsSupersetOf(items)) {
                    wasChanged = false;
                    return this;
                }
                var set = canMutate ? this : new NamespaceSetManyObject(this.Set);
                wasChanged = true;
                set.Set.UnionWith(items);
                return set;
            }

            public INamespaceSet Clone() {
                switch (Set.Count) {
                    case 0:
                        return new NamespaceSetEmptyObject();
                    case 1:
                        return new NamespaceSetOneObject(Set.First());
                    case 2:
                        return new NamespaceSetTwoObject(Set);
                    default:
                        return new NamespaceSetManyObject(Set);
                }
            }

            public bool Contains(Namespace item) {
                return Set.Contains(item);
            }

            public bool SetEquals(INamespaceSet other) {
                if (other == null) {
                    return false;
                }
                foreach (var ns in Set) {
                    if (!other.Contains(ns, Comparer)) {
                        return false;
                    }
                }
                if (other.Comparer != Comparer) {
                    foreach (var ns in Set) {
                        if (!other.Contains(ns, Comparer)) {
                            return false;
                        }
                    }
                }
                return true;
            }

            public int Count {
                get { return Set.Count; }
            }

            public IEnumerator<Namespace> GetEnumerator() {
                return Set.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return Set.GetEnumerator();
            }

            public IEqualityComparer<Namespace> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetEmptyObject : INamespaceSet {
            public static readonly INamespaceSet Instance = new NamespaceSetEmptyObject();

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                return item;
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                wasChanged = true;
                return item;
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                if (items is NamespaceSetEmptyObject || items is NamespaceSetEmptyUnion) {
                    return this;
                }
                if (items is Namespace || items is NamespaceSetOneObject || items is NamespaceSetTwoObject) {
                    return (INamespaceSet)items;
                }
                return items.Any() ? NamespaceSet.Create(items) : this;
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                if (items is NamespaceSetEmptyObject || items is NamespaceSetEmptyUnion) {
                    wasChanged = false;
                    return this;
                }
                if (items is Namespace || items is NamespaceSetOneObject || items is NamespaceSetTwoObject) {
                    wasChanged = true;
                    return (INamespaceSet)items;
                }
                wasChanged = items.Any();
                return wasChanged ? NamespaceSet.Create(items) : this;
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return false;
            }

            public bool SetEquals(INamespaceSet other) {
                return other != null && other.Count == 0;
            }

            public int Count {
                get { return 0; }
            }

            public IEnumerator<Namespace> GetEnumerator() {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<Namespace> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }


        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetOneObject : INamespaceSet {
            public readonly Namespace Value;

            public NamespaceSetOneObject(Namespace value) {
                Value = value;
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                if (ObjectComparer.Instance.Equals(Value, item)) {
                    return this;
                } else {
                    return new NamespaceSetTwoObject(Value, item);
                }
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                if (ObjectComparer.Instance.Equals(Value, item)) {
                    wasChanged = false;
                    return this;
                } else {
                    wasChanged = true;
                    return new NamespaceSetTwoObject(Value, item);
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                NamespaceSetOneObject ns1;
                NamespaceSetTwoObject ns2;
                if ((ns1 = items as NamespaceSetOneObject) != null) {
                    return Add(ns1.Value, canMutate);
                } else if ((ns2 = items as NamespaceSetTwoObject) != null) {
                    if (ns2.Contains(Value)) {
                        return ns2;
                    }
                    return new NamespaceSetManyObject(ns2, Value);
                } else {
                    return new NamespaceSetManyObject(items).Add(Value);
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                NamespaceSetOneObject ns1;
                NamespaceSetTwoObject ns2;
                if ((ns1 = items as NamespaceSetOneObject) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else if ((ns2 = items as NamespaceSetTwoObject) != null) {
                    wasChanged = true;
                    if (ns2.Contains(Value)) {
                        return ns2;
                    }
                    return new NamespaceSetManyObject(ns2, Value);
                } else {
                    return new NamespaceSetManyObject(items).Add(Value, out wasChanged);
                }
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return ObjectComparer.Instance.Equals(Value, item);
            }

            public bool SetEquals(INamespaceSet other) {
                Namespace ns;
                NamespaceSetOneObject ns1o;
                NamespaceSetOneUnion ns1u;
                if ((ns = other as Namespace) != null) {
                    return ObjectComparer.Instance.Equals(Value, ns);
                } else if ((ns1o = other as NamespaceSetOneObject) != null) {
                    return ObjectComparer.Instance.Equals(Value, ns1o.Value);
                } else if ((ns1u = other as NamespaceSetOneUnion) != null) {
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

            public IEnumerator<Namespace> GetEnumerator() {
                return new SetOfOneEnumerator<Namespace>(Value);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<Namespace> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetTwoObject : INamespaceSet {
            public readonly Namespace Value1, Value2;

            public NamespaceSetTwoObject(Namespace value1, Namespace value2) {
                Value1 = value1;
                Value2 = value2;
            }

            public NamespaceSetTwoObject(IEnumerable<Namespace> set) {
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

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                if (ObjectComparer.Instance.Equals(Value1, item) || ObjectComparer.Instance.Equals(Value2, item)) {
                    return this;
                }
                return new NamespaceSetManyObject(this, item);
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                if (ObjectComparer.Instance.Equals(Value1, item) || ObjectComparer.Instance.Equals(Value2, item)) {
                    wasChanged = false;
                    return this;
                }
                wasChanged = true;
                return new NamespaceSetManyObject(this, item);
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneObject ns1;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1 = items as NamespaceSetOneObject) != null) {
                    return Add(ns1.Value, canMutate);
                } else {
                    return new NamespaceSetManyObject(items).Add(Value1).Add(Value2);
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneObject ns1;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1 = items as NamespaceSetOneObject) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else {
                    bool wasChanged1, wasChanged2;
                    var set = new NamespaceSetManyObject(items).Add(Value1, out wasChanged1).Add(Value2, out wasChanged2);
                    wasChanged = wasChanged1 || wasChanged2;
                    return set;
                }
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return ObjectComparer.Instance.Equals(Value1, item) || ObjectComparer.Instance.Equals(Value2, item);
            }

            public bool SetEquals(INamespaceSet other) {
                var ns2 = other as NamespaceSetTwoObject;
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

            public IEnumerator<Namespace> GetEnumerator() {
                yield return Value1;
                yield return Value2;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEqualityComparer<Namespace> Comparer {
                get { return ObjectComparer.Instance; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }




        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetManyUnion : INamespaceSet {
            public readonly HashSet<Namespace> Set;

            public NamespaceSetManyUnion(IEnumerable<Namespace> items, UnionComparer comparer) {
                Set = new HashSet<Namespace>(items, comparer);
            }

            internal NamespaceSetManyUnion(NamespaceSetTwoUnion firstTwo, Namespace third, UnionComparer comparer, out bool wasChanged) {
                Set = new HashSet<Namespace>(comparer);
                Set.Add(firstTwo.Value1);
                wasChanged = true;
                if (!Set.Add(firstTwo.Value2) || !Set.Add(third)) {
                    Set.Clear();
                    Set.UnionWith(firstTwo.Concat(third).UnionIter(comparer, out wasChanged));
                }
            }

            private NamespaceSetManyUnion(HashSet<Namespace> set) {
                Set = set;
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                bool dummy;
                return Add(item, out dummy, canMutate);
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                var set = Set;
                if (canMutate) {
                    if (set.Add(item)) {
                        wasChanged = true;
                        return this;
                    }
                } else if (!set.Contains(item)) {
                    set = new HashSet<Namespace>(set, set.Comparer);
                    wasChanged = set.Add(item);
                    return wasChanged ? new NamespaceSetManyUnion(set) : this;
                } else {
                    set = new HashSet<Namespace>(set, set.Comparer);
                }

                var cmp = Comparer;
                var newItem = item;
                bool newItemAlreadyInSet = false;
                foreach (var ns in Set) {
                    if (cmp.Equals(ns, newItem)) {
                        bool changed = false;
                        newItem = cmp.MergeTypes(ns, newItem, out changed);
                        newItemAlreadyInSet |= !changed;
                    }
                }
                int removed;
                if (!newItemAlreadyInSet) {
                    removed = set.RemoveWhere(ns => cmp.Equals(ns, newItem));
                    set.Add(newItem);
                } else {
                    removed = set.RemoveWhere(ns => cmp.Equals(ns, newItem) && !Object.ReferenceEquals(ns, newItem));
                }

                wasChanged = (removed > 1) | !newItemAlreadyInSet;

                return (canMutate | !wasChanged) ? this : new NamespaceSetManyUnion(set);
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                bool anyAdded = false;
                var cmp = Comparer;
                var set = canMutate ? Set : new HashSet<Namespace>(Set, Set.Comparer);
                foreach (var item in items) {
                    if (set.Add(item)) {
                        anyAdded = true;
                    } else {
                        bool dummy;
                        var newItem = item;
                        foreach (var ns in set) {
                            if (Object.ReferenceEquals(ns, newItem)) {
                                return this;
                            }
                            if (cmp.Equals(ns, newItem)) {
                                newItem = cmp.MergeTypes(ns, newItem, out dummy);
                            }
                        }
                        set.RemoveWhere(ns => cmp.Equals(ns, item));
                        set.Add(newItem);
                    }
                }
                return (canMutate | !anyAdded) ? this : new NamespaceSetManyUnion(set);
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                bool anyAdded = false;
                var cmp = Comparer;
                var set = canMutate ? Set : new HashSet<Namespace>(Set, Set.Comparer);
                wasChanged = false;
                foreach (var item in items) {
                    if (set.Add(item)) {
                        anyAdded = true;
                    } else {
                        bool changed = false;
                        var newItem = item;
                        foreach (var ns in set) {
                            if (Object.ReferenceEquals(ns, newItem)) {
                                return this;
                            }
                            if (cmp.Equals(ns, newItem)) {
                                newItem = cmp.MergeTypes(ns, newItem, out changed);
                            }
                        }
                        int removed = set.RemoveWhere(ns => cmp.Equals(ns, item));
                        set.Add(newItem);
                        anyAdded |= (removed > 1) | changed;
                    }
                }
                wasChanged = anyAdded;
                return (canMutate | !anyAdded) ? this : new NamespaceSetManyUnion(set);
            }

            public INamespaceSet Clone() {
                switch (Set.Count) {
                    case 0:
                        return NamespaceSetEmptyUnion.Instances[Comparer.Strength];
                    case 1:
                        return new NamespaceSetOneUnion(Set.First(), Comparer);
                    case 2: {
                            var tup = NamespaceSetTwoUnion.FromEnumerable(Set, Comparer);
                            if (tup == null) {
                                return new NamespaceSetManyUnion(Set, Comparer);
                            } else if (tup.Item1 == null && tup.Item2 == null) {
                                return NamespaceSetEmptyUnion.Instances[Comparer.Strength];
                            } else if (tup.Item2 == null) {
                                return new NamespaceSetOneUnion(tup.Item1, Comparer);
                            } else {
                                return new NamespaceSetTwoUnion(tup.Item1, tup.Item2, Comparer);
                            }
                        }
                    default:
                        return new NamespaceSetManyUnion(Set, Comparer);
                }
            }

            public bool Contains(Namespace item) {
                return Set.Contains(item);
            }

            public bool SetEquals(INamespaceSet other) {
                if (other == null) {
                    return false;
                }
                foreach (var ns in Set) {
                    if (!other.Contains(ns, Comparer)) {
                        return false;
                    }
                }
                if (other.Comparer != Comparer) {
                    foreach (var ns in Set) {
                        if (!other.Contains(ns, Comparer)) {
                            return false;
                        }
                    }
                }
                return true;
            }

            public int Count {
                get { return Set.Count; }
            }

            public IEnumerator<Namespace> GetEnumerator() {
                return Set.GetEnumerator();
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return Set.GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return (UnionComparer)Set.Comparer; }
            }

            IEqualityComparer<Namespace> INamespaceSet.Comparer {
                get { return Set.Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetEmptyUnion : INamespaceSet {
            public static readonly INamespaceSet[] Instances = UnionComparer.Instances.Select(cmp => new NamespaceSetEmptyUnion(cmp)).ToArray();

            private readonly UnionComparer _comparer;

            public NamespaceSetEmptyUnion(UnionComparer comparer) {
                _comparer = comparer;
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                return new NamespaceSetOneUnion(item, Comparer);
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                wasChanged = true;
                return new NamespaceSetOneUnion(item, Comparer);
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                if (items is NamespaceSetOneUnion || items is NamespaceSetTwoUnion || items is NamespaceSetEmptyUnion) {
                    return (INamespaceSet)items;
                }
                return items.Any() ? NamespaceSet.CreateUnion(items, Comparer) : this;
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                if (items is NamespaceSetEmptyObject || items is NamespaceSetEmptyUnion) {
                    wasChanged = false;
                    return this;
                }
                if (items is NamespaceSetOneUnion || items is NamespaceSetTwoUnion) {
                    wasChanged = true;
                    return (INamespaceSet)items;
                }
                wasChanged = items.Any();
                return wasChanged ? NamespaceSet.CreateUnion(items, Comparer) : this;
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return false;
            }

            public bool SetEquals(INamespaceSet other) {
                return other != null && other.Count == 0;
            }

            public int Count {
                get { return 0; }
            }

            public IEnumerator<Namespace> GetEnumerator() {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<Namespace> INamespaceSet.Comparer {
                get { return ((NamespaceSetEmptyUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }


        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetOneUnion : INamespaceSet {
            public readonly Namespace Value;
            private readonly UnionComparer _comparer;

            public NamespaceSetOneUnion(Namespace value, UnionComparer comparer) {
                Value = value;
                _comparer = comparer;
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                if (Object.ReferenceEquals(Value, item)) {
                    return this;
                } else if (Comparer.Equals(Value, item)) {
                    bool wasChanged;
                    var newItem = Comparer.MergeTypes(Value, item, out wasChanged);
                    return wasChanged ? new NamespaceSetOneUnion(newItem, Comparer) : this;
                } else {
                    return new NamespaceSetTwoUnion(Value, item, Comparer);
                }
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                if (Object.ReferenceEquals(Value, item)) {
                    wasChanged = false;
                    return this;
                } else if (Comparer.Equals(Value, item)) {
                    var newItem = Comparer.MergeTypes(Value, item, out wasChanged);
                    return wasChanged ? new NamespaceSetOneUnion(newItem, Comparer) : this;
                } else {
                    wasChanged = true;
                    return new NamespaceSetTwoUnion(Value, item, Comparer);
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneUnion ns1;
                NamespaceSetOneObject nsO1;
                NamespaceSetTwoUnion ns2;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1 = items as NamespaceSetOneUnion) != null) {
                    return Add(ns1.Value, canMutate);
                } else if ((nsO1 = items as NamespaceSetOneObject) != null) {
                    return Add(nsO1.Value, canMutate);
                } else if ((ns2 = items as NamespaceSetTwoUnion) != null && ns2.Comparer == Comparer) {
                    return ns2.Add(Value, false);
                } else {
                    return new NamespaceSetManyUnion(items, Comparer).Add(Value).Trim();
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneUnion ns1;
                NamespaceSetOneObject nsO1;
                NamespaceSetTwoUnion ns2;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1 = items as NamespaceSetOneUnion) != null) {
                    return Add(ns1.Value, out wasChanged, canMutate);
                } else if ((nsO1 = items as NamespaceSetOneObject) != null) {
                    return Add(nsO1.Value, out wasChanged, canMutate);
                } else if ((ns2 = items as NamespaceSetTwoUnion) != null && ns2.Comparer == Comparer) {
                    return ns2.Add(Value, out wasChanged, false);
                } else {
                    return new NamespaceSetManyUnion(Value, Comparer).Union(items, out wasChanged).Trim();
                }
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return Comparer.Equals(Value, item);
            }

            public bool SetEquals(INamespaceSet other) {
                var ns1 = other as NamespaceSetOneUnion;
                if (ns1 != null) {
                    return Comparer.Equals(Value, ns1.Value);
                } else if (other == null) {
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

            public IEnumerator<Namespace> GetEnumerator() {
                return new SetOfOneEnumerator<Namespace>(Value);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<Namespace> INamespaceSet.Comparer {
                get { return ((NamespaceSetOneUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }

        [DebuggerDisplay(DebugViewProxy.DisplayString), DebuggerTypeProxy(typeof(DebugViewProxy))]
        sealed class NamespaceSetTwoUnion : INamespaceSet {
            public readonly Namespace Value1, Value2;
            private readonly UnionComparer _comparer;

            public NamespaceSetTwoUnion(Namespace value1, Namespace value2, UnionComparer comparer) {
                Debug.Assert(!comparer.Equals(value1, value2));
                Value1 = value1;
                Value2 = value2;
                _comparer = comparer;
            }

            internal static Tuple<Namespace, Namespace> FromEnumerable(IEnumerable<Namespace> set, UnionComparer comparer) {
                using (var e = set.GetEnumerator()) {
                    if (!e.MoveNext()) {
                        return new Tuple<Namespace, Namespace>(null, null);
                    }
                    var value1 = e.Current;
                    if (!e.MoveNext()) {
                        return new Tuple<Namespace, Namespace>(value1, null);
                    }
                    var value2 = e.Current;
                    if (comparer.Equals(e.Current, value1)) {
                        bool dummy;
                        return new Tuple<Namespace, Namespace>(comparer.MergeTypes(value1, value2, out dummy), null);
                    }
                    if (e.MoveNext()) {
                        return null;
                    }
                    return new Tuple<Namespace, Namespace>(value1, value2);
                }
            }

            public NamespaceSetTwoUnion(IEnumerable<Namespace> set, UnionComparer comparer) {
                _comparer = comparer;
                var tup = FromEnumerable(set, comparer);
                if (tup == null || tup.Item2 == null) {
                    throw new InvalidOperationException("Sequence requires exactly two values");
                }
                Value1 = tup.Item1;
                Value2 = tup.Item2;
            }

            public INamespaceSet Add(Namespace item, bool canMutate = true) {
                bool dummy;
                return Add(item, out dummy, canMutate);
            }

            public INamespaceSet Add(Namespace item, out bool wasChanged, bool canMutate = true) {
                bool dummy;
                if (Object.ReferenceEquals(Value1, item) || Object.ReferenceEquals(Value2, item)) {
                    wasChanged = false;
                    return this;
                } else if (Comparer.Equals(Value1, item)) {
                    var newValue = Comparer.MergeTypes(Value1, item, out wasChanged);
                    if (!wasChanged) {
                        return this;
                    }
                    if (Comparer.Equals(Value2, newValue)) {
                        return new NamespaceSetOneUnion(Comparer.MergeTypes(Value2, newValue, out dummy), Comparer);
                    } else {
                        return new NamespaceSetTwoUnion(newValue, Value2, Comparer);
                    }
                } else if (Comparer.Equals(Value2, item)) {
                    var newValue = Comparer.MergeTypes(Value2, item, out wasChanged);
                    if (!wasChanged) {
                        return this;
                    }
                    if (Comparer.Equals(Value1, newValue)) {
                        return new NamespaceSetOneUnion(Comparer.MergeTypes(Value1, newValue, out dummy), Comparer);
                    } else {
                        return new NamespaceSetTwoUnion(Value1, newValue, Comparer);
                    }
                }
                wasChanged = true;
                return new NamespaceSetManyUnion(this, item, Comparer, out dummy);
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneObject ns1o;
                NamespaceSetOneUnion ns1u;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, canMutate);
                } else if ((ns1o = items as NamespaceSetOneObject) != null) {
                    return Add(ns1o.Value, canMutate);
                } else if ((ns1u = items as NamespaceSetOneUnion) != null) {
                    return Add(ns1u.Value, canMutate);
                } else {
                    return new NamespaceSetManyUnion(this, Comparer).Union(items);
                }
            }

            public INamespaceSet Union(IEnumerable<Namespace> items, out bool wasChanged, bool canMutate = true) {
                Namespace ns;
                NamespaceSetOneObject ns1o;
                NamespaceSetOneUnion ns1u;
                if ((ns = items as Namespace) != null) {
                    return Add(ns, out wasChanged, canMutate);
                } else if ((ns1o = items as NamespaceSetOneObject) != null) {
                    return Add(ns1o.Value, out wasChanged, canMutate);
                } else if ((ns1u = items as NamespaceSetOneUnion) != null) {
                    return Add(ns1u.Value, out wasChanged, canMutate);
                } else {
                    return new NamespaceSetManyUnion(this, Comparer).Union(items, out wasChanged);
                }
            }

            public INamespaceSet Clone() {
                return this;
            }

            public bool Contains(Namespace item) {
                return Comparer.Equals(Value1, item) || Comparer.Equals(Value2, item);
            }

            public bool SetEquals(INamespaceSet other) {
                var ns2 = other as NamespaceSetTwoUnion;
                if (ns2 != null) {
                    return Comparer.Equals(Value1, ns2.Value1) && Comparer.Equals(Value2, ns2.Value2) ||
                        Comparer.Equals(Value1, ns2.Value2) && Comparer.Equals(Value2, ns2.Value1);
                } else if (other != null) {
                    return other.All(ns => Comparer.Equals(Value1) || Comparer.Equals(Value2));
                } else {
                    return false;
                }
            }

            public int Count {
                get { return 2; }
            }

            public IEnumerator<Namespace> GetEnumerator() {
                yield return Value1;
                yield return Value2;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            internal UnionComparer Comparer {
                get { return _comparer; }
            }

            IEqualityComparer<Namespace> INamespaceSet.Comparer {
                get { return ((NamespaceSetTwoUnion)this).Comparer; }
            }

            public override string ToString() {
                return DebugViewProxy.ToString(this);
            }
        }

    }

}
