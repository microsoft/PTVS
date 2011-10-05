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
using System.Text;
using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Specialized built-in instance for sequences (lists, tuples)
    /// </summary>
    internal class SequenceInfo : IterableInfo {
        public SequenceInfo(ISet<Namespace>[] indexTypes, BuiltinClassInfo seqType)
            : base(indexTypes, seqType) {
        }

        public override int? GetLength() {
            return IndexTypes.Length;
        }

        public override ISet<Namespace> GetIndex(Node node, AnalysisUnit unit, ISet<Namespace> index) {
            ReturnValue.AddDependency(unit);
            int? constIndex = GetConstantIndex(index);

            if (constIndex != null && constIndex.Value < IndexTypes.Length) {
                // TODO: Warn if outside known index and no appends?
                return IndexTypes[constIndex.Value];
            }

            SliceInfo sliceInfo = GetSliceIndex(index);
            if (sliceInfo != null) {
                return this.SelfSet;
            }

            EnsureUnionType();
            return UnionType;
        }

        private SliceInfo GetSliceIndex(ISet<Namespace> index) {
            foreach (var type in index) {
                if (type is SliceInfo) {
                    return type as SliceInfo;
                }
            }
            return null;
        }

        internal static int? GetConstantIndex(ISet<Namespace> index) {
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

        public override string Description {
            get {
                EnsureUnionType();
                StringBuilder result = new StringBuilder(_type.Name);
                var unionType = UnionType.GetUnionType();
                if (unionType != null) {
                    result.Append(" of " + unionType.ShortDescription);
                } else {
                    result.Append("()");
                }

                return result.ToString();
            }
        }

        public override bool UnionEquals(Namespace ns) {
            SequenceInfo si = ns as SequenceInfo;
            if (si == null) {
                return false;
            }

            return si.IndexTypes.Length == IndexTypes.Length;
        }

        public override int UnionHashCode() {
            return IndexTypes.Length;
        }
    }
}
