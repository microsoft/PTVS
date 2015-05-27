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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ClassInfo : AnalysisValue, IReferenceableContainer {
        private AnalysisUnit _analysisUnit;
        private readonly List<IAnalysisSet> _bases;
        internal Mro _mro;
        private readonly InstanceInfo _instanceInfo;
        private ClassScope _scope;
        private readonly int _declVersion;
        private VariableDef _metaclass;
        private ReferenceDict _references;
        private VariableDef _subclasses;
        private AnalysisValue _baseUserType;    // base most user defined type, used for unioning types during type explosion
        private readonly PythonAnalyzer _projectState;

        internal ClassInfo(ClassDefinition klass, AnalysisUnit outerUnit) {
            _instanceInfo = new InstanceInfo(this);
            _bases = new List<IAnalysisSet>();
            _declVersion = outerUnit.ProjectEntry.AnalysisVersion;
            _projectState = outerUnit.ProjectState;
            _mro = new Mro(this);
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return _analysisUnit;
            }
        }

        internal void SetAnalysisUnit(AnalysisUnit unit) {
            Debug.Assert(_analysisUnit == null);
            _analysisUnit = unit;
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (unit != null) {
                return AddCall(node, keywordArgNames, unit, args);
            }

            return _instanceInfo.SelfSet;
        }

        private IAnalysisSet AddCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, IAnalysisSet[] argumentVars) {
            var init = GetMemberNoReferences(node, unit, "__init__", false);
            var initArgs = Utils.Concat(_instanceInfo.SelfSet, argumentVars);

            foreach (var initFunc in init) {
                initFunc.Call(node, unit, initArgs, keywordArgNames);
            }

            // TODO: If we checked for metaclass, we could pass it in as the cls arg here
            var n = GetMemberNoReferences(node, unit, "__new__", false);
            var newArgs = Utils.Concat(SelfSet, argumentVars);
            var newResult = AnalysisSet.Empty;
            bool anyCustom = false;
            foreach (var newFunc in n) {
                if (!(newFunc is BuiltinFunctionInfo)) {
                    anyCustom = true;
                }
                newResult = newResult.Union(newFunc.Call(node, unit, newArgs, keywordArgNames));
            }

            if (anyCustom) {
                return newResult;
            }

            if (newResult.Count == 0 || newResult.All(ns => ns.IsOfType(unit.ProjectState.ClassInfos[BuiltinTypeId.Object]))) {
                return _instanceInfo.SelfSet;
            }
            return newResult;
        }

        public ClassDefinition ClassDefinition {
            get { return _analysisUnit.Ast as ClassDefinition; }
        }

        public override string ShortDescription {
            get {
                return ClassDefinition.Name;
            }
        }

        public override string Description {
            get {
                var res = "class " + ClassDefinition.Name;
                if (!String.IsNullOrEmpty(Documentation)) {
                    res += Environment.NewLine + Documentation;
                }
                return res;
            }
        }

        public override string Name {
            get {
                return ClassDefinition.Name;
            }
        }

        public VariableDef SubClasses {
            get {
                if (_subclasses == null) {
                    _subclasses = new VariableDef();
                }
                return _subclasses;
            }
        }

        public VariableDef MetaclassVariable {
            get {
                return _metaclass;
            }
        }

        public VariableDef GetOrCreateMetaclassVariable() {
            if (_metaclass == null) {
                _metaclass = new VariableDef();
            }
            return _metaclass;
        }

        public override string Documentation {
            get {
                if (ClassDefinition.Body != null) {
                    return ClassDefinition.Body.Documentation.TrimDocumentation();
                }
                return "";
            }
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                if (_declVersion == DeclaringModule.AnalysisVersion) {
                    var start = ClassDefinition.NameExpression.GetStart(ClassDefinition.GlobalParent);
                    return new[] { new LocationInfo(DeclaringModule, start.Line, start.Column) };
                }
                return LocationInfo.Empty;
            }
        }

        internal override BuiltinTypeId TypeId {
            get {
                return BuiltinTypeId.Type;
            }
        }

        public override IPythonType PythonType {
            get {
                return _projectState.Types[BuiltinTypeId.Type];
            }
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            return klass.Contains(_projectState.ClassInfos[BuiltinTypeId.Type]);
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _analysisUnit.ProjectEntry;
            }
        }

        public override int DeclaringVersion {
            get {
                return _declVersion;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                var result = new List<OverloadResult>();
                VariableDef init;
                if (Scope.TryGetVariable("__init__", out init)) {
                    // this type overrides __init__, display that for it's help
                    foreach (var initFunc in init.TypesNoCopy) {
                        foreach (var overload in initFunc.Overloads) {
                            result.Add(GetInitOverloadResult(overload));
                        }
                    }
                }

                VariableDef @new;
                if (Scope.TryGetVariable("__new__", out @new)) {
                    foreach (var newFunc in @new.TypesNoCopy) {
                        foreach (var overload in newFunc.Overloads) {
                            result.Add(GetNewOverloadResult(overload));
                        }
                    }
                }

                if (result.Count == 0) {
                    foreach (var baseClass in _bases) {
                        foreach (var ns in baseClass) {
                            if (ns.TypeId == BuiltinTypeId.Object) {
                                continue;
                            }
                            if (ns.Push()) {
                                try {
                                    foreach (var overload in ns.Overloads) {
                                        result.Add(
                                            new SimpleOverloadResult(
                                                overload.Parameters,
                                                ClassDefinition.Name,
                                                overload.Documentation
                                            )
                                        );
                                    }
                                } finally {
                                    ns.Pop();
                                }
                            }
                        }
                    }
                }

                if (result.Count == 0) {
                    // Old style class?
                    result.Add(new SimpleOverloadResult(new ParameterResult[0], ClassDefinition.Name, ClassDefinition.Body.Documentation.TrimDocumentation()));
                }

                // TODO: Filter out duplicates?
                return result;
            }
        }

        private SimpleOverloadResult GetNewOverloadResult(OverloadResult overload) {
            var doc = overload.Documentation;
            return new SimpleOverloadResult(
                overload.Parameters.RemoveFirst(),
                ClassDefinition.Name,
                String.IsNullOrEmpty(doc) ? Documentation : doc
            );
        }

        private SimpleOverloadResult GetInitOverloadResult(OverloadResult overload) {
            var doc = overload.Documentation;
            return new SimpleOverloadResult(
                overload.Parameters.RemoveFirst(),
                ClassDefinition.Name,
                String.IsNullOrEmpty(doc) ? Documentation : doc
            );
        }

        public IEnumerable<IAnalysisSet> Bases {
            get {
                return _bases;
            }
        }

        public override IEnumerable<IAnalysisSet> Mro {
            get {
                return _mro;
            }
        }

        public void ClearBases() {
            _bases.Clear();
            _baseUserType = null;
            _mro.Recompute();
        }

        public void SetBases(IEnumerable<IAnalysisSet> bases) {
            _bases.Clear();
            _bases.AddRange(bases);
            _baseUserType = null;
            _mro.Recompute();
        }

        public void SetBase(int index, IAnalysisSet baseSet) {
            while (index >= _bases.Count) {
                _bases.Add(AnalysisSet.Empty);
            }
            _bases[index] = baseSet;
            _baseUserType = null;
            _mro.Recompute();
        }

        public InstanceInfo Instance {
            get {
                return _instanceInfo;
            }
        }

        public override IAnalysisSet GetInstanceType() {
            return Instance;
        }

        /// <summary>
        /// Gets all members of this class that are not inherited from its base classes.
        /// </summary>
        public IDictionary<string, IAnalysisSet> GetAllImmediateMembers(IModuleContext moduleContext) {
            var result = new Dictionary<string, IAnalysisSet>(Scope.VariableCount);

            foreach (var v in Scope.AllVariables) {
                v.Value.ClearOldValues();
                if (v.Value.VariableStillExists) {
                    result[v.Key] = v.Value.Types;
                }
            }

            if (!result.ContainsKey("__doc__")) {
                result["__doc__"] = GetObjectMember(moduleContext, "__doc__");
            }
            if (!result.ContainsKey("__class__")) {
                result["__class__"] = GetObjectMember(moduleContext, "__class__");
            }

            return result;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
            var result = _mro.GetAllMembers(moduleContext);

            if (_metaclass != null) {
                foreach (var type in _metaclass.Types) {
                    if (type.Push()) {
                        try {
                            foreach (var nameValue in type.GetAllMembers(moduleContext)) {
                                result[nameValue.Key] = nameValue.Value.GetDescriptor(null, this, type, Instance.ProjectState._evalUnit);
                            }
                        } finally {
                            type.Pop();
                        }
                    }
                }
            }
            return result;
        }

        private AnalysisValue GetObjectMember(IModuleContext moduleContext, string name) {
            return _analysisUnit.ProjectState.GetAnalysisValueFromObjects(_analysisUnit.ProjectState.Types[BuiltinTypeId.Object].GetMember(moduleContext, name));
        }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new EncodedLocation(unit.Tree, node));
            }
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            // Must unconditionally call the base implementation of GetMember
            var ignored = base.GetMember(node, unit, name);

            return GetMemberNoReferences(node, unit, name).GetDescriptor(node, unit.ProjectState._noneInst, this, unit);
        }

        /// <summary>
        /// Get the member of this class by name that is not inherited from one of its base classes.
        /// </summary>
        public IAnalysisSet GetImmediateMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var result = AnalysisSet.Empty;
            var v = Scope.GetVariable(node, unit, name, addRef);
            if (v != null) {
                result = v.Types;
            }
            return result;
        }

        public IAnalysisSet GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var result = _mro.GetMemberNoReferences(node, unit, name, addRef);
            if (result != null && result.Count > 0) {
                return result;
            }

            if (_metaclass != null) {
                foreach (var type in _metaclass.Types) {
                    if (type.Push()) {
                        try {
                            foreach (var metaValue in type.GetMember(node, unit, name)) {
                                foreach (var boundValue in metaValue.GetDescriptor(node, this, type, unit)) {
                                    result = result.Union(boundValue);
                                }
                            }
                        } finally {
                            type.Pop();
                        }
                    }
                }

                if (result != null && result.Count > 0) {
                    return result;
                }
            }

            return GetOldStyleMember(name, unit.DeclaringModule.InterpreterContext);
        }

        private IAnalysisSet GetOldStyleMember(string name, IModuleContext context) {
            switch (name) {
                case "__doc__":
                case "__class__":
                    return GetObjectMember(context, name).SelfSet;
            }
            return AnalysisSet.Empty;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            variable.AddAssignment(node, unit);
            variable.AddTypes(unit, value);
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            var v = Scope.GetVariable(node, unit, name);
            if (v != null) {
                v.AddReference(node, unit);
            }
        }

        public override PythonMemberType MemberType {
            get {
                return PythonMemberType.Class;
            }
        }

        public override string ToString() {
            return "user class " + _analysisUnit.FullName + " (" + _declVersion + ")";
        }

        public ClassScope Scope {
            get { return _scope; }
            set {
                // Scope should only be set once
                Debug.Assert(_scope == null);
                _scope = value;
            }
        }

        private void EnsureBaseUserType() {
            if (_baseUserType == null) {
                foreach (var typeList in Bases) {
                    foreach (var type in typeList) {
                        ClassInfo ci = type as ClassInfo;

                        if (ci != null && ci.Push()) {
                            try {
                                ci.EnsureBaseUserType();
                                if (ci._baseUserType != null) {
                                    _baseUserType = ci._baseUserType;
                                } else {
                                    _baseUserType = ci;
                                }
                            } finally {
                                ci.Pop();
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Provides a stable ordering of class definitions that is used solely
        /// to ensure that unioning two classes is symmetrical.
        /// 
        /// Otherwise, classes C and D would be merged asymmetrically:
        /// 
        /// class A: pass
        /// class B: pass
        /// class C(A, B): pass
        /// class D(B, A): pass
        /// </summary>
        /// <remarks>
        /// This does not have to be 100% reliable in order to avoid breaking
        /// the analysis (except when FULL_VALIDATION is active). It is also
        /// called very often, so there is more to be lost by making it robust
        /// and slow.
        /// 
        /// The current implementation will break only when two classes are
        /// defined with the same name at the same character index in two
        /// different files and with problematic MROs.
        /// </remarks>
        private static bool IsFirstForMroUnion(ClassDefinition cd1, ClassDefinition cd2) {
            if (cd1.StartIndex != cd2.StartIndex) {
                return cd1.StartIndex > cd2.StartIndex;
            }
            return cd1.NameExpression.Name.CompareTo(cd2.NameExpression.Name) > 0;
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return _projectState.ClassInfos[BuiltinTypeId.Type];

            } else if (strength >= MergeStrength.ToBaseClass) {
                var ci = ns as ClassInfo;
                if (ci != null) {
                    IEnumerable<AnalysisValue> mro1;
                    AnalysisValue[] mro2;
                    if (IsFirstForMroUnion(ClassDefinition, ci.ClassDefinition)) {
                        mro1 = Mro.SelectMany().Except(_projectState.DoNotUnionInMro);
                        mro2 = ci.Mro.SelectMany().Except(_projectState.DoNotUnionInMro).ToArray();
                    } else {
                        mro1 = ci.Mro.SelectMany().Except(_projectState.DoNotUnionInMro);
                        mro2 = Mro.SelectMany().Except(_projectState.DoNotUnionInMro).ToArray();
                    }
                    return mro1.FirstOrDefault(cls => mro2.Contains(cls)) ?? _projectState.ClassInfos[BuiltinTypeId.Object];
                }

                var bci = ns as BuiltinClassInfo;
                if (bci != null) {
                    return bci;
                }
            }

            return base.UnionMergeTypes(ns, strength);
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                var type = _projectState.ClassInfos[BuiltinTypeId.Type];
                return ns is ClassInfo || ns is BuiltinClassInfo || ns == type || ns == type.Instance;

            } else if (strength >= MergeStrength.ToBaseClass) {
                var ci = ns as ClassInfo;
                if (ci != null) {
                    IEnumerable<AnalysisValue> mro1;
                    AnalysisValue[] mro2;
                    if (IsFirstForMroUnion(ClassDefinition, ci.ClassDefinition)) {
                        mro1 = Mro.SelectMany().Except(_projectState.DoNotUnionInMro);
                        mro2 = ci.Mro.SelectMany().Except(_projectState.DoNotUnionInMro).ToArray();
                    } else {
                        mro1 = ci.Mro.SelectMany().Except(_projectState.DoNotUnionInMro);
                        mro2 = Mro.SelectMany().Except(_projectState.DoNotUnionInMro).ToArray();
                    }
                    return mro1.Any(cls => mro2.Contains(cls));
                }

                var bci = ns as BuiltinClassInfo;
                if (bci != null &&
                    !_projectState.DoNotUnionInMro.Contains(this) &&
                    !_projectState.DoNotUnionInMro.Contains(bci)) {
                    return Mro.AnyContains(bci);
                }
            }

            return base.UnionEquals(ns, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToBaseClass) {
                return _projectState.ClassInfos[BuiltinTypeId.Type].GetHashCode();
            }

            return base.UnionHashCode(strength);
        }

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            var result = new List<IReferenceable>();
            VariableDef def;
            if (_scope.TryGetVariable(name, out def)) {
                result.Add(def);
            }

            if (Push()) {
                try {
                    result.AddRange(Bases.SelectMany(b => GetDefinitions(name, b)));
                    result.AddRange(GetDefinitions(name, SubClasses.TypesNoCopy));
                } finally {
                    Pop();
                }
            }

            return result;
        }

        private IEnumerable<IReferenceable> GetDefinitions(string name, IEnumerable<AnalysisValue> nses) {
            var result = new List<IReferenceable>();
            foreach (var subType in nses) {
                if (subType.Push()) {
                    IReferenceableContainer container = subType as IReferenceableContainer;
                    if (container != null) {
                        result.AddRange(container.GetDefinitions(name));
                    }
                    subType.Pop();
                }
            }
            return result;
        }

        #endregion

        #region IReferenceable Members

        internal override IEnumerable<LocationInfo> References {
            get {
                if (_references != null) {
                    return _references.AllReferences;
                }
                return new LocationInfo[0];
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents the method resolution order of a Python class according to C3 rules.
    /// </summary>
    /// <remarks>
    /// The rules are described in detail at http://www.python.org/download/releases/2.3/mro/
    /// </remarks>
    internal class Mro : DependentData, IEnumerable<IAnalysisSet> {
        private readonly ClassInfo _classInfo;
        private List<IAnalysisSet> _mroList;
        private bool _isValid = true;

        public Mro(ClassInfo classInfo) {
            _classInfo = classInfo;
            _mroList = new List<IAnalysisSet> { classInfo };
        }

        public bool IsValid {
            get { return _isValid; }
        }

        public IEnumerator<IAnalysisSet> GetEnumerator() {
            return _mroList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Recompute() {
            var mroList = new List<IAnalysisSet> { _classInfo.SelfSet };
            var isValid = true;

            var bases = _classInfo.Bases;
            if (bases.Any()) {
                var mergeList = new List<List<AnalysisValue>>();
                var finalMro = new List<AnalysisValue>();

                foreach (var baseClass in bases.SelectMany()) {
                    var klass = baseClass as ClassInfo;
                    var builtInClass = baseClass as BuiltinClassInfo;
                    if (klass != null && klass.Push()) {
                        try {
                            if (!klass._mro.IsValid) {
                                isValid = false;
                                break;
                            }
                            finalMro.Add(klass);
                            mergeList.Add(klass.Mro.SelectMany().ToList());
                        } finally {
                            klass.Pop();
                        }
                    } else if (builtInClass != null && builtInClass.Push()) {
                        try {
                            finalMro.Add(builtInClass);
                            mergeList.Add(builtInClass.Mro.SelectMany().ToList());
                        } finally {
                            builtInClass.Pop();
                        }
                    }
                }

                if (isValid) {
                    mergeList.Add(finalMro);
                    mergeList.RemoveAll(mro => mro.Count == 0);

                    while (mergeList.Count > 0) {
                        AnalysisValue nextInMro = null;

                        for (int i = 0; i < mergeList.Count; ++i) {
                            // Select candidate head
                            var candidate = mergeList[i][0];

                            // Look for the candidate in the tails of every other MRO
                            if (!mergeList.Any(baseMro => baseMro.Skip(1).Contains(candidate))) {
                                // Candidate is good, so stop searching.
                                nextInMro = candidate;
                                break;
                            }
                        }

                        // No valid MRO for this class
                        if (nextInMro == null) {
                            isValid = false;
                            break;
                        }

                        mroList.Add(nextInMro);

                        // Remove all instances of that class from potentially being returned again
                        foreach (var mro in mergeList) {
                            mro.RemoveAll(ns => ns == nextInMro);
                        }

                        // Remove all lists that are now empty.
                        mergeList.RemoveAll(mro => mro.Count == 0);
                    }
                }
            }

            // If the MRO is invalid, we only want the class itself to be there so that we
            // will show all members defined in it, but nothing else.
            if (!isValid) {
                mroList.Clear();
                mroList.Add(_classInfo.SelfSet);
            }

            if (_isValid != isValid || !_mroList.SequenceEqual(mroList)) {
                _isValid = isValid;
                _mroList = mroList;
                EnqueueDependents();
            }
        }

        public IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext) {
            return GetAllMembersOfMro(this, moduleContext);
        }

        /// <summary>
        /// Compute a list of all members, given the MRO list of types, and taking override rules into account.
        /// </summary>
        public static IDictionary<string, IAnalysisSet> GetAllMembersOfMro(IEnumerable<IAnalysisSet> mro, IModuleContext moduleContext) {
            var result = new Dictionary<string, IAnalysisSet>();

            // MRO is a list of namespaces corresponding to classes, but each entry can be a union of several different classes.
            // Therefore, within a single entry, we want to make a union of members from each; but between entries, we
            // want the first entry in MRO to suppress any members with the same names from the following entries.
            foreach (var entry in mro) {
                var entryMembers = new Dictionary<string, IAnalysisSet>();
                foreach (var ns in entry) {
                    // If it's another non-builtin class, we don't want its inherited members, since we'll account
                    // for them while processing our own MRO - we only want its immediate members.
                    var classInfo = ns as ClassInfo;
                    var classMembers = classInfo != null ? classInfo.GetAllImmediateMembers(moduleContext) : ns.GetAllMembers(moduleContext);

                    foreach (var kvp in classMembers) {
                        IAnalysisSet existing;
                        if (!entryMembers.TryGetValue(kvp.Key, out existing)) {
                            entryMembers[kvp.Key] = kvp.Value;
                        } else {
                            entryMembers[kvp.Key] = existing.Union(kvp.Value);
                        }
                    }
                }

                foreach (var member in entryMembers) {
                    if (!result.ContainsKey(member.Key)) {
                        result.Add(member.Key, member.Value);
                    }
                }
            }

            return result;
        }

        public IAnalysisSet GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            return GetMemberFromMroNoReferences(this, node, unit, name, addRef);
        }

        /// <summary>
        /// Get the member by name, given the MRO list, and taking override rules into account.
        /// </summary>
        public static IAnalysisSet GetMemberFromMroNoReferences(IEnumerable<IAnalysisSet> mro, Node node, AnalysisUnit unit, string name, bool addRef = true) {
            if (mro == null) {
                return AnalysisSet.Empty;
            }

            // Union all members within a single MRO entry, but stop at the first entry that yields a non-empty set since it overrides any that follow.
            var result = AnalysisSet.Empty;
            foreach (var mroEntry in mro) {
                foreach (var ns in mroEntry) {
                    var classInfo = ns as ClassInfo;
                    if (classInfo != null) {
                        var v = classInfo.Scope.GetVariable(node, unit, name, addRef);
                        if (v != null) {
                            result = result.Union(v.Types);
                        }
                    } else {
                        result = result.Union(ns.GetMember(node, unit, name));
                    }
                }

                if (result != null && result.Count > 0) {
                    break;
                }
            }
            return result;
        }
    }
}
