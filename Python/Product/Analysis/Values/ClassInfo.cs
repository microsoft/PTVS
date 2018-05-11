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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ClassInfo : AnalysisValue, IReferenceableContainer, IHasRichDescription, IHasQualifiedName {
        private AnalysisUnit _analysisUnit;
        private readonly List<IAnalysisSet> _bases;
        internal Mro _mro;
        private readonly InstanceInfo _instanceInfo;
        private ClassScope _scope;
        private readonly int _declVersion;
        private VariableDef _metaclass;
        private ReferenceDict _references;
        private VariableDef _subclasses;
        private IAnalysisSet _baseSpecialization;
        private readonly PythonAnalyzer _projectState;

        internal ClassInfo(ClassDefinition klass, AnalysisUnit outerUnit) {
            _instanceInfo = new InstanceInfo(this);
            _bases = new List<IAnalysisSet>();
            _declVersion = outerUnit.ProjectEntry.AnalysisVersion;
            _projectState = outerUnit.State;
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

        private IAnalysisSet AddCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, IAnalysisSet[] args) {
            var init = GetMemberNoReferences(node, unit, "__init__", false);
            var initArgs = Utils.Concat(_instanceInfo.SelfSet, args);

            foreach (var initFunc in init) {
                initFunc.Call(node, unit, initArgs, keywordArgNames);
            }

            // TODO: If we checked for metaclass, we could pass it in as the cls arg here
            var n = GetMemberNoReferences(node, unit, "__new__", false);
            var newArgs = Utils.Concat(SelfSet, args);
            var newResult = AnalysisSet.Empty;
            bool anyCustom = false;
            foreach (var newFunc in n) {
                if (!(newFunc is BuiltinFunctionInfo) && !(newFunc is SpecializedCallable)) {
                    anyCustom = true;
                }
                newResult = newResult.Union(newFunc.Call(node, unit, newArgs, keywordArgNames).Resolve(unit));
            }

            if (anyCustom) {
                return newResult;
            }

            

            if (newResult.Count == 0 || newResult.All(ns => ns.IsOfType(unit.State.ClassInfos[BuiltinTypeId.Object]))) {
                if (_baseSpecialization != null && _baseSpecialization.Count != 0) {
                    var specializedInstances = _baseSpecialization.Call(
                        node, unit, args, keywordArgNames
                    );

                    var res = (SpecializedInstanceInfo)unit.Scope.GetOrMakeNodeValue(
                        node,
                        NodeValueKind.SpecializedInstance,
                        (node_) => new SpecializedInstanceInfo(this, specializedInstances)
                    );

                    res._instances = specializedInstances;
                    return res;
                }

                return _instanceInfo.SelfSet;
            }
            return newResult;
        }

        public ClassDefinition ClassDefinition {
            get { return _analysisUnit.Ast as ClassDefinition; }
        }

        private static string FormatExpression(Expression baseClass) {
            NameExpression ne = baseClass as NameExpression;
            if (ne != null) {
                return ne.Name;
            }

            MemberExpression me = baseClass as MemberExpression;
            if (me != null) {
                string expr = FormatExpression(me.Target);
                if (expr != null) {
                    return expr + "." + me.Name ?? string.Empty;
                }
            }

            return null;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "class ");
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, FullyQualifiedName);
            
            if (ClassDefinition.BasesInternal.Length > 0) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
                bool comma = false;
                foreach (var baseClass in ClassDefinition.BasesInternal) {
                    if (comma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }

                    string baseStr = FormatExpression(baseClass.Expression);
                    if (baseStr != null) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, baseStr);
                    }

                    comma = true;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");
            }
        }

        public string FullyQualifiedName {
            get {
                var name = ClassDefinition.Name;
                for (var stmt = ClassDefinition.Parent; stmt != null; stmt = stmt.Parent) {
                    if (stmt.IsGlobal) {
                        return DeclaringModule.ModuleName + "." + name;
                    }
                    if (!string.IsNullOrEmpty(stmt.Name)) {
                        name = stmt.Name + "." + name;
                    }
                }
                return name;
            }
        }

        public KeyValuePair<string, string> FullyQualifiedNamePair {
            get {
                var name = ClassDefinition.Name;
                for (var stmt = ClassDefinition.Parent; stmt != null; stmt = stmt.Parent) {
                    if (stmt.IsGlobal) {
                        return new KeyValuePair<string, string>(DeclaringModule.ModuleName, name);
                    }
                    if (stmt is ClassDefinition) {
                        name = stmt.Name + "." + name;
                    } else {
                        break;
                    }
                }
                throw new NotSupportedException();
            }
        }


        public override string ShortDescription {
            get {
                return ClassDefinition.Name;
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
                    var start = ClassDefinition.GetStart(ClassDefinition.GlobalParent);
                    var end = ClassDefinition.GetEnd(ClassDefinition.GlobalParent);
                    return new[] { new LocationInfo(DeclaringModule.FilePath, DeclaringModule.DocumentUri, start.Line, start.Column, end.Line, end.Column) };
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
                                            new OverloadResult(
                                                overload.Parameters,
                                                ClassDefinition.Name,
                                                overload.Documentation,
                                                overload.ReturnType
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
                    result.Add(new OverloadResult(
                        new ParameterResult[0],
                        ClassDefinition.Name,
                        ClassDefinition.Body.Documentation.TrimDocumentation(),
                        new[] { ShortDescription }
                    ));
                }

                // TODO: Filter out duplicates?
                return result;
            }
        }

        private OverloadResult GetNewOverloadResult(OverloadResult overload) {
            var doc = overload.Documentation;
            return new OverloadResult(
                overload.Parameters.RemoveFirst(),
                ClassDefinition.Name,
                String.IsNullOrEmpty(doc) ? Documentation : doc,
                overload.ReturnType
            );
        }

        private OverloadResult GetInitOverloadResult(OverloadResult overload) {
            var doc = overload.Documentation;
            return new OverloadResult(
                overload.Parameters.RemoveFirst(),
                ClassDefinition.Name,
                String.IsNullOrEmpty(doc) ? Documentation : doc,
                overload.ReturnType
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

        public void SetBases(IEnumerable<IAnalysisSet> bases) {
            _bases.Clear();
            _bases.AddRange(bases);
            _mro.Recompute();

            RecomputeBaseSpecialization();
        }

        private void RecomputeBaseSpecialization() {
            IAnalysisSet builtinClassSet = AnalysisSet.Empty;
            foreach (var classInfo in _mro) {
                BuiltinClassInfo builtin = classInfo as BuiltinClassInfo;
                if (builtin != null && builtin.TypeId != BuiltinTypeId.Object) {
                    var builtinType = _projectState.GetBuiltinType(builtin.PythonType);

                    if (builtinType.GetType() != typeof(BuiltinClassInfo)) {
                        // we have a specialized built-in class, we want its behavior too...
                        builtinClassSet = builtinClassSet.Union(builtinType.SelfSet, true);
                    }
                }
            }
            _baseSpecialization = builtinClassSet;
        }

        public void SetBase(int index, IAnalysisSet baseSet) {
            while (index >= _bases.Count) {
                _bases.Add(AnalysisSet.Empty);
            }
            _bases[index] = baseSet;
            _mro.Recompute();
            RecomputeBaseSpecialization();
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
        public IDictionary<string, IAnalysisSet> GetAllImmediateMembers(IModuleContext moduleContext, GetMemberOptions options) {
            var result = new Dictionary<string, IAnalysisSet>(Scope.VariableCount);

            foreach (var v in Scope.AllVariables) {
                if (!options.ForEval()) {
                    v.Value.ClearOldValues();
                }
                if (v.Value.VariableStillExists) {
                    result[v.Key] = v.Value.Types;
                }
            }

            if (!options.HasFlag(GetMemberOptions.DeclaredOnly)) {
                if (!result.ContainsKey("__doc__")) {
                    result["__doc__"] = GetObjectMember(moduleContext, "__doc__");
                }
                if (!result.ContainsKey("__class__")) {
                    result["__class__"] = GetObjectMember(moduleContext, "__class__");
                }
            }

            return result;
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            IDictionary<string, IAnalysisSet> result;
            if (options.HasFlag(GetMemberOptions.DeclaredOnly)) {
                result = GetAllImmediateMembers(moduleContext, options);
            } else {
                result = _mro.GetAllMembers(moduleContext, options);
            }

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
            return _analysisUnit.State.GetAnalysisValueFromObjects(_analysisUnit.State.Types[BuiltinTypeId.Object].GetMember(moduleContext, name));
        }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new EncodedLocation(unit, node));
            }
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return GetMemberNoReferences(node, unit, name).GetDescriptor(node, unit.State._noneInst, this, unit);
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
        internal static bool IsFirstForMroUnion(AnalysisValue ns1, AnalysisValue ns2) {
            var ci1 = ns1 as ClassInfo;
            var ci2 = ns2 as ClassInfo;

            if (ci1 == null && ci2 != null) {
                return true;
            } else if (ci1 != null && ci2 == null) {
                return false;
            } else if (ci1 != null && ci2 != null) {
                return ci1.ClassDefinition.StartIndex > ci2.ClassDefinition.StartIndex;
            }

            return string.CompareOrdinal(ns1.Name, ns2.Name) > 0;
        }

        internal static AnalysisValue GetFirstCommonBase(PythonAnalyzer state, AnalysisValue ns1, AnalysisValue ns2) {
            if (ns1.MemberType != PythonMemberType.Class || ns2.MemberType != PythonMemberType.Class) {
                return null;
            }

            (ns1.Mro as Mro)?.RecomputeIfNecessary();
            (ns2.Mro as Mro)?.RecomputeIfNecessary();

            var mro1 = ns1.Mro.SelectMany().ToArray();
            var mro2 = ns2.Mro.SelectMany().ToArray();

            if (!IsFirstForMroUnion(ns1, ns2)) {
                var tmp = mro1;
                mro1 = mro2;
                mro2 = tmp;
            }

            var mro2Set = new HashSet<AnalysisValue>(mro2.MaybeEnumerate().SelectMany(), ObjectComparer.Instance);
            var commonBase = mro1.MaybeEnumerate().SelectMany().Where(v => v is ClassInfo || v is BuiltinClassInfo).FirstOrDefault(mro2Set.Contains);
            if (commonBase == null || commonBase.TypeId == BuiltinTypeId.Object || commonBase.TypeId == BuiltinTypeId.Type) {
                return null;
            }
            if (commonBase.Push()) {
                try {
#if FULL_VALIDATION
                    Validation.Assert(GetFirstCommonBase(state, ns1, commonBase) != null, $"No common base between {ns1} and {commonBase}");
                    Validation.Assert(GetFirstCommonBase(state, ns2, commonBase) != null, $"No common base between {ns2} and {commonBase}");
#endif
                    if (GetFirstCommonBase(state, ns1, commonBase) == null || GetFirstCommonBase(state, ns2, commonBase) == null) {
                        return null;
                    }
                } finally {
                    commonBase.Pop();
                }
            }
            return commonBase;
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                AnalysisValue type;
                if (TypeId == ns.TypeId && (type = _projectState.ClassInfos[TypeId]) != null) {
                    return type;
                }
                return _projectState.ClassInfos[BuiltinTypeId.Type];

            } else if (strength >= MergeStrength.ToBaseClass) {
                var commonBase = GetFirstCommonBase(_projectState, this, ns);
                if (commonBase != null) {
                    return commonBase;
                }

                return _projectState.ClassInfos[BuiltinTypeId.Object];
            }

            return base.UnionMergeTypes(ns, strength);
        }

        internal override bool UnionEquals(AnalysisValue ns, int strength) {
            if (strength >= MergeStrength.ToObject) {
                var type = _projectState.ClassInfos[BuiltinTypeId.Type];
                return ns is ClassInfo || ns is BuiltinClassInfo || ns == type || ns == type.Instance;

            } else if (strength >= MergeStrength.ToBaseClass) {
                return GetFirstCommonBase(_projectState, this, ns) != null;
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
        private List<AnalysisValue> _mroList;
        private bool _isValid = true;

        public Mro(ClassInfo classInfo) {
            _classInfo = classInfo;
            _mroList = new List<AnalysisValue> { classInfo };
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
            var mroList = new List<AnalysisValue> { _classInfo };
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
                mroList.Add(_classInfo);
            }

            if (_isValid != isValid || !_mroList.SequenceEqual(mroList)) {
                _isValid = isValid;
                _mroList = mroList;
                EnqueueDependents();
            }
        }

        public IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options) {
            return GetAllMembersOfMro(this, moduleContext, options);
        }

        /// <summary>
        /// Compute a list of all members, given the MRO list of types, and taking override rules into account.
        /// </summary>
        public static IDictionary<string, IAnalysisSet> GetAllMembersOfMro(IEnumerable<IAnalysisSet> mro, IModuleContext moduleContext, GetMemberOptions options) {
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
                    var classMembers = classInfo != null ? classInfo.GetAllImmediateMembers(moduleContext, options) : ns.GetAllMembers(moduleContext);

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
            if (addRef) {
                AddDependency(unit);
            }
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

        internal void RecomputeIfNecessary() {
            if (IsValid && _mroList.Any()) {
                var typeId = _mroList.Last().TypeId;
                if (typeId == BuiltinTypeId.Object || typeId == BuiltinTypeId.Type) {
                    return;
                }
            }

            Recompute();
        }
    }
}
