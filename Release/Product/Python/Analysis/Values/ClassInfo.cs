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
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ClassInfo : UserDefinedInfo, IReferenceableContainer {
        private readonly List<ISet<Namespace>> _bases;
        private Mro _mro;
        private readonly InstanceInfo _instanceInfo;
        private readonly ClassScope _scope;
        private readonly int _declVersion;
        private VariableDef _metaclass;
        private ReferenceDict _references;
        private VariableDef _subclasses;
        private Namespace _baseUserType;    // base most user defined type, used for unioning types during type explosion

        internal ClassInfo(AnalysisUnit unit, ClassDefinition klass)
            : base(unit) {
            _instanceInfo = new InstanceInfo(this);
            _bases = new List<ISet<Namespace>>();
            _scope = new ClassScope(this, klass);
            _declVersion = unit.ProjectEntry.AnalysisVersion;
            _mro = new Mro(this);
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            if (unit != null) {
                AddCall(node, keywordArgNames, unit, args);
            }

            return _instanceInfo.SelfSet;
        }

        private void AddCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, ISet<Namespace>[] argumentVars) {
            var init = GetMemberNoReferences(node, unit, "__init__", false);
            var initArgs = Utils.Concat(_instanceInfo.SelfSet, argumentVars);

            foreach (var initFunc in init) {
                initFunc.Call(node, unit, initArgs, keywordArgNames);
            }

            // TODO: If we checked for metaclass, we could pass it in as the cls arg here
            var n = GetMemberNoReferences(node, unit, "__new__", false);
            var newArgs = Utils.Concat(EmptySet<Namespace>.Instance, argumentVars);
            foreach (var newFunc in n) {
                // TODO: Really we should be returning the result of __new__ if it's overridden
                newFunc.Call(node, unit, newArgs, keywordArgNames);
            }
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

        public IEnumerable<ISet<Namespace>> Bases {
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

        public void SetBases(IEnumerable<ISet<Namespace>> bases) {
            _bases.Clear();
            _bases.AddRange(bases);
            _baseUserType = null;
            Mro.Recompute();
        }

        public void SetBase(int index, ISet<Namespace> baseSet) {
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
        public IDictionary<string, ISet<Namespace>> GetAllImmediateMembers(IModuleContext moduleContext) {
            var result = new Dictionary<string, ISet<Namespace>>(Scope.Variables.Count);

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

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
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

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return GetMemberNoReferences(node, unit, name).GetDescriptor(node, unit.ProjectState._noneInst, this, unit);
        }

        /// <summary>
        /// Get the member of this class by name that is not inherited from one of its base classes.
        /// </summary>
        public ISet<Namespace> GetImmediateMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            ISet<Namespace> result = null;
            var v = Scope.GetVariable(node, unit, name, addRef);
            if (v != null) {
                result = v.Types;
            }
            return result;
        }

        public ISet<Namespace> GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
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

        private ISet<Namespace> GetOldStyleMember(string name, IModuleContext context) {
            switch (name) {
                case "__doc__": 
                case "__class__":
                    return GetObjectMember(context, name).SelfSet;
            }
            return EmptySet<Namespace>.Instance;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
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
            get {
                return _scope;
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

        public override bool UnionEquals(Namespace ns) {
            if (Object.ReferenceEquals(this, ns)) {
                return true;
            }

            ClassInfo otherClass = ns as ClassInfo;
            if (otherClass == null) {
                return false;
            }

            EnsureBaseUserType();
            if (_baseUserType != null) {
                otherClass.EnsureBaseUserType();
                return otherClass._baseUserType == _baseUserType;
            }
            return false;
        }

        public override int UnionHashCode() {
            EnsureBaseUserType();
            if (_baseUserType != null) {
                return _baseUserType.GetHashCode();
            }
            return GetHashCode();
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

        private IEnumerable<IReferenceable> GetDefinitions(string name,IEnumerable<Namespace> nses) {
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
    internal class Mro : DependentData, IEnumerable<ISet<Namespace>> {
        private readonly ClassInfo _classInfo;
        private List<ISet<Namespace>> _mroList;
        private bool _isValid = true;

        public Mro(ClassInfo classInfo) {
            _classInfo = classInfo;
            _mroList = new List<ISet<Namespace>> { classInfo };
        }

        public bool IsValid {
            get { return _isValid; }
        }

        public IEnumerator<ISet<Namespace>> GetEnumerator() {
            return _mroList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public void Recompute() {
            var mroList = new List<ISet<Namespace>> { _classInfo.SelfSet };
            var isValid = true;

            var bases = _classInfo.Bases;
            if (bases.Any()) {
                var mergeList = new List<List<Namespace>>();
                var finalMro = new List<Namespace>();

                foreach (var baseClass in bases.SelectMany(x => x)) {
                    var klass = baseClass as ClassInfo;
                    var builtInClass = baseClass as BuiltinClassInfo;
                    if (klass != null && klass.Push()) {
                        try {
                            if (!klass.Mro.IsValid) {
                                isValid = false;
                                break;
                            }
                            finalMro.Add(klass);
                            mergeList.Add(klass.Mro.SelectMany(x => x).ToList());
                        } finally {
                            klass.Pop();
                        }
                    } else if (builtInClass != null && builtInClass.Push()) {
                        try {
                            finalMro.Add(builtInClass);
                            mergeList.Add(builtInClass.Mro.SelectMany(x => x).ToList());
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

        public IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            return GetAllMembersOfMro(this, moduleContext);
        }

        /// <summary>
        /// Compute a list of all members, given the MRO list of types, and taking override rules into account.
        /// </summary>
        public static IDictionary<string, ISet<Namespace>> GetAllMembersOfMro(IEnumerable<ISet<Namespace>> mro, IModuleContext moduleContext) {
            var result = new Dictionary<string, ISet<Namespace>>();

            // MRO is a list of namespaces corresponding to classes, but each entry can be a union of several different classes.
            // Therefore, within a single entry, we want to make a union of members from each; but between entries, we
            // want the first entry in MRO to suppress any members with the same names from the following entries.
            foreach (var entry in mro) {
                var entryMembers = new Dictionary<string, ISet<Namespace>>();
                foreach (var ns in entry) {
                    // If it's another non-builtin class, we don't want its inherited members, since we'll account
                    // for them while processing our own MRO - we only want its immediate members.
                    var classInfo = ns as ClassInfo;
                    var classMembers = classInfo != null ? classInfo.GetAllImmediateMembers(moduleContext) : ns.GetAllMembers(moduleContext);

                    foreach (var kvp in classMembers) {
                        ISet<Namespace> existing;
                        bool ownExisting = false;
                        if (!entryMembers.TryGetValue(kvp.Key, out existing) || existing.Count == 0) {
                            entryMembers[kvp.Key] = kvp.Value;
                        } else {
                            ISet<Namespace> tmp = existing.Union(kvp.Value, ref ownExisting);
                            entryMembers[kvp.Key] = tmp;
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

        public ISet<Namespace> GetMemberNoReferences(Node node, AnalysisUnit unit, string name, bool addRef = true) {
            return GetMemberFromMroNoReferences(this, node, unit, name, addRef);
        }

        /// <summary>
        /// Get the member by name, given the MRO list, and taking override rules into account.
        /// </summary>
        public static ISet<Namespace> GetMemberFromMroNoReferences(IEnumerable<ISet<Namespace>> mro, Node node, AnalysisUnit unit, string name, bool addRef = true) {
            if (mro == null) {
                return EmptySet<Namespace>.Instance;
            }

            // Union all members within a single MRO entry, but stop at the first entry that yields a non-empty set since it overrides any that follow.
            ISet<Namespace> result = null;
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
