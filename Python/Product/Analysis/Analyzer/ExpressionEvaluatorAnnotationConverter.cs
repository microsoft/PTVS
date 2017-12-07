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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Analyzer {
    class ExpressionEvaluatorAnnotationConverter : TypeAnnotationConverter<IAnalysisSet> {
        private readonly ExpressionEvaluator _eval;
        private readonly Node _node;
        private readonly AnalysisUnit _unit;
        private readonly bool _returnInternalTypes;

        public ExpressionEvaluatorAnnotationConverter(ExpressionEvaluator eval, Node node, AnalysisUnit unit, bool returnInternalTypes = false) {
            _eval = eval ?? throw new ArgumentNullException(nameof(eval));
            _node = node ?? throw new ArgumentNullException(nameof(node));
            _unit = unit ?? throw new ArgumentNullException(nameof(unit));
            _returnInternalTypes = returnInternalTypes;
        }

        public override IAnalysisSet Finalize(IAnalysisSet type) {
            // Filter out any TypingTypeInfo items that have leaked through
            if (!_returnInternalTypes && type.Split(out IReadOnlyList<TypingTypeInfo> typeInfo, out IAnalysisSet rest)) {
                return rest.UnionAll(typeInfo.Select(n => n.Finalize(_eval, _node, _unit)));
            }

            return type;
        }

        public override IAnalysisSet LookupName(string name) {
            var res = _eval.LookupAnalysisSetByName(_node, name);

            if (res.Any()) {
                return res;
            }

            return null;
        }

        public override IAnalysisSet MakeNameType(string name) {
            return new ConstantInfo(_unit.ProjectState.ClassInfos[BuiltinTypeId.Unicode], name, PythonMemberType.Constant);
        }

        public override IAnalysisSet GetTypeMember(IAnalysisSet baseType, string member) {
            return baseType.GetMember(_node, _unit, member);
        }

        public override IAnalysisSet MakeGeneric(IAnalysisSet baseType, IReadOnlyList<IAnalysisSet> args) {
            if (baseType.Split(out IReadOnlyList<TypingTypeInfo> typeInfo, out var rest)) {
                return rest.UnionAll(
                    typeInfo.Select(tti => tti.MakeGeneric(args))
                );
            }
            return baseType;
        }

        public override IAnalysisSet MakeUnion(IReadOnlyList<IAnalysisSet> types) {
            return AnalysisSet.UnionAll(types);
        }

        public override IAnalysisSet MakeList(IReadOnlyList<IAnalysisSet> types) {
            return new TypingTypeInfo(" List").MakeGeneric(types);
        }

        public override IAnalysisSet MakeOptional(IAnalysisSet type) {
            return type.Add(_unit.ProjectState._noneInst);
        }

        public override IAnalysisSet GetNonOptionalType(IAnalysisSet optionalType) {
            if (optionalType.Split(v => v.TypeId != Interpreter.BuiltinTypeId.NoneType, out var notNone, out _)) {
                return notNone;
            }
            return AnalysisSet.Empty;
        }

        public override IReadOnlyList<IAnalysisSet> GetUnionTypes(IAnalysisSet unionType) {
            return unionType.ToArray<IAnalysisSet>();
        }
    }
}
