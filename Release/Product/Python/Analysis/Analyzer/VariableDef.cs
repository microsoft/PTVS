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
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    abstract class DependentData<TStorageType>  where TStorageType : DependencyInfo {
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

        public bool AddTypes(Node node, AnalysisUnit unit, IEnumerable<T> newTypes) {
            bool added = false;

            var dependencies = GetDependentItems(unit.ProjectEntry);
            foreach (var value in newTypes) {
                if (dependencies.Types.Add(value, unit.ProjectState)) {
                    added = true;
                }
            }

            if (added) {
                EnqueueDependents();
            }

            return added;
        }

        public ISet<T> Types {
            get {
                if (_dependencies.Count != 0) {
                    HashSet<T> res = new HashSet<T>();
                    foreach (var mod in _dependencies.Values) {
                        res.UnionWith(mod.Types);
                    }
                    return res;
                }
                return EmptySet<T>.Instance;
            }
        }

        public void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                // TODO: This could be improved.  We could avoid eagerly going from index spans -> full location spans by holding onto
                // the node and parent and lazily translating.
                SourceSpan span = GetSpan(node, unit);
                AddReference(new SimpleSrcLocation(span), unit.DeclaringModule.ProjectEntry).AddDependentUnit(unit);
            }
        }

        private static SourceSpan GetSpan(Node node, AnalysisUnit unit) {
            MemberExpression me = node as MemberExpression;
            SourceSpan span;
            if (me != null) {
                span = me.GetNameSpan(unit.Ast.GlobalParent);
            } else {
                span = node.GetSpan(unit.Ast.GlobalParent);
            }
            return span;
        }

        public TypedDependencyInfo<T> AddReference(SimpleSrcLocation location, IProjectEntry module) {
            var depUnits = GetDependentItems(module);
            depUnits.AddReference(location);
            return depUnits;
        }

        public void AddAssignment(SimpleSrcLocation location, IProjectEntry entry) {
            var depUnits = GetDependentItems(entry);
            depUnits.AddAssignment(location);
        }

        public void AddAssignment(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                AddAssignment(new SimpleSrcLocation(GetSpan(node, unit)), unit.DeclaringModule.ProjectEntry);
            }
        }

        public IEnumerable<KeyValuePair<IProjectEntry, SimpleSrcLocation>> References {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.References != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.References) {
                                yield return new KeyValuePair<IProjectEntry, SimpleSrcLocation>(keyValue.Key, reference);
                            }
                        }
                    }
                }
            }
        }

        public IEnumerable<KeyValuePair<IProjectEntry, SimpleSrcLocation>> Definitions {
            get {
                if (_dependencies.Count != 0) {
                    foreach (var keyValue in _dependencies) {
                        if (keyValue.Value.Assignments != null && keyValue.Key.AnalysisVersion == keyValue.Value.Version) {
                            foreach (var reference in keyValue.Value.Assignments) {
                                yield return new KeyValuePair<IProjectEntry, SimpleSrcLocation>(keyValue.Key, reference);
                            }
                        }
                    }
                }
            }
        }
    }

    class VariableDef : VariableDef<Namespace> {
        public virtual bool IsEphemeral {
            get {
                return false;
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
        private readonly int _declaringVersion;
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
        public readonly SequenceInfo List;

        public ListParameterVariableDef(AnalysisUnit unit, Node location)
            : base(unit.DeclaringModule.ProjectEntry, location) {            
            List = new SequenceInfo(new ISet<Namespace>[0], unit.ProjectState._tupleType);
            AddTypes(location, unit, List.SelfSet);
        }

        public ListParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy)
            : base(unit.DeclaringModule.ProjectEntry, location, copy) {
            List = new SequenceInfo(new ISet<Namespace>[0], unit.ProjectState._tupleType);
            AddTypes(location, unit, List.SelfSet);
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
            Dict = new DictionaryInfo(new HashSet<Namespace>(), new HashSet<Namespace>(), unit.ProjectState);
            AddTypes(location, unit, Dict.SelfSet);
        }

        public DictParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy)
            : base(unit.DeclaringModule.ProjectEntry, location, copy) {
            Dict = new DictionaryInfo(new HashSet<Namespace>(), new HashSet<Namespace>(), unit.ProjectState);
            AddTypes(location, unit, Dict.SelfSet);
        }
    }
}
