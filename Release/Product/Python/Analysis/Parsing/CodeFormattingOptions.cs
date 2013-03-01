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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        #region Class Definition Options

        /// <summary>
        /// Space before the parenthesis in a class declaration.
        [CodeFormattingExample("class X (object): pass", "class X(object): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Classes)]
        [CodeFormattingDescription("SpaceBeforeClassDeclarationParenShort", "SpaceBeforeClassDeclarationParenLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceBeforeClassDeclarationParen { get; set; }

        /// <summary>
        /// Space after the opening paren and before the closing paren in a class definition.
        /// </summary>
        [CodeFormattingExample("class X( object ): pass", "class X(object): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Classes)]
        [CodeFormattingDescription("SpaceWithinClassDeclarationParensShort", "SpaceWithinClassDeclarationParensLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinClassDeclarationParens { get; set; }

        /// <summary>
        /// Space within empty base class list for a class definition.
        /// </summary>
        [CodeFormattingExample("class X( ): pass", "class X(): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Classes)]
        [CodeFormattingDescription("SpaceWithinEmptyBaseClassListShort", "SpaceWithinEmptyBaseClassListLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinEmptyBaseClassList { get; set; }

        #endregion

        #region Method Definition Options

        /* Method Definitions */

        /// <summary>
        /// Space before the parenthesis in a function declaration.
        [CodeFormattingExample("def X (): pass", "def X(): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Functions)]
        [CodeFormattingDescription("SpaceBeforeFunctionDeclarationParenShort", "SpaceBeforeFunctionDeclarationParenLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceBeforeFunctionDeclarationParen { get; set; }

        /// <summary>
        /// Space after the opening paren and before the closing paren in a function definition.
        /// </summary>
        [CodeFormattingExample("def X( a, b ): pass", "def X(a, b): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Functions)]
        [CodeFormattingDescription("SpaceWithinFunctionDeclarationParensShort", "SpaceWithinFunctionDeclarationParensLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinFunctionDeclarationParens { get; set; }

        /// <summary>
        /// Space within empty parameter list for a function definition.
        /// </summary>
        [CodeFormattingExample("def X( ): pass", "def X(): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Functions)]
        [CodeFormattingDescription("SpaceWithinEmptyParameterListShort", "SpaceWithinEmptyParameterListLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinEmptyParameterList { get; set; }

        /// <summary>
        /// Spaces around the equals for a default value in a parameter list.
        /// </summary>
        [CodeFormattingExample("def X(a = 42): pass", "def X(a=42): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Functions)]
        [CodeFormattingDescription("SpaceAroundDefaultValueEqualsShort", "SpaceAroundDefaultValueEqualsLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceAroundDefaultValueEquals { get; set; }

        /// <summary>
        /// Spaces around the arrow annotation in a function definition.
        /// </summary>
        [CodeFormattingExample("def X() -> 42: pass", "def X()->42: pass")]
        [CodeFormattingCategory(CodeFormattingCategory.Functions)]
        [CodeFormattingDescription("SpaceAroundAnnotationArrowShort", "SpaceAroundAnnotationArrowLong")]
        [CodeFormattingDefaultValue(true)]
        public bool? SpaceAroundAnnotationArrow { get; set; }

        #endregion

        #region Function Call Options

        /// <summary>
        /// Space before the parenthesis in a call expression.
        /// </summary>
        [CodeFormattingExample("X ()", "X()")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceBeforeCallParenShort", "SpaceBeforeCallParenLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceBeforeCallParen { get; set; }

        /// <summary>
        /// Spaces within the parenthesis in a call expression with no arguments.
        /// </summary>
        [CodeFormattingExample("X( )", "X()")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceWithinEmptyCallArgumentListShort", "SpaceWithinEmptyCallArgumentListLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinEmptyCallArgumentList { get; set; }

        /// <summary>
        /// Space within the parenthesis in a call expression.
        /// </summary>
        [CodeFormattingExample("X( a, b )", "X(a, b)")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceWithinCallParensShort", "SpaceWithinCallParensLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinCallParens { get; set; }

        #endregion

        #region Other Spacing

        [CodeFormattingExample("( a )", "(a)")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpacesWithinParenthesisExpressionShort", "SpacesWithinParenthesisExpressionLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpacesWithinParenthesisExpression { get; set; }

        [CodeFormattingExample("( )", "()")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceWithinEmptyTupleExpressionShort", "SpaceWithinEmptyTupleExpressionLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinEmptyTupleExpression { get; set; }

        [CodeFormattingExample("( a, b )", "(a, b)")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpacesWithinParenthesisedTupleExpressionShort", "SpacesWithinParenthesisedTupleExpressionLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpacesWithinParenthesisedTupleExpression { get; set; }

        [CodeFormattingExample("[ ]", "[]")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpacesWithinEmptyListExpressionShort", "SpacesWithinEmptyListExpressionLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpacesWithinEmptyListExpression { get; set; }

        [CodeFormattingExample("[ a, b ]", "[a, b]")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpacesWithinEmptyListExpressionShort", "SpacesWithinEmptyListExpressionLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpacesWithinListExpression { get; set; }

        /* Index Expressions */
        [CodeFormattingExample("x [i]", "x[i]")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceBeforeIndexBracketShort", "SpaceBeforeIndexBracketLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceBeforeIndexBracket { get; set; }

        [CodeFormattingExample("x[ i ]", "x[i]")]
        [CodeFormattingCategory(CodeFormattingCategory.Spacing)]
        [CodeFormattingDescription("SpaceWithinIndexBracketsShort", "SpaceWithinIndexBracketsLong")]
        [CodeFormattingDefaultValue(false)]
        public bool? SpaceWithinIndexBrackets { get; set; }

        #endregion

        #region Operators

        [CodeFormattingExample("a + b", "a+b")]
        [CodeFormattingCategory(CodeFormattingCategory.Operators)]
        [CodeFormattingDescription("SpacesAroundBinaryOperatorsShort", "SpacesAroundBinaryOperatorsLong")]
        [CodeFormattingDefaultValue(true)]
        public bool? SpacesAroundBinaryOperators { get; set; }

        [CodeFormattingExample("a = b", "a=b")]
        [CodeFormattingCategory(CodeFormattingCategory.Operators)]
        [CodeFormattingDescription("SpacesAroundAssignmentOperatorShort", "SpacesAroundAssignmentOperatorLong")]
        [CodeFormattingDefaultValue(true)]
        public bool? SpacesAroundAssignmentOperator { get; set; }

        #endregion

        #region Statements

        [CodeFormattingExample("import sys\r\nimport pickle", "import sys, pickle")]
        [CodeFormattingCategory(CodeFormattingCategory.Statements)]
        [CodeFormattingDescription("ReplaceMultipleImportsWithMultipleStatementsShort", "ReplaceMultipleImportsWithMultipleStatementsLong")]
        [CodeFormattingDefaultValue(true)]
        public bool ReplaceMultipleImportsWithMultipleStatements { get; set; }

        [CodeFormattingExample("x = 42", "x = 42;")]
        [CodeFormattingCategory(CodeFormattingCategory.Statements)]
        [CodeFormattingDescription("RemoveTrailingSemicolonsShort", "RemoveTrailingSemicolonsLong")]
        [CodeFormattingDefaultValue(true)]
        public bool RemoveTrailingSemicolons { get; set; }

        [CodeFormattingExample("x = 42\r\ny = 100", "x = 42; y = 100")]
        [CodeFormattingCategory(CodeFormattingCategory.Statements)]
        [CodeFormattingDescription("BreakMultipleStatementsPerLineShort", "BreakMultipleStatementsPerLineLong")]
        [CodeFormattingDefaultValue(true)]
        public bool BreakMultipleStatementsPerLine { get; set; }

        #endregion

        /*
        #region New Lines

        [CodeFormattingExample("# Specifies the number of lines which whould appear between top-level classes and functions")]
        [CodeFormattingCategory(CodeFormattingCategory.NewLines)]
        [CodeFormattingDescription("LinesBetweenLevelDeclarationsShort", "LinesBetweenLevelDeclarationsLong")]
        [CodeFormattingDefaultValue(2)]
        public int LinesBetweenLevelDeclarations { get; set; }

        [CodeFormattingExample("# Specifies the number of lines between methods in classes")]
        [CodeFormattingCategory(CodeFormattingCategory.NewLines)]
        [CodeFormattingDescription("LinesBetweenMethodsInClassShort", "LinesBetweenMethodsInClassLong")]
        [CodeFormattingDefaultValue(1)]
        public int LinesBetweenMethodsInClass { get; set; }

        [CodeFormattingExample("class C:\r\n    def f(): pass\r\n\r\n    def g(): pass", "class C:\r\n    def f(): pass\r\n\r\n\r\n    def g(): pass")]
        [CodeFormattingCategory(CodeFormattingCategory.NewLines)]
        [CodeFormattingDescription("RemoveExtraLinesBetweenMethodsShort", "RemoveExtraLinesBetweenMethodsLong")]
        [CodeFormattingDefaultValue(false)]
        public bool RemoveExtraLinesBetweenMethods { get; set; }

        #endregion*/

        #region Wrapping

        [CodeFormattingExample("# Wrapped to 40 columns:\r\n# There should be one-- and preferably\r\n# only one --obvious way to do it.", "# Not wapped:\r\n# There should be one-- and preferably only one --obvious way to do it.")]
        [CodeFormattingCategory(CodeFormattingCategory.Wrapping)]
        [CodeFormattingDescription("WrapCommentsShort", "WrapCommentsLong")]
        [CodeFormattingDefaultValue(true)]
        public bool WrapComments { get; set; }

        [CodeFormattingExample("Sets the width for wrapping comments and doc strings.")]
        [CodeFormattingCategory(CodeFormattingCategory.Wrapping)]
        [CodeFormattingDescription("WrappingWidthShort", "WrappingWidthLong")]
        [CodeFormattingDefaultValue(80)]
        public int WrappingWidth { get; set; }

        #endregion

        /// <summary>
        /// Appends one of 3 strings depending upon a code formatting setting.  The 3 settings are the on and off
        /// settings as well as the original formatting if the setting is not set.
        /// </summary>
        internal void Append(StringBuilder res, bool? setting, string ifOn, string ifOff, string originalFormatting) {
            if (!String.IsNullOrWhiteSpace(originalFormatting) || setting == null) {
                // there's a comment in the formatting, so we need to preserve it.
                ReflowComment(res, originalFormatting);
            } else {
                res.Append(setting.Value ? ifOn : ifOff);
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
            if (text.EndsWith("\r") || text.EndsWith("\n")) {
                // split will give us an extra entry, but there's not really an extra line
                lineCount = lines.Length - 1;
            }
            int reflowStartingLine = 0, curLine = 0;
            do {
                string commentPrefix = GetCommentPrefix(lines[curLine]);
                if (commentPrefix == null) {
                    // non-commented line (empty?), just append it and continue
                    // to the next comment prefix if we have one
                    res.Append(lines[curLine] + (NewLineFormat ?? Environment.NewLine));
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

                if (lastSpace - curOffset < columnCutoff || columnCutoff == defaultColumnCutoff) {
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
                } else {
                    // current word is too long to append.  Start the next line.
                    while (newText.Length > 0 && newText[newText.Length - 1] == ' ') {
                        newText.Length = newText.Length - 1;
                    }
                    linesWritten++;
                    newText.Append(newLine);
                    columnCutoff = defaultColumnCutoff;
                }

                if (curOffset >= lines[curLine].Length) {
                    // we're not reading from the next line
                    if (lastSpace - curOffset < columnCutoff) {
                        // preserve existing new lines if they are shorter
                        // than the maximum length

                        // and remove any trailing whitespace from the line 
                        // before appending the new line
                        while (newText.Length > 0 && newText[newText.Length - 1] == ' ') {
                            newText.Length = newText.Length - 1;
                        }

                        linesWritten++;
                        columnCutoff = defaultColumnCutoff;
                        newText.Append(newLine);
                    }

                    curLine++;
                    curOffset = prefix.Length;

                }
            }
            return newText.ToString();
        }

    }
}
