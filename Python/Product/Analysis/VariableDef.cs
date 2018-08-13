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
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    abstract class DependentData<TStorageType> where TStorageType : DependencyInfo {
        internal SingleDict<IVersioned, TStorageType> _dependencies;

        /// <summary>
        /// Clears old values from old modules.  These old values are values which were assigned from
        /// an out of data analysis.
        /// </summary>
        public void ClearOldValues() {
            foreach (var module in _dependencies.Keys.ToArray()) {
                ClearOldValues(module);
            }
        }

        /// <summary>
        /// Clears old values from the specified module.  These old values are values which were assigned from
        /// an out of data analysis.
        /// </summary>
        /// <param name="fromModule"></param>
        public void ClearOldValues(IVersioned fromModule) {
            TStorageType deps;
            if (_dependencies.TryGetValue(fromModule, out deps)) {
                if (deps.Version != fromModule.AnalysisVersion) {
                    _dependencies.Remove(fromModule);
                }
            }
        }

        protected TStorageType GetDependentItems(IVersioned module) {
            TStorageType result;
            if (!_dependencies.TryGetValue(module, out result) || result.Version != module.AnalysisVersion) {
                _dependencies[module] = result = NewDefinition(module.AnalysisVersion);
            }
            return result;
        }

        protected abstract TStorageType NewDefinition(int version);

        /// <summary>
        /// Enqueues any nodes which depend upon this type into the provided analysis queue for
        /// further analysis.
        /// </summary>
        public virtual void EnqueueDependents(IVersioned assigner = null, IProjectEntry declaringScope = null) {
            bool hasOldValues = false;
            foreach (var keyValue in _dependencies) {
                if (keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                    if (assigner == null || IsVisible(keyValue.Key, declaringScope, assigner)) {
                        var val = keyValue.Value;
                        if (val.DependentUnits != null) {
                            foreach (var analysisUnit in val.DependentUnits) {
                                analysisUnit.Enqueue();
                            }
                        }
                    }
                } else {
                    hasOldValues = true;
                }
            }

            if (hasOldValues) {
                ClearOldValues();
            }
        }

        public bool AddDependency(AnalysisUnit unit) {
            if (!unit.ForEval) {
                return GetDependentItems(unit.DependencyProject).AddDependentUnit(unit);
            }
            return false;
        }

        protected static bool IsVisible(IVersioned accessor, IVersioned declaringScope, IVersioned assigningScope) {
            return true;
            /*
            if (accessor != null && accessor.IsVisible(assigningScope)) {
                return true;
            }
            if (declaringScope != null && declaringScope.IsVisible(assigningScope)) {
                return true;
            }
            return false;*/
        }

    }

    class DependentData : DependentData<DependencyInfo> {
        protected override DependencyInfo NewDefinition(int version) {
            return new DependencyInfo(version);
        }
    }

    /// <summary>
    /// A VariableDef represents a collection of type information and dependencies
    /// upon that type information.  
    /// 
    /// The collection of type information is represented by a set of AnalysisValue
    /// objects.  This set includes all of the types that are known to have been
    /// seen for this variable.
    /// 
    /// Dependency data is added when an one value is assigned to a variable.  
    /// For example, for the statement:
    /// 
    ///     fob = value
    /// 
    /// There will be a variable def for the name "fob", and "value" will evaluate
    /// to a collection of namespaces.  When value is assigned to
    /// fob the types in value will be propagated to fob's VariableDef by a call
    /// to AddDependentTypes.  If value adds any new type information to fob
    /// then the caller needs to re-analyze anyone who is dependent upon fob's
    /// values.  If "value" was a VariableDef as well, rather than some arbitrary 
    /// expression, then reading "value" would have made the code being analyzed dependent 
    /// upon "value".  After a call to AddTypes the caller needs to check the 
    /// return value and if new types were added (returns true) needs to re-enque it's scope.
    /// 
    /// Dependencies are stored in a dictionary keyed off of the IProjectEntry object.
    /// This is a consistent object which always represents the same module even
    /// across multiple analyses.  The object is versioned so that when we encounter
    /// a new version all the old dependencies will be thrown away when a variable ref 
    /// is updated with new dependencies.
    /// 
    /// TODO: We should store built-in types not keyed off of the ModuleInfo.
    /// </summary>
    abstract class TypedDef<T> : DependentData<T> where T : TypedDependencyInfo {
        /// <summary>
        /// This limit is used to prevent analysis from continuing forever due
        /// to bugs or unanalyzable code. It is tested in Types and
        /// TypesNoCopy, where an accurate type count is available without
        /// requiring extra computation, and variables exceeding the limit are
        /// added to LockedVariableDefs. AddTypes is a no-op for instances in
        /// this set.
        /// </summary>
        internal const int HARD_TYPE_LIMIT = 1000;

        static readonly ConditionalWeakTable<TypedDef<T>, object> LockedVariableDefs = new ConditionalWeakTable<TypedDef<T>, object>();
        static readonly object LockedVariableDefsValue = new object();

        protected IAnalysisSet _emptySet = AnalysisSet.Empty;
        private IAnalysisSet _cache;

        /// <summary>
        /// Marks the current VariableDef as exceeding the limit and not to be
        /// added to in future. It is virtual to allow subclasses to try and
        /// 'rescue' the VariableDef, for example, by combining types.
        /// </summary>
        protected virtual void ExceedsTypeLimit() {
            object dummy;
            var uc = _emptySet.Comparer as UnionComparer;
            if (uc == null) {
                MakeUnion(0);
            } else if (uc.Strength < UnionComparer.MAX_STRENGTH) {
                MakeUnion(uc.Strength + 1);
            } else if (!LockedVariableDefs.TryGetValue(this, out dummy)) {
                LockedVariableDefs.Add(this, LockedVariableDefsValue);
                // The remainder of this block logs diagnostic information to
                // allow the VariableDef to be identified.
                int total = 0;
                var typeCounts = new Dictionary<string, int>();
                foreach (var type in TypesNoCopy) {
                    var str = type.ToString() ?? "";
                    int count;
                    if (!typeCounts.TryGetValue(str, out count)) {
                        count = 0;
                    }
                    typeCounts[str] = count + 1;
                    total += 1;
                }
                var typeCountList = typeCounts.OrderByDescending(kv => kv.Value).Select(kv => "{0}x {1}".FormatInvariant(kv.Value, kv.Key)).ToList();
                Debug.Write("{0} exceeded type limit.\nStack trace:\n{1}\nContents:\n    Count = {2}\n    {3}\n".FormatInvariant(
                    GetType().Name,
                    new StackTrace(true),
                    total,
                    string.Join("\n    ", typeCountList)));
                AnalysisLog.ExceedsTypeLimit(GetType().Name, total, string.Join(", ", typeCountList));
            }
        }

        protected int EstimateTypeCount(IAnalysisSet extraTypes = null) {
            // Use a fast estimate of the number of types we have, since this
            // function will be called very often.
            var roughSet = new HashSet<AnalysisValue>();
            foreach (var info in _dependencies.Values) {
                roughSet.UnionWith(info.Types);
            }
            if (extraTypes != null) {
                roughSet.UnionWith(extraTypes);
            }
            return roughSet.Count;
        }

        public bool AddTypes(AnalysisUnit unit, IAnalysisSet newTypes, bool enqueue = true, IProjectEntry declaringScope = null) {
            return AddTypes(unit.DependencyProject, newTypes, enqueue, declaringScope);
        }

        // Set checks ensure that the wasChanged result is correct. The checks
        // are memory intensive, since they perform the add an extra time. The
        // flag is static but non-const to allow it to be enabled while
        // debugging.
#if FULL_VALIDATION
        private static bool ENABLE_SET_CHECK = true;
#elif DEBUG
        private static bool ENABLE_SET_CHECK = false;
#endif

        public bool AddTypes(IVersioned projectEntry, IAnalysisSet newTypes, bool enqueue = true, IProjectEntry declaringScope = null) {
            object dummy;
            if (LockedVariableDefs.TryGetValue(this, out dummy)) {
                return false;
            }
            
            bool added = false;
            if (newTypes.Count > 0) {
                var dependencies = GetDependentItems(projectEntry);

                foreach (var value in newTypes) {

#if DEBUG || FULL_VALIDATION
                    if (ENABLE_SET_CHECK) {
                        bool testAdded;
                        var original = dependencies.ToImmutableTypeSet();
                        var afterAdded = original.Add(value, out testAdded, false);
                        if (afterAdded.Comparer == original.Comparer) {
                            if (testAdded) {
                                if (!ObjectComparer.Instance.Equals(afterAdded, original)) {
                                    // Double validation, as sometimes testAdded is a false positive
                                    afterAdded = original.Add(value, out testAdded, false);
                                    if (testAdded) {
                                        Validation.Assert(!ObjectComparer.Instance.Equals(afterAdded, original), $"Inconsistency adding {value} to {original}");
                                    }
                                }
                            } else if (afterAdded.Count == original.Count) {
                                Validation.Assert(ObjectComparer.Instance.Equals(afterAdded, original), $"Inconsistency not adding {value} to {original}");
                            }
                        }
                    }
#endif

                    if (dependencies.AddType(value)) {
                        added = true;
                    }
                }
            }

            if (added) {
                _cache = null;
            }
            if (added && enqueue) {
                EnqueueDependents(projectEntry, declaringScope);
            }

            return added;
        }

        public IAnalysisSet GetTypes(AnalysisUnit accessor, ProjectEntry declaringScope = null) {
            bool needsCopy;
            var res = GetTypesWorker(accessor.ProjectEntry, declaringScope, out needsCopy);
            if (needsCopy) {
                res = res.Clone();
            }
            return res;
        }

        public IAnalysisSet GetTypesNoCopy(AnalysisUnit accessor, IProjectEntry declaringScope = null) {
            return GetTypesNoCopy(accessor.ProjectEntry, declaringScope);
        }

        public IAnalysisSet GetTypesNoCopy(IProjectEntry accessor = null, IProjectEntry declaringScope = null) {
            bool needsCopy;
            return GetTypesWorker(accessor, declaringScope, out needsCopy);
        }

        private IAnalysisSet GetTypesWorker(IProjectEntry accessor, IProjectEntry declaringScope, out bool needsCopy) {
            needsCopy = false;
            var res = _emptySet;
            if (_dependencies.Count != 0) {
                SingleDict<IVersioned, T>.SingleDependency oneDependency;
                if (_dependencies.TryGetSingleDependency(out oneDependency)) {
                    if (oneDependency.Value.Types.Count > 0 && IsVisible(accessor, declaringScope, oneDependency.Key)) {
                        var types = oneDependency.Value.Types;
                        if (types != null) {
                            needsCopy = !(types is IImmutableAnalysisSet);
                            res = types;
                        }
                    }
                } else {
                    foreach (var kvp in (AnalysisDictionary<IVersioned, T>)_dependencies._data) {
                        if (kvp.Value.Types.Count > 0 && IsVisible(accessor, declaringScope, kvp.Key)) {
                            res = res.Union(kvp.Value.Types);
                        }
                    }
                }
            }

            if (res.Count > HARD_TYPE_LIMIT) {
                ExceedsTypeLimit();
            }

            return res;
        }

        public bool HasTypes {
            get {
                if (_dependencies.Count == 0) {
                    return false;
                }
                if (_cache?.Count > 0) {
                    return true;
                }

                T oneDependency;
                if (_dependencies.TryGetSingleValue(out oneDependency)) {
                    return oneDependency.Types.Count > 0;
                } else {
                    foreach (var mod in ((AnalysisDictionary<IVersioned, T>)_dependencies._data)) {
                        if (mod.Value.Types.Count > 0) {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Returns a possibly mutable hash set of types.  Because the set may be mutable
        /// you can only use this version if you are directly consuming the set and know
        /// that this VariableDef will not be mutated while you would be enumerating over
        /// the resulting set.
        /// </summary>
        public IAnalysisSet TypesNoCopy {
            get {
                var res = _cache;
                if (res != null) {
                    return res;
                }
                res = _emptySet;
                if (_dependencies.Count != 0) {
                    T oneDependency;
                    if (_dependencies.TryGetSingleValue(out oneDependency)) {
                        res = oneDependency.Types ?? AnalysisSet.Empty;
                    } else {

                        foreach (var mod in _dependencies.DictValues) {
                            if (mod.Types.Count > 0) {
                                res = res.Union(mod.Types);
                            }
                        }
                    }
                }

                if (res.Count > HARD_TYPE_LIMIT) {
                    ExceedsTypeLimit();
                }

                return _cache = res;
            }
        }

        /// <summary>
        /// Returns the set of types which currently are stored in the VariableDef.  The
        /// resulting set will not mutate in the future even if the types in the VariableDef
        /// change in the future.
        /// </summary>
        public IAnalysisSet Types => TypesNoCopy;

        public virtual bool IsEphemeral => false;

        /// <summary>
        /// If the number of types associated with this variable exceeds a
        /// given limit, increases the union strength. This will cause more
        /// types to be combined.
        /// </summary>
        /// <param name="typeCount">The number of types at which to increase
        /// union strength.</param>
        /// <param name="extraTypes">A set of types that is about to be added.
        /// The estimated number of types includes these types.</param>
        /// <returns>True if the type set was modified. This may be safely
        /// ignored in many cases, since modifications will reenqueue dependent
        /// units automatically.</returns>
        internal bool MakeUnionStrongerIfMoreThan(int typeCount, IAnalysisSet extraTypes = null) {
            if (EstimateTypeCount(extraTypes) >= typeCount) {
                return MakeUnionStronger();
            }
            return false;
        }

        internal bool MakeUnionStronger() {
            var uc = _emptySet.Comparer as UnionComparer;
            int strength = uc != null ? uc.Strength + 1 : 0;
            return MakeUnion(strength);
        }

        internal bool MakeUnion(int strength) {
            if (strength > UnionStrength) {
                bool anyChanged = false;

                _emptySet = AnalysisSet.CreateUnion(strength);
                foreach (var value in _dependencies.Values) {
                    anyChanged |= value.MakeUnion(strength);
                }

                if (anyChanged) {
                    _cache = null;
                    EnqueueDependents();
                    return true;
                }
            }
            return false;
        }

        internal int UnionStrength {
            get {
                var uc = _emptySet.Comparer as UnionComparer;
                return uc != null ? uc.Strength : -1;
            }
        }
    }

    class TypedDef : TypedDef<TypedDependencyInfo> {
        internal static TypedDef[] EmptyArray = new TypedDef[0];

        public TypedDef() {
        }

        protected override TypedDependencyInfo NewDefinition(int version) {
            return new TypedDependencyInfo(version, _emptySet);
        }

        /// <summary>
        /// Returns an infinite sequence of VariableDef instances. This can be
        /// used with .Take(x).ToArray() to create an array of x instances.
        /// </summary>
        internal static IEnumerable<TypedDef> Generator {
            get {
                while (true) {
                    yield return new TypedDef();
                }
            }
        }

    }

    class VariableDef : TypedDef<ReferenceableDependencyInfo>, IReferenceable {
        internal static VariableDef[] EmptyArray = new VariableDef[0];

#if VARDEF_STATS
        ~VariableDef() {
            if (_dependencies.Count == 0) {
                IncStat("NoDeps");
            } else {
                IncStat("TypeCount_{0:D3}".FormatInvariant(Types.Count));
                IncStat("DepCount_{0:D3}".FormatInvariant(_dependencies.Count));
                IncStat(
                    "TypeXDepCount_{0:D3},{1:D3}".FormatInvariant(
                        Types.Count,
                        _dependencies.Count
                    )
                );
                IncStat("References_{0:D3}".FormatInvariant(References.Count()));
                IncStat("Assignments_{0:D3}".FormatInvariant(Definitions.Count()));
                foreach (var dep in _dependencies.Values) {
                    IncStat("DepUnits_{0:D3}".FormatInvariant(dep.DependentUnits.MaybeEnumerate().Count()));
                }
            }
        }
#endif

        internal static IEnumerable<VariableDef> Generator {
            get {
                while (true) {
                    yield return new VariableDef();
                }
            }
        }


        /// <summary>
        /// Checks to see if a variable still exists.  This depends upon the variable not
        /// being ephemeral and that we still have valid type information for dependents.
        /// </summary>
        public bool VariableStillExists {
            get {
                if (!IsEphemeral) {
                    if (HasTypes) {
                        return true;
                    }

                    foreach (var dep in _dependencies) {
                        if (dep.Value.Assignments != null) {
                            return true;
                        }
                    }
                }
                return false;
            }
        }

        internal bool CopyTo(VariableDef to) {
            bool anyChange = false;
            Debug.Assert(this != to);
            foreach (var keyValue in _dependencies) {
                var projEntry = keyValue.Key;
                var dependencies = keyValue.Value;

                anyChange |= to.AddTypes(projEntry, dependencies.Types, false);
                foreach (var unit in dependencies.DependentUnits) {
                    anyChange |= to.AddDependency(unit);
                }
                foreach (var encodedLoc in dependencies._references) {
                    anyChange |= to.AddReference(encodedLoc, projEntry);
                }
                foreach (var assignment in dependencies._assignments) {
                    anyChange |= to.AddAssignment(assignment, projEntry);
                }
            }
            return anyChange;
        }

        protected override ReferenceableDependencyInfo NewDefinition(int version) {
            return new ReferenceableDependencyInfo(version, _emptySet);
        }

        public bool AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                var deps = GetDependentItems(unit.DependencyProject);
                return deps.AddReference(new EncodedLocation(unit, node)) && deps.AddDependentUnit(unit);
            }
            return false;
        }

        public bool AddReference(EncodedLocation location, IVersioned module) {
            return GetDependentItems(module).AddReference(location);
        }

        public bool AddAssignment(EncodedLocation location, IVersioned entry) {
            return GetDependentItems(entry).AddAssignment(location);
        }

        public bool AddAssignment(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                return AddAssignment(new EncodedLocation(unit, node), unit.DependencyProject);
            }
            return false;
        }

        public virtual IEnumerable<EncodedLocation> References {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.References != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.References) {
                                yield return reference;
                            }
                        }
                    }
                }
            }
        }

        public virtual IEnumerable<EncodedLocation> Definitions {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.Assignments != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.Assignments) {
                                yield return reference;
                            }
                        }
                    }
                }
            }
        }

        internal virtual bool IsAlwaysAssigned { get; set; }

        internal virtual bool IsAssigned => IsAlwaysAssigned || _dependencies.Any(d => d.Value._assignments.Count != 0);

#if VARDEF_STATS
        internal static Dictionary<string, int> _variableDefStats = new Dictionary<string, int>();

        private static void IncStat(string stat) {
            if (_variableDefStats.ContainsKey(stat)) {
                _variableDefStats[stat] += 1;
            } else {
                _variableDefStats[stat] = 1;
            }
        }

        internal static void DumpStats() {
            for (int i = 0; i < 3; i++) {
                System.GC.Collect(2, System.GCCollectionMode.Forced);
                System.GC.WaitForPendingFinalizers();
            }

            List<string> values = new List<string>();
            foreach (var keyValue in _variableDefStats) {
                values.Add("{0}: {1}".FormatInvariant(keyValue.Key, keyValue.Value));
            }
            values.Sort();
            foreach (var value in values) {
                System.Console.WriteLine(value);
            }
        }
#endif
    }


    /// <summary>
    /// A variable def which was created on a read.  We need to create a variable def when
    /// we read from a class/instance where the member isn't defined yet - that lets us successfully
    /// get all of the references back if there is later an assignment.  But if there are
    /// no assignments then the variable doesn't really exist and we won't list it in the available members.
    /// </summary>
    sealed class EphemeralVariableDef : VariableDef {
        public override bool IsEphemeral {
            get {
                return !HasTypes;
            }
        }
    }

    /// <summary>
    /// A variable def which has a specific location where it is defined (currently just function parameters).
    /// </summary>
    class LocatedVariableDef : VariableDef {
        public LocatedVariableDef(ProjectEntry entry, EncodedLocation location) {
            Entry = entry;
            Location = location;
            DeclaringVersion = entry.AnalysisVersion;
        }

        public LocatedVariableDef(ProjectEntry entry, EncodedLocation location, VariableDef copy) {
            Entry = entry;
            Location = location;
            _dependencies = copy._dependencies;
            DeclaringVersion = entry.AnalysisVersion;
        }

        public int DeclaringVersion { get; set; }
        public ProjectEntry Entry { get; }
        public EncodedLocation Location { get; set; }
        internal override bool IsAssigned => true;

        public override IEnumerable<EncodedLocation> Definitions =>
            Enumerable.Repeat(Location, 1).Concat(base.Definitions);
    }

}
