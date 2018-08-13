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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class TupleBuiltinClassInfo : SequenceBuiltinClassInfo {
        private AnalysisValue[] _tupleTypes;

        public TupleBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
            _tupleTypes = (classObj as IPythonSequenceType)?.IndexTypes?.Select(projectState.GetAnalysisValueFromObjects).ToArray();
        }

        protected override BuiltinInstanceInfo MakeInstance() {
            return new TupleBuiltinInstanceInfo(this);
        }

        internal override SequenceInfo MakeFromIndexes(Node node, ProjectEntry entry) {
            if (_tupleTypes != null) {
                return new SequenceInfo(_tupleTypes.Select(t => {
                    var v = new VariableDef();
                    v.AddTypes(entry, t);
                    return v;
                }).ToArray(), this, node, entry);
            }

            if (_indexTypes.Length > 0) {
                var vals = _indexTypes.Zip(VariableDef.Generator, (t, v) => { v.AddTypes(entry, t, false, entry); return v; }).ToArray();
                return new SequenceInfo(vals, this, node, entry);
            } else {
                return new SequenceInfo(VariableDef.EmptyArray, this, node, entry);
            }
        }
    }

    class TupleBuiltinInstanceInfo : SequenceBuiltinInstanceInfo {
        public TupleBuiltinInstanceInfo(BuiltinClassInfo classObj)
            : base(classObj, false, false) { }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (ClassInfo is TupleBuiltinClassInfo tuple && tuple.IndexTypes.Count > 0) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, TypeName);
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool needComma = false;
                foreach (var v in tuple.IndexTypes.Take(4)) {
                    if (needComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    foreach (var kv in v.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]", defaultIfEmpty: "None")) {
                        yield return kv;
                    }
                    needComma = true;
                }
                if (tuple.IndexTypes.Count > 4) {
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "...");
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            } else {
                foreach (var kv in base.GetRichDescription()) {
                    yield return kv;
                }
            }
        }
    }
}
