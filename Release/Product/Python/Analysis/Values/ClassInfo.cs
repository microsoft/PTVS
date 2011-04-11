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
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class ClassInfo : UserDefinedInfo, IReferenceableContainer {
        private readonly List<ISet<Namespace>> _bases;
        private readonly InstanceInfo _instanceInfo;
        private readonly ClassScope _scope;
        private readonly int _declVersion;
        private ReferenceDict _references;
        private VariableDef<ClassInfo> _subclasses;

        internal ClassInfo(AnalysisUnit unit)
            : base(unit) {
            _instanceInfo = new InstanceInfo(this);
            _bases = new List<ISet<Namespace>>();
            _scope = new ClassScope(this, unit.Ast);
            _declVersion = unit.ProjectEntry.Version;
        }

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, string[] keywordArgNames) {
            if (unit != null) {
                AddCall(node, keywordArgNames, unit, args);
            }

            return _instanceInfo.SelfSet;
        }

        private void AddCall(Node node, string[] keywordArgNames, AnalysisUnit unit, ISet<Namespace>[] argumentVars) {
            var init = GetMember(node, unit, "__init__");
            var initArgs = Utils.Concat(_instanceInfo.SelfSet, argumentVars);

            foreach (var initFunc in init) {
                initFunc.Call(node, unit, initArgs, keywordArgNames);
            }

            // TODO: If we checked for metaclass, we could pass it in as the cls arg here
            var n = GetMember(node, unit, "__new__");
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

        public VariableDef<ClassInfo> SubClasses {
            get {
                if (_subclasses == null) {
                    _subclasses = new VariableDef<ClassInfo>();
                }
                return _subclasses;
            }
        }

        public override string Documentation {
            get {
                if (ClassDefinition.Body != null) {
                    return ClassDefinition.Body.Documentation.TrimDocumentation();
                }
                return "";
            }
        }

        public override LocationInfo Location {
            get {
                return new LocationInfo(DeclaringModule, ClassDefinition.Start.Line, ClassDefinition.Start.Column);
            }
        }

        public override IPythonType PythonType {
            get {
                return this._analysisUnit.ProjectState.Interpreter.GetBuiltinType(BuiltinTypeId.Type);
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
                    foreach (var initFunc in init.Types) {
                        foreach (var overload in initFunc.Overloads) {
                            result.Add(GetInitOverloadResult(overload));
                        }
                    }
                }

                VariableDef @new;
                if (Scope.Variables.TryGetValue("__new__", out @new)) {
                    foreach (var newFunc in @new.Types) {
                        foreach (var overload in newFunc.Overloads) {
                            result.Add(GetNewOverloadResult(overload));
                        }
                    }
                }

                if (result.Count == 0) {
                    foreach (var baseClass in _bases) {
                        foreach (var ns in baseClass) {
                            foreach (var overload in ns.Overloads) {
                                result.Add(
                                    new OverloadResult(
                                        overload.Parameters,
                                        ClassDefinition.Name
                                    )
                                );
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

        public List<ISet<Namespace>> Bases {
            get {
                return _bases;
            }
        }

        public InstanceInfo Instance {
            get {
                return _instanceInfo;
            }
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            var result = new Dictionary<string, ISet<Namespace>>(Scope.Variables.Count);
            foreach (var v in Scope.Variables) {
                result[v.Key] = v.Value.Types;
            }

            foreach (var b in _bases) {
                foreach (var ns in b) {
                    if (ns.Push()) {
                        try {
                            foreach (var kvp in ns.GetAllMembers(moduleContext)) {
                                ISet<Namespace> existing;
                                if (!result.TryGetValue(kvp.Key, out existing) || existing.Count == 0) {
                                    result[kvp.Key] = kvp.Value;
                                } else {
                                    HashSet<Namespace> tmp = new HashSet<Namespace>();
                                    tmp.UnionWith(existing);
                                    tmp.UnionWith(kvp.Value);

                                    result[kvp.Key] = tmp;
                                }
                            }
                        } finally {
                            ns.Pop();
                        }
                    }
                }
            }
            return result;
        }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new SimpleSrcLocation(node.Span));
            }
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            return GetMemberNoReferences(node, unit, name);
        }

        public ISet<Namespace> GetMemberNoReferences(Node node, AnalysisUnit unit, string name) {
            ISet<Namespace> result = null;
            bool ownResult = false;
            var v = Scope.GetVariable(node, unit, name);
            if (v != null) {
                ownResult = false;
                result = v.Types;
            }

            // TODO: Need to search MRO, not bases
            foreach (var baseClass in _bases) {
                foreach (var baseRef in baseClass) {
                    if (baseRef.Push()) {
                        try {
                            ClassInfo klass = baseRef as ClassInfo;
                            ISet<Namespace> baseMembers;
                            if (klass != null) {
                                baseMembers = klass.GetMember(node, unit, name);
                            } else {
                                BuiltinClassInfo builtinClass = baseRef as BuiltinClassInfo;
                                if (builtinClass != null) {
                                    baseMembers = builtinClass.GetMember(node, unit, name);
                                } else {
                                    baseMembers = baseRef.GetMember(node, unit, name);
                                }
                            }

                            AddNewMembers(ref result, ref ownResult, baseMembers);
                        } finally {
                            baseRef.Pop();
                        }
                    }
                }
            }
            return result ?? EmptySet<Namespace>.Instance;
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            var variable = Scope.CreateVariable(node, unit, name, false);
            variable.AddAssignment(node, unit);
            variable.AddTypes(node, unit, value);
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            var v = Scope.GetVariable(node, unit, name);
            if (v != null) {
                v.AddReference(node, unit);
            }
        }

        private static void AddNewMembers(ref ISet<Namespace> result, ref bool ownResult, ISet<Namespace> newMembers) {
            if (!ownResult) {
                if (result != null) {
                    // merging with unowned set for first time
                    if (result.Count == 0) {
                        result = newMembers;
                    } else if (newMembers.Count != 0) {
                        result = new HashSet<Namespace>(result);
                        result.UnionWith(newMembers);
                        ownResult = true;
                    }
                } else {
                    // getting members for the first time
                    result = newMembers;
                }
            } else {
                // just merging in the new members
                result.UnionWith(newMembers);
            }
        }

        public override PythonMemberType ResultType {
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
}
