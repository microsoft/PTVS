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
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class FunctionView : MemberView {
        readonly IPythonFunction _function;
        readonly bool _isMethod;
        string _summary;
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
                var args = new List<HashSet<string>>();

                if (_summary == null) {
                    foreach (var overload in _function.Overloads) {
                        var parameters = overload.GetParameters();
                        for (int i = 0; i < parameters.Length; ++i) {
                            while (args.Count <= i) {
                                args.Add(new HashSet<string>());
                            }

                            args[i].Add(parameters[i].Name);
                        }
                    }

                    _summary = string.Join(", ", args.Select(a => {
                        if (a.Count > 1) {
                            return "{" + string.Join(", ", a) + "}";
                        } else {
                            return a.FirstOrDefault() ?? "(null)";
                        }
                    }));
                }
                return _summary;
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
            writer.WriteLine("{0}def {2}({3}):", currentIndent, DisplayType, Name, OverloadSummary);
            exportChildren = SortedChildren;
        }
    }
}
