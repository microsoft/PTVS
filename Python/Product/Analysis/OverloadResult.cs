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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis {
    public class OverloadResult : IOverloadResult {
        private readonly ParameterResult[] _parameters;
        private readonly string _name;

        public OverloadResult(ParameterResult[] parameters, string name) {
            _parameters = parameters;
            _name = name;
        }

        public string Name {
            get { return _name; }
        }
        public virtual string Documentation {
            get { return null; }
        }
        public virtual ParameterResult[] Parameters {
            get { return _parameters; }
        }
    }

    class SimpleOverloadResult : OverloadResult {
        private readonly string _documentation;
        public SimpleOverloadResult(ParameterResult[] parameters, string name, string documentation)
            : base(parameters, name) {
            _documentation = ParameterResult.Trim(documentation);
        }

        public override string Documentation {
            get {
                return _documentation;
            }
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
            if (defaultValue != null && defaultValue == String.Empty) {
                isOptional = true;
                defaultValue = null;
            }

            return new ParameterResult(name, "", typeName, isOptional, null, defaultValue);
        }
    }

    class OverloadResultComparer : EqualityComparer<OverloadResult> {
        public static IEqualityComparer<OverloadResult> Instance = new OverloadResultComparer();

        public override bool Equals(OverloadResult x, OverloadResult y) {
            if (x == null | y == null) {
                return x == null & y == null;
            }

            if (x.Name != y.Name || x.Documentation != y.Documentation) {
                return false;
            }

            if (x.Parameters == null | y.Parameters == null) {
                return x.Parameters == null & y.Parameters == null;
            }

            if (x.Parameters.Length != y.Parameters.Length) {
                return false;
            }

            for (int i = 0; i < x.Parameters.Length; ++i) {
                if (!x.Parameters[i].Equals(y.Parameters[i])) {
                    return false;
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
                    hc ^= p.GetHashCode();
                }
            }
            return hc;
        }
    }
}
