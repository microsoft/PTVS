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

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    class DjangoTemplateTagBlock : DjangoBlock {
        private readonly int _argStart;
        private readonly string _tagType;

        public DjangoTemplateTagBlock(BlockParseInfo parseInfo, int argStart, string tagType)
            : base(parseInfo) {
            _argStart = argStart;
            _tagType = tagType;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Args.Split(' ');
            int argStart = parseInfo.Command.Length + parseInfo.Start;
            string tagType = null;

            foreach (var word in words) {
                if (!String.IsNullOrEmpty(word)) {
                    tagType = word;
                    break;
                }
                argStart += 1;
            }
            // TODO: It'd be nice to report an error if we have more than one word
            // or if it's an unrecognized tag
            return new DjangoTemplateTagBlock(parseInfo, argStart, tagType);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            foreach (var span in base.GetSpans()) {
                yield return span;
            }
            if (_tagType != null) {
                yield return new BlockClassification(
                    new Span(_argStart, _tagType.Length),
                    Classification.Keyword
                );
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            if (_tagType == null) {
                return GetTagList();
            } else if (position >= _argStart && position < _argStart + _tagType.Length) {
                // filter based upon entered text
                string filter = _tagType.Substring(0, position - _argStart);
                return GetTagList().Where(tag => tag.DisplayText.StartsWith(filter, StringComparison.CurrentCultureIgnoreCase));
            }
            return new CompletionInfo[0];
        }

        private static CompletionInfo[] GetTagList() {
            return new[] {
                    new CompletionInfo("openblock", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("closeblock", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("openvariable", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("closevariable", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("openbrace", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("closebrace", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("opencomment", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("closecomment", StandardGlyphGroup.GlyphKeyword),
                };
        }
    }
}
