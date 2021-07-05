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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    /// <summary>
    /// Handles blocks which take an unlimited number of variable arguments.  Includes
    /// ifchanged and firstof
    /// </summary>
    class DjangoMultiVariableArgumentBlock : DjangoBlock {
        private readonly DjangoVariable[] _variables;

        public DjangoMultiVariableArgumentBlock(BlockParseInfo parseInfo, params DjangoVariable[] variables)
            : base(parseInfo) {
            _variables = variables;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Args.Split(' ');
            List<BlockClassification> argClassifications = new List<BlockClassification>();

            int wordStart = parseInfo.Start + parseInfo.Command.Length;

            return new DjangoMultiVariableArgumentBlock(parseInfo, ParseVariables(words, wordStart));
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return GetCompletions(context, position, _variables);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            foreach (var span in base.GetSpans()) {
                yield return span;
            }

            foreach (var variable in _variables) {
                foreach (var span in variable.GetSpans()) {
                    yield return span;
                }
            }
        }
    }
}
