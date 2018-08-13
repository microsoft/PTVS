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
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class DictBuiltinInstanceInfo : BuiltinInstanceInfo, IHasRichDescription {
        private readonly IPythonLookupType _dict;

        public DictBuiltinInstanceInfo(DictBuiltinClassInfo klass, IPythonLookupType dict)
            : base(klass) {
            _dict = dict;
            KeyType = klass.ProjectState.GetAnalysisSetFromObjects(dict.KeyTypes);
            ValueType = klass.ProjectState.GetAnalysisSetFromObjects(dict.ValueTypes);
        }

        protected IAnalysisSet KeyType { get; }
        protected IAnalysisSet ValueType { get; }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            var indexType = index.Select(av => av.PythonType).Where(pt => pt != null);
            if (indexType != null && indexType.Any()) {
                return unit.State.GetAnalysisSetFromObjects(indexType.SelectMany(_dict.GetIndex));
            }
            return ValueType;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return KeyType;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, ClassInfo.Name);
            if (KeyType.Any() || ValueType.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                foreach(var kv in KeyType.GetRichDescriptions(unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
                foreach (var kv in ValueType.GetRichDescriptions(prefix: ", ", unionPrefix: "[", unionSuffix: "]")) {
                    yield return kv;
                }
            }
        }
    }
}
