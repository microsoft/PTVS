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
using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class TypingTypeInfo : AnalysisValue {
        private readonly string _baseName;

        public TypingTypeInfo(string baseName) {
            _baseName = baseName;
        }

        public TypingTypeInfo MakeGeneric(IReadOnlyList<IAnalysisSet> args) {
            return this;
        }

        public IAnalysisSet Finalize(ExpressionEvaluator eval, Node node, AnalysisUnit unit) {
            var analyzer = unit.ProjectState;

            switch (_baseName) {
                case "Callable": return analyzer.ClassInfos[BuiltinTypeId.Function];
                case "Tuple": return analyzer.ClassInfos[BuiltinTypeId.Tuple];
                case "Container": return analyzer.ClassInfos[BuiltinTypeId.List];
                case "ItemsView": return analyzer.ClassInfos[BuiltinTypeId.DictItems];
                case "Iterable": return analyzer.ClassInfos[BuiltinTypeId.Tuple];
                case "Iterator": return analyzer.ClassInfos[BuiltinTypeId.TupleIterator];
                case "KeysView": return analyzer.ClassInfos[BuiltinTypeId.DictKeys];
                case "Mapping": return analyzer.ClassInfos[BuiltinTypeId.Dict];
                case "MappingView": return analyzer.ClassInfos[BuiltinTypeId.Dict];
                case "MutableMapping": return analyzer.ClassInfos[BuiltinTypeId.Dict];
                case "MutableSequence": return analyzer.ClassInfos[BuiltinTypeId.List];
                case "MutableSet": return analyzer.ClassInfos[BuiltinTypeId.Set];
                case "Sequence": return analyzer.ClassInfos[BuiltinTypeId.Tuple];
                case "ValuesView": return analyzer.ClassInfos[BuiltinTypeId.DictValues];
                case "Dict": return analyzer.ClassInfos[BuiltinTypeId.Dict];
                case "List": return analyzer.ClassInfos[BuiltinTypeId.List];
                case "Set": return analyzer.ClassInfos[BuiltinTypeId.Set];
                case "FrozenSet": return analyzer.ClassInfos[BuiltinTypeId.FrozenSet];
                case "NamedTuple": return analyzer.ClassInfos[BuiltinTypeId.Tuple];
                case "Generator": return analyzer.ClassInfos[BuiltinTypeId.Generator];
            }

            return null;
        }
    }
}
