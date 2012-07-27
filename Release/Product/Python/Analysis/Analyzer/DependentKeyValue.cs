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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

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
        private static Dictionary<Namespace, HashSet<Namespace>> EmptyDict = new Dictionary<Namespace, HashSet<Namespace>>();
        private ISet<Namespace> _allValues;

        protected override KeyValueDependencyInfo NewDefinition(int version) {
            return new KeyValueDependencyInfo(version);
        }

        public bool AddTypes(AnalysisUnit unit, IEnumerable<Namespace> keyTypes, IEnumerable<Namespace> valueTypes, bool enqueue = true) {
            return AddTypes(unit.ProjectEntry, keyTypes, valueTypes, enqueue);
        }

        public bool AddTypes(IProjectEntry projectEntry, IEnumerable<Namespace> keyTypes, IEnumerable<Namespace> valueTypes, bool enqueue = true) {
            var dependencies = GetDependentItems(projectEntry);

            bool added = false;
            foreach (var key in keyTypes) {
                TypeUnion<Namespace> values;
                if (!dependencies.KeyValues.TryGetValue(key, out values)) {
                    dependencies.KeyValues[key] = values = new TypeUnion<Namespace>();
                    added = true;
                }

                foreach (var value in valueTypes) {
                    if (values.Add(value)) {
                        added = true;
                        _allValues = null;
                    }
                }
            }

            if (added && enqueue) {
                EnqueueDependents();
            }

            return added;
        }

        public ISet<Namespace> KeyTypes {
            get {
                if (_dependencies.Count == 0) {
                    return EmptySet<Namespace>.Instance;
                }

                HashSet<Namespace> res = null;
                foreach (var keyValue in _dependencies.Values) {
                    if (res == null) {
                        res = new HashSet<Namespace>(keyValue.KeyValues.Keys);
                    } else {
                        res.UnionWith(keyValue.KeyValues.Keys);
                    }
                }

                return (ISet<Namespace>)res ?? EmptySet<Namespace>.Instance;
            }
        }

        public ISet<Namespace> AllValueTypes {
            get {
                if (_allValues != null) {
                    return _allValues;
                }
                if (_dependencies.Count == 0) {
                    return EmptySet<Namespace>.Instance;
                }

                HashSet<Namespace> res = null;
                foreach (var dependency in _dependencies.Values) {
                    foreach (var keyValue in dependency.KeyValues) {
                        if (res == null) {
                            res = new HashSet<Namespace>(keyValue.Value.ToSetNoCopy());
                        } else {
                            res.UnionWith(keyValue.Value.ToSetNoCopy());
                        }
                    }
                }

                return (_allValues = (ISet<Namespace>)res ?? EmptySet<Namespace>.Instance);
            }
        }

        public ISet<Namespace> GetValueType(ISet<Namespace> keyTypes) {
            ISet<Namespace> res = null;
            if (_dependencies.Count != 0) {
                Namespace ns = keyTypes as Namespace;
                foreach (var keyValue in _dependencies.Values) {
                    TypeUnion<Namespace> union;
                    if (ns != null) {
                        // optimize for the case where we're just looking up
                        // a single Namespace object which hasn't been copied into
                        // a set
                        if (keyValue.KeyValues.TryGetValue(ns, out union)) {
                            res = UpdateSet(res, union);
                        }
                    } else {
                        foreach (var keyType in keyTypes) {
                            if (keyValue.KeyValues.TryGetValue(keyType, out union)) {
                                res = UpdateSet(res, union);
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

            return res ?? EmptySet<Namespace>.Instance;
        }

        private static ISet<Namespace> UpdateSet(ISet<Namespace> res, TypeUnion<Namespace> union) {
            if (res == null) {
                res = union.ToSet();
            } else {
                if (!(res is HashSet<Namespace>)) {
                    res = new HashSet<Namespace>(res);
                }
                res.UnionWith(union);
            }
            return res;
        }

        public Dictionary<Namespace, HashSet<Namespace>> KeyValueTypes {
            get {
                if (_dependencies.Count != 0) {
                    Dictionary<Namespace, HashSet<Namespace>> res = null;
                    foreach (var mod in _dependencies.Values) {
                        if (res == null) {
                            res = new Dictionary<Namespace, HashSet<Namespace>>();
                            foreach (var keyValue in mod.KeyValues) {
                                res[keyValue.Key] = new HashSet<Namespace>(keyValue.Value);
                            }
                        } else {
                            foreach (var keyValue in mod.KeyValues) {
                                HashSet<Namespace> existing;
                                if (!res.TryGetValue(keyValue.Key, out existing)) {
                                    res[keyValue.Key] = new HashSet<Namespace>(keyValue.Value);
                                } else {
                                    foreach (var value in keyValue.Value) {
                                        existing.Add(value);
                                    }
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
        internal void CopyFrom(DependentKeyValue dependentKeyValue) {
            bool added = false;
            foreach (var otherDependency in dependentKeyValue._dependencies) {
                var deps = GetDependentItems(otherDependency.Key);

                foreach (var keyValue in otherDependency.Value.KeyValues) {
                    TypeUnion<Namespace> union;
                    if (!deps.KeyValues.TryGetValue(keyValue.Key, out union)) {
                        deps.KeyValues[keyValue.Key] = union = new TypeUnion<Namespace>();
                    }

                    foreach (var type in keyValue.Value) {
                        if (union.Add(type)) {
                            added = true;
                        }
                    }
                }
            }

            if (added) {
                EnqueueDependents();
            }
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
