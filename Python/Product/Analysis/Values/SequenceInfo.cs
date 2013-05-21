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

using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class SequenceInfo : IterableInfo {
        public SequenceInfo(VariableDef[] indexTypes, BuiltinClassInfo seqType, Node node)
            : base(indexTypes, seqType, node) {
        }

        public override int? GetLength() {
            return IndexTypes.Length;
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            switch (operation) {
                case PythonOperator.Multiply:
                    IAnalysisSet res = AnalysisSet.Empty;
                    foreach (var type in rhs) {
                        var typeId = type.TypeId;

                        if (typeId == BuiltinTypeId.Int || typeId == BuiltinTypeId.Long) {
                            res = res.Union(this);
                        } else {
                            var partialRes = type.ReverseBinaryOperation(node, unit, operation, SelfSet);
                            if (partialRes != null) {
                                res = res.Union(partialRes);
                            }
                        }

                    }
                    return res;
            }
            return base.BinaryOperation(node, unit, operation, rhs);
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            int? constIndex = GetConstantIndex(index);

            if (constIndex != null && constIndex.Value < IndexTypes.Length) {
                // TODO: Warn if outside known index and no appends?
                IndexTypes[constIndex.Value].AddDependency(unit);
                return IndexTypes[constIndex.Value].Types;
            }

            SliceInfo sliceInfo = GetSliceIndex(index);
            if (sliceInfo != null) {
                return this.SelfSet;
            }

            if (IndexTypes.Length == 0) {
                IndexTypes = new[] { new VariableDef() };
            }

            IndexTypes[0].AddDependency(unit);

            EnsureUnionType();
            return UnionType;
        }

        private SliceInfo GetSliceIndex(IAnalysisSet index) {
            foreach (var type in index) {
                if (type is SliceInfo) {
                    return type as SliceInfo;
                }
            }
            return null;
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

        public override string ShortDescription {
            get {
                return _type.Name;
            }
        }

        public override string ToString() {
            return Description;
        }

        public override string Description {
            get {
                return MakeDescription(_type.Name);
            }
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
    }

    internal class StarArgsSequenceInfo : SequenceInfo {
        public StarArgsSequenceInfo(VariableDef[] variableDef, BuiltinClassInfo builtinClassInfo, Node node)
            : base(variableDef, builtinClassInfo, node) {
        }

        internal void SetIndex(AnalysisUnit unit, int index, IAnalysisSet value) {
            if (index >= IndexTypes.Length) {
                var newTypes = new VariableDef[index + 1];
                for (int i = 0; i < newTypes.Length; ++i) {
                    if (i < IndexTypes.Length) {
                        newTypes[i] = IndexTypes[i];
                    } else {
                        newTypes[i] = new VariableDef();
                    }
                }
                IndexTypes = newTypes;
            }
            IndexTypes[index].AddTypes(unit, value);
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            int? constIndex = GetConstantIndex(index);

            if (constIndex != null) {
                SetIndex(unit, constIndex.Value, value);
            } else {
                if (IndexTypes.Length == 0) {
                    IndexTypes = new[] { new VariableDef() };
                }
                IndexTypes[0].AddTypes(unit, value);
            }
        }


        public override string ShortDescription {
            get {
                return base.ShortDescription;
            }
        }

        public override string ToString() {
            return "*" + base.ToString();
        }

        internal int TypesCount {
            get {
                if (IndexTypes == null) {
                    return 0;
                }
                return IndexTypes.Aggregate(0, (total, it) => total + it.TypesNoCopy.Count);
            }
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
}
