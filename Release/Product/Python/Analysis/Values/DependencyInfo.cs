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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;

namespace Microsoft.PythonTools.Analysis.Values {
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
        internal Dictionary<Namespace, INamespaceSet> KeyValues = new Dictionary<Namespace, INamespaceSet>();

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

            var matches = new Dictionary<Namespace, List<KeyValuePair<Namespace, INamespaceSet>>>(cmp);
            foreach (var keyValue in KeyValues) {
                List<KeyValuePair<Namespace, INamespaceSet>> values;
                if (!matches.TryGetValue(keyValue.Key, out values)) {
                    values = matches[keyValue.Key] = new List<KeyValuePair<Namespace, INamespaceSet>>();
                }
                values.Add(keyValue);
            }

            KeyValues = new Dictionary<Namespace, INamespaceSet>(cmp);
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

    internal class TypedDependencyInfo<T> : DependencyInfo where T : Namespace {
        private INamespaceSet _types;
        public ISet<EncodedLocation> _references, _assignments;

        public TypedDependencyInfo(int version)
            : this(version, NamespaceSet.Empty) { }

        public TypedDependencyInfo(int version, INamespaceSet emptySet)
            : base(version) {
            _types = emptySet;
        }

        public bool AddType(Namespace ns) {
            bool wasChanged;
            _types = _types.Add(ns, out wasChanged);
            return wasChanged;
        }

        internal bool MakeUnion(int strength) {
            bool wasChanged;
            _types = _types.AsUnion(strength, out wasChanged);
            return wasChanged;
        }

        public INamespaceSet ToImmutableTypeSet() {
            return _types.Clone();
        }

        public INamespaceSet Types {
            get {
                return _types;
            }
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
