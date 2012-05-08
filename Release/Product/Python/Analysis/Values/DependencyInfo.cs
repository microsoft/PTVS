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
            HashSetExtensions.AddValue(ref _dependentUnits, unit);
        }

        public int Version {
            get {
                return _version;
            }
        }
    }

    internal class TypedDependencyInfo<T> : DependencyInfo where T : Namespace {
        private TypeUnion<T> _union;
        public ISet<EncodedLocation> _references, _assignments;

        public TypedDependencyInfo(int version)
            : base(version) {
        }

        public TypeUnion<T> Types {
            get {
                if (_union == null) {
                    _union = new TypeUnion<T>();
                }
                return _union;
            }
            set {
                _union = value;
            }
        }

        public bool HasTypes {
            get {
                return _union != null && _union.Count > 0;
            }
        }

        public bool HasReferences {
            get {
                return _references != null;
            }
        }

        public void AddReference(EncodedLocation location) {
            HashSetExtensions.AddValue(ref _references, location);
        }

        public ISet<EncodedLocation> References {
            get {
                return _references;
            }
            set {
                _references = value;
            }
        }

        public bool HasAssignments {
            get {
                return _assignments != null;
            }
        }

        public void AddAssignment(EncodedLocation location) {
            HashSetExtensions.AddValue(ref _assignments, location);
        }

        public ISet<EncodedLocation> Assignments {
            get {
                return _assignments;
            }
            set {
                _assignments = value;
            }
        }
    }
}
