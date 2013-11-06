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

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    abstract class DependentData<TStorageType> where TStorageType : DependencyInfo {
        internal SingleDict<IProjectEntry, TStorageType> _dependencies;

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
        public void ClearOldValues(IProjectEntry fromModule) {
            TStorageType deps;
            if (_dependencies.TryGetValue(fromModule, out deps)) {
                if (deps.Version != fromModule.AnalysisVersion) {
                    _dependencies.Remove(fromModule);
                }
            }
        }

        protected TStorageType GetDependentItems(IProjectEntry module) {
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
        public void EnqueueDependents() {
            bool hasOldValues = false;
            foreach (var keyValue in _dependencies) {
                if (keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                    var val = keyValue.Value;
                    if (val.DependentUnits != null) {
                        foreach (var analysisUnit in val.DependentUnits) {
                            analysisUnit.Enqueue();
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
                return GetDependentItems(unit.DeclaringModule.ProjectEntry).AddDependentUnit(unit);
            }
            return false;
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
    class VariableDef : DependentData<TypedDependencyInfo<AnalysisValue>>, IReferenceable {
        internal static VariableDef[] EmptyArray = new VariableDef[0];

        /// <summary>
        /// Returns an infinite sequence of VariableDef instances. This can be
        /// used with .Take(x).ToArray() to create an array of x instances.
        /// </summary>
        internal static IEnumerable<VariableDef> Generator {
            get {
                while (true) {
                    yield return new VariableDef();
                }
            }
        }

        /// <summary>
        /// This limit is used to prevent analysis from continuing forever due
        /// to bugs or unanalyzable code. It is tested in Types and
        /// TypesNoCopy, where an accurate type count is available without
        /// requiring extra computation, and variables exceeding the limit are
        /// added to LockedVariableDefs. AddTypes is a no-op for instances in
        /// this set.
        /// </summary>
        internal const int HARD_TYPE_LIMIT = 1000;

        static readonly ConditionalWeakTable<VariableDef, object> LockedVariableDefs = new ConditionalWeakTable<VariableDef, object>();
        static readonly object LockedVariableDefsValue = new object();

        private IAnalysisSet _emptySet = AnalysisSet.Empty;

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
                    var str = type.ToString();
                    int count;
                    if (!typeCounts.TryGetValue(str, out count)) {
                        count = 0;
                    }
                    typeCounts[str] = count + 1;
                    total += 1;
                }
                var typeCountList = typeCounts.OrderByDescending(kv => kv.Value).Select(kv => string.Format("{0}x {1}", kv.Value, kv.Key)).ToList();
                Debug.Write(string.Format("{0} exceeded type limit.\nStack trace:\n{1}\nContents:\n    Count = {2}\n    {3}\n",
                    GetType().Name,
                    new StackTrace(true),
                    total,
                    string.Join("\n    ", typeCountList)));
                AnalysisLog.ExceedsTypeLimit(GetType().Name, total, string.Join(", ", typeCountList));
            }
        }

#if VARDEF_STATS
        internal static Dictionary<string, int> _variableDefStats = new Dictionary<string, int>();

        ~VariableDef() {
            if (_dependencies.Count == 0) {
                IncStat("NoDeps");
            } else {
                IncStat(String.Format("TypeCount_{0:D3}", Types.Count));
                IncStat(String.Format("DepCount_{0:D3}", _dependencies.Count));
                IncStat(
                    String.Format(
                        "TypeXDepCount_{0:D3},{1:D3}", 
                        Types.Count, 
                        _dependencies.Count
                    )
                );
                IncStat(String.Format("References_{0:D3}", References.Count()));
                IncStat(String.Format("Assignments_{0:D3}", Definitions.Count()));
                foreach (var dep in _dependencies.Values) {
                    IncStat(String.Format("DepUnits_{0:D3}", dep.DependentUnits == null ? 0 : dep.DependentUnits.Count));
                }
            }
        }

        private static void IncStat(string stat) {
            if (_variableDefStats.ContainsKey(stat)) {
                _variableDefStats[stat] += 1;
            } else {
                _variableDefStats[stat] = 1;
            }
        }

        internal static void DumpStats() {
            for (int i = 0; i < 3; i++) {
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
            }

            List<string> values = new List<string>();
            foreach (var keyValue in VariableDef._variableDefStats) {
                values.Add(String.Format("{0}: {1}", keyValue.Key, keyValue.Value));
            }
            values.Sort();
            foreach (var value in values) {
                Console.WriteLine(value);
            }
        }
#endif

        protected override TypedDependencyInfo<AnalysisValue> NewDefinition(int version) {
            return new TypedDependencyInfo<AnalysisValue>(version, _emptySet);
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

        public bool AddTypes(AnalysisUnit unit, IEnumerable<AnalysisValue> newTypes, bool enqueue = true) {
            return AddTypes(unit.ProjectEntry, newTypes, enqueue);
        }

        // Set checks ensure that the wasChanged result is correct. The checks
        // are memory intensive, since they perform the add an extra time. The
        // flag is static but non-const to allow it to be enabled while
        // debugging.
#if FULL_VALIDATION || DEBUG
        private static bool ENABLE_SET_CHECK = false;
#endif

        public bool AddTypes(IProjectEntry projectEntry, IEnumerable<AnalysisValue> newTypes, bool enqueue = true) {
            object dummy;
            if (LockedVariableDefs.TryGetValue(this, out dummy)) {
                return false;
            }
            
            bool added = false;
            foreach (var value in newTypes) {
                var declaringModule = value.DeclaringModule;
                if (declaringModule == null || declaringModule.AnalysisVersion == value.DeclaringVersion) {
                    var newTypesEntry = value.DeclaringModule ?? projectEntry;

                    var dependencies = GetDependentItems(newTypesEntry);

#if DEBUG || FULL_VALIDATION
                    if (ENABLE_SET_CHECK) {
                        bool testAdded;
                        var original = dependencies.ToImmutableTypeSet();
                        var afterAdded = original.Add(value, out testAdded, false);
                        if (afterAdded.Comparer == original.Comparer) {
                            if (testAdded) {
                                Validation.Assert(!ObjectComparer.Instance.Equals(afterAdded, original));
                            } else {
                                Validation.Assert(ObjectComparer.Instance.Equals(afterAdded, original));
                            }
                        }
                    }
#endif

                    if (dependencies.AddType(value)) {
                        added = true;
                    }
                }
            }

            if (added && enqueue) {
                EnqueueDependents();
            }

            return added;
        }

        /// <summary>
        /// Returns a possibly mutable hash set of types.  Because the set may be mutable
        /// you can only use this version if you are directly consuming the set and know
        /// that this VariableDef will not be mutated while you would be enumerating over
        /// the resulting set.
        /// </summary>
        public IAnalysisSet TypesNoCopy {
            get {
                var res = _emptySet;
                if (_dependencies.Count != 0) {
                    TypedDependencyInfo<AnalysisValue> oneDependency;
                    if (_dependencies.TryGetSingleValue(out oneDependency)) {
                        res = oneDependency.Types ?? AnalysisSet.Empty;
                    } else {

                        foreach (var mod in _dependencies.DictValues) {
                            res = res.Union(mod.Types);
                        }
                    }
                }

                if (res.Count > HARD_TYPE_LIMIT) {
                    ExceedsTypeLimit();
                }

                return res;
            }
        }

        /// <summary>
        /// Returns the set of types which currently are stored in the VariableDef.  The
        /// resulting set will not mutate in the future even if the types in the VariableDef
        /// change in the future.
        /// </summary>
        public IAnalysisSet Types {
            get {
                return TypesNoCopy.Clone();
            }
        }

        public bool AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                var deps = GetDependentItems(unit.DeclaringModule.ProjectEntry);
                return deps.AddReference(new EncodedLocation(unit.Tree, node)) && deps.AddDependentUnit(unit);
            }
            return false;
        }

        public bool AddReference(EncodedLocation location, IProjectEntry module) {
            return GetDependentItems(module).AddReference(location);
        }

        public bool AddAssignment(EncodedLocation location, IProjectEntry entry) {
            return GetDependentItems(entry).AddAssignment(location);
        }

        public bool AddAssignment(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                return AddAssignment(new EncodedLocation(unit.Tree, node), unit.DeclaringModule.ProjectEntry);
            }
            return false;
        }

        public IEnumerable<KeyValuePair<IProjectEntry, EncodedLocation>> References {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.References != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.References) {
                                yield return new KeyValuePair<IProjectEntry, EncodedLocation>(keyValue.Key, reference);
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<IProjectEntry, EncodedLocation>> Definitions {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.Assignments != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.Assignments) {
                                yield return new KeyValuePair<IProjectEntry, EncodedLocation>(keyValue.Key, reference);
                            }
                        }
                    }
                }
            }
        }

        public virtual bool IsEphemeral {
            get {
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
                if (dependencies.DependentUnits != null) {
                    foreach (var unit in dependencies.DependentUnits) {
                        anyChange |= to.AddDependency(unit);
                    }
                }
                if (dependencies._references != null) {
                    foreach (var encodedLoc in dependencies._references) {
                        anyChange |= to.AddReference(encodedLoc, projEntry);
                    }
                }
                if (dependencies._assignments != null) {
                    foreach (var assignment in dependencies._assignments) {
                        anyChange |= to.AddAssignment(assignment, projEntry);
                    }
                }
            }
            return anyChange;
        }


        /// <summary>
        /// Checks to see if a variable still exists.  This depends upon the variable not
        /// being ephemeral and that we still have valid type information for dependents.
        /// </summary>
        public bool VariableStillExists {
            get {
                return !IsEphemeral && (_dependencies.Count > 0 || TypesNoCopy.Count > 0);
            }
        }

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

    /// <summary>
    /// A variable def which was created on a read.  We need to create a variable def when
    /// we read from a class/instance where the member isn't defined yet - that lets us successfully
    /// get all of the references back if there is later an assignment.  But if there are
    /// no assignments then the variable doesn't really exist and we won't list it in the available members.
    /// </summary>
    sealed class EphemeralVariableDef : VariableDef {
        public override bool IsEphemeral {
            get {
                return TypesNoCopy.Count == 0;
            }
        }
    }

    /// <summary>
    /// A variable def which has a specific location where it is defined (currently just function parameters).
    /// </summary>
    class LocatedVariableDef : VariableDef {
        private readonly ProjectEntry _entry;
        private int _declaringVersion;
        private Node _location;

        public LocatedVariableDef(ProjectEntry entry, Node location) {
            _entry = entry;
            _location = location;
            _declaringVersion = entry.AnalysisVersion;
        }

        public LocatedVariableDef(ProjectEntry entry, Node location, VariableDef copy) {
            _entry = entry;
            _location = location;
            _dependencies = copy._dependencies;
            _declaringVersion = entry.AnalysisVersion;
        }

        public int DeclaringVersion {
            get {
                return _declaringVersion;
            }
            set {
                _declaringVersion = value;
            }
        }

        public ProjectEntry Entry {
            get {
                return _entry;
            }
        }

        public Node Node {
            get {
                return _location;
            }
            set {
                _location = value;
            }
        }
    }

}
