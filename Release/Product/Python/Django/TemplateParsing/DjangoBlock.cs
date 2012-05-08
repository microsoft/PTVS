/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Django.TemplateParsing {
    class DjangoBlock {
        private static readonly Dictionary<string, Func<BlockParseInfo, DjangoBlock>> _parsers = new Dictionary<string, Func<BlockParseInfo, DjangoBlock>>() {
            {"autoescape", DjangoAutoEscapeBlock.Parse},
            {"comment", DjangoCommentBlock.Parse},
            {"cycle", DjangoCycleBlock.Parse},
            {"debug", DjangoDebugBlock.Parse},
            {"filter", DjangoFilterBlock.Parse},
            {"firstof", DjangoFirstOfBlock.Parse},
            {"for", DjangoForBlock.Parse},
            {"ifequal", DjangoIfEqualBlock.Parse},
            {"ifnotequal", DjangoIfNotEqualBlock.Parse},
            {"if", DjangoIfBlock.Parse},
            {"ifchanged", DjangoIfChangedBlock.Parse},
            {"ssi", DjangoSsiBlock.Parse},
            {"load", DjangoLoadBlock.Parse},
            {"now", DjangoNowBlock.Parse},
            {"regroup", DjangoRegroupBlock.Parse},
            {"spaceless", DjangoSpacelessBlock.Parse}
        };

        public static DjangoBlock Parse(string text) {
            int start = 0;
            if (text.StartsWith("{%") && text.EndsWith("%}")) {
                text = DjangoVariable.GetTrimmedFilterText(text, out start);
                if (text == null) {
                    return null;
                }
            }

            var words = text.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0) {
                Func<BlockParseInfo, DjangoBlock> parser;
                if (!_parsers.TryGetValue(words[0], out parser)) {
                    parser = DjangoUnknownBlock.Parse;
                }

                return parser(new BlockParseInfo(text, start));
            }

            return null;
        }

        public virtual IEnumerable<BlockClassification> GetSpans() {
            yield break;
        }
    }

    /// <summary>
    /// args: 'on' or 'off'
    /// </summary>
    class DjangoAutoEscapeBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoUnknownBlock : DjangoBlock {
        private readonly int _start, _length, _otherLength;

        public DjangoUnknownBlock(int start, int length, int otherLength) {
            _start = start;
            _length = length;
            _otherLength = otherLength;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Text.Split(' ');
            if (words.Length > 0) {
                return new DjangoUnknownBlock(
                    parseInfo.Start,
                    words[0].Length,
                    parseInfo.Text.Length - (words[0].Length + 1)
                );
            }

            return null;
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(_start, _length), Classification.Keyword);
            if (_otherLength != -1) {
                yield return new BlockClassification(new Span(_start + _length + 1, _otherLength), Classification.ExcludedCode);
            }
        }
    }

    /// <summary>
    /// ends with 'endcomment'
    /// </summary>
    class DjangoCommentBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    /// <summary>
    /// inside loop takes args for multiple strings ({% cycle 'row1' 'row2' %})
    /// cycle 'foo' 'bar' as baz and then can refer:
    /// cycle baz
    /// </summary>
    class DjangoCycleBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoCsrfBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoDebugBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoFilterBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoFirstOfBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoForBlock : DjangoBlock {
        public readonly int Start, InStart;

        public DjangoForBlock(int start, int inStart) {
            Start = start;
            InStart = inStart;
        }

        public static DjangoForBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Text.Split(' ');
            int argEnd;
            int inStart = -1;
            if (words.Length > 0 && words[words.Length - 1] == "reversed") {
                argEnd = words.Length - 3;
            } else {
                argEnd = words.Length - 2;
            }
            
            if (words[argEnd] == "in") {
                inStart = parseInfo.Start;
                for (int i = 0; i < argEnd; i++) {
                    inStart += words[i].Length + 1;
                }
            }

            // parse the arguments...
            for (int i = 1; i < argEnd; i++) {

            }

            return new DjangoForBlock(parseInfo.Start, inStart);            
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(Start, 3), Classification.Keyword);
            if (InStart != -1) {
                yield return new BlockClassification(new Span(InStart, 2), Classification.Keyword);
            }
        }
    }

    class DjangoIfEqualBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoIfNotEqualBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoIfBlock : DjangoBlock {
        private readonly int _start;

        public DjangoIfBlock(int start) {
            _start = start;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoIfBlock(parseInfo.Start);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(_start, 2), Classification.Keyword);
        }
    }

    class DjangoIfChangedBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoSsiBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoLoadBlock : DjangoBlock {
        private readonly int _start, _fromStart, _nameStart, _fromNameStart;

        public DjangoLoadBlock(int start, int fromStart, int nameStart, int fromNameStart) {
            _start = start;
            _fromStart = fromStart;
            _nameStart = nameStart;
            _fromNameStart = fromNameStart;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            // TODO: Need to handle whitespace better
            // TODO: Need to split identifiers into individual components
            var words = parseInfo.Text.Split(' ');
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
                // load foo from bar
                
            }

            return new DjangoLoadBlock(parseInfo.Start, fromStart, nameStart, fromNameStart);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(_start, 4), Classification.Keyword);
            if (_fromStart != -1) {
                yield return new BlockClassification(new Span(_fromStart, 4), Classification.Keyword);
            }
        }
    }

    class DjangoNowBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoRegroupBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoSpacelessBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoTemplateTagBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoUrlBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoWidthRatioBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
        }
    }

    class DjangoWithBlock : DjangoBlock {
        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return DjangoUnknownBlock.Parse(parseInfo);
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
        ExcludedCode
    }

    class BlockParseInfo {
        public readonly string Text;
        public readonly int Start;

        public BlockParseInfo(string text, int start) {
            Text = text;
            Start = start;
        }
    }
}
