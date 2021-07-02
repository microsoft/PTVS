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
    class DjangoIfOrIfNotEqualBlock : DjangoBlock
    {
        private readonly DjangoVariable[] _args;

        public DjangoIfOrIfNotEqualBlock(BlockParseInfo parseInfo, params DjangoVariable[] args)
            : base(parseInfo)
        {
            _args = args;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo)
        {
            return new DjangoIfOrIfNotEqualBlock(
                parseInfo,
                ParseVariables(parseInfo.Args.Split(' '), parseInfo.Start + parseInfo.Command.Length, 2)
            );
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position)
        {
            return GetCompletions(context, position, _args, 2);
        }

        public override IEnumerable<BlockClassification> GetSpans()
        {
            foreach (var span in base.GetSpans())
            {
                yield return span;
            }

            foreach (var variable in _args)
            {
                foreach (var span in variable.GetSpans())
                {
                    yield return span;
                }
            }
        }
    }
}
