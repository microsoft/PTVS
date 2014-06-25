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
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class SetInfo : SequenceInfo {
        public SetInfo(PythonAnalyzer projectState, Node node, ProjectEntry entry)
            : base(VariableDef.EmptyArray, projectState.ClassInfos[BuiltinTypeId.Set], node, entry) { }

        public void AddTypes(AnalysisUnit unit, IAnalysisSet types) {
            base.AddTypes(unit, new[] { types });
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            IAnalysisSet res;

            switch (operation) {
                case PythonOperator.BitwiseOr:
                    var seq = (SetInfo)unit.Scope.GetOrMakeNodeValue(
                        node,
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
