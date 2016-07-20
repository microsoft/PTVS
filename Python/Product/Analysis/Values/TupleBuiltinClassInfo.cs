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
    class TupleBuiltinClassInfo : SequenceBuiltinClassInfo {
        public TupleBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        internal override SequenceInfo MakeFromIndexes(Node node, ProjectEntry entry) {
            if (_indexTypes.Count > 0) {
                var vals = new[] { new VariableDef() };
                vals[0].AddTypes(entry, _indexTypes, false, entry);
                return new SequenceInfo(vals, this, node, entry);
            } else {
                return new SequenceInfo(VariableDef.EmptyArray, this, node, entry);
            }
        }
    }
}
