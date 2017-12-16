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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public class OverloadResult : IOverloadResult {
        private readonly ParameterResult[] _parameters;
        private readonly string _name;

        public OverloadResult(ParameterResult[] parameters, string name, string documentation = null) {
            _parameters = parameters;
            _name = name;
            Documentation = documentation;
        }

        public string Name {
            get { return _name; }
        }
        public virtual string Documentation { get; }
        public virtual ParameterResult[] Parameters {
            get { return _parameters; }
        }

        internal virtual OverloadResult WithNewParameters(ParameterResult[] newParameters) {
            return new OverloadResult(newParameters, _name);
        }

        private static string Longest(string x, string y) {
            if (x == null) {
                return y;
            } else if (y == null) {
                return x;
            }

            return x.Length > y.Length ? x : y;
        }

        private static IEnumerable<string> CommaSplit(string x) {
            if (string.IsNullOrEmpty(x)) {
                yield break;
            }

            var sb = new StringBuilder();
            int nestCount = 0;
            foreach (var c in x) {
                if (c == ',' && nestCount == 0) {
                    yield return sb.ToString().Trim();
                    sb.Clear();
                    continue;
                }

                if (c == '(' || c == '[' || c == '{') {
                    nestCount += 1;
                } else if (c == ')' || c == ']' || c == '}') {
                    nestCount -= 1;
                }
                sb.Append(c);
            }

            if (sb.Length > 0) {
                yield return sb.ToString().Trim();
            }
        }

        private static string Merge(string x, string y) {
            return string.Join(", ",
                CommaSplit(x).Concat(CommaSplit(y)).OrderBy(n => n).Distinct()
            );
        }

        public static OverloadResult Merge(IEnumerable<OverloadResult> overloads) {
            overloads = overloads.ToArray();

            var name = overloads.Select(o => o.Name).OrderByDescending(n => n?.Length ?? 0).FirstOrDefault();
            var doc = overloads.Select(o => o.Documentation).OrderByDescending(n => n?.Length ?? 0).FirstOrDefault();
            var parameters = overloads.Select(o => o.Parameters).Aggregate(Array.Empty<ParameterResult>(), (all, pms) => {
                var res = all.Concat(pms.Skip(all.Length)).ToArray();

                for (int i = 0; i < res.Length; ++i) {
                    if (res[i] == null) {
                        res[i] = pms[i];
                    } else {
                        var l = res[i];
                        var r = pms[i];
                        res[i] = new ParameterResult(
                            Longest(l.Name, r.Name),
                            Longest(l.Documentation, r.Documentation),
                            Merge(l.Type, r.Type),
                            l.IsOptional || r.IsOptional,
                            l.Variables?.Concat(r.Variables.MaybeEnumerate()).ToArray() ?? r.Variables,
                            Longest(l.DefaultValue, r.DefaultValue)
                        );
                    }
                }

                return res;
            });

            return new OverloadResult(parameters, name, doc);
        }
    }

    class SimpleOverloadResult : OverloadResult {
        private readonly string _documentation;
        public SimpleOverloadResult(ParameterResult[] parameters, string name, string documentation)
            : base(parameters, name) {
            _documentation = documentation;
        }

        public override string Documentation {
            get {
                return _documentation;
            }
        }

        internal override OverloadResult WithNewParameters(ParameterResult[] newParameters) {
            return new SimpleOverloadResult(newParameters, Name, _documentation);
        }
    }

    class BuiltinFunctionOverloadResult : OverloadResult {
        private readonly IPythonFunctionOverload _overload;
        private ParameterResult[] _parameters;
        private readonly ParameterResult[] _extraParameters;
        private readonly int _removedParams;
        private readonly PythonAnalyzer _projectState;
        private readonly Func<string> _fallbackDoc;
        private string _doc;
        private static readonly string _calculating = "Documentation is still being calculated, please try again soon.";

        internal BuiltinFunctionOverloadResult(PythonAnalyzer state, string name, IPythonFunctionOverload overload, int removedParams, Func<string> fallbackDoc, params ParameterResult[] extraParams)
            : base(null, name) {
            _fallbackDoc = fallbackDoc;
            _overload = overload;
            _extraParameters = extraParams;
            _removedParams = removedParams;
            _projectState = state;

            CalculateDocumentation();
        }

        internal BuiltinFunctionOverloadResult(PythonAnalyzer state, string name, IPythonFunctionOverload overload, int removedParams, params ParameterResult[] extraParams)
            : this(state, name, overload, removedParams, null, extraParams) {
        }

        internal BuiltinFunctionOverloadResult(PythonAnalyzer state, IPythonFunctionOverload overload, int removedParams, string name, Func<string> fallbackDoc, params ParameterResult[] extraParams)
            : base(null, name) {
            _overload = overload;
            _extraParameters = extraParams;
            _removedParams = removedParams;
            _projectState = state;
            _fallbackDoc = fallbackDoc;

            CalculateDocumentation();
        }

        internal override OverloadResult WithNewParameters(ParameterResult[] newParameters) {
            return new BuiltinFunctionOverloadResult(
                _projectState,
                Name,
                _overload,
                0,
                _fallbackDoc,
                newParameters
            );
        }

        public override string Documentation {
            get {
                return _doc;
            }
        }

        private void CalculateDocumentation() {
            // initially fill in w/ a string saying we don't yet have the documentation
            _doc = _calculating;

            // give the documentation a brief time period to complete synchrnously.
            var task = Task.Factory.StartNew(DocCalculator);
            task.Wait(50);
        }

        private void DocCalculator() {
            StringBuilder doc = new StringBuilder();
            if (!String.IsNullOrEmpty(_overload.Documentation)) {
                doc.AppendLine(_overload.Documentation);
            }

            foreach (var param in _overload.GetParameters()) {
                if (!String.IsNullOrEmpty(param.Documentation)) {
                    doc.AppendLine();
                    doc.Append(param.Name);
                    doc.Append(": ");
                    doc.Append(param.Documentation);
                }
            }

            if (!String.IsNullOrEmpty(_overload.ReturnDocumentation)) {
                doc.AppendLine();
                doc.AppendLine();
                doc.Append("Returns: ");
                doc.Append(_overload.ReturnDocumentation);
            }

            if (doc.Length == 0 && _fallbackDoc != null) {
                _doc = _fallbackDoc();
            } else {
                _doc = doc.ToString();
            }
        }

        public override ParameterResult[] Parameters {
            get {
                if (_parameters == null) {
                    if (_overload != null) {
                        var target = _overload;

                        var pinfo = _overload.GetParameters();
                        var result = new List<ParameterResult>(pinfo.Length + _extraParameters.Length);
                        int ignored = 0;
                        ParameterResult kwDict = null;
                        foreach (var param in pinfo) {
                            if (ignored < _removedParams) {
                                ignored++;
                            } else {
                                var paramResult = GetParameterResultFromParameterInfo(param);
                                if (param.IsKeywordDict) {
                                    kwDict = paramResult;
                                } else {
                                    result.Add(paramResult);
                                }
                            }
                        }

                        result.InsertRange(0, _extraParameters);

                        // always add kw dict last.  When defined in C# and combined w/ params 
                        // it has to come earlier than it's legally allowed in Python so we 
                        // move it to the end for intellisense purposes here.
                        if (kwDict != null) {
                            result.Add(kwDict);
                        }
                        _parameters = result.ToArray();
                    } else {
                        _parameters = new ParameterResult[0];
                    }
                }
                return _parameters;
            }
        }

        internal ParameterResult GetParameterResultFromParameterInfo(IParameterInfo param) {
            string name = param.Name;

            string typeName;
            if (param.ParameterTypes != null) {
                typeName = param.ParameterTypes.Where(p => p != _projectState.Types[BuiltinTypeId.NoneType]).Select(p => p.Name).FirstOrDefault();
            } else {
                typeName = "object";
            }
            if (param.IsParamArray) {
                name = "*" + name;
                var advType = param.ParameterTypes as IAdvancedPythonType;
                if (advType != null && advType.IsArray) {
                    var elemType = advType.GetElementType();
                    if (elemType == _projectState.Types[BuiltinTypeId.Object]) {
                        typeName = "sequence";
                    } else {
                        typeName = elemType.Name + " sequence";
                    }
                }
            } else if (param.IsKeywordDict) {
                name = "**" + name;
                typeName = "object";
            }

            bool isOptional = false;
            string defaultValue = param.DefaultValue;
            if (defaultValue != null && defaultValue.Length == 0) {
                isOptional = true;
                defaultValue = null;
            }

            return new ParameterResult(name, "", typeName, isOptional, null, defaultValue);
        }
    }

    class OverloadResultComparer : EqualityComparer<OverloadResult> {
        public static IEqualityComparer<OverloadResult> Instance = new OverloadResultComparer(false);
        public static IEqualityComparer<OverloadResult> WeakInstance = new OverloadResultComparer(true);

        private readonly bool _weak;

        private OverloadResultComparer(bool weak) {
            _weak = weak;
        }

        public override bool Equals(OverloadResult x, OverloadResult y) {
            if (x == null | y == null) {
                return x == null & y == null;
            }

            if (x.Name != y.Name || (!_weak && x.Documentation != y.Documentation)) {
                return false;
            }

            if (x.Parameters == null | y.Parameters == null) {
                return x.Parameters == null & y.Parameters == null;
            }

            if (x.Parameters.Length != y.Parameters.Length) {
                return false;
            }

            for (int i = 0; i < x.Parameters.Length; ++i) {
                if (_weak) {
                    if (!x.Parameters[i].Name.Equals(y.Parameters[i].Name)) {
                        return false;
                    }
                } else {
                    if (!x.Parameters[i].Equals(y.Parameters[i])) {
                        return false;
                    }
                }
            }

            return true;
        }

        public override int GetHashCode(OverloadResult obj) {
            // Don't use Documentation for hash code, since it changes over time
            // in some implementations of IOverloadResult.
            int hc = 552127 ^ obj.Name.GetHashCode();
            if (obj.Parameters != null) {
                foreach (var p in obj.Parameters) {
                    hc ^= _weak ? p.Name.GetHashCode() : p.GetHashCode();
                }
            }
            return hc;
        }
    }
}
