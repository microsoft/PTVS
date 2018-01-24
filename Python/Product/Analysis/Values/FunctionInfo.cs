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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class FunctionInfo : AnalysisValue, IReferenceableContainer, IHasRichDescription {
        private Dictionary<AnalysisValue, IAnalysisSet> _methods;
        private Dictionary<string, VariableDef> _functionAttrs;
        private readonly FunctionAnalysisUnit _analysisUnit;
        private ReferenceDict _references;
        private readonly int _declVersion;
        private string _doc;

        private readonly ClosureSetDefinition _closureDefinition;
        private readonly Dictionary<ClosureSet, FunctionAnalysisUnit> _callsWithClosure;

        internal FunctionInfo(FunctionDefinition node, AnalysisUnit declUnit, InterpreterScope declScope) {
            ProjectEntry = declUnit.ProjectEntry;
            FunctionDefinition = node;
            _declVersion = declUnit.ProjectEntry.AnalysisVersion;

            if (FunctionDefinition.Name == "__new__") {
                IsClassMethod = true;
            }

            _doc = node.Body?.Documentation?.TrimDocumentation();

            _analysisUnit = new FunctionAnalysisUnit(this, declUnit, declScope, ProjectEntry);

            if (node.ContainsNestedFreeVariables) {
                _closureDefinition = new ClosureSetDefinition(node.Variables);
                _callsWithClosure = new Dictionary<ClosureSet, FunctionAnalysisUnit>();
            }
        }

        public ProjectEntry ProjectEntry { get; }

        public override IPythonProjectEntry DeclaringModule => _analysisUnit.ProjectEntry;

        public FunctionDefinition FunctionDefinition { get; }

        public override AnalysisUnit AnalysisUnit => _analysisUnit;

        public override int DeclaringVersion => _declVersion;

        public IReadOnlyList<string> ClosureNames { get; }

        public bool IsStatic { get; set; }
        public bool IsClassMethod { get; set; }
        public bool IsProperty { get; set; }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var callArgs = ArgumentSet.FromArgs(FunctionDefinition, unit, args, keywordArgNames);

            if (keywordArgNames != null && keywordArgNames.Any()) {
                _analysisUnit.AddNamedParameterReferences(unit, keywordArgNames);
            }

            var res = DoCall(node, unit, _analysisUnit, callArgs);

            if (_closureDefinition != null) {
                FunctionAnalysisUnit calledUnit;
                var key = _closureDefinition.Get(node, unit);
                lock (_callsWithClosure) {
                    if (!_callsWithClosure.TryGetValue(key, out calledUnit)) {
                        calledUnit = new FunctionClosureAnalysisUnit(_analysisUnit);
                    }
                    // Always replace the key
                    _callsWithClosure[key] = calledUnit;
                }

                res = res.Union(DoCall(node, unit, calledUnit, callArgs));
            }


            var context = new ResolutionContext {
                Caller = this,
                CallArgs = callArgs
            };

            if (res.Split(out IReadOnlyList<LazyValueInfo> pi, out res)) {
                res = res.Union(pi.SelectMany(p => {
                    var r = p.Resolve(unit, context);
                    if (!r.Any() && unit.ForEval) {
                        return p.Resolve(unit);
                    }
                    return r;
                }));
            }

            return res;
        }

        private IAnalysisSet DoCall(Node node, AnalysisUnit callingUnit, FunctionAnalysisUnit calledUnit, ArgumentSet callArgs) {
            calledUnit.UpdateParameters(callArgs);
            calledUnit.ReturnValue.AddDependency(callingUnit);
            return calledUnit.ReturnValue.Types;
        }

        public IAnalysisSet ResolveParameter(AnalysisUnit unit, string name) {
            var vd = (_analysisUnit.Scope as FunctionScope)?.GetParameter(name);
            if (unit != AnalysisUnit) {
                vd?.AddDependency(unit);
            }
            return vd?.Types ?? AnalysisSet.Empty;
        }

        public IAnalysisSet ResolveParameter(AnalysisUnit unit, string name, ArgumentSet arguments) {
            var parameters = FunctionDefinition.Parameters;
            if (parameters == null || parameters.Count == 0) {
                return ResolveParameter(unit, name);
            }

            for (int i = 0; i < parameters.Count; ++i) {
                if (parameters[i].Name == name) {
                    IAnalysisSet res = AnalysisSet.Empty;
                    if (i < arguments.Count) {
                        res = res.Union(arguments.Args[i]);
                    }
                    if (parameters[i].IsList) {
                        res = res.Add(new LazyIndexableInfo(parameters[i], arguments.SequenceArgs, () => ResolveParameter(_analysisUnit, parameters[i].Name)));
                    }
                    if (parameters[i].IsDictionary) {
                        res = res.Add(new LazyIndexableInfo(parameters[i], arguments.DictArgs, () => ResolveParameter(_analysisUnit, parameters[i].Name)));
                    }
                    return res;
                }
            }

            return ResolveParameter(unit, name);
        }

        public override string Name {
            get {
                return FunctionDefinition.Name;
            }
        }

        internal IEnumerable<KeyValuePair<string, string>> GetParameterString() {
            for (int i = 0; i < FunctionDefinition.Parameters.Count; i++) {
                if (i != 0) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                var p = FunctionDefinition.Parameters[i];

                var name = MakeParameterName(p);
                var annotation = GetAnnotation(ProjectState, p, DeclaringModule.Tree);
                var defaultValue = GetDefaultValue(ProjectState, p, DeclaringModule.Tree);

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Parameter, name);
                if (!string.IsNullOrWhiteSpace(annotation)) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " : ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, annotation);
                }
                if (!string.IsNullOrWhiteSpace(defaultValue)) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " = ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, defaultValue);
                }
            }
        }

        internal static IEnumerable<KeyValuePair<string, string>> GetReturnTypeString(Func<int, IAnalysisSet> getReturnValue) {
            var retTypes = getReturnValue(0);
            for (int strength = 1; retTypes.Count > 10 && strength <= UnionComparer.MAX_STRENGTH; ++strength) {
                retTypes = getReturnValue(strength);
            }

            var seenNames = new HashSet<string>();
            var addDots = false;
            var descriptions = new List<string>();
            foreach (var av in retTypes) {
                if (av == null) {
                    continue;
                }

                if (av.Push()) {
                    try {
                        var desc = av.ShortDescription;
                        if (!string.IsNullOrWhiteSpace(desc) && seenNames.Add(desc)) {
                            descriptions.Add(desc.Replace("\r\n", "\n").Replace("\n", "\r\n    "));
                        }
                    } finally {
                        av.Pop();
                    }
                } else {
                    addDots = true;
                }
            }

            var first = true;
            descriptions.Sort();
            foreach (var desc in descriptions) {
                if (first) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " -> ");
                    first = false;
                } else {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, desc);
            }

            if (addDots) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "...");
            }
        }

        internal static IEnumerable<KeyValuePair<string,string>> GetDocumentationString(string documentation) {
            if (!String.IsNullOrEmpty(documentation)) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, documentation);
            }
        }

        internal IEnumerable<KeyValuePair<string, string>> GetQualifiedLocationString() {
            var qualifiedNameParts = new Stack<string>();
            for (var item = FunctionDefinition.Parent; item is FunctionDefinition || item is ClassDefinition; item = item.Parent) {
                if (!string.IsNullOrEmpty(item.Name)) {
                    qualifiedNameParts.Push(item.Name);
                }
            }
            if (qualifiedNameParts.Count > 0) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "declared in ");
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, string.Join(".", qualifiedNameParts));
            }
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (FunctionDefinition.IsLambda) {
                bool needsLambda = true;
                foreach (var kv in GetParameterString()) {
                    if (needsLambda) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "lambda ");
                        needsLambda = false;
                    }
                    yield return kv;
                }
                if (needsLambda) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "lambda:");
                } else {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ":");
                }

                yield return new KeyValuePair<string, string>(
                    WellKnownRichDescriptionKinds.Misc, 
                    (
                        (FunctionDefinition.Body as ReturnStatement)?.Expression ??
                        (Node)(FunctionDefinition.Body as ExpressionStatement)?.Expression ??
                        FunctionDefinition.Body
                    ).ToCodeString(DeclaringModule.Tree)
                );
            } else {
                if (FunctionDefinition.IsCoroutine) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "async ");
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "def ");
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, GetFullName());
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "(");
                foreach (var kv in GetParameterString()) {
                    yield return kv;
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ")");
            }

            foreach (var kv in GetReturnTypeString(GetReturnValue)) {
                yield return kv;
            }
            bool hasNl = false;
            var nlKind = WellKnownRichDescriptionKinds.EndOfDeclaration;
            foreach (var kv in GetDocumentationString(Documentation)) {
                if (!hasNl) {
                    yield return new KeyValuePair<string, string>(nlKind, "\r\n");
                    nlKind = WellKnownRichDescriptionKinds.Misc;
                    hasNl = true;
                }
                yield return kv;
            }
            hasNl = false;
            foreach (var kv in GetQualifiedLocationString()) {
                if (!hasNl) {
                    yield return new KeyValuePair<string, string>(nlKind, "\r\n");
                    hasNl = true;
                }
                yield return kv;
            }
        }

        private string GetFullName() {
            var name = FunctionDefinition.Name;
            for (var stmt = FunctionDefinition.Parent; stmt != null; stmt = stmt.Parent) {
                if (stmt.IsGlobal) {
                    return DeclaringModule.ModuleName + "." + name;
                }
                if (!string.IsNullOrEmpty(stmt.Name)) {
                    name = stmt.Name + "." + name;
                }
            }
            return name;
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            if ((instance == ProjectState._noneInst && !IsClassMethod) || IsStatic) {
                return SelfSet;
            }
            if (_methods == null) {
                _methods = new Dictionary<AnalysisValue, IAnalysisSet>();
            }

            IAnalysisSet result;
            if (!_methods.TryGetValue(instance, out result) || result == null) {
                if (IsClassMethod) {
                    _methods[instance] = result = new BoundMethodInfo(this, context).SelfSet;
                } else {
                    _methods[instance] = result = new BoundMethodInfo(this, instance).SelfSet;
                }
            }

            if (IsProperty) {
                return result.Call(node, unit, ExpressionEvaluator.EmptySets, ExpressionEvaluator.EmptyNames);
            }

            return result;
        }

        public override string Documentation {
            get {
                return _doc ?? "";
            }
        }

        public override PythonMemberType MemberType {
            get {
                return IsProperty ? PythonMemberType.Property : PythonMemberType.Function;
            }
        }

        public override string ToString() {
            return "FunctionInfo " + _analysisUnit.FullName + " (" + _declVersion + ")";
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                var start = FunctionDefinition.NameExpression.GetStart(FunctionDefinition.GlobalParent);
                var end = FunctionDefinition.GetEnd(FunctionDefinition.GlobalParent);
                return new[] {
                    new LocationInfo(
                        ProjectEntry.FilePath,
                        start.Line,
                        start.Column,
                        end.Line,
                        end.Column
                    )
                };
            }
        }

        private class StringArrayComparer : IEqualityComparer<string[]> {
            private IEqualityComparer<string> _comparer;

            public StringArrayComparer() {
                _comparer = StringComparer.Ordinal;
            }

            public StringArrayComparer(IEqualityComparer<string> comparer) {
                _comparer = comparer;
            }
            
            public bool Equals(string[] x, string[] y) {
                if (x == null || y == null) {
                    return x == null && y == null;
                }

                if (x.Length != y.Length) {
                    return false;
                }

                for (int i = 0; i < x.Length; ++i) {
                    if (!_comparer.Equals(x[i], y[i])) {
                        return false;
                    }
                }
                return true;
            }

            public int GetHashCode(string[] obj) {
                if (obj == null) {
                    return 0;
                }
                int hc = 261563 ^ obj.Length;
                foreach (var p in obj) {
                    hc ^= _comparer.GetHashCode(p);
                }
                return hc;
            }
        }

        public override IEnumerable<OverloadResult> Overloads {
            get {
                if (_functionAttrs != null && _functionAttrs.TryGetValue("__wrapped__", out VariableDef wrapped)) {
                    foreach (var o in wrapped.TypesNoCopy.SelectMany(n => n.Overloads)) {
                        yield return o;
                    }
                }

                var unit = AnalysisUnit;
                var vars = FunctionDefinition.Parameters.Select(p => {
                    VariableDef param;
                    if (unit.Scope.TryGetVariable(p.Name, out param)) {
                        return param;
                    }
                    return null;
                }).ToArray();

                var parameters = vars.Select(p => p == null ? null : string.Join(", ", p.Types.GetShortDescriptions())).ToArray();
                var refs = vars.Select(v => ProjectEntry.Analysis.ToVariables(v)).ToArray();

                yield return new SimpleOverloadResult(
                    FunctionDefinition.Parameters.Select((p, i) => ToParameterResult(p, parameters[i], refs[i], ProjectState, DeclaringModule.Tree)).ToArray(),
                    FunctionDefinition.Name,
                    Documentation
                );
            }
        }

        internal static ParameterResult ToParameterResult(Parameter p, string type, IEnumerable<AnalysisVariable> refs, PythonAnalyzer state, PythonAst tree) {
            if (p.IsDictionary) {
                return new ParameterResult("**" + p.Name ?? "", null, null, false, refs, null);
            } else if (p.IsList) {
                return new ParameterResult("*" + p.Name ?? "", null, null, false, refs, null);
            }

            var name = p.Name ?? "";
            var defaultValue = GetDefaultValue(state, p, tree);
            return new ParameterResult(name, null, type, false, refs, defaultValue);
        }

        internal static string MakeParameterName(Parameter curParam) {
            string name = curParam.Name;
            if (curParam.IsDictionary) {
                name = "**" + name;
            } else if (curParam.IsList) {
                name = "*" + curParam.Name;
            }

            return name;
        }

        internal static string GetDefaultValue(PythonAnalyzer state, Parameter curParam, PythonAst tree) {
            var v = curParam.DefaultValue;
            if (v == null) {
                return null;
            } else if (v is ConstantExpression ce) {
                return ce.GetConstantRepr(state.LanguageVersion);
            } else if (v is NameExpression ne) {
                return ne.Name;
            } else if (v is DictionaryExpression dict) {
                return dict.Items.Any() ? "{...}" : "{}";
            } else if (v is ListExpression list) {
                return list.Items.Any() ? "[...]" : "[]";
            } else if (v is TupleExpression tuple) {
                return tuple.Items.Any() ? "(...)" : "()";
            } else {
                return v.ToCodeString(tree, CodeFormattingOptions.Traditional).Trim();
            }
        }

        internal static string GetAnnotation(PythonAnalyzer state, Parameter curParam, PythonAst tree) {
            var a = curParam.Annotation;
            if (a == null) {
                return null;
            }
            return a.ToCodeString(tree, CodeFormattingOptions.Traditional).Trim();
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            if (_functionAttrs == null) {
                _functionAttrs = new Dictionary<string, VariableDef>();
            }

            VariableDef varRef;
            if (!_functionAttrs.TryGetValue(name, out varRef)) {
                _functionAttrs[name] = varRef = new VariableDef();
            }
            varRef.AddAssignment(node, unit);
            varRef.AddTypes(unit, value, true, DeclaringModule);

            if (name == "__doc__") {
                _doc = string.Join(Environment.NewLine, varRef.TypesNoCopy.OfType<ConstantInfo>()
                    .Select(ci => (ci.Value as string) ?? (ci.Value as AsciiString)?.String)
                    .Where(s => !string.IsNullOrEmpty(s) && (_doc == null || !_doc.Contains(s)))
                );
            }
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return ProjectState.ClassInfos[BuiltinTypeId.Function].GetMember(node, unit, name);
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            VariableDef tmp;
            if (_functionAttrs != null && _functionAttrs.TryGetValue(name, out tmp)) {
                tmp.AddDependency(unit);
                tmp.AddReference(node, unit);

                return tmp.Types;
            }

            // TODO: Create one and add a dependency
            if (name == "__name__") {
                return unit.State.GetConstant(FunctionDefinition.Name);
            }
            if (name == "__doc__") {
                return unit.State.GetConstant(Documentation);
            }

            return GetTypeMember(node, unit, name);
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (!options.HasFlag(GetMemberOptions.DeclaredOnly) && (_functionAttrs == null || _functionAttrs.Count == 0)) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].GetAllMembers(moduleContext, options);
            }

            Dictionary<string, IAnalysisSet> res;
            if (options.HasFlag(GetMemberOptions.DeclaredOnly)) {
                res = new Dictionary<string, IAnalysisSet>();
            } else {
                res = new Dictionary<string, IAnalysisSet>(ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.GetAllMembers(moduleContext));
            }

            if (_functionAttrs != null) {
                foreach (var variable in _functionAttrs) {
                    IAnalysisSet existing;
                    if (!res.TryGetValue(variable.Key, out existing)) {
                        res[variable.Key] = variable.Value.Types;
                    } else {
                        res[variable.Key] = existing.Union(variable.Value.TypesNoCopy);
                    }
                }
            }
            return res;
        }

        // Returns False if no more parameters can be updated for this unit.
        private bool UpdateSingleDefaultParameter(AnalysisUnit unit, InterpreterScope scope, int index, IParameterInfo info) {
            if (index >= FunctionDefinition.Parameters.Count) {
                return false;
            }
            VariableDef param;
            var name = FunctionDefinition.Parameters[index].Name;
            if (scope.TryGetVariable(name, out param)) {
                var av = ProjectState.GetAnalysisSetFromObjects(info.ParameterTypes);

                if ((info.IsParamArray && !(param is ListParameterVariableDef)) ||
                    (info.IsKeywordDict && !(param is DictParameterVariableDef))) {
                    return false;
                }

                param.AddTypes(unit, av, true, DeclaringModule);
            }

            return true;
        }

        internal void UpdateDefaultParameters(AnalysisUnit unit, IEnumerable<IParameterInfo> parameters) {
            var finishedScopes = new HashSet<InterpreterScope>();
            var scopeSet = new HashSet<InterpreterScope>();
            scopeSet.Add(AnalysisUnit.Scope);

            int index = 0;
            foreach (var p in parameters) {
                foreach (var scope in scopeSet) {
                    if (finishedScopes.Contains(scope)) {
                        continue;
                    }

                    if (!UpdateSingleDefaultParameter(unit, scope, index, p)) {
                        finishedScopes.Add(scope);
                    }
                }
                index += 1;
            }
        }

        internal IAnalysisSet[] GetParameterTypes(int unionStrength = 0) {
            var result = new IAnalysisSet[FunctionDefinition.Parameters.Count];
            var units = new HashSet<AnalysisUnit>();
            units.Add(AnalysisUnit);

            for (int i = 0; i < result.Length; ++i) {
                result[i] = (unionStrength >= 0 && unionStrength <= UnionComparer.MAX_STRENGTH)
                    ? AnalysisSet.CreateUnion(UnionComparer.Instances[unionStrength])
                    : AnalysisSet.Empty;

                VariableDef param;
                foreach (var unit in units) {
                    if (unit != null && unit.Scope != null && unit.Scope.TryGetVariable(FunctionDefinition.Parameters[i].Name, out param)) {
                        result[i] = result[i].Union(param.TypesNoCopy);
                    }
                }
            }

            return result;
        }

        internal IAnalysisSet GetReturnValue(int unionStrength = 0) {
            var result = (unionStrength >= 0 && unionStrength <= UnionComparer.MAX_STRENGTH)
                ? AnalysisSet.CreateUnion(UnionComparer.Instances[unionStrength])
                : AnalysisSet.Empty;

            var fau = AnalysisUnit as FunctionAnalysisUnit;
            if (fau != null) {
                result = result.Union(fau.ReturnValue.TypesNoCopy.Resolve(fau));
            }

            return result;
        }

        public PythonAnalyzer ProjectState { get { return ProjectEntry.ProjectState; } }

        internal override void AddReference(Node node, AnalysisUnit unit) {
            if (!unit.ForEval) {
                if (_references == null) {
                    _references = new ReferenceDict();
                }
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new EncodedLocation(unit, node));
            }
        }

        internal override IEnumerable<LocationInfo> References {
            get {
                if (_references != null) {
                    return _references.AllReferences;
                }
                return new LocationInfo[0];
            }
        }

        public override IPythonType PythonType {
            get { return ProjectState.Types[BuiltinTypeId.Function]; }
        }

        internal override bool IsOfType(IAnalysisSet klass) {
            return klass.Contains(ProjectState.ClassInfos[BuiltinTypeId.Function]);
        }

        public override bool Equals(object obj) {
            if (obj is FunctionInfo fi) {
                return fi.FunctionDefinition == FunctionDefinition;
            }
            return false;
        }

        public override int GetHashCode() => FunctionDefinition.GetHashCode();

        internal override bool UnionEquals(AnalysisValue av, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return av is FunctionInfo || av is BuiltinFunctionInfo || av == ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionEquals(av, strength);
        }

        internal override int UnionHashCode(int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance.UnionHashCode(strength);
            }
            return base.UnionHashCode(strength);
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            if (strength >= MergeStrength.ToObject) {
                return ProjectState.ClassInfos[BuiltinTypeId.Function].Instance;
            }
            return base.UnionMergeTypes(av, strength);
        }

        #region IReferenceableContainer Members

        public IEnumerable<IReferenceable> GetDefinitions(string name) {
            VariableDef def;
            if (_functionAttrs != null && _functionAttrs.TryGetValue(name, out def)) {
                return new IReferenceable[] { def };
            }
            return new IReferenceable[0];
        }

        #endregion

    }
}
