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
    class ListBuiltinClassInfo : SequenceBuiltinClassInfo {
        public ListBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        protected override BuiltinInstanceInfo MakeInstance() {
            return new SequenceBuiltinInstanceInfo(this, false, false);
        }

        internal override SequenceInfo MakeFromIndexes(Node node, ProjectEntry entry) {
            if (_indexTypes.Length > 0) {
                var vals = _indexTypes.Zip(VariableDef.Generator, (t, v) => { v.AddTypes(entry, t, false, entry); return v; }).ToArray();
                return new ListInfo(vals, this, node, entry);
            } else {
                return new ListInfo(VariableDef.EmptyArray, this, node, entry);
            }
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _type.Name);
            if (_indexTypes == null || _indexTypes.Length == 0) {
                yield break;
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " of ");
            foreach (var kv in AnalysisSet.UnionAll(_indexTypes).GetRichDescriptions()) {
                yield return kv;
            }
        }
    }
}
