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
using System.Linq;
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
        /// <param name="blockStart"></param>
        public DjangoBlock(BlockParseInfo parseInfo) {
            ParseInfo = parseInfo;
        }

        /// <summary>
        /// Parses the text and returns a DjangoBlock.  Returns null if the block is empty
        /// or consists entirely of whitespace.
        /// </summary>
        public static DjangoBlock Parse(string text) {
            int start = 0;
            if (text.StartsWith("{%")) {
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
                    return CompletionInfo.ToCompletionInfo(vars.Keys, StandardGlyphGroup.GlyphGroupField);
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
                {"templatetag", DjangoTemplateTagBlock.Parse}
            };
        }
    }

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
                    if (word.StartsWith("\r") || word.StartsWith("\n")) {
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

    /// <summary>
    /// Handles blocks which don't take any arguments.  Includes debug, csrf, comment
    /// </summary>
    class DjangoArgumentlessBlock : DjangoBlock {
        public DjangoArgumentlessBlock(BlockParseInfo parseInfo)
            : base(parseInfo) {
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoArgumentlessBlock(parseInfo);
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return new CompletionInfo[0];
        }
    }

    class DjangoFilterBlock : DjangoBlock {
        private readonly DjangoVariable _variable;

        public DjangoFilterBlock(BlockParseInfo parseInfo, DjangoVariable variable)
            : base(parseInfo) {
            _variable = variable;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            int start = 0;
            for (int i = 0; i < parseInfo.Args.Length && parseInfo.Args[i] == ' '; i++, start++) {
            }

            var variable = DjangoVariable.Parse(
                "var|" + parseInfo.Args.Substring(start),
                parseInfo.Start + start + parseInfo.Command.Length
            );

            return new DjangoFilterBlock(parseInfo, variable);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            foreach (var span in base.GetSpans()) {
                yield return span;
            }

            if (_variable.Filters != null) {
                foreach (var filter in _variable.Filters) {
                    foreach (var span in filter.GetSpans(-4)) {
                        yield return span;
                    }
                }
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return _variable.GetCompletions(context, position + 4);
        }
    }

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

    class DjangoIfOrIfNotEqualBlock : DjangoBlock {
        private readonly DjangoVariable[] _args;

        public DjangoIfOrIfNotEqualBlock(BlockParseInfo parseInfo, params DjangoVariable[] args)
            : base(parseInfo) {
            _args = args;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoIfOrIfNotEqualBlock(
                parseInfo,
                ParseVariables(parseInfo.Args.Split(' '), parseInfo.Start + parseInfo.Command.Length, 2)
            );
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return GetCompletions(context, position, _args, 2);
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            foreach (var span in base.GetSpans()) {
                yield return span;
            }

            foreach (var variable in _args) {
                foreach (var span in variable.GetSpans()) {
                    yield return span;
                }
            }
        }
    }

    class DjangoIfBlock : DjangoBlock {
        public readonly BlockClassification[] Args;

        public DjangoIfBlock(BlockParseInfo parseInfo, params BlockClassification[] args)
            : base(parseInfo) {
            Args = args;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            var words = parseInfo.Args.Split(' ');
            List<BlockClassification> argClassifications = new List<BlockClassification>();

            int wordStart = parseInfo.Start + parseInfo.Command.Length;
            foreach (var word in words) {
                bool hasNewline = false;
                if (word.Contains('\r') || word.Contains('\n')) {
                    hasNewline = true;
                    if (word.Trim().Length == 0) {
                        break;
                    }
                }
                if (!String.IsNullOrEmpty(word)) {
                    Classification curKind;
                    switch (word) {
                        case "and":
                        case "or":
                        case "not": curKind = Classification.Keyword; break;
                        default: curKind = Classification.Identifier; break;
                    }

                    argClassifications.Add(
                        new BlockClassification(
                            new Span(wordStart, word.Length),
                            curKind
                        )
                    );
                }

                if (hasNewline) {
                    break;
                }

                wordStart += word.Length + 1;
            }

            return new DjangoIfBlock(parseInfo, argClassifications.ToArray());
        }

        public override IEnumerable<BlockClassification> GetSpans() {
            yield return new BlockClassification(new Span(ParseInfo.Start, ParseInfo.Command.Length), Classification.Keyword);
            foreach (var arg in Args) {
                yield return arg;
            }
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            // no argument yet, or the last argument was a keyword, then we are completing an identifier
            if (Args.Length == 0 || Args.Last().Classification == Classification.Keyword || position <= Args.Last().Span.End) {
                // get the variables
                return Enumerable.Concat(
                    base.GetCompletions(context, position),
                    new[] {
                        new CompletionInfo("not", StandardGlyphGroup.GlyphKeyword)
                    }
                );
            } else {
                // last word was an identifier, so we'll complete and/or
                return new[] {
                    new CompletionInfo("and", StandardGlyphGroup.GlyphKeyword),
                    new CompletionInfo("or", StandardGlyphGroup.GlyphKeyword)
                };
            }
        }
    }

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
                // load foo from bar

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

    class DjangoSpacelessBlock : DjangoBlock {
        public DjangoSpacelessBlock(BlockParseInfo parseInfo)
            : base(parseInfo) {
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoSpacelessBlock(parseInfo);
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return new CompletionInfo[0];
        }
    }

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
                return GetTagList().Where(tag => tag.DisplayText.StartsWith(filter));
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

    class DjangoWidthRatioBlock : DjangoBlock {
        private readonly DjangoVariable[] _variables;

        public DjangoWidthRatioBlock(BlockParseInfo parseInfo, params DjangoVariable[] variables)
            : base(parseInfo) {
            _variables = variables;
        }

        public static DjangoBlock Parse(BlockParseInfo parseInfo) {
            return new DjangoWidthRatioBlock(parseInfo,
                ParseVariables(parseInfo.Args.Split(' '), parseInfo.Command.Length + parseInfo.Start, 3));
        }

        public override IEnumerable<CompletionInfo> GetCompletions(IDjangoCompletionContext context, int position) {
            return GetCompletions(context, position, _variables, 3);
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

    class BlockParseInfo {
        public readonly string Command;
        public readonly string Args;
        public readonly int Start;

        public BlockParseInfo(string command, string text, int start) {
            Command = command;
            Args = text;
            Start = start;
        }
    }
}
