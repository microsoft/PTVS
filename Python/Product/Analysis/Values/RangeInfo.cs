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

using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    internal class RangeInfo : BuiltinInstanceInfo {
        public RangeInfo(IPythonType seqType, PythonAnalyzer state)
            : base(state.ClassInfos[BuiltinTypeId.List]) {
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return ProjectState.ClassInfos[BuiltinTypeId.Int].Instance;
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            // TODO: Return correct index value if we have a constant
            /*int? constIndex = SequenceInfo.GetConstantIndex(index);

            if (constIndex != null && constIndex.Value < _indexTypes.Count) {
                // TODO: Warn if outside known index and no appends?
                return _indexTypes[constIndex.Value];
            }*/

            return ProjectState.ClassInfos[BuiltinTypeId.Int].Instance;
        }
    }
}
