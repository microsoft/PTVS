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
    internal class FunctionInfo : UserDefinedInfo, IReferenceableContainer {
        private Dictionary<Namespace, ISet<Namespace>> _methods;
        private Dictionary<string, VariableDef> _functionAttrs;
        private VariableDef _returnValue;
        public bool IsStatic;
        public bool IsClassMethod;
        public bool IsProperty;
        private ReferenceDict _references;
        private readonly int _declVersion;
        [ThreadStatic]
        private static List<Namespace> _descriptionStack;

        internal FunctionInfo(AnalysisUnit unit)
            : base(unit) {
            _returnValue = new VariableDef();
            _declVersion = unit.ProjectEntry.AnalysisVersion;
        }

        public ProjectEntry ProjectEntry {
            get {
                return _analysisUnit.ProjectEntry;
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

        public override ISet<Namespace> Call(Node node, AnalysisUnit unit, ISet<Namespace>[] args, NameExpression[] keywordArgNames) {
            if (unit != null) {
                ReturnValue.AddDependency(unit);

                AddCall(node, keywordArgNames, unit, args);
            }

            return ReturnValue.Types;
        }

        protected void AddCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, ISet<Namespace>[] args) {
            if (ParameterTypes != null) {
                bool added = false;

                // TODO: Warn when a keyword argument is provided and it maps to
                // something which is also a positional argument:
                // def f(a, b, c):
                //    print a, b, c
                //
                // f(2, 3, a=42)

                if (PropagateCall(node, keywordArgNames, unit, args, added)) {
                    // new inputs to the function, it needs to be analyzed.
                    _analysisUnit.Enqueue();
                }
            }
        }

        private bool PropagateCall(Node node, NameExpression[] keywordArgNames, AnalysisUnit unit, ISet<Namespace>[] args, bool added) {
            for (int i = 0; i < args.Length; i++) {
                int kwIndex = i - (args.Length - keywordArgNames.Length);
                if (kwIndex >= 0) {
                    var curArg = keywordArgNames[kwIndex];
                    if (curArg == null) {
                        continue;
                    }
                    switch (curArg.Name) {
                        case "*":
                            int lastPos = args.Length - keywordArgNames.Length;
                            foreach (var type in args[i]) {
                                int? argLen = type.GetLength();
                                if (argLen != null) {
                                    for (int j = 0; j < argLen.Value; j++) {
                                        var indexType = type.GetIndex(node, unit, _analysisUnit.ProjectState.GetConstant(j));

                                        int paramIndex = lastPos + j;
                                        if (paramIndex >= ParameterTypes.Length) {
                                            break;
                                        } else if (AddParameterType(unit, indexType, lastPos + j)) {
                                            added = true;
                                        }
                                    }
                                }
                            }
                            break;
                        case "**":
                            // TODO: Handle keyword argument splatting
                            break;
                        default:
                            bool found = false;
                            for (int j = 0; j < ParameterTypes.Length; j++) {
                                string paramName = GetParameterName(j);
                                if (paramName == curArg.Name) {
                                    ParameterTypes[j].AddReference(curArg, unit);
                                    added = AddParameterType(unit, args[i], j) || added;
                                    found = true;
                                    break;
                                }
                            }

                            if (!found) {
                                for (int j = ParameterTypes.Length - 1; j >= 0; j--) {
                                    var curFuncArg = FunctionDefinition.Parameters[j];
                                    if (curFuncArg.IsDictionary) {
                                        AddParameterType(unit, args[i], j);
                                        break;
                                    } else if (!curFuncArg.IsKeywordOnly) {
                                        break;
                                    }
                                }
                            }
                            // TODO: Report a warning if we don't find the keyword argument and we don't 
                            // have a ** parameter.
                            break;
                    }
                } else if (i < ParameterTypes.Length) {
                    // positional argument
                    added = AddParameterType(unit, args[i], i) || added;
                } else {
                    for (int j = ParameterTypes.Length - 1; j >= 0; j--) {
                        var curArg = FunctionDefinition.Parameters[j];
                        if (curArg.IsList) {
                            AddParameterType(unit, args[i], j);
                            break;
                        } else if (!curArg.IsDictionary && !curArg.IsKeywordOnly) {
                            break;
                        }
                    }
                }
            }
            return added;
        }

        internal bool AddParameterType(AnalysisUnit unit, ISet<Namespace> arg, int parameterIndex) {
            switch (FunctionDefinition.Parameters[parameterIndex].Kind) {
                case ParameterKind.Dictionary:
                    Debug.Assert(ParameterTypes[parameterIndex] is DictParameterVariableDef);

                    return ((DictParameterVariableDef)ParameterTypes[parameterIndex]).Dict.AddValueTypes(arg);
                case ParameterKind.List:
                    Debug.Assert(ParameterTypes[parameterIndex] is ListParameterVariableDef);

                    return ((ListParameterVariableDef)ParameterTypes[parameterIndex]).List.AddTypes(new[] { arg });
                case ParameterKind.Normal:
                    if (ParameterTypes[parameterIndex].AddTypes(FunctionDefinition.Parameters[parameterIndex], unit, arg)) {
                        return true;
                    }
                    break;
            }
            return false;
        }

        public override string Description {
            get {
                StringBuilder result;
                if (FunctionDefinition.IsLambda) {
                    result = new StringBuilder("lambda ...: ");
                    if (FunctionDefinition.IsGenerator) {
                        result.Append(((YieldExpression)((ExpressionStatement)FunctionDefinition.Body).Expression).Expression.ToCodeString(DeclaringModule.Tree));
                    } else {
                        result.Append(((ReturnStatement)FunctionDefinition.Body).Expression.ToCodeString(DeclaringModule.Tree));
                    }
                } else {
                    result = new StringBuilder("def ");
                    result.Append(FunctionDefinition.Name);
                    result.Append("(...)"); // TOOD: Include parameter information?
                    if (!String.IsNullOrEmpty(Documentation)) {
                        result.AppendLine();
                        result.Append(Documentation);
                    }
                }

                var desc = FunctionDescription;
                if (!String.IsNullOrWhiteSpace(desc)) {
                    result.Append(desc);
                }
                return result.ToString();
            }
        }

        public string FunctionDescription {
            get {
                StringBuilder result = new StringBuilder();
                bool first = true;
                foreach (var ns in ReturnValue.Types) {
                    if (ns == null) {
                        continue;
                    }

                    if (ns.Push()) {
                        try {
                            if (ns.Description == null) {
                                continue;
                            }

                            if (first) {
                                result.Append(" -> ");
                                first = false;
                            } else {
                                result.Append(", ");
                            }
                            AppendDescription(result, ns);
                        } finally {
                            ns.Pop();
                        }
                    } else {
                        result.Append("...");
                    }
                }
                
                return result.ToString();
            }
        }

        private static void AppendDescription(StringBuilder result, Namespace key) {
            if (DescriptionStack.Contains(key)) {
                result.Append("...");
            } else {
                DescriptionStack.Add(key);
                try {
                    result.Append(key.Description);
                } finally {
                    DescriptionStack.Pop();
                }
            }
        }

        private static List<Namespace> DescriptionStack {
            get {
                if (_descriptionStack == null) {
                    _descriptionStack = new List<Namespace>();
                }
                return _descriptionStack;
            }
        }

        public FunctionDefinition FunctionDefinition {
            get {
                return (_analysisUnit.Ast as FunctionDefinition);
            }
        }

        public override ISet<Namespace> GetDescriptor(Node node, Namespace instance, Namespace context, AnalysisUnit unit) {
            if ((instance == ProjectState._noneInst && !IsClassMethod) || IsStatic) {
                return SelfSet;
            }
            if (IsProperty) {
                ReturnValue.AddDependency(unit);
                return ReturnValue.Types;
            }

            if (_methods == null) {
                _methods = new Dictionary<Namespace, ISet<Namespace>>();
            }

            ISet<Namespace> result;
            if (!_methods.TryGetValue(instance, out result) || result == null) {
                if (IsClassMethod) {
                    _methods[instance] = result = new BoundMethodInfo(this, context).SelfSet;
                } else {
                    _methods[instance] = result = new BoundMethodInfo(this, instance).SelfSet;
                }
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

        public override PythonMemberType ResultType {
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

                    var newParam = MakeParameterResult(ProjectState, curParam, ProjectEntry.Analysis.ToVariables(ParameterTypes[i]));
                    parameters[i] = newParam;
                }

                return new OverloadResult[] {
                    new SimpleOverloadResult(parameters, FunctionDefinition.Name, Documentation)
                };
            }
        }

        internal static ParameterResult MakeParameterResult(PythonAnalyzer state, Parameter curParam, IEnumerable<IAnalysisVariable> variables = null) {
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
                }

                NameExpression nameExpr = curParam.DefaultValue as NameExpression;
                if (nameExpr != null) {
                    name = name + " = " + nameExpr.Name;
                }

                DictionaryExpression dict = curParam.DefaultValue as DictionaryExpression;
                if (dict != null) {
                    if (dict.Items.Count == 0) {
                        name = name + " = {}";
                    } else {
                        name = name + " = {...}";
                    }
                }

                ListExpression list = curParam.DefaultValue as ListExpression;
                if (list != null) {
                    if (list.Items.Count == 0) {
                        name = name + " = []";
                    } else {
                        name = name + " = [...]";
                    }
                }

                TupleExpression tuple = curParam.DefaultValue as TupleExpression;
                if (tuple != null) {
                    if (tuple.Items.Count == 0) {
                        name = name + " = ()";
                    } else {
                        name = name + " = (...)";
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
                for (int i = 0; i < bytes.Length; i++) {
                    if (bytes[i] == '\'') {
                        res.Append("\\'");
                    } else {
                        res.Append(bytes[i]);
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
                string str = value.Value as string;
                for (int i = 0; i < str.Length; i++) {
                    if (str[i] == '\'') {
                        res.Append("\\'");
                    } else {
                        res.Append(str[i]);
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

        public override void SetMember(Node node, AnalysisUnit unit, string name, ISet<Namespace> value) {
            if (_functionAttrs == null) {
                _functionAttrs = new Dictionary<string, VariableDef>();
            }

            VariableDef varRef;
            if (!_functionAttrs.TryGetValue(name, out varRef)) {
                _functionAttrs[name] = varRef = new VariableDef();
            }
            varRef.AddAssignment(node, unit);
            varRef.AddTypes(node, unit, value);
        }

        public override ISet<Namespace> GetMember(Node node, AnalysisUnit unit, string name) {
            VariableDef tmp;
            if (_functionAttrs != null && _functionAttrs.TryGetValue(name, out tmp)) {
                tmp.AddDependency(unit);
                tmp.AddReference(node, unit);

                return tmp.Types;
            }
            // TODO: Create one and add a dependency

            return ProjectState._functionType.GetMember(node, unit, name);
        }

        public override IDictionary<string, ISet<Namespace>> GetAllMembers(IModuleContext moduleContext) {
            if (_functionAttrs == null || _functionAttrs.Count == 0) {
                return ProjectState._functionType.GetAllMembers(moduleContext);
            }

            var res = new Dictionary<string, ISet<Namespace>>(ProjectState._functionType.GetAllMembers(moduleContext));
            foreach (var variable in _functionAttrs) {
                ISet<Namespace> existing;
                if (!res.TryGetValue(variable.Key, out existing)) {
                    res[variable.Key] = existing = new HashSet<Namespace>();
                } else if (!(existing is HashSet<Namespace>)) {
                    // someone has overwritten a function attribute with their own value
                    res[variable.Key] = existing = new HashSet<Namespace>(existing);
                }
                
                existing.UnionWith(variable.Value.Types);
            }
            return res;
        }

        private string GetParameterName(int index) {
            return FunctionDefinition.Parameters[index].Name;
        }

        public VariableDef ReturnValue {
            get { return _returnValue; }
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
