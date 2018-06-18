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
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class SequenceBuiltinInstanceInfo : BaseIterableValue {
        private readonly bool _supportsMod;

        public SequenceBuiltinInstanceInfo(BuiltinClassInfo klass, bool sequenceOfSelf, bool supportsMod)
            : base(klass) {
            _supportsMod = supportsMod;

            var seqInfo = klass as SequenceBuiltinClassInfo;
            if (seqInfo != null) {
                UnionType = AnalysisSet.UnionAll(seqInfo.IndexTypes);
            } else if (sequenceOfSelf) {
                UnionType = SelfSet;
            } else {
                UnionType = AnalysisSet.Empty;
            }
        }

        protected override void EnsureUnionType() { }

        protected override IAnalysisSet MakeIteratorInfo(Node n, AnalysisUnit unit) {
            return new FixedIteratorValue(
                UnionType,
                BaseIteratorValue.GetIteratorTypeFromType(ClassInfo, unit)
            );
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            if (ClassInfo is SequenceBuiltinClassInfo seq && seq.IndexTypes?.Count > 0) {
                int? constIndex = SequenceInfo.GetConstantIndex(index);

                if (constIndex != null) {
                    if (constIndex.Value < 0) {
                        constIndex += seq.IndexTypes.Count;
                    }
                    if (0 <= constIndex.Value && constIndex.Value < seq.IndexTypes.Count) {
                        return seq.IndexTypes[constIndex.Value];
                    }
                }

                if (index.Split(out IReadOnlyList<SliceInfo> sliceInfo, out _)) {
                    return this.SelfSet;
                }
            }

            return UnionType;
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return UnionType;
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, Parsing.PythonOperator operation, IAnalysisSet rhs) {
            var res = AnalysisSet.Empty;
            switch (operation) {
                case PythonOperator.Add:
                    foreach (var type in rhs) {
                        if (type.IsOfType(ClassInfo)) {
                            res = res.Union(ClassInfo.Instance);
                        } else {
                            res = res.Union(type.ReverseBinaryOperation(node, unit, operation, SelfSet));
                        }
                    }
                    break;
                case PythonOperator.Mod:
                    if (_supportsMod) {
                        res = SelfSet;
                    }
                    break;
                case PythonOperator.Multiply:
                    foreach (var type in rhs) {
                        if (type.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Int]) || type.IsOfType(ProjectState.ClassInfos[BuiltinTypeId.Long])) {
                            res = res.Union(ClassInfo.Instance);
                        } else {
                            var partialRes = ConstantInfo.NumericOp(node, this, unit, operation, rhs);
                            if (partialRes != null) {
                                res = res.Union(partialRes);
                            }
                        }
                    }
                    break;
            }
            if (res.Count == 0) {
                return CallReverseBinaryOp(node, unit, operation, rhs);
            }
            return res;
        }

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (UnionType == this) {
                return new[] {
                    new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Type, _type.Name)
                };
            }
            return base.GetRichDescription();
        }
    }
}
