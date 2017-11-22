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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    class ExpressionEvaluatorAnnotationConverter : TypeAnnotationConverter<IAnalysisSet> {
        private readonly ExpressionEvaluator _eval;
        private readonly Node _node;
        private readonly AnalysisUnit _unit;

        public ExpressionEvaluatorAnnotationConverter(ExpressionEvaluator eval, Node node, AnalysisUnit unit) {
            _eval = eval ?? throw new ArgumentNullException(nameof(eval));
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _unit = unit ?? throw new ArgumentNullException(nameof(unit));
        }

        public override IAnalysisSet Finalize(IAnalysisSet type) {
            return type;
        }

        public override IAnalysisSet LookupName(string name) {
            return _eval.LookupAnalysisSetByName(_node, name);
        }

        public override IAnalysisSet GetTypeMember(IAnalysisSet baseType, string member) {
            return baseType.GetMember(_node, _unit, member);
        }

        public override IAnalysisSet MakeUnion(IReadOnlyList<IAnalysisSet> types) {
            return AnalysisSet.UnionAll(types);
        }

        public override IReadOnlyList<IAnalysisSet> GetUnionTypes(IAnalysisSet unionType) {
            return unionType.ToArray();
        }
    }
}
