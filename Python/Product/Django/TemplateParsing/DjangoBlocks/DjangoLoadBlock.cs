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
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    class DjangoLoadBlock : DjangoBlock {
        private readonly int _fromStart, _nameStart, _fromNameStart;

        public DjangoLoadBlock(BlockParseInfo parseInfo, int fromStart, int nameStart, int fromNameStart)
            : base(parseInfo) {
            _fromStart = fromStart;
            _nameStart = nameStart;
            _fromNameStart = fromNameStart;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            // TODO: Need to handle whitespace better
            // TODO: Need to split identifiers into individual components
            var words = parseInfo.Args.Split(' ');
            int fromNameStart = -1;
            int fromStart = -1;
            int nameStart = parseInfo.Start + 1;
            for (int i = 1; i < words.Length; i++) {
                if (String.IsNullOrWhiteSpace(words[i])) {
                    nameStart += words[i].Length + 1;
                } else {
                    break;
                }
            }

            if (words.Length >= 4 && words[words.Length - 2] == "from") {
                // load fob from oar

            }

            return new DjangoLoadBlock(parseInfo, fromStart, nameStart, fromNameStart);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(ParseInfo.Start, 4), Classification.Keyword);
            if (_fromStart != -1) {
                yield return new BlockClassification(new Span(_fromStart, 4), Classification.Keyword);
            }
        }
    }
}
