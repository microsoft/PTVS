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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Parsing {
    /// <summary>
    /// Provides options for formatting code when calling Node.ToCodeString.
    /// 
    /// By default a newly created CodeFormattingOptions will result in any
    /// source code being formatted to be round tripped identical to the original
    /// input code.  Modifying any of the options from the defaults may result in 
    /// the code being formatted according to the option.
    /// 
    /// For boolean options setting to true will enable altering the code as described,
    /// and setting them to false will leave the code unmodified.
    /// 
    /// For bool? options setting the option to true will modify the code one way, setting
    /// it to false will modify it another way, and setting it to null will leave the code
    /// unmodified.
    /// </summary>
    public sealed class CodeFormattingOptions {
        internal static CodeFormattingOptions Default = new CodeFormattingOptions();    // singleton with no options set, internal so no one mutates it
        private static Regex _commentRegex = new Regex("^[\t ]*#+[\t ]*");
        private const string _sentenceTerminators = ".!?";

        public string NewLineFormat { get; set; }

        public static CodeFormattingOptions Traditional { get; } = new CodeFormattingOptions {
            SpaceAfterComma = true,
            SpaceAfterDot = false,
            SpaceAfterLambdaColon = true,
            SpaceAroundAnnotationArrow = true,
            SpaceAroundDefaultValueEquals = true,
            SpaceBeforeCallParen = false,
            SpaceBeforeClassDeclarationParen = false,
            SpaceBeforeComma = false,
            SpaceBeforeFunctionDeclarationParen = false,
            SpaceBeforeIndexBracket = false,
            SpaceBeforeDot = false,
            SpaceBeforeLambdaColon = false,
            SpacesAroundAssignmentOperator = true,
            SpacesAroundBinaryOperators = true,
            SpacesWithinEmptyListExpression = false,
            SpacesWithinListExpression = false,
            SpacesWithinParenthesisedTupleExpression = false,
            SpacesWithinParenthesisExpression = false,
            SpaceWithinCallParens = false,
            SpaceWithinClassDeclarationParens = false,
            SpaceWithinEmptyBaseClassList = false,
            SpaceWithinEmptyCallArgumentList = false,
            SpaceWithinEmptyParameterList = false,
            SpaceWithinEmptyTupleExpression = false,
            SpaceWithinFunctionDeclarationParens = false,
            SpaceWithinIndexBrackets = false,
            RemoveTrailingSemicolons = true
        };

        #region Class Definition Options

        /// <summary>
        /// Space before the parenthesis in a class declaration.
        /// </summary>
        public bool? SpaceBeforeClassDeclarationParen { get; set; }

        /// <summary>
        /// Space after the opening paren and before the closing paren in a class definition.
        /// </summary>
        public bool? SpaceWithinClassDeclarationParens { get; set; }

        /// <summary>
        /// Space within empty base class list for a class definition.
        /// </summary>
        public bool? SpaceWithinEmptyBaseClassList { get; set; }

        #endregion

        #region Method Definition Options

        /* Method Definitions */

        /// <summary>
        /// Space before the parenthesis in a function declaration.
        /// </summary>
        public bool? SpaceBeforeFunctionDeclarationParen { get; set; }

        /// <summary>
        /// Space after the opening paren and before the closing paren in a function definition.
        /// </summary>
        public bool? SpaceWithinFunctionDeclarationParens { get; set; }

        /// <summary>
        /// Space within empty parameter list for a function definition.
        /// </summary>
        public bool? SpaceWithinEmptyParameterList { get; set; }

        /// <summary>
        /// Spaces around the equals for a default value in a parameter list.
        /// </summary>
        public bool? SpaceAroundDefaultValueEquals { get; set; }

        /// <summary>
        /// Spaces around the arrow annotation in a function definition.
        /// </summary>
        public bool? SpaceAroundAnnotationArrow { get; set; }

        #endregion

        #region Function Call Options

        /// <summary>
        /// Space before the parenthesis in a call expression.
        /// </summary>
        public bool? SpaceBeforeCallParen { get; set; }

        /// <summary>
        /// Spaces within the parenthesis in a call expression with no arguments.
        /// </summary>
        public bool? SpaceWithinEmptyCallArgumentList { get; set; }

        /// <summary>
        /// Space within the parenthesis in a call expression.
        /// </summary>
        public bool? SpaceWithinCallParens { get; set; }

        #endregion

        #region Other Spacing

        public bool? SpacesWithinParenthesisExpression { get; set; }

        public bool? SpaceWithinEmptyTupleExpression { get; set; }

        public bool? SpacesWithinParenthesisedTupleExpression { get; set; }

        public bool? SpacesWithinEmptyListExpression { get; set; }

        public bool? SpacesWithinListExpression { get; set; }

        public bool? SpaceBeforeIndexBracket { get; set; }

        public bool? SpaceWithinIndexBrackets { get; set; }

        public bool? SpaceBeforeComma { get; set; }
        public bool? SpaceAfterComma { get; set; }

        public bool? SpaceBeforeDot { get; set; }
        public bool? SpaceAfterDot { get; set; }

        public bool? SpaceBeforeLambdaColon { get; set; }
        public bool? SpaceAfterLambdaColon { get; set; }

        public bool? SpaceBeforeSliceColon { get; set; }
        public bool? SpaceAfterSliceColon { get; set; }

        #endregion

        #region Operators

        public bool? SpacesAroundBinaryOperators { get; set; }

        public bool? SpacesAroundAssignmentOperator { get; set; }

        #endregion

        #region Statements

        public bool ReplaceMultipleImportsWithMultipleStatements { get; set; }

        public bool RemoveTrailingSemicolons { get; set; }

        public bool BreakMultipleStatementsPerLine { get; set; }

        #endregion

        /*
        #region New Lines

        public int LinesBetweenLevelDeclarations { get; set; }

        public int LinesBetweenMethodsInClass { get; set; }

        public bool RemoveExtraLinesBetweenMethods { get; set; }

        #endregion*/

        #region Wrapping

        public bool WrapComments { get; set; }

        public int WrappingWidth { get; set; }

        #endregion

        internal bool UseVerbatimImage { get; set; } = true;

        /// <summary>
        /// Appends one of 3 strings depending upon a code formatting setting.  The 3 settings are the on and off
        /// settings as well as the original formatting if the setting is not set.
        /// </summary>
        internal void Append(StringBuilder res, bool? setting, string ifOn, string ifOff, string originalFormatting) {
            if (!setting.HasValue) {
                // no preference, so use the original formatting
                if (!string.IsNullOrEmpty(originalFormatting)) {
                    ReflowComment(res, originalFormatting);
                }
            } else if (originalFormatting == null || originalFormatting.IndexOf('#') < 0) {
                // no original text, so use the setting
                res.Append(setting.Value ? ifOn : ifOff);
            } else {
                // there's a comment in the formatting, so we need to preserve it.
                ReflowComment(res, originalFormatting);
            } 
        }

        /// <summary>
        /// Given the whitespace from the proceeding line gets the whitespace that should come for following lines.
        /// 
        /// This strips extra new lines and takes into account the code formatting new lines options.
        /// </summary>
        internal string GetNextLineProceedingText(string proceeding) {
            int newLine;
            var additionalProceeding = proceeding;
            if ((newLine = additionalProceeding.LastIndexOfAny(new[] { '\r', '\n' })) == -1) {
                additionalProceeding = (NewLineFormat ?? Environment.NewLine) + proceeding;
            } else {
                // we just want to capture the indentation, not multiple newlines.
                additionalProceeding = (NewLineFormat ?? Environment.NewLine) + proceeding.Substring(newLine + 1);
            }
            return additionalProceeding;
        }

        internal void ReflowComment(StringBuilder res, string text) {
            if (!WrapComments || String.IsNullOrWhiteSpace(text) || text.IndexOf('#') == -1) {
                res.Append(text);
                return;
            }

            // figure out how many characters we have on the line not related to the comment,
            // we'll try and align with this.  For example:
            // (1, # This is a comment which will be wrapped
            //  2, ...
            // Should wrap to:
            // (1, # This is a comment 
            //     # which will be wrapped
            //  2,
            //
            int charsOnCurrentLine = GetCharsOnLastLine(res);

            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int lineCount = lines.Length;
            if (text.EndsWithOrdinal("\r") || text.EndsWithOrdinal("\n")) {
                // split will give us an extra entry, but there's not really an extra line
                lineCount = lines.Length - 1;
            }
            int reflowStartingLine = 0, curLine = 0;
            do { 
                string commentPrefix = GetCommentPrefix(lines[curLine]);
                if (commentPrefix == null) {
                    // non-commented line (empty?), just append it and continue
                    // to the next comment prefix if we have one
                    res.Append(lines[curLine] + (curLine == lineCount - 1 ? "" : (NewLineFormat ?? Environment.NewLine)));
                    charsOnCurrentLine = GetCharsOnLastLine(res);
                    reflowStartingLine = curLine + 1;
                } else if (curLine == lineCount - 1 || GetCommentPrefix(lines[curLine + 1]) != commentPrefix) {
                    // last line, or next line mismatches with our comment prefix.  So reflow the text now
                    res.Append(
                        ReflowText(
                            commentPrefix,
                            new string(' ', charsOnCurrentLine) + commentPrefix,    // TODO: Tabs instead of spaces
                            NewLineFormat ?? Environment.NewLine,
                            WrappingWidth,
                            lines.Skip(reflowStartingLine).Take(curLine - reflowStartingLine + 1).ToArray()
                        )
                    );
                    reflowStartingLine = curLine + 1;
                }
            } while (++curLine < lineCount);
        }

        private static int GetCharsOnLastLine(StringBuilder res) {
            for (int i = res.Length - 1; i >= 0; i--) {
                if (res[i] == '\n' || res[i] == '\r') {
                    return res.Length - i - 1;
                }
            }
            return res.Length;
        }

        internal static string GetCommentPrefix(string text) {
            var match = _commentRegex.Match(text);
            if (match.Success) {
                return text.Substring(0, match.Length);
            }
            return null;
        }

        internal static string ReflowText(string prefix, string additionalLinePrefix, string newLine, int maxLength, string[] lines) {
            int curLine = 0, curOffset = prefix.Length, linesWritten = 0;
            int columnCutoff = maxLength - prefix.Length;
            int defaultColumnCutoff = columnCutoff;
            StringBuilder newText = new StringBuilder();
            while (curLine < lines.Length) {
                string curLineText = lines[curLine];
                int lastSpace = curLineText.Length;

                // skip leading white space
                while (curOffset < curLineText.Length && Char.IsWhiteSpace(curLineText[curOffset])) {
                    curOffset++;
                }

                // find next word
                for (int i = curOffset; i < curLineText.Length; i++) {
                    if (Char.IsWhiteSpace(curLineText[i])) {
                        lastSpace = i;
                        break;
                    }
                }

                bool startNewLine = lastSpace - curOffset >= columnCutoff &&    // word won't fit in remaining space
                                    columnCutoff != defaultColumnCutoff;        // we're not already at the start of a new line

                if (!startNewLine) {
                    // we found a like break in the region and it's a reasonable size or
                    // we have a really long word that we need to append unbroken
                    if (columnCutoff == defaultColumnCutoff) {
                        // first time we're appending to this line
                        newText.Append(linesWritten == 0 ? prefix : additionalLinePrefix);
                    }

                    newText.Append(curLineText, curOffset, lastSpace - curOffset);

                    // append appropriate spacing
                    if (_sentenceTerminators.IndexOf(curLineText[lastSpace - 1]) != -1 ||   // we end in punctuation
                        ((lastSpace - curOffset) > 1 &&                                     // we close a paren that ends in punctuation
                        curLineText[lastSpace - curOffset] == ')' &&
                        _sentenceTerminators.IndexOf(curLineText[lastSpace - 2]) != -1)) {

                        newText.Append("  ");
                        columnCutoff -= lastSpace - curOffset + 2;
                    } else {
                        newText.Append(' ');
                        columnCutoff -= lastSpace - curOffset + 1;
                    }
                    curOffset = lastSpace + 1;

                    // if we reached the end of the line preserve the existing line break.
                    startNewLine = curOffset >= lines[curLine].Length;
                }

                if (startNewLine) {
                    // remove any trailing white space
                    while (newText.Length > 0 && newText[newText.Length - 1] == ' ') {
                        newText.Length = newText.Length - 1;
                    }
                    linesWritten++;
                    newText.Append(newLine);
                    columnCutoff = defaultColumnCutoff;
                }

                if (curOffset >= lines[curLine].Length) {
                    // we're now reading from the next line
                    curLine++;
                    curOffset = prefix.Length;
                }
            }
            return newText.ToString();
        }

    }
}
