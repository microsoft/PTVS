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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ClassInfo : Namespace, IReferenceableContainer {
        private AnalysisUnit _analysisUnit;
        private readonly List<INamespaceSet> _bases;
        private Mro _mro;
        private readonly InstanceInfo _instanceInfo;
        private ClassScope _scope;
        private readonly int _declVersion;
        private VariableDef _metaclass;
        private ReferenceDict _references;
        private VariableDef _subclasses;
        private Namespace _baseUserType;    // base most user defined type, used for unioning types during type explosion
        private readonly PythonAnalyzer _projectState;

        internal ClassInfo(ClassDefinition klass, AnalysisUnit outerUnit) {
            _instanceInfo = new InstanceInfo(this);
            _bases = new List<INamespaceSet>();
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

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            if (unit != null) {
                return AddCall(node, keywordArgNames, unit, args);
            }

            return _instanceInfo.SelfSet;
        }

        private INamespaceSet AddCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, INamespaceSet[] argumentVars) {
            var init = GetMemberNoReferences(node, unit, "__init__", false);
            var initArgs = Utils.Concat(_instanceInfo.SelfSet, argumentVars);

            foreach (var initFunc in init) {
                initFunc.Call(node, unit, initArgs, keywordArgNames);
            }

            // TODO: If we checked for metaclass, we could pass it in as the cls arg here
            var n = GetMemberNoReferences(node, unit, "__new__", false);
            var newArgs = Utils.Concat(SelfSet, argumentVars);
            var newResult = NamespaceSet.Empty;
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

            if (newResult.Count == 0 || newResult.All(ns => ns == unit.ProjectState._objectType.Instance)) {
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

        public override IPythonType PythonType {
            get {
                return this._analysisUnit.ProjectState.Types.Type;
            }
        }

        public override ProjectEntry DeclaringModule {
            get {
                return _analysisUnit.ProjectEntry;
            }
        }

        public override int DeclaringVersion {
            get {
                return _declVersion;
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                var result = new List<OverloadResult>();
                VariableDef init;
                if (Scope.Variables.TryGetValue("__init__", out init)) {
                    // this type overrides __init__, display that for it's help
                    foreach (var initFunc in init.TypesNoCopy) {
                        foreach (var overload in initFunc.Overloads) {
                            result.Add(GetInitOverloadResult(overload));
                        }
                    }
                }

                VariableDef @new;
                if (Scope.Variables.TryGetValue("__new__", out @new)) {
                    foreach (var newFunc in @new.TypesNoCopy) {
                        foreach (var overload in newFunc.Overloads) {
                            result.Add(GetNewOverloadResult(overload));
                        }
                    }
                }

                if (result.Count == 0) {
                    foreach (var baseClass in _bases) {
                        foreach (var ns in baseClass) {
                            if (ns.Push()) {
                                try {
                                    foreach (var overload in ns.Overloads) {
                                        result.Add(
                                            new OverloadResult(
                                                overload.Parameters,
                                                ClassDefinition.Name
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

        public IEnumerable<INamespaceSet> Bases {
            get {
                return _bases;
            }
        }

        public Mro Mro {
            get {
                return _mro;
            }
        }

        public void ClearBases() {
            _bases.Clear();
            _baseUserType = null;
            Mro.Recompute();
        }

        public void SetBases(IEnumerable<INamespaceSet> bases) {
            _bases.Clear();
            _bases.AddRange(bases);
            _baseUserType = null;
            Mro.Recompute();
        }

        public void SetBase(int index, INamespaceSet baseSet) {
            while (index >= _bases.Count) {
                _bases.Add(NamespaceSet.Empty);
            }
            _bases[index] = baseSet;
            _baseUserType = null;
            Mro.Recompute();
        }

        public InstanceInfo Instance {
            get {
                return _instanceInfo;
            }
        }

        /// <summary>
        /// Gets all members of this class that are not inherited from its base classes.
        /// </summary>
        public IDictionary<string, INamespaceSet> GetAllImmediateMembers(IModuleContext moduleContext) {
            var result = new Dictionary<string, INamespaceSet>(Scope.Variables.Count);

            foreach (var v in Scope.Variables) {
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

        public override IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            var result = Mro.GetAllMembers(moduleContext);

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

        private Namespace GetObjectMember(IModuleContext moduleContext, string name) {
            return _analysisUnit.ProjectState.GetNamespaceFromObjects(_analysisUnit.ProjectState.Types.Object.GetMember(moduleContext, name));
        }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new EncodedLocation(unit.Tree, node));
            }
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
            return GetMemberNoReferences(node, unit, name).GetDescriptor(node, unit.ProjectState._noneInst, this, unit);
        }

        /// <summary>
        /// Get the member of this class by name that is not inherited from one of its base classes.
        /// </summary>
        public INamespaceSet GetImmediateMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var result = NamespaceSet.Empty;
            var v = Scope.GetVariable(node, unit, name, addRef);
            if (v != null) {
                result = v.Types;
            }
            return result;
        }

        public INamespaceSet GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            var result = Mro.GetMemberNoReferences(node, unit, name, addRef);
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

        private INamespaceSet GetOldStyleMember(string name, IModuleContext context) {
            switch (name) {
                case "__doc__":
                case "__class__":
                    return GetObjectMember(context, name).SelfSet;
            }
            return NamespaceSet.Empty;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, INamespaceSet value) {
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

        private const int MERGE_TO_BASE_STRENGTH = 1;
        private const int MERGE_TO_TYPE_STRENGTH = 3;

        internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
            if (Object.ReferenceEquals(this, ns)) {
                return this;
            }
            if (strength < MERGE_TO_BASE_STRENGTH) {
                return this;
            } else if (strength < MERGE_TO_TYPE_STRENGTH) {
                var ci = ns as ClassInfo;
                if (ci == null) {
                    return (Namespace)(ns as BuiltinClassInfo) ?? this;
                }

                var mro1 = Mro.SelectMany();
                var mro2 = ci.Mro.ToArray();
                return mro1.FirstOrDefault(cls => mro2.AnyContains(cls)) ?? this;
            } else {
                return _projectState._typeObj;
            }
        }

        public override bool UnionEquals(Namespace ns, int strength) {
            if (Object.ReferenceEquals(this, ns)) {
                return true;
            }
            if (strength < MERGE_TO_BASE_STRENGTH) {
                return Equals(ns);
            } else if (strength < MERGE_TO_TYPE_STRENGTH) {
                var ci = ns as ClassInfo;
                if (ci == null) {
                    var bci = ns as BuiltinClassInfo;
                    if (bci == null || bci == _projectState._objectType) {
                        return false;
                    }
                    return Mro.AnyContains(bci);
                }

                var mro1 = Mro.SelectMany();
                var mro2 = ci.Mro.ToArray();
                return mro1.Any(cls => cls != _projectState._objectType && mro2.AnyContains(cls));
            } else {
                return ns is ClassInfo;
            }
        }

        public override int UnionHashCode(int strength) {
            if (strength < MERGE_TO_BASE_STRENGTH) {
                return GetHashCode();
            } else {
                return _projectState._typeObj.GetHashCode();
            }
        }

        #region IVariableDefContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            VariableDef def;
            if (_scope.Variables.TryGetValue(name, out def)) {
                yield return def;
            }

            if (Push()) {
                foreach (var baseClassSet in Bases) {
                    foreach (var subdef in GetDefinitions(name, baseClassSet)) {
                        yield return subdef;
                    }
                }

                foreach (var subdef in GetDefinitions(name, SubClasses.Types)) {
                    yield return subdef;
                }
                Pop();
            }
        }

        private IEnumerable<IReferenceable> GetDefinitions(string name, IEnumerable<Namespace> nses) {
            foreach (var subType in nses) {
                if (subType.Push()) {
                    IReferenceableContainer container = subType as IReferenceableContainer;
                    if (container != null) {
                        foreach (var baseDef in container.GetDefinitions(name)) {
                            yield return baseDef;
                        }
                    }
                    subType.Pop();
                }
            }
        }

        #endregion

        #region IReferenceable Members

        public override IEnumerable<LocationInfo> References {
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
    internal class Mro : DependentData, IEnumerable<INamespaceSet> {
        private readonly ClassInfo _classInfo;
        private List<INamespaceSet> _mroList;
        private bool _isValid = true;

        public Mro(ClassInfo classInfo) {
            _classInfo = classInfo;
            _mroList = new List<INamespaceSet> { classInfo };
        }

        public bool IsValid {
            get { return _isValid; }
        }

        public IEnumerator<INamespaceSet> GetEnumerator() {
            return _mroList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Recompute() {
            var mroList = new List<INamespaceSet> { _classInfo.SelfSet };
            var isValid = true;

            var bases = _classInfo.Bases;
            if (bases.Any()) {
                var mergeList = new List<List<Namespace>>();
                var finalMro = new List<Namespace>();

                foreach (var baseClass in bases.SelectMany()) {
                    var klass = baseClass as ClassInfo;
                    var builtInClass = baseClass as BuiltinClassInfo;
                    if (klass != null && klass.Push()) {
                        try {
                            if (!klass.Mro.IsValid) {
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
                    if (finalMro.Any()) {
                        mergeList.Add(finalMro);
                    }

                    while (mergeList.Count > 0) {
                        Namespace nextInMro = null;

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

        public IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            return GetAllMembersOfMro(this, moduleContext);
        }

        /// <summary>
        /// Compute a list of all members, given the MRO list of types, and taking override rules into account.
        /// </summary>
        public static IDictionary<string, INamespaceSet> GetAllMembersOfMro(IEnumerable<INamespaceSet> mro, IModuleContext moduleContext) {
            var result = new Dictionary<string, INamespaceSet>();

            // MRO is a list of namespaces corresponding to classes, but each entry can be a union of several different classes.
            // Therefore, within a single entry, we want to make a union of members from each; but between entries, we
            // want the first entry in MRO to suppress any members with the same names from the following entries.
            foreach (var entry in mro) {
                var entryMembers = new Dictionary<string, INamespaceSet>();
                foreach (var ns in entry) {
                    // If it's another non-builtin class, we don't want its inherited members, since we'll account
                    // for them while processing our own MRO - we only want its immediate members.
                    var classInfo = ns as ClassInfo;
                    var classMembers = classInfo != null ? classInfo.GetAllImmediateMembers(moduleContext) : ns.GetAllMembers(moduleContext);

                    foreach (var kvp in classMembers) {
                        INamespaceSet existing;
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

        public INamespaceSet GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            return GetMemberFromMroNoReferences(this, node, unit, name, addRef);
        }

        /// <summary>
        /// Get the member by name, given the MRO list, and taking override rules into account.
        /// </summary>
        public static INamespaceSet GetMemberFromMroNoReferences(IEnumerable<INamespaceSet> mro, Node node, AnalysisUnit unit, string name, bool addRef = true) {
            if (mro == null) {
                return NamespaceSet.Empty;
            }

            // Union all members within a single MRO entry, but stop at the first entry that yields a non-empty set since it overrides any that follow.
            var result = NamespaceSet.Empty;
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
