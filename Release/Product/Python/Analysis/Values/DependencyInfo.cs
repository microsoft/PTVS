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

        public void AddDependentUnit(AnalysisUnit unit) {
            AddValue(ref _dependentUnits, unit);
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
        internal Dictionary<Namespace, TypeUnion> KeyValues = new Dictionary<Namespace,TypeUnion>();

        public KeyValueDependencyInfo(int version)
            : base(version) {
        }

    }

    internal class TypedDependencyInfo<T> : DependencyInfo where T : Namespace {
        private ISet<Namespace> _types;
        public ISet<EncodedLocation> _references, _assignments;

        public TypedDependencyInfo(int version)
            : base(version) {
        }

        public bool AddType(Namespace ns) {
            return TypeUnion.Add(ref _types, ns);
        }

        public ISet<Namespace> ToImmutableTypeSet() {
            if (_types == null) {
                return EmptySet<Namespace>.Instance;
            } else if (_types is Namespace || _types is SetOfTwo<Namespace>) {
                return _types;
            }

            return new HashSet<Namespace>(_types);
        }

        public ISet<Namespace> Types {
            get {
                return _types;
            }
        }

        public bool HasTypes {
            get {
                return _types != null;
            }
        }

        public void AddReference(EncodedLocation location) {
            HashSetExtensions.AddValue(ref _references, location);
        }

        public IEnumerable<EncodedLocation> References {
            get {
                return _references;
            }
        }

        public void AddAssignment(EncodedLocation location) {
            HashSetExtensions.AddValue(ref _assignments, location);
        }

        public IEnumerable<EncodedLocation> Assignments {
            get {
                return _assignments;
            }
        }
    }
}
