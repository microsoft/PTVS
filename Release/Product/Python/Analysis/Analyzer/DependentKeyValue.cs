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
    /// Like a VariableDef, but used for tracking key/value pairs in a dictionary.
    /// 
    /// The full key/value pair represenets the dependent data, and just like a VariableDef
    /// it's added based upon the project entry which is being analyzed.
    /// 
    /// This ultimately enables us to resolve an individual key back to a specific
    /// value when individual keys are being used.  That lets us preserve strong type
    /// information across dictionaries which are polymorphic based upon their keys.
    /// 
    /// This works best for dictionaries whoses keys are objects that we closel track.
    /// Currently that includes strings, types, small integers, etc...
    /// </summary>
    class DependentKeyValue : DependentData<KeyValueDependencyInfo> {
        private static Dictionary<Namespace, INamespaceSet> EmptyDict = new Dictionary<Namespace, INamespaceSet>();
        private INamespaceSet _allValues;

        protected override KeyValueDependencyInfo NewDefinition(int version) {
            return new KeyValueDependencyInfo(version);
        }

        public bool AddTypes(AnalysisUnit unit, IEnumerable<Namespace> keyTypes, IEnumerable<Namespace> valueTypes, bool enqueue = true) {
            return AddTypes(unit.ProjectEntry, unit.ProjectState, keyTypes, valueTypes, enqueue);
        }

        public bool AddTypes(IProjectEntry projectEntry, PythonAnalyzer projectState, IEnumerable<Namespace> keyTypes, IEnumerable<Namespace> valueTypes, bool enqueue = true) {
            var dependencies = GetDependentItems(projectEntry);

            if (dependencies.KeyValues.Count > projectState.Limits.DictKeyTypes) {
                dependencies.MakeUnionStronger();
            }

            bool anyAdded = false;
            foreach (var key in keyTypes) {
                INamespaceSet values;
                if (!dependencies.KeyValues.TryGetValue(key, out values)) {
                    values = NamespaceSet.Create(valueTypes);
                    anyAdded = true;
                } else {
                    bool added;
                    values = values.Union(valueTypes, out added);
                    anyAdded |= added;
                }
                if (anyAdded && values.Count > projectState.Limits.DictValueTypes) {
                    values = values.AsStrongerUnion();
                }
                dependencies.KeyValues[key] = values;
            }

            if (anyAdded) {
                _allValues = null;
            }
            if (anyAdded && enqueue) {
                EnqueueDependents();
            }

            return anyAdded;
        }

        public INamespaceSet KeyTypes {
            get {
                if (_dependencies.Count == 0) {
                    return NamespaceSet.Empty;
                }

                var res = NamespaceSet.Empty; ;
                foreach (var keyValue in _dependencies.Values) {
                    res = res.Union(keyValue.KeyValues.Keys);
                }

                return res;
            }
        }

        public INamespaceSet AllValueTypes {
            get {
                if (_allValues != null) {
                    return _allValues;
                }
                if (_dependencies.Count == 0) {
                    return NamespaceSet.Empty;
                }

                var res = NamespaceSet.Empty;
                foreach (var dependency in _dependencies.Values) {
                    foreach (var keyValue in dependency.KeyValues) {
                        res = res.Union(keyValue.Value);
                    }
                }

                _allValues = res;
                return res;
            }
        }

        public INamespaceSet GetValueType(INamespaceSet keyTypes) {
            var res = NamespaceSet.Empty;
            if (_dependencies.Count != 0) {
                Namespace ns = keyTypes as Namespace;
                foreach (var keyValue in _dependencies.Values) {
                    INamespaceSet union;
                    if (ns != null) {
                        // optimize for the case where we're just looking up
                        // a single Namespace object which hasn't been copied into
                        // a set
                        if (keyValue.KeyValues.TryGetValue(ns, out union)) {
                            res = res.Union(union);
                        }
                    } else {
                        foreach (var keyType in keyTypes) {
                            if (keyValue.KeyValues.TryGetValue(keyType, out union)) {
                                res = res.Union(union);
                            }
                        }
                    }
                }

                if (res == null || res.Count == 0) {
                    // This isn't ideal, but it's the best we can do for now.  The problem is
                    // that we are potentially returning AllValueTypes much too early.  We could
                    // later receive a key which would satisfy getting this value type.  If that
                    // happens we will re-analyze the code which is doing this get, but because
                    // we've already returned AllValueTypes the code has been analyzed with
                    // more types then it can really receive.  But currently we have no way
                    // to either remove the types that were previously returned, and we have
                    // no way to schedule the re-analysis of the code which is doing the get
                    // after we've completed the analysis.  
                    return AllValueTypes;
                }
            }

            return res ?? NamespaceSet.Empty;
        }

        public Dictionary<Namespace, INamespaceSet> KeyValueTypes {
            get {
                if (_dependencies.Count != 0) {
                    Dictionary<Namespace, INamespaceSet> res = null;
                    foreach (var mod in _dependencies.Values) {
                        if (res == null) {
                            res = new Dictionary<Namespace, INamespaceSet>();
                            foreach (var keyValue in mod.KeyValues) {
                                res[keyValue.Key] = keyValue.Value;
                            }
                        } else {
                            foreach (var keyValue in mod.KeyValues) {
                                INamespaceSet existing;
                                if (!res.TryGetValue(keyValue.Key, out existing)) {
                                    res[keyValue.Key] = keyValue.Value;
                                } else {
                                    res[keyValue.Key] = existing.Union(keyValue.Value, canMutate: false);
                                }
                            }
                        }
                    }
                    return res ?? EmptyDict;
                }

                return EmptyDict;
            }
        }

        /// <summary>
        /// Copies the key/value types from the provided DependentKeyValue into this
        /// DependentKeyValue.
        /// </summary>
        internal bool CopyFrom(DependentKeyValue dependentKeyValue, bool enqueue = true) {
            bool anyAdded = false;
            foreach (var otherDependency in dependentKeyValue._dependencies) {
                var deps = GetDependentItems(otherDependency.Key);

                // TODO: Is this correct?
                if (deps == otherDependency.Value) {
                    continue;
                }

                foreach (var keyValue in otherDependency.Value.KeyValues) {
                    INamespaceSet union;
                    if (!deps.KeyValues.TryGetValue(keyValue.Key, out union)) {
                        deps.KeyValues[keyValue.Key] = union = keyValue.Value;
                        anyAdded = true;
                    } else {
                        bool added;
                        deps.KeyValues[keyValue.Key] = union.Union(keyValue.Value, out added, canMutate: false);
                        anyAdded |= added;
                    }
                }
            }

            if (anyAdded && enqueue) {
                EnqueueDependents();
            }
            return anyAdded;
        }

        /// <summary>
        /// Copies all of our key types into the provided VariableDef.
        /// </summary>
        internal void CopyKeysTo(VariableDef to) {
            bool added = false;
            foreach (var dependency in _dependencies) {
                added |= to.AddTypes(dependency.Key, dependency.Value.KeyValues.Keys);
            }

            if (added) {
                EnqueueDependents();
            }
        }

        /// <summary>
        /// Copies all of our value types into the provided VariableDef.
        /// </summary>
        internal void CopyValuesTo(VariableDef to) {
            bool added = false;
            foreach (var dependency in _dependencies) {
                foreach (var value in dependency.Value.KeyValues) {
                    added |= to.AddTypes(dependency.Key, value.Value);
                }
            }

            if (added) {
                EnqueueDependents();
            }
        }
    }

}
