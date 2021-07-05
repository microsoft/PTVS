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
    /// args: 'on' or 'off'
    /// </summary>
    class DjangoAutoEscapeBlock : DjangoBlock {
        private readonly int _argStart, _argLength;

        public DjangoAutoEscapeBlock(BlockParseInfo parseInfo, int argStart, int argLength)
            : base(parseInfo) {
            _argStart = argStart;
            _argLength = argLength;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            var args = parseInfo.Args.Split(' ');
            int argStart = -1, argLength = -1;
            for (int i = 0; i < args.Length; i++) {
                var word = args[i];
                if (!String.IsNullOrEmpty(word)) {
                    if (word.StartsWithOrdinal("\r") || word.StartsWithOrdinal("\n")) {
                        // unterminated tag
                        break;
                    }
                    argStart = parseInfo.Start + parseInfo.Command.Length + i;
                    argLength = args[i].Length;
                    break;
                }
            }

            return new DjangoAutoEscapeBlock(parseInfo, argStart, argLength);
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            if (_argStart == -1) {
                return new[] {
                    new CompletionInfo(
                        "on",
                        StandardGlyphGroup.GlyphGroupVariable
                    ),
                    new CompletionInfo(
                        "off",
                        StandardGlyphGroup.GlyphGroupVariable
                    )
                };
            }
            return new CompletionInfo[0];
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            foreach (var span in base.GetSpans()) {
                yield return span;
            }

            if (_argStart != -1) {
                yield return new BlockClassification(
                    new Span(_argStart, _argLength),
                    Classification.Keyword
                );
            }
        }
    }
}
