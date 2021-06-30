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
using Microsoft.PythonTools.Django.TemplateParsing.DjangoBlocks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class DjangoBlock {
        public readonly BlockParseInfo ParseInfo;

        private static readonly Dictionary<string, Func<BlockParseInfo, DjangoBlock>> _parsers = MakeBlockTable();
        private static readonly string[] EmptyStrings = new string[0];

        /// <summary>
        /// Creates a new DjangoBlock capturing the start index of the block command (for, debug, etc...).
        /// </summary>
        public DjangoBlock(BlockParseInfo parseInfo) {
            ParseInfo = parseInfo;
        }

        /// <summary>
        /// Parses the text and returns a DjangoBlock.  Returns null if the block is empty
        /// or consists entirely of whitespace.
        /// </summary>
        public static DjangoBlock Parse(string text, bool trim = false) {
            int start = 0;
            if (text.StartsWithOrdinal("{%")) {
                text = DjangoVariable.GetTrimmedFilterText(text, ref start);
                if (text == null) {
                    return null;
                }
            }

            int firstChar = 0;
            for (int i = 0; i < text.Length; i++) {
                if (text[i] != ' ') {
                    firstChar = i;
                    break;
                }
            }

            int length = 0;
            for (int i = firstChar; i < text.Length && text[i] != ' '; i++, length++) ;

            if (length > 0) {
                string blockCmd = text.Substring(firstChar, length);
                if (Char.IsLetterOrDigit(blockCmd[0])) {
                    string args = text.Substring(firstChar + length, text.Length - (firstChar + length));
                    if (trim) {
                        args = args.TrimEnd();
                    }

                    Func<BlockParseInfo, DjangoBlock> parser;
                    if (!_parsers.TryGetValue(blockCmd, out parser)) {
                        parser = DjangoUnknownBlock.Parse;
                    }

                    return parser(new BlockParseInfo(blockCmd, args, firstChar + start));
                }
            }

            return null;
        }

        protected static DjangoVariable[] ParseVariables(string[] words, int wordStart, int maxVars = Int32.MaxValue) {
            List<DjangoVariable> variables = new List<DjangoVariable>();
            foreach (var word in words) {
                bool hasNewline = false;
                if (word.Contains('\r') || word.Contains('\n')) {
                    hasNewline = true;
                    if (word.Trim().Length == 0) {
                        break;
                    }
                }
                if (!String.IsNullOrEmpty(word)) {
                    variables.Add(DjangoVariable.Parse(word, wordStart));
                    if (variables.Count == maxVars) {
                        break;
                    }
                }

                if (hasNewline) {
                    break;
                }

                wordStart += word.Length + 1;
            }
            return variables.ToArray();
        }

        protected static IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position, DjangoVariable[] variables, int max = Int32.MaxValue) {
            for (int i = 0; i < variables.Length; i++) {
                if (position >= variables[i].ExpressionStart &&
                    (i == variables.Length - 1 || position < variables[i + 1].ExpressionStart)) {
                    var res = variables[i].GetCompletions(context, position);
                    if (res.Count() != 0) {
                        return res;
                    }
                }
            }

            if (variables.Length < max) {
                var vars = context.Variables;
                if (vars != null) {
                    return CompletionInfo.ToCompletionInfo(vars, StandardGlyphGroup.GlyphGroupField);
                }
            }

            return new CompletionInfo[0];
        }

        public virtual IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(
                new Span(ParseInfo.Start, ParseInfo.Command.Length),
                Classification.Keyword
            );
        }

        public virtual IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return CompletionInfo.ToCompletionInfo(context.Variables, StandardGlyphGroup.GlyphGroupField);
        }

        public virtual IEnumerable<string> GetVariables() {
            return EmptyStrings;
        }

        private static Dictionary<string, Func<BlockParseInfo, DjangoBlock>> MakeBlockTable() {
            return new Dictionary<string, Func<BlockParseInfo, DjangoBlock>>() {
                {"autoescape", DjangoAutoEscapeBlock.Parse},
                {"comment", DjangoArgumentlessBlock.Parse},
                {"cycle", DjangoUnknownBlock.Parse},
                {"csrf", DjangoArgumentlessBlock.Parse},
                {"debug", DjangoArgumentlessBlock.Parse},
                {"filter", DjangoFilterBlock.Parse},
                {"firstof", DjangoMultiVariableArgumentBlock.Parse},
                {"for", DjangoForBlock.Parse},
                {"ifequal", DjangoIfOrIfNotEqualBlock.Parse},
                {"ifnotequal", DjangoIfOrIfNotEqualBlock.Parse},
                {"if", DjangoIfBlock.Parse},
                {"elif", DjangoIfBlock.Parse},
                {"ifchanged", DjangoMultiVariableArgumentBlock.Parse},
                {"ssi", DjangoUnknownBlock.Parse},
                {"load", DjangoLoadBlock.Parse},
                {"now", DjangoUnknownBlock.Parse},
                {"regroup", DjangoUnknownBlock.Parse},
                {"spaceless", DjangoSpacelessBlock.Parse},
                {"widthratio", DjangoWidthRatioBlock.Parse},
                {"templatetag", DjangoTemplateTagBlock.Parse},
                {"url", DjangoUrlBlock.Parse}
            };
        }
    }

    struct BlockClassification {
        public readonly Span Span;
        public readonly Classification Classification;

        public BlockClassification(Span span, Classification classification) {
            Span = span;
            Classification = classification;
        }
    }

    enum Classification {
        None,
        Keyword,
        ExcludedCode,
        Identifier,
        Literal,
        Number,
        Dot
    }
}
