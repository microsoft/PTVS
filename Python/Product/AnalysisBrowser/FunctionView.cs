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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class FunctionView : MemberView {
        readonly IPythonFunction _function;
        readonly bool _isMethod;
        string _overloadSummary, _returnTypesSummary;
        readonly Lazy<IEnumerable<IAnalysisItemView>> _returnTypes;
        List<FunctionOverloadView> _overloads;

        public FunctionView(IModuleContext context, string name, IPythonFunction member, bool isMethod)
            : base(context, name, member) {
            _function = member;
            _isMethod = isMethod;
            _returnTypes = new Lazy<IEnumerable<IAnalysisItemView>>(CalculateReturnTypes);
        }

        public string OverloadSummary {
            get {
                if (_overloadSummary != null) {
                    return _overloadSummary;
                }

                var args = new List<HashSet<string>>();
                var types = new List<HashSet<string>>();

                foreach (var overload in _function.Overloads) {
                    var parameters = overload.GetParameters();
                    for (int i = 0; i < parameters.Length; ++i) {
                        while (args.Count <= i) {
                            args.Add(new HashSet<string>());
                            types.Add(new HashSet<string>());
                        }

                        args[i].Add(parameters[i].Name);
                        if (parameters[i].ParameterTypes != null) {
                            types[i].UnionWith(parameters[i].ParameterTypes
                                .Select(p => p?.Name)
                                .Where(n => !string.IsNullOrEmpty(n))
                            );
                        }
                    }
                }

                _overloadSummary = string.Join(", ", args.Zip(types, (a, t) => {
                    string ts = string.Empty;
                    if (t.Count > 1) {
                        ts = " : {" + string.Join(", ", t.Ordered()) + "}";
                    } else if (t.Count == 1) {
                        ts = " : " + t.FirstOrDefault();
                    }
                    if (a.Count > 1) {
                        return "{" + string.Join(", ", a.Ordered()) + ts + "}";
                    } else {
                        return (a.FirstOrDefault() ?? "(null)") + ts;
                    }
                }));
                return _overloadSummary;
            }
        }

        private IEnumerable<IAnalysisItemView> CalculateReturnTypes() {
            var seen = new HashSet<IAnalysisItemView>();

            foreach (var overload in _function.Overloads) {
                if (overload.ReturnType != null) {
                    foreach (var type in overload.ReturnType.Select(t => MemberView.Make(_context, t.Name, t))) {
                        if (seen.Add(type)) {
                            yield return type;
                        }
                    }
                }
            }
        }

        public IEnumerable<IAnalysisItemView> ReturnTypes {
            get {
                return _returnTypes.Value;
            }
        }

        public string ReturnTypesSummary {
            get {
                if (_returnTypesSummary != null) {
                    return _returnTypesSummary;
                }

                var types = new HashSet<string>(ReturnTypes.Select(t => t?.Name ?? "(null)")).Ordered().ToList();
                if (types.Count > 1) {
                    _returnTypesSummary = "{" + string.Join(", ", types) + "}";
                } else if (types.Count == 1) {
                    _returnTypesSummary = types[0];
                } else {
                    _returnTypesSummary = "{}";
                }
                return _returnTypesSummary;
            }
        }

        public override string SortKey {
            get { return "5"; }
        }

        public override string DisplayType {
            get { return _isMethod ? "Method" : "Function"; }
        }

        public override IEnumerable<IAnalysisItemView> Children {
            get {
                if (_overloads == null) {
                    _overloads = _function.Overloads.Select(o => new FunctionOverloadView(_context, Name, o)).ToList();
                }
                return _overloads;
            }
        }

        public override IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                foreach (var p in base.Properties) {
                    yield return p;
                }

                if (!string.IsNullOrEmpty(_function.Documentation)) {
                    yield return new KeyValuePair<string, object>("__doc__", _function.Documentation);
                }

                int i = 1;
                foreach (var t in ReturnTypes) {
                    yield return new KeyValuePair<string, object>(string.Format("Retval #{0}", i++), t);
                }
            }
        }

        public override void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine("{0}def {1}({2}):", currentIndent, Name, OverloadSummary);
            exportChildren = SortedChildren;
        }

        public override void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            writer.WriteLine(
                "{0}{1}({2}) -> {3}",
                currentIndent,
                Name,
                OverloadSummary,
                ReturnTypesSummary
            );
            exportChildren = null;
        }
    }
}
