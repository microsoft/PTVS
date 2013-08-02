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

        public FunctionOverloadView(IModuleContext context, string name, IPythonFunctionOverload overload) {
            _context = context;
            Name = name;
            _overload = overload;
            _prototype = new Lazy<string>(CalculatePrototype);
            _returnTypes = new Lazy<IEnumerable<IAnalysisItemView>>(CalculateReturnTypes);
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
            get { return Enumerable.Empty<IAnalysisItemView>(); }
        }

        public IEnumerable<IAnalysisItemView> SortedChildren {
            get { return Children; }
        }

        public string SourceLocation {
            get { return "No location"; }
        }

        public IEnumerable<KeyValuePair<string, object>> Properties {
            get {
                yield return new KeyValuePair<string, object>("__doc__", _overload.Documentation);
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

    }
}
