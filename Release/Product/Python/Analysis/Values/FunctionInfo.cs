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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class FunctionInfo : Namespace, IReferenceableContainer {
        private Dictionary<Namespace, INamespaceSet> _methods;
        private Dictionary<string, VariableDef> _functionAttrs;
        private readonly FunctionDefinition _functionDefinition;
        private readonly FunctionAnalysisUnit _analysisUnit;
        private readonly ProjectEntry _projectEntry;
        public bool IsStatic;
        public bool IsClassMethod;
        public bool IsProperty;
        private ReferenceDict _references;
        private readonly int _declVersion;

        static readonly CallChain _arglessCall = new CallChain(new CallExpression(null, null));
        internal Dictionary<CallChain, FunctionAnalysisUnit> _allCalls;

        internal FunctionInfo(FunctionDefinition node, AnalysisUnit declUnit, InterpreterScope declScope) {
            _projectEntry = declUnit.ProjectEntry;
            _functionDefinition = node;
            _declVersion = declUnit.ProjectEntry.AnalysisVersion;

            if (Name == "__new__") {
                IsClassMethod = true;
            }

            _analysisUnit = new FunctionAnalysisUnit(this, declUnit, declScope);
        }

        public ProjectEntry ProjectEntry {
            get {
                return _projectEntry;
            }
        }

        public override ProjectEntry DeclaringModule {
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

        public override INamespaceSet Call(Node node, AnalysisUnit unit, INamespaceSet[] args, NameExpression[] keywordArgNames) {
            var callArgs = ArgumentSet.FromArgs(FunctionDefinition, unit, args, keywordArgNames);

            if (_allCalls == null) {
                _allCalls = new Dictionary<CallChain, FunctionAnalysisUnit>();
            }

            FunctionAnalysisUnit calledUnit;
            bool updateArguments = true;

            if (callArgs.Count == 0 || (ProjectState.Limits.UnifyCallsToNew && Name == "__new__")) {
                calledUnit = (FunctionAnalysisUnit)AnalysisUnit;
            } else {
                var chain = new CallChain(node, unit, unit.ProjectState.Limits.CallDepth);
                if (!_allCalls.TryGetValue(chain, out calledUnit)) {
                    if (unit.ForEval) {
                        // Call expressions that weren't analyzed get the union result
                        // of all calls to this function.
                        var res = NamespaceSet.Empty;
                        foreach (var call in _allCalls.Values) {
                            res = res.Union(call.ReturnValue.TypesNoCopy);
                        }
                        return res;
                    } else {
                        _allCalls[chain] = calledUnit = new FunctionAnalysisUnit((FunctionAnalysisUnit)AnalysisUnit, chain, callArgs);
                        updateArguments = false;
                    }
                }
            }

            if (updateArguments && calledUnit.UpdateParameters(callArgs)) {
#if DEBUG
                // Checks whether these arguments can be added ad nauseum.
                if (calledUnit.UpdateParameters(callArgs) && calledUnit.UpdateParameters(callArgs) && calledUnit.UpdateParameters(callArgs)) {
                    AnalysisLog.Add("BadArgs", calledUnit, callArgs);
                }
#endif
                AnalysisLog.UpdateUnit(calledUnit);
            }


            calledUnit.ReturnValue.AddDependency(unit);
            return calledUnit.ReturnValue.Types;
        }

        public override string Name {
            get {
                return FunctionDefinition.Name;
            }
        }

        internal void AddParameterString(StringBuilder result) {
            // TODO: Include parameter information
            result.Append("...");
        }

        internal void AddReturnTypeString(StringBuilder result) {
            bool first = true;
            for (int strength = 0; strength <= UnionComparer.MAX_STRENGTH; ++strength) {
                var retTypes = GetReturnValue(strength);
                if (retTypes.Count == 0) {
                    first = false;
                    break;
                }
                if (retTypes.Count <= 10) {
                    var seenNames = new HashSet<string>();
                    foreach (var ns in retTypes) {
                        if (ns == null) {
                            continue;
                        }

                        if (ns.Push()) {
                            try {
                                if (!string.IsNullOrWhiteSpace(ns.ShortDescription) && seenNames.Add(ns.ShortDescription)) {
                                    if (first) {
                                        result.Append(" -> ");
                                        first = false;
                                    } else {
                                        result.Append(", ");
                                    }
                                    AppendDescription(result, ns);
                                }
                            } finally {
                                ns.Pop();
                            }
                        } else {
                            result.Append("...");
                        }
                    }
                    break;
                }
            }
        }

        internal void AddDocumentationString(StringBuilder result) {
            if (!String.IsNullOrEmpty(Documentation)) {
                result.AppendLine();
                result.Append(Documentation);
            }
        }

        internal void AddQualifiedLocationString(StringBuilder result) {
            var qualifiedNameParts = new Stack<string>();
            for (var item = FunctionDefinition.Parent; item is FunctionDefinition || item is ClassDefinition; item = item.Parent) {
                if (!string.IsNullOrEmpty(item.Name)) {
                    qualifiedNameParts.Push(item.Name);
                }
            }
            if (qualifiedNameParts.Count > 0) {
                result.AppendLine();
                result.Append("declared in ");
                result.Append(string.Join(".", qualifiedNameParts));
            }
        }

        public override string Description {
            get {
                var result = new StringBuilder();
                if (FunctionDefinition.IsLambda) {
                    result.Append("lambda ");
                    AddParameterString(result);
                    result.Append(": ");

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
                        result.Append(yieldExpr.ToCodeString(DeclaringModule.Tree));
                    } else {
                        result.Append(((ReturnStatement)FunctionDefinition.Body).Expression.ToCodeString(DeclaringModule.Tree));
                    }
                } else {
                    result.Append("def ");
                    result.Append(FunctionDefinition.Name);
                    result.Append("(");
                    AddParameterString(result);
                    result.Append(")");
                }

                AddReturnTypeString(result);
                AddDocumentationString(result);
                AddQualifiedLocationString(result);

                return result.ToString();
            }
        }

        private static void AppendDescription(StringBuilder result, Namespace key) {
            result.Append(key.ShortDescription);
        }

        public override INamespaceSet GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            if ((instance == ProjectState._noneInst && !IsClassMethod) || IsStatic) {
                return SelfSet;
            }
            if (_methods == null) {
                _methods = new Dictionary<Namespace, INamespaceSet>();
            }

            INamespaceSet result;
            if (!_methods.TryGetValue(instance, out result) || result == null) {
                if (IsClassMethod) {
                    _methods[instance] = result = new BoundMethodInfo(this, context).SelfSet;
                } else {
                    _methods[instance] = result = new BoundMethodInfo(this, instance).SelfSet;
                }
            }

            if (IsProperty) {
                return result.Call(node, unit, ExpressionEvaluator.EmptyNamespaces, ExpressionEvaluator.EmptyNames);
            }

            return result;
        }

        public override string Documentation {
            get {
                if (FunctionDefinition.Body != null) {
                    return FunctionDefinition.Body.Documentation.TrimDocumentation();
                }
                return "";
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
                return new[] { new LocationInfo(
                    ProjectEntry,
                    start.Line,
                    start.Column)
                };
            }
        }

        public override ICollection<OverloadResult> Overloads {
            get {
                var parameters = new ParameterResult[FunctionDefinition.Parameters.Count];
                for (int i = 0; i < FunctionDefinition.Parameters.Count; i++) {
                    var curParam = FunctionDefinition.Parameters[i];
                    VariableDef param;
                    if (AnalysisUnit.Scope.Variables.TryGetValue(curParam.Name, out param)) {
                        parameters[i] = MakeParameterResult(ProjectState, curParam, DeclaringModule.Tree,
                            ProjectEntry.Analysis.ToVariables(param));
                    }
                }

                return new OverloadResult[] {
                    new SimpleOverloadResult(parameters, FunctionDefinition.Name, Documentation)
                };
            }
        }

        internal static ParameterResult MakeParameterResult(PythonAnalyzer state, Parameter curParam, PythonAst tree, IEnumerable<IAnalysisVariable> variables = null) {
            string name = curParam.Name;
            if (curParam.IsDictionary) {
                name = "**" + name;
            } else if (curParam.IsList) {
                name = "*" + curParam.Name;
            }

            if (curParam.DefaultValue != null) {
                // TODO: Support all possible expressions for default values, we should
                // probably have a PythonAst walker for expressions or we should add ToCodeString()
                // onto Python ASTs so they can round trip
                ConstantExpression defaultValue = curParam.DefaultValue as ConstantExpression;
                if (defaultValue != null) {
                    name = name + " = " + GetConstantRepr(state, defaultValue);
                } else {

                    NameExpression nameExpr = curParam.DefaultValue as NameExpression;
                    if (nameExpr != null) {
                        name = name + " = " + nameExpr.Name;
                    } else {

                        DictionaryExpression dict = curParam.DefaultValue as DictionaryExpression;
                        if (dict != null) {
                            if (dict.Items.Count == 0) {
                                name = name + " = {}";
                            } else {
                                name = name + " = {...}";
                            }
                        } else {

                            ListExpression list = curParam.DefaultValue as ListExpression;
                            if (list != null) {
                                if (list.Items.Count == 0) {
                                    name = name + " = []";
                                } else {
                                    name = name + " = [...]";
                                }
                            } else {

                                TupleExpression tuple = curParam.DefaultValue as TupleExpression;
                                if (tuple != null) {
                                    if (tuple.Items.Count == 0) {
                                        name = name + " = ()";
                                    } else {
                                        name = name + " = (...)";
                                    }
                                } else {
                                    name = name + " = " + curParam.DefaultValue.ToCodeString(tree);
                                }
                            }
                        }
                    }
                }
            }

            var newParam = new ParameterResult(name, String.Empty, "object", false, variables);
            return newParam;
        }

        private static string GetConstantRepr(PythonAnalyzer state, ConstantExpression value) {
            if (value.Value == null) {
                return "None";
            } else if (value.Value is AsciiString) {
                StringBuilder res = new StringBuilder();
                if (state.LanguageVersion.Is3x()) {
                    res.Append("b");
                }
                res.Append("'");
                var bytes = ((AsciiString)value.Value).String;
                foreach (var b in bytes) {
                    switch(b) {
                        case '\a': res.Append("\\a"); break;
                        case '\b': res.Append("\\b"); break;
                        case '\f': res.Append("\\f"); break;
                        case '\n': res.Append("\\n"); break;
                        case '\r': res.Append("\\r"); break;
                        case '\t': res.Append("\\t"); break;
                        case '\v': res.Append("\\v"); break;
                        case '\'': res.Append("\\'"); break;
                        case '\\': res.Append("\\\\"); break;
                        default: res.Append(b); break;
                    }
                }
                res.Append("'");
                return res.ToString();
            } else if (value.Value is string) {
                StringBuilder res = new StringBuilder();
                if (state.LanguageVersion.Is2x()) {
                    res.Append("u");
                }

                res.Append("'");
                string str = (string)value.Value;
                foreach (var c in str) {
                    switch (c) {
                        case '\a': res.Append("\\a"); break;
                        case '\b': res.Append("\\b"); break;
                        case '\f': res.Append("\\f"); break;
                        case '\n': res.Append("\\n"); break;
                        case '\r': res.Append("\\r"); break;
                        case '\t': res.Append("\\t"); break;
                        case '\v': res.Append("\\v"); break;
                        case '\'': res.Append("\\'"); break;
                        case '\\': res.Append("\\\\"); break;
                        default: res.Append(c); break;
                    }
                }
                res.Append("'");
                return res.ToString();
            } else if (value.Value is Complex) {
                Complex x = (Complex)value.Value;

                if (x.Real != 0) {
                    if (x.Imaginary < 0 || IsNegativeZero(x.Imaginary)) {
                        return "(" + FormatComplexValue(x.Real) + FormatComplexValue(x.Imaginary) + "j)";
                    } else /* x.Imaginary() is NaN or >= +0.0 */ {
                        return "(" + FormatComplexValue(x.Real) + "+" + FormatComplexValue(x.Imaginary) + "j)";
                    }
                }

                return FormatComplexValue(x.Imaginary) + "j";
            } else if (value.Value is BigInteger) {
                if (state.LanguageVersion.Is2x()) {
                    return value.Value.ToString() + "L";
                }
            }

            // TODO: We probably need to handle more primitives here
            return value.Value.ToString();
        }

        private static NumberFormatInfo FloatingPointNumberFormatInfo;

        private static NumberFormatInfo nfi {
            get {
                if (FloatingPointNumberFormatInfo == null) {
                    NumberFormatInfo numberFormatInfo = ((CultureInfo)CultureInfo.InvariantCulture.Clone()).NumberFormat;
                    // The CLI formats as "Infinity", but CPython formats differently
                    numberFormatInfo.PositiveInfinitySymbol = "inf";
                    numberFormatInfo.NegativeInfinitySymbol = "-inf";
                    numberFormatInfo.NaNSymbol = "nan";
                    numberFormatInfo.NumberDecimalDigits = 0;

                    FloatingPointNumberFormatInfo = numberFormatInfo;
                }
                return FloatingPointNumberFormatInfo;
            }
        }

        private static string FormatComplexValue(double x) {
            return String.Format(nfi, "{0,0:f0}", x);
        }

        private static bool IsNegativeZero(double value) {
            return (value == 0.0) && double.IsNegativeInfinity(1.0 / value);
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, INamespaceSet value) {
            if (_functionAttrs == null) {
                _functionAttrs = new Dictionary<string, VariableDef>();
            }

            VariableDef varRef;
            if (!_functionAttrs.TryGetValue(name, out varRef)) {
                _functionAttrs[name] = varRef = new VariableDef();
            }
            varRef.AddAssignment(node, unit);
            varRef.AddTypes(unit, value);
        }

        public override INamespaceSet GetMember(Node node, AnalysisUnit unit, string name) {
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

            return ProjectState._functionType.GetMember(node, unit, name);
        }

        public override IDictionary<string, INamespaceSet> GetAllMembers(IModuleContext moduleContext) {
            if (_functionAttrs == null || _functionAttrs.Count == 0) {
                return ProjectState._functionType.GetAllMembers(moduleContext);
            }

            var res = new Dictionary<string, INamespaceSet>(ProjectState._functionType.GetAllMembers(moduleContext));
            foreach (var variable in _functionAttrs) {
                INamespaceSet existing;
                if (!res.TryGetValue(variable.Key, out existing)) {
                    res[variable.Key] = variable.Value.Types;
                } else {
                    res[variable.Key] = existing.Union(variable.Value.TypesNoCopy);
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
            if (scope.Variables.TryGetValue(name, out param)) {
                var ns = ProjectState.GetNamespaceFromObjects(info.ParameterType);

                if ((info.IsParamArray && !(param is ListParameterVariableDef)) ||
                    (info.IsKeywordDict && !(param is DictParameterVariableDef))) {
                    return false;
                }

                param.AddTypes(unit, ns);
            }

            return true;
        }

        internal void UpdateDefaultParameters(AnalysisUnit unit, IEnumerable<IParameterInfo> parameters) {
            var finishedScopes = new HashSet<InterpreterScope>();
            var scopeSet = new HashSet<InterpreterScope>();
            scopeSet.Add(AnalysisUnit.Scope);
            if (_allCalls != null) {
                scopeSet.UnionWith(_allCalls.Select(au => au.Value.Scope));
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

        internal INamespaceSet[] GetParameterTypes(int unionStrength = 0) {
            var result = new INamespaceSet[FunctionDefinition.Parameters.Count];
            var units = new HashSet<AnalysisUnit>();
            units.Add(AnalysisUnit);
            if (_allCalls != null) {
                units.UnionWith(_allCalls.Values);
            }

            for (int i = 0; i < result.Length; ++i) {
                result[i] = (unionStrength >= 0 && unionStrength <= UnionComparer.MAX_STRENGTH)
                    ? NamespaceSet.CreateUnion(UnionComparer.Instances[unionStrength])
                    : NamespaceSet.Empty;

                VariableDef param;
                foreach (var unit in units) {
                    if (unit != null && unit.Scope != null && unit.Scope.Variables.TryGetValue(FunctionDefinition.Parameters[i].Name, out param)) {
                        result[i] = result[i].Union(param.TypesNoCopy);
                    }
                }
            }

            return result;
        }

        internal INamespaceSet GetReturnValue(int unionStrength = 0) {
            var result = (unionStrength >= 0 && unionStrength <= UnionComparer.MAX_STRENGTH)
                ? NamespaceSet.CreateUnion(UnionComparer.Instances[unionStrength])
                : NamespaceSet.Empty;

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
                _references.GetReferences(unit.DeclaringModule.ProjectEntry).AddReference(new EncodedLocation(unit.Tree, node));
            }
        }

        public override IEnumerable<LocationInfo> References {
            get {
                if (_references != null) {
                    return _references.AllReferences;
                }
                return new LocationInfo[0];
            }
        }

        public override IPythonType PythonType {
            get { return ProjectState.Types.Function; }
        }

        public override bool Equals(object obj) {
            var other = obj as FunctionInfo;
            if (other == null) {
                return false;
            }

            return Name == other.Name && ((Location == null && other.Location == null) || (Location != null && Location.Equals(other.Location)));
        }

        public override int GetHashCode() {
            return Location == null ? GetType().GetHashCode() : Location.GetHashCode();
        }

        private const int REQUIRED_STRENGTH = 3;

        public override bool UnionEquals(Namespace ns, int strength) {
            if (strength < REQUIRED_STRENGTH) {
                return base.UnionEquals(ns, strength);
            } else {
                return ns == ProjectState._functionType.Instance || ns is FunctionInfo;
            }
        }

        public override int UnionHashCode(int strength) {
            if (strength < REQUIRED_STRENGTH) {
                return base.UnionHashCode(strength);
            } else {
                return ProjectState._functionType.Instance.GetHashCode();
            }
        }

        internal override Namespace UnionMergeTypes(Namespace ns, int strength) {
            if (strength < REQUIRED_STRENGTH) {
                return base.UnionMergeTypes(ns, strength);
            } else {
                return ProjectState._functionType.Instance;
            }
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
