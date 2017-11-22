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
        private readonly FunctionDefinition _functionDefinition;
        private readonly FunctionAnalysisUnit _analysisUnit;
        private readonly ProjectEntry _projectEntry;
        public bool IsStatic;
        public bool IsClassMethod;
        public bool IsProperty;
        private ReferenceDict _references;
        private readonly int _declVersion;
        private int _callDepthLimit;
        private int _callsSinceLimitChange;
        private string _doc;

        internal CallChainSet _allCalls;

        internal FunctionInfo(FunctionDefinition node, AnalysisUnit declUnit, InterpreterScope declScope) {
            _projectEntry = declUnit.ProjectEntry;
            _functionDefinition = node;
            _declVersion = declUnit.ProjectEntry.AnalysisVersion;

            if (_functionDefinition.Name == "__new__") {
                IsClassMethod = true;
            }

            _doc = node.Body?.Documentation?.TrimDocumentation();

            object value;
            if (!ProjectEntry.Properties.TryGetValue(AnalysisLimits.CallDepthKey, out value) ||
                (_callDepthLimit = (value as int?) ?? -1) < 0) {
                _callDepthLimit = declUnit.ProjectState.Limits.CallDepth;
            }

            _analysisUnit = new FunctionAnalysisUnit(this, declUnit, declScope, _projectEntry);
        }

        public ProjectEntry ProjectEntry {
            get {
                return _projectEntry;
            }
        }

        public override IPythonProjectEntry DeclaringModule {
            get {
                return _analysisUnit.ProjectEntry;
            }
        }

        public FunctionDefinition FunctionDefinition {
            get {
                return _functionDefinition;
            }
        }

        public override AnalysisUnit AnalysisUnit {
            get {
                return _analysisUnit;
            }
        }

        public override int DeclaringVersion {
            get {
                return _declVersion;
            }
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var callArgs = ArgumentSet.FromArgs(FunctionDefinition, unit, args, keywordArgNames);

            FunctionAnalysisUnit calledUnit;
            bool updateArguments = true;

            if (callArgs.Count == 0 ||
                (ProjectState.Limits.UnifyCallsToNew && Name == "__new__") ||
                _callDepthLimit == 0) {
                calledUnit = (FunctionAnalysisUnit)AnalysisUnit;
            } else if (FunctionDefinition.ReturnAnnotation != null) {
                // TODO: Reevaluate return annotation in context of arguments
                calledUnit = (FunctionAnalysisUnit)AnalysisUnit;
            } else {
                if (_allCalls == null) {
                    _allCalls = new CallChainSet();
                }

                var chain = new CallChain(node, unit, _callDepthLimit);
                var aggregate = GetAggregate(unit);
                if (!_allCalls.TryGetValue(aggregate, chain, _callDepthLimit, out calledUnit)) {
                    if (unit.ForEval) {
                        // Call expressions that weren't analyzed get the union result
                        // of all calls to this function.
                        var res = AnalysisSet.Empty;
                        foreach (var call in _allCalls.Values) {
                            res = res.Union(call.ReturnValue.GetTypesNoCopy(unit, DeclaringModule));
                        }
                        return res;
                    } else {
                        _callsSinceLimitChange += 1;
                        if (_callsSinceLimitChange >= ProjectState.Limits.DecreaseCallDepth && _callDepthLimit > 1) {
                            _callDepthLimit -= 1;
                            _callsSinceLimitChange = 0;
                            AnalysisLog.ReduceCallDepth(this, _allCalls.Count, _callDepthLimit);

                            _allCalls.Clear();
                            chain = chain.Trim(_callDepthLimit);
                        }

                        calledUnit = new CalledFunctionAnalysisUnit(aggregate, (FunctionAnalysisUnit)AnalysisUnit, chain, callArgs);
                        _allCalls.Add(aggregate, chain, calledUnit);
                        updateArguments = false;
                    }
                }
            }

            if (updateArguments && calledUnit.UpdateParameters(callArgs)) {
                AnalysisLog.UpdateUnit(calledUnit);
            }
            if (keywordArgNames != null && keywordArgNames.Any()) {
                calledUnit.AddNamedParameterReferences(unit, keywordArgNames);
            }

            calledUnit.ReturnValue.AddDependency(unit);
            return calledUnit.ReturnValue.Types;
        }

        private IVersioned GetAggregate(AnalysisUnit unit) {
            IVersioned agg;
            var fau = unit as CalledFunctionAnalysisUnit;
            if (fau == null || _callDepthLimit == 1) {
                // The caller is top-level or a normal FAU, not one with a call chain.
                // Just aggregate the caller w/ ourselves
                agg = AggregateProjectEntry.GetAggregate(unit.ProjectEntry, ProjectEntry);
            } else {
                // The caller is part of a call chain, aggregate ourselves with everyone
                // else who's in it.
                agg = AggregateProjectEntry.GetAggregate(fau.DependencyProject, ProjectEntry);
            }

            return agg;
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
                var defaultValue = GetDefaultValue(ProjectState, p, DeclaringModule.Tree);

                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Parameter, name);
                if (!String.IsNullOrWhiteSpace(defaultValue)) {
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

                if (FunctionDefinition.IsGenerator) {
                    var lambdaExpr = ((ExpressionStatement)FunctionDefinition.Body).Expression;
                    Expression yieldExpr = null;
                    YieldExpression ye;
                    YieldFromExpression yfe;
                    if ((ye = lambdaExpr as YieldExpression) != null) {
                        yieldExpr = ye.Expression;
                    } else if ((yfe = lambdaExpr as YieldFromExpression) != null) {
                        yieldExpr = yfe.Expression;
                    } else {
                        Debug.Assert(false, "lambdaExpr is not YieldExpression or YieldFromExpression");
                    }
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, yieldExpr.ToCodeString(DeclaringModule.Tree));
                } else {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, ((ReturnStatement)FunctionDefinition.Body).Expression.ToCodeString(DeclaringModule.Tree));
                }
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

                var references = new Dictionary<string[], IEnumerable<AnalysisVariable>[]>(new StringArrayComparer());

                var units = new HashSet<AnalysisUnit>();
                units.Add(AnalysisUnit);
                if (_allCalls != null) {
                    units.UnionWith(_allCalls.Values);
                }

                foreach (var unit in units) {
                    var vars = FunctionDefinition.Parameters.Select(p => {
                        VariableDef param;
                        if (unit.Scope.TryGetVariable(p.Name, out param)) {
                            return param;
                        }
                        return null;
                    }).ToArray();

                    var parameters = vars
                        .Select(p => string.Join(", ", p.Types.Select(av => av.ShortDescription).OrderBy(s => s).Distinct()))
                        .ToArray();

                    IEnumerable<AnalysisVariable>[] refs;
                    if (references.TryGetValue(parameters, out refs)) {
                        refs = refs.Zip(vars, (r, v) => r.Concat(ProjectEntry.Analysis.ToVariables(v))).ToArray();
                    } else {
                        refs = vars.Select(v => ProjectEntry.Analysis.ToVariables(v)).ToArray();
                    }
                    references[parameters] = refs;
                }

                foreach (var keyValue in references) {
                    yield return new SimpleOverloadResult(
                        FunctionDefinition.Parameters.Select((p, i) => {
                            var name = MakeParameterName(p);
                            var defaultValue = GetDefaultValue(ProjectState, p, DeclaringModule.Tree);
                            var type = keyValue.Key[i];
                            var refs = keyValue.Value[i];
                            return new ParameterResult(name, string.Empty, type, false, refs, defaultValue);
                        }).ToArray(),
                        FunctionDefinition.Name,
                        Documentation
                    );
                }
            }
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
            if (curParam.DefaultValue != null) {
                // TODO: Support all possible expressions for default values, we should
                // probably have a PythonAst walker for expressions or we should add ToCodeString()
                // onto Python ASTs so they can round trip
                ConstantExpression defaultValue = curParam.DefaultValue as ConstantExpression;
                if (defaultValue != null) {
                    return defaultValue.GetConstantRepr(state.LanguageVersion);
                } else {

                    NameExpression nameExpr = curParam.DefaultValue as NameExpression;
                    if (nameExpr != null) {
                        return nameExpr.Name;
                    } else {

                        DictionaryExpression dict = curParam.DefaultValue as DictionaryExpression;
                        if (dict != null) {
                            if (dict.Items.Count == 0) {
                                return "{}";
                            } else {
                                return "{...}";
                            }
                        } else {

                            ListExpression list = curParam.DefaultValue as ListExpression;
                            if (list != null) {
                                if (list.Items.Count == 0) {
                                    return "[]";
                                } else {
                                    return "[...]";
                                }
                            } else {

                                TupleExpression tuple = curParam.DefaultValue as TupleExpression;
                                if (tuple != null) {
                                    if (tuple.Items.Count == 0) {
                                        return "()";
                                    } else {
                                        return "(...)";
                                    }
                                } else {
                                    return curParam.DefaultValue.ToCodeString(tree).Trim();
                                }
                            }
                        }
                    }
                }
            }
            return null;
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
                return unit.ProjectState.GetConstant(FunctionDefinition.Name);
            }
            if (name == "__doc__") {
                return unit.ProjectState.GetConstant(Documentation);
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
            if (_allCalls != null) {
                scopeSet.UnionWith(_allCalls.Values.Select(au => au.Scope));
            }

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
            if (_allCalls != null) {
                units.UnionWith(_allCalls.Values);
            }

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

            var units = new HashSet<AnalysisUnit>();
            units.Add(AnalysisUnit);
            if (_allCalls != null) {
                units.UnionWith(_allCalls.Values);
            }

            result = result.UnionAll(units.OfType<FunctionAnalysisUnit>().Select(unit => unit.ReturnValue.TypesNoCopy));

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
