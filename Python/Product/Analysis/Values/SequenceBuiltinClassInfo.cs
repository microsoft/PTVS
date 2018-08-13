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
using System.Linq;
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized ClassInfo for sequence types.
    /// </summary>
    abstract class SequenceBuiltinClassInfo : BuiltinClassInfo {
        protected readonly IAnalysisSet[] _indexTypes;

        public SequenceBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
            var seqType = classObj as IPythonSequenceType;
            if (seqType != null && seqType.IndexTypes != null) {
                _indexTypes = seqType.IndexTypes.Select(projectState.GetAnalysisValueFromObjects).Select(AnalysisValueSetExtensions.GetInstanceType).ToArray();
            } else {
                _indexTypes = Array.Empty<IAnalysisSet>();
            }
        }

        internal IReadOnlyList<IAnalysisSet> IndexTypes => _indexTypes;

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            if (args.Length == 1) {
                var res = unit.Scope.GetOrMakeNodeValue(
                    node,
                    NodeValueKind.Sequence,
                    (node_) => MakeFromIndexes(node_, unit.ProjectEntry)
                ) as SequenceInfo;

                List<IAnalysisSet> seqTypes = new List<IAnalysisSet>();
                foreach (var type in args[0]) {
                    if (type is SequenceInfo seqInfo) {
                        for (int i = 0; i < seqInfo.IndexTypes.Length; i++) {
                            if (seqTypes.Count == i) {
                                seqTypes.Add(seqInfo.IndexTypes[i].Types);
                            } else {
                                seqTypes[i] = seqTypes[i].Union(seqInfo.IndexTypes[i].Types);
                            }
                        }
                    } else {
                        var defaultIndexType = type.GetIndex(node, unit, ProjectState.GetConstant(0));
                        if (seqTypes.Count == 0) {
                            seqTypes.Add(defaultIndexType);
                        } else {
                            seqTypes[0] = seqTypes[0].Union(defaultIndexType);
                        }
                    }
                }

                res.AddTypes(unit, seqTypes.ToArray());

                return res;
            }

            return base.Call(node, unit, args, keywordArgNames);
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _type.Name);
            if (_indexTypes == null || _indexTypes.Length == 0) {
                yield break;
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, " of ");
            string prefix = null;
            foreach (var kv in _indexTypes.Take(6).SelectMany(t => t.GetRichDescriptions(prefix: prefix, unionPrefix: "{", unionSuffix: "}"))) {
                yield return kv;
                prefix = ", ";
            }
        }

        internal override int UnionHashCode(int strength) {
            if (strength < MergeStrength.ToObject && _indexTypes.Any()) {
                var type = ProjectState.ClassInfos[TypeId];
                if (type != null) {
                    // Use our unspecialized type's hash code.
                    return type.UnionHashCode(strength);
                }
            }
            return base.UnionHashCode(strength);
        }

        internal abstract SequenceInfo MakeFromIndexes(Node node, ProjectEntry entry);
    }
}
