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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
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

        public void AddDependency(AnalysisUnit unit) {
            if (!unit.ForEval) {
                GetDependentItems(unit.DeclaringModule.ProjectEntry).AddDependentUnit(unit);
            }
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
    /// The collection of type information is represented by a HashSet of Namespace
    /// objects.  This set includes all of the types that are known to have been
    /// seen for this variable.
    /// 
    /// Dependency data is added when an one value is assigned to a variable.  
    /// For example for the statement:
    /// 
    /// foo = value
    /// 
    /// There will be a variable def for the name "foo", and "value" will evaluate
    /// to a collection of namespaces.  When value is assigned to
    /// foo the types in value will be propagated to foo's VariableDef by a call
    /// to AddDependentTypes.  If value adds any new type information to foo
    /// then the caller needs to re-analyze anyone who is dependent upon foo'
    /// s values.  If "value" was a VariableDef as well, rather than some arbitrary 
    /// expression, then reading "value" would have made the code being analyzed dependent 
    /// upon "value".  After a call to AddTypes the caller needs to check the 
    /// return value and if new types were added (returns true) needs to re-enque it's scope.
    /// 
    /// Dependecies are stored in a dictionary keyed off of the IProjectEntry object.
    /// This is a consistent object which always represents the same module even
    /// across multiple analysis.  The object is versioned so that when we encounter
    /// a new version all the old dependencies will be thrown away when a variable ref 
    /// is updated with new dependencies.
    /// 
    /// TODO: We should store built-in types not keyed off of the ModuleInfo.
    /// </summary>
    class VariableDef<T> : DependentData<TypedDependencyInfo<T>>, IReferenceable where T : Namespace {
        public VariableDef() { }

        protected override TypedDependencyInfo<T> NewDefinition(int version) {
            return new TypedDependencyInfo<T>(version);
        }

        public bool AddTypes(AnalysisUnit unit, IEnumerable<T> newTypes, bool enqueue = true) {
            return AddTypes(unit.ProjectEntry, newTypes, enqueue);
        }

        public bool AddTypes(IProjectEntry projectEntry, IEnumerable<T> newTypes, bool enqueue = true) {
            bool added = false;
            foreach (var value in newTypes) {
                var declaringModule = value.DeclaringModule;
                if (declaringModule == null || declaringModule.AnalysisVersion == value.DeclaringVersion) {
                    var dependencies = GetDependentItems(value.DeclaringModule ?? projectEntry);

                    if (dependencies.Types.Add(value)) {
                        added = true;
                    }
                }
            }

            if (added && enqueue) {
                EnqueueDependents();
            }

            return added;
        }

        public ISet<T> Types {
            get {
                if (_dependencies.Count != 0) {
                    TypedDependencyInfo<T> oneDependency;
                    if (_dependencies.TryGetSingleValue(out oneDependency)) {
                        return oneDependency.Types.ToSet();
                    }

                    ISet<T> res = null;
                    foreach (var mod in _dependencies.DictValues) {
                        if (mod.HasTypes) {
                            if (res == null) {
                                res = new HashSet<T>(mod.Types.ToSetNoCopy());
                            } else {
                                res.UnionWith(mod.Types.ToSetNoCopy());
                            }
                        }
                    }
                    return res ?? EmptySet<T>.Instance;
                }

                return EmptySet<T>.Instance;
            }
        }

        public void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                AddReference(new EncodedLocation(unit.Tree, node), unit.DeclaringModule.ProjectEntry).AddDependentUnit(unit);
            }
        }

        public TypedDependencyInfo<T> AddReference(EncodedLocation location, IProjectEntry module) {
            var depUnits = GetDependentItems(module);
            depUnits.AddReference(location);
            return depUnits;
        }

        public void AddAssignment(EncodedLocation location, IProjectEntry entry) {
            var depUnits = GetDependentItems(entry);
            depUnits.AddAssignment(location);
        }

        public void AddAssignment(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                AddAssignment(new EncodedLocation(unit.Tree, node), unit.DeclaringModule.ProjectEntry);
            }
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
    }

    class VariableDef : VariableDef<Namespace> {
        internal static VariableDef[] EmptyArray = new VariableDef[0];

        public virtual bool IsEphemeral {
            get {
                return false;
            }
        }

        internal void CopyTo(VariableDef to) {
            Debug.Assert(this != to);
            foreach (var keyValue in _dependencies) {
                var projEntry = keyValue.Key;
                var dependencies = keyValue.Value;

                to.AddTypes(projEntry, dependencies.Types);
                if (dependencies._references != null) {
                    foreach (var encodedLoc in dependencies._references) {
                        to.AddReference(encodedLoc, projEntry);
                    }
                }
                if (dependencies._assignments != null) {
                    foreach (var assignment in dependencies._assignments) {
                        to.AddAssignment(assignment, projEntry);
                    }
                }
            }
        }


        /// <summary>
        /// Checks to see if a variable still exists.  This depends upon the variable not
        /// being ephemeral and that we still have valid type information for dependents.
        /// </summary>
        public bool VariableStillExists {
            get {
                return !IsEphemeral && (_dependencies.Count > 0 || Types.Count > 0);
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
                return Types.Count == 0;
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

    /// <summary>
    /// Represents a *args parameter for a function definition.  Holds onto a SequenceInfo which
    /// includes all of the types passed in via splatting or extra position arguments.
    /// </summary>
    sealed class ListParameterVariableDef : LocatedVariableDef {
        public SequenceInfo List;

        public ListParameterVariableDef(AnalysisUnit unit, Node location, bool addType = true)
            : base(unit.DeclaringModule.ProjectEntry, location) {
            List = new StarArgsSequenceInfo(VariableDef.EmptyArray, unit.ProjectState._tupleType);
            if (addType) {
                AddTypes(unit, List.SelfSet);
            }
        }

        public ListParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy, bool addType = true)
            : base(unit.DeclaringModule.ProjectEntry, location, copy) {
            List = new SequenceInfo(VariableDef.EmptyArray, unit.ProjectState._tupleType);
            if (addType) {
                AddTypes(unit, List.SelfSet);
            }
        }
    }

    /// <summary>
    /// Represents a **args parameter for a function definition.  Holds onto a DictionaryInfo
    /// which includes all of the types passed in via splatting or unused keyword arguments.
    /// </summary>
    sealed class DictParameterVariableDef : LocatedVariableDef {
        public readonly DictionaryInfo Dict;

        public DictParameterVariableDef(AnalysisUnit unit, Node location)
            : base(unit.DeclaringModule.ProjectEntry, location) {
            Dict = new DictionaryInfo(unit.ProjectEntry);
            AddTypes(unit, Dict.SelfSet);
        }

        public DictParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy)
            : base(unit.DeclaringModule.ProjectEntry, location, copy) {
            Dict = new DictionaryInfo(unit.ProjectEntry);
            AddTypes(unit, Dict.SelfSet);
        }
    }
}
