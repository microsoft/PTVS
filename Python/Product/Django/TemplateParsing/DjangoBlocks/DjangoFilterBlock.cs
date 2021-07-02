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

namespace Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks
{
    class DjangoFilterBlock : DjangoBlock
    {
        private readonly DjangoVariable _variable;

        public DjangoFilterBlock(BlockParseInfo parseInfo, DjangoVariable variable)
            : base(parseInfo)
        {
            _variable = variable;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo)
        {
            int start = 0;
            for (int i = 0; i < parseInfo.Args.Length && parseInfo.Args[i] == ' '; i++, start++)
            {
            }

            var variable = DjangoVariable.Parse(
                "var|" + parseInfo.Args.Substring(start),
                parseInfo.Start + start + parseInfo.Command.Length
            );

            return new DjangoFilterBlock(parseInfo, variable);
        }

        public override IEnumerable<BlockClassification> GetSpans()
        {
            foreach (var span in base.GetSpans())
            {
                yield return span;
            }

            if (_variable.Filters != null)
            {
                foreach (var filter in _variable.Filters)
                {
                    foreach (var span in filter.GetSpans(-4))
                    {
                        yield return span;
                    }
                }
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position)
        {
            return _variable.GetCompletions(context, position + 4);
        }
    }
}
