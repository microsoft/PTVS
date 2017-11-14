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
using System.Linq;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    /// <summary>
    /// contains information about dependencies.  Each DependencyInfo is 
    /// attached to a VariableRef in a dictionary keyed off of the ProjectEntry.
    /// 
    /// Module -> The Module this DependencyInfo object tracks.
    /// DependentUnits -> What needs to change if this VariableRef is updated.
    /// Types -> Types that this VariableRef has received from the Module.
    /// </summary>
    internal class DependencyInfo {
        private readonly int _version;
        private ISet<AnalysisUnit> _dependentUnits;

        public DependencyInfo(int version) {
            _version = version;
        }

        public ISet<AnalysisUnit> DependentUnits {
            get {
                return _dependentUnits; 
            }
        }

        public bool AddDependentUnit(AnalysisUnit unit) {
            return AddValue(ref _dependentUnits, unit);
        }

        public int Version {
            get {
                return _version;
            }
        }

        internal static bool AddValue(ref ISet<AnalysisUnit> references, AnalysisUnit value) {
            AnalysisUnit prevNs;
            SetOfTwo<AnalysisUnit> prevSetOfTwo;
            if (references == null) {
                references = value;
                return true;
            } else if ((prevNs = references as AnalysisUnit) != null) {
                if (references != value) {
                    references = new SetOfTwo<AnalysisUnit>(prevNs, value);
                    return true;
                }
            } else if ((prevSetOfTwo = references as SetOfTwo<AnalysisUnit>) != null) {
                if (value != prevSetOfTwo.Value1 && value != prevSetOfTwo.Value2) {
                    references = new HashSet<AnalysisUnit>(prevSetOfTwo);
                    references.Add(value);
                    return true;
                }
            } else {
                return references.Add(value);
            }
            return false;
        }
    }

    internal class KeyValueDependencyInfo : DependencyInfo {
        internal Dictionary<AnalysisValue, IAnalysisSet> KeyValues = new Dictionary<AnalysisValue, IAnalysisSet>();

        public KeyValueDependencyInfo(int version)
            : base(version) {
        }

        internal void MakeUnionStronger() {
            var cmp = KeyValues.Comparer as UnionComparer;
            if (cmp != null && cmp.Strength == UnionComparer.MAX_STRENGTH) {
                return;
            }
            if (cmp == null) {
                cmp = UnionComparer.Instances[0];
            } else {
                cmp = UnionComparer.Instances[cmp.Strength + 1];
            }

            var matches = new Dictionary<AnalysisValue, List<KeyValuePair<AnalysisValue, IAnalysisSet>>>(cmp);
            foreach (var keyValue in KeyValues) {
                List<KeyValuePair<AnalysisValue, IAnalysisSet>> values;
                if (!matches.TryGetValue(keyValue.Key, out values)) {
                    values = matches[keyValue.Key] = new List<KeyValuePair<AnalysisValue, IAnalysisSet>>();
                }
                values.Add(keyValue);
            }

            KeyValues = new Dictionary<AnalysisValue, IAnalysisSet>(cmp);
            foreach (var list in matches.Values) {
                bool dummy;
                var key = list[0].Key;
                var value = list[0].Value.AsUnion(cmp, out dummy);

                foreach (var item in list.Skip(1)) {
                    key = cmp.MergeTypes(key, item.Key, out dummy);
                    value = value.Union(item.Value);
                }

                KeyValues[key] = value;
            }
        }
    }

    internal class TypedDependencyInfo : DependencyInfo {
        private IAnalysisSet _types;
#if FULL_VALIDATION
        internal int _changeCount = 0;
#endif

        public TypedDependencyInfo(int version)
            : this(version, AnalysisSet.Empty) { }

        public TypedDependencyInfo(int version, IAnalysisSet emptySet)
            : base(version) {
            _types = emptySet;
        }

        static bool TAKE_COPIES = false;

        public bool AddType(AnalysisValue ns) {
            if (!ns.IsCurrent) {
                return false;
            }

            bool wasChanged;
            IAnalysisSet prev;
            if (TAKE_COPIES) {
                prev = _types.Clone();
            } else {
                prev = _types;
            }
            if (prev.Any(av => !av.IsCurrent)) {
                _types = AnalysisSet.Create(prev.Where(av => av.IsCurrent), prev.Comparer).Add(ns, out wasChanged);
            } else {
                _types = prev.Add(ns, out wasChanged);
            }
#if FULL_VALIDATION
            _changeCount += wasChanged ? 1 : 0;
            // The value doesn't mean anything, we just want to know if a variable is being
            // updated too often.
            Validation.Assert<ChangeCountExceededException>(_changeCount < 10000);
#endif
            return wasChanged;
        }

        internal bool MakeUnion(int strength) {
            bool wasChanged;
            _types = _types.AsUnion(strength, out wasChanged);
            return wasChanged;
        }

        public IAnalysisSet ToImmutableTypeSet() {
            return _types.Clone();
        }

        public IAnalysisSet Types {
            get {
                return _types;
            }
        }

    }

    internal class ReferenceableDependencyInfo : TypedDependencyInfo {
        public ISet<EncodedLocation> _references, _assignments;

        public ReferenceableDependencyInfo(int version)
            : base(version) { }

        public ReferenceableDependencyInfo(int version, IAnalysisSet emptySet)
            : base(version, emptySet) {
        }


        public bool AddReference(EncodedLocation location) {
            return HashSetExtensions.AddValue(ref _references, location);
        }

        public IEnumerable<EncodedLocation> References {
            get {
                return _references;
            }
        }

        public bool AddAssignment(EncodedLocation location) {
            return HashSetExtensions.AddValue(ref _assignments, location);
        }

        public IEnumerable<EncodedLocation> Assignments {
            get {
                return _assignments;
            }
        }
    }
}
