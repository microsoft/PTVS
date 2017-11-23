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
    internal class SetInfo : SequenceInfo {
        public SetInfo(PythonAnalyzer projectState, Node node, ProjectEntry entry)
            : base(VariableDef.EmptyArray, projectState.ClassInfos[BuiltinTypeId.Set], node, entry) { }

        internal SetInfo(BuiltinClassInfo seqType, Node node, ProjectEntry entry, VariableDef[] indexTypes)
            : base(indexTypes, seqType, node, entry) { }

        public void AddTypes(AnalysisUnit unit, IAnalysisSet types) {
            base.AddTypes(unit, new[] { types });
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            IAnalysisSet res;

            switch (operation) {
                case PythonOperator.BitwiseOr:
                    var seq = (SetInfo)unit.Scope.GetOrMakeNodeValue(
                        node,
                        NodeValueKind.Set,
                        _ => new SetInfo(ProjectState, node, unit.ProjectEntry)
                    );
                    seq.AddTypes(unit, GetEnumeratorTypes(node, unit));
                    foreach (var type in rhs.Where(t => t.IsOfType(ClassInfo))) {
                        seq.AddTypes(unit, type.GetEnumeratorTypes(node, unit));
                    }
                    res = seq;
                    break;
                case PythonOperator.BitwiseAnd:
                case PythonOperator.ExclusiveOr:
                case PythonOperator.Subtract:
                    res = this;
                    break;
                default:
                    res = CallReverseBinaryOp(node, unit, operation, rhs);
                    break;
            }

            return res;
        }
    }
}
