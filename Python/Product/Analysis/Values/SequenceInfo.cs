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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class SequenceInfo : IterableValue {
        public SequenceInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node, IPythonProjectEntry entry)
            : base(indexTypes, seqType, node) {
            DeclaringModule = entry;
            DeclaringVersion = entry.AnalysisVersion;
        }

        public override IPythonProjectEntry DeclaringModule { get; }
        public override int DeclaringVersion { get; }
        public override int? GetLength() => IndexTypes.Length;

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            SequenceInfo seq = null;
            VariableDef idx = null;
            var res = AnalysisSet.Empty;
            switch (operation) {
                case PythonOperator.Add:
                    foreach (var type in rhs.Where(t => !t.IsOfType(ClassInfo))) {
                        res = res.Union(CallReverseBinaryOp(node, unit, operation, rhs));
                    }
                    
                    foreach (var type in rhs.Where(t => t.IsOfType(ClassInfo))) {
                        if (seq == null) {
                            seq = (SequenceInfo)unit.Scope.GetOrMakeNodeValue(node,
                                NodeValueKind.Sequence,
                                _ => new SequenceInfo(new[] { new VariableDef() }, ClassInfo, node, unit.ProjectEntry)
                            );
                            idx = seq.IndexTypes[0];
                            idx.AddTypes(unit, GetEnumeratorTypes(node, unit), true, DeclaringModule);
                        }
                        idx.AddTypes(unit, type.GetEnumeratorTypes(node, unit), true, DeclaringModule);
                        idx.MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes);
                    }

                    if (seq != null) {
                        res = res.Union(seq);
                    }
                    break;
                case PythonOperator.Multiply:
                    foreach (var type in rhs) {
                        var typeId = type.TypeId;

                        if (typeId == BuiltinTypeId.Int || typeId == BuiltinTypeId.Long) {
                            res = res.Union(this);
                        } else {
                            res = res.Union(CallReverseBinaryOp(node, unit, operation, type));
                        }

                    }
                    break;
                default:
                    res = CallReverseBinaryOp(node, unit, operation, rhs);
                    break;
            }
            return res;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            int? constIndex = GetConstantIndex(index);

            if (constIndex != null) {
                if (constIndex.Value < 0) {
                    constIndex += IndexTypes.Length;
                }
                if (0 <= constIndex.Value && constIndex.Value < IndexTypes.Length) {
                    // TODO: Warn if outside known index and no appends?
                    IndexTypes[constIndex.Value].AddDependency(unit);
                    return IndexTypes[constIndex.Value].Types;
                }
            }

            if (index.Split(out IReadOnlyList<SliceInfo> sliceInfo, out _)) {
                return this.SelfSet;
            }

            if (!unit.ForEval) {
                if (IndexTypes.Length == 0) {
                    IndexTypes = new[] { new VariableDef() };
                }

                IndexTypes[0].AddDependency(unit);
            }

            EnsureUnionType();
            return UnionType;
        }

        internal static int? GetConstantIndex(IAnalysisSet index) {
            int? constIndex = null;
            int typeCount = 0;
            foreach (var type in index) {
                object constValue = type.GetConstantValue();
                if (constValue != null && constValue is int) {
                    constIndex = (int)constValue;
                }

                typeCount++;
            }
            if (typeCount != 1) {
                constIndex = null;
            }
            return constIndex;
        }


        public override IEnumerable<KeyValuePair<IAnalysisSet, IAnalysisSet>> GetItems() {
            for (int i = 0; i < IndexTypes.Length; i++) {
                var value = IndexTypes[i];

                yield return new KeyValuePair<IAnalysisSet, IAnalysisSet>(
                    ProjectState.GetConstant(i),
                    value.Types
                );
            }
        }

        protected override IAnalysisSet CreateWithNewTypes(Node node, VariableDef[] types) {
            return new SequenceInfo(types, ClassInfo, node, DeclaringModule);
        }
    }

    internal class StarArgsSequenceInfo : SequenceInfo {
        public StarArgsSequenceInfo(VariableDef[] variableDef, BuiltinClassInfo builtinClassInfo, Node node, IPythonProjectEntry entry)
            : base(variableDef, builtinClassInfo, node, entry) {
        }

        internal void SetIndex(AnalysisUnit unit, int index, IAnalysisSet value) {
            var types = IndexTypes;
            if (index < 0) {
                index += types.Length;
                if (index < 0) {
                    return;
                }
            }

            if (index >= types.Length) {
                IndexTypes = types = types.Concat(VariableDef.Generator).Take(index + 1).ToArray();
            }
            types[index].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, value);
            types[index].AddTypes(unit, value, true, DeclaringModule);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            int? constIndex = GetConstantIndex(index);

            if (constIndex != null) {
                SetIndex(unit, constIndex.Value, value);
            } else {
                if (IndexTypes.Length == 0) {
                    IndexTypes = new[] { new VariableDef() };
                }
                IndexTypes[0].MakeUnionStrongerIfMoreThan(ProjectState.Limits.IndexTypes, value);
                IndexTypes[0].AddTypes(unit, value, true, DeclaringModule);
            }
        }

        public override string ToString() {
            return "*" + base.ToString();
        }

        internal void MakeUnionStronger() {
            if (IndexTypes != null) {
                foreach (var it in IndexTypes) {
                    it.MakeUnionStronger();
                }
            }
        }

        internal bool MakeUnionStrongerIfMoreThan(int typeCount, IAnalysisSet extraTypes = null) {
            bool anyChanged = false;
            if (IndexTypes != null) {
                foreach (var it in IndexTypes) {
                    anyChanged |= it.MakeUnionStrongerIfMoreThan(typeCount, extraTypes);
                }
            }
            return anyChanged;
        }
    }

    /// <summary>
    /// Represents a *args parameter for a function definition.  Holds onto a SequenceInfo which
    /// includes all of the types passed in via splatting or extra position arguments.
    /// </summary>
    sealed class ListParameterVariableDef : LocatedVariableDef {
        public readonly StarArgsSequenceInfo List;
        public readonly string Name;

        public ListParameterVariableDef(AnalysisUnit unit, Node location, string name)
            : base(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location)) {
            Name = name;
            List = new StarArgsSequenceInfo(
                VariableDef.EmptyArray,
                unit.State.ClassInfos[BuiltinTypeId.Tuple],
                location,
                unit.ProjectEntry
            );
            base.AddTypes(unit, List, false, unit.DeclaringModule.ProjectEntry);
        }

        public ListParameterVariableDef(AnalysisUnit unit, Node location, VariableDef copy)
            : base(unit.DeclaringModule.ProjectEntry, new EncodedLocation(unit, location), copy) {
            List = new StarArgsSequenceInfo(
                VariableDef.EmptyArray,
                unit.State.ClassInfos[BuiltinTypeId.Tuple],
                location,
                unit.ProjectEntry
            );
            base.AddTypes(unit, List, false, unit.DeclaringModule.ProjectEntry);
        }
    }

}
