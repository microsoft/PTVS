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

using Microsoft.PythonTools.Analysis.Interpreter;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class RangeInfo : BuiltinInstanceInfo {
        public RangeInfo(IPythonType seqType, PythonAnalyzer state)
            : base(state._listType) {
        }

        public override INamespaceSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return ProjectState._intType.SelfSet;
        }

        public override INamespaceSet GetIndex(Node node, AnalysisUnit unit, INamespaceSet index) {
            // TODO: Return correct index value if we have a constant
            /*int? constIndex = SequenceInfo.GetConstantIndex(index);

            if (constIndex != null && constIndex.Value < _indexTypes.Count) {
                // TODO: Warn if outside known index and no appends?
                return _indexTypes[constIndex.Value];
            }*/

            return ProjectState._intType.SelfSet;
        }
    }
}
