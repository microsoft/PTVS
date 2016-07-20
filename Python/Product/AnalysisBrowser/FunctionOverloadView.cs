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
using System.Text;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.Browser {
    class FunctionOverloadView : IAnalysisItemView {
        readonly IModuleContext _context;
        readonly IPythonFunctionOverload _overload;
        readonly Lazy<string> _prototype;
        readonly Lazy<IEnumerable<IAnalysisItemView>> _returnTypes;
        readonly Lazy<IEnumerable<IAnalysisItemView>> _parameters;

        public FunctionOverloadView(IModuleContext context, string name, IPythonFunctionOverload overload) {
            _context = context;
            Name = name;
            _overload = overload;
            _prototype = new Lazy<string>(CalculatePrototype);
            _returnTypes = new Lazy<IEnumerable<IAnalysisItemView>>(CalculateReturnTypes);
            _parameters = new Lazy<IEnumerable<IAnalysisItemView>>(CalculateParameters);
        }

        public string Name { get; private set; }

        public string DisplayType {
            get { return "Function overload"; }
        }

        private string CalculatePrototype() {
            var proto = new StringBuilder("(");
            foreach (var arg in _overload.GetParameters()) {
                if (arg.IsParamArray) {
                    proto.Append("*");
                } else if (arg.IsKeywordDict) {
                    proto.Append("**");
                }
                proto.Append(arg.Name);
                proto.Append("=");

                var types = arg.ParameterTypes == null ?
                    new string[0] :
                    arg.ParameterTypes.Select(p => p.Name).Distinct().OrderBy(p => p).ToArray();
                if (types.Length == 1) {
                    proto.Append(types[0]);
                } else {
                    proto.AppendFormat("{{{0}}}", string.Join(", ", types));
                }

                if (!string.IsNullOrEmpty(arg.DefaultValue)) {
                    proto.AppendFormat(" [{0}]", arg.DefaultValue);
                }

                proto.Append(", ");
            }

            if (proto.Length > 2) {
                proto.Length -= 2;
            }
            proto.Append(")");

            return proto.ToString();
        }

        private IEnumerable<IAnalysisItemView> CalculateReturnTypes() {
            if (_overload.ReturnType != null) {
                return _overload.ReturnType.Select(t => MemberView.Make(_context, t.Name, t));
            } else {
                return Enumerable.Empty<IAnalysisItemView>();
            }
        }

        private IEnumerable<IAnalysisItemView> CalculateParameters() {
            var parameters = _overload.GetParameters();
            if (parameters == null || parameters.Length == 0) {
                return Enumerable.Empty<IAnalysisItemView>();
            }

            return parameters.Select(p => new ParameterView(_context, p));
        }

        public IEnumerable<IAnalysisItemView> ReturnTypes {
            get {
                return _returnTypes.Value;
            }
        }

        public string Prototype { get { return _prototype.Value; } }

        public override string ToString() {
            return Prototype;
        }

        public string SortKey {
            get { return "6b"; }
        }

        public IEnumerable<IAnalysisItemView> Children {
            get { return _parameters.Value; }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return Children; }
        }

        public string SourceLocation {
            get { return "No location"; }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                if (!string.IsNullOrEmpty(_overload.Documentation)) {
                    yield return new KeyValuePair<string, object>("__doc__", _overload.Documentation);
                }
            }
        }

        public void ExportToTree(
            TextWriter writer,
            string currentIndent,
            string indent,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            var returnTypes = string.Join(", ", ReturnTypes.Select(t => {
                var sw = new StringWriter();
                IEnumerable<IAnalysisItemView> dummy;
                t.ExportToTree(sw, "", indent, out dummy);
                return "(" + sw.ToString().Trim() + ")";
            }).Distinct());

            if (!string.IsNullOrEmpty(returnTypes)) {
                writer.WriteLine("{0}Overload: {1} -> {2}", currentIndent, Prototype, returnTypes);
            } else {
                writer.WriteLine("{0}Overload: {1}", currentIndent, Prototype);
            }
            exportChildren = null;
        }

        public void ExportToDiffable(
            TextWriter writer,
            string currentIndent,
            string indent,
            Stack<IAnalysisItemView> exportStack,
            out IEnumerable<IAnalysisItemView> exportChildren
        ) {
            exportChildren = null;
        }
    }
}
