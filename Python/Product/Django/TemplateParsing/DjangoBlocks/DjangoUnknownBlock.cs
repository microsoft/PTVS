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

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks {
    class DjangoUnknownBlock : DjangoBlock {
        public DjangoUnknownBlock(BlockParseInfo parseInfo)
            : base(parseInfo) {
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoUnknownBlock(parseInfo);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(
                new Span(ParseInfo.Start, ParseInfo.Command.Length),
                Classification.Keyword
            );

            if (ParseInfo.Args.Length > 0) {
                yield return new BlockClassification(
                    new Span(ParseInfo.Start + ParseInfo.Command.Length, ParseInfo.Args.Length),
                    Classification.ExcludedCode
                );
            }
        }
    }
}
