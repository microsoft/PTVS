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

using Microsoft.PythonTools.Analysis.Analyzer;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    class DictBuiltinClassInfo : BuiltinClassInfo {
        public DictBuiltinClassInfo(IPythonType classObj, PythonAnalyzer projectState)
            : base(classObj, projectState) {
        }

        protected override BuiltinInstanceInfo MakeInstance() => PythonType is IPythonLookupType lt ? new DictBuiltinInstanceInfo(this, lt) : new BuiltinInstanceInfo(this);

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var res = (DictionaryInfo)unit.Scope.GetOrMakeNodeValue(
                node,
                NodeValueKind.Dictionary,
                (node_) => new DictionaryInfo(unit.ProjectEntry, node)
            );

            if (keywordArgNames.Length > 0) {
                for (int i = 0; i < keywordArgNames.Length; i++) {
                    var curName = keywordArgNames[i].Name;
                    var curArg = args[args.Length - keywordArgNames.Length + i];
                    if (curName == "**") {
                        foreach (var value in curArg) {
                            CopyFrom(args, res);
                        }
                    } else if (curName != "*") {
                        res.AddTypes(
                            node,
                            unit,
                            ProjectState.GetConstant(curName),
                            curArg
                        );
                    }
                }
            } else if (args.Length == 1) {
                foreach (var value in args[0]) {
                    CopyFrom(args, res);
                }
            }
            return res;
        }

        private static void CopyFrom(IAnalysisSet[] args, DictionaryInfo res) {
            DictionaryInfo copied = args[0] as DictionaryInfo;
            if (copied != null) {
                res.CopyFrom(copied);
            }
        }
    }
}
