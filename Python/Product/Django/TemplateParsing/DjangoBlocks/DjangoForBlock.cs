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
    class DjangoForBlock : DjangoBlock {
        public readonly int InStart;
        public readonly int VariableEnd;
        public readonly DjangoVariable Variable;
        public readonly int ArgsEnd;
        public readonly int ReversedStart;
        private readonly Tuple<string, int>[] _definedVars;
        private static readonly char[] NewLines = new[] { '\r', '\n' };

        public DjangoForBlock(BlockParseInfo parseInfo, int inStart, DjangoVariable variable, int argsEnd, int reversedStart, Tuple<string, int>[] definedVars)
            : base(parseInfo) {
            InStart = inStart;
            Variable = variable;
            ArgsEnd = argsEnd;
            ReversedStart = reversedStart;
            _definedVars = definedVars;
        }

        public static DjangoForBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Args.Split(' ');
            int inStart = -1;

            int inOffset = 0, inIndex = -1;
            var definitions = new List<Tuple<string, int>>();
            for (int i = 0; i < words.Length; i++) {
                var word = words[i];
                if (word == "in") {
                    inStart = inOffset + parseInfo.Start + parseInfo.Command.Length;
                    inIndex = i;
                    break;
                } else if (words[i].IndexOfAny(NewLines) != -1) {
                    // unterminated tag
                    break;
                }

                if (!String.IsNullOrEmpty(word)) {
                    definitions.Add(new Tuple<string, int>(word, inOffset + parseInfo.Start + parseInfo.Command.Length));
                }
                inOffset += words[i].Length + 1;
            }

            // parse the arguments...
            int reversedStart = -1;
            DjangoVariable variable = null;
            int argsEnd = -1;
            if (inIndex != -1) {
                string filterText = "";
                argsEnd = inStart + "in".Length + 1;
                for (int i = inIndex + 1; i < words.Length; i++) {
                    int nlStart = words[i].IndexOfAny(NewLines);
                    string trimmed = words[i];
                    if (nlStart != -1) {
                        trimmed = words[i].Substring(0, nlStart);
                    }

                    if (i != inIndex + 1 && trimmed == words[i]) { // if we trimmed we don't have an extra space
                        filterText += " ";
                        argsEnd += 1;
                    }

                    if (trimmed == "reversed") {
                        reversedStart = argsEnd;
                        break;
                    }

                    filterText += trimmed;
                    argsEnd += trimmed.Length;
                    if (trimmed != words[i]) {
                        // unterminated tag
                        break;
                    }
                }

                var trimmedFilter = filterText.TrimStart(' ');

                variable = DjangoVariable.Parse(trimmedFilter,
                    inStart + "in".Length + 1 + filterText.Length - trimmedFilter.Length);
            }

            return new DjangoForBlock(parseInfo, inStart, variable, argsEnd, reversedStart, definitions.ToArray());
        }

        public override IEnumerable<string> GetVariables() {
            foreach (var word in _definedVars) {
                yield return word.Item1;
            }
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(ParseInfo.Start, 3), Classification.Keyword);
            foreach (var word in _definedVars) {
                yield return new BlockClassification(new Span(word.Item2, word.Item1.Length), Classification.Identifier);
            }
            if (InStart != -1) {
                yield return new BlockClassification(new Span(InStart, 2), Classification.Keyword);
            }
            if (Variable != null) {
                foreach (var span in Variable.GetSpans()) {
                    yield return span;
                }
            }
            if (ReversedStart != -1) {
                yield return new BlockClassification(new Span(ReversedStart, "reversed".Length), Classification.Keyword);
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            if (InStart == -1 || position < InStart) {
                return new CompletionInfo[0];
            } else if (Variable != null && position > InStart) {
                var res = Variable.GetCompletions(context, position);
                if (position > ArgsEnd &&
                    ReversedStart == -1 &&
                    Variable.Expression != null) {
                    return System.Linq.Enumerable.Concat(
                        res,
                        new[] { new CompletionInfo("reversed", StandardGlyphGroup.GlyphKeyword) }
                    );
                }
                return res;
            }

            return base.GetCompletions(context, position);
        }
    }
}
