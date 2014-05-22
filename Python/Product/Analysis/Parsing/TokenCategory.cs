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

namespace Microsoft.PythonTools.Parsing {

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1027:MarkEnumsWithFlags")]
    public enum TokenCategory {
        None,

        /// <summary>
        /// A token marking an end of stream.
        /// </summary>
        EndOfStream,

        /// <summary>
        /// A space, tab, or newline.
        /// </summary>
        WhiteSpace,

        /// <summary>
        /// A block comment.
        /// </summary>
        Comment,

        /// <summary>
        /// A single line comment.
        /// </summary>
        LineComment,

        /// <summary>
        /// A documentation comment.
        /// </summary>
        DocComment,

        /// <summary>
        /// A numeric literal.
        /// </summary>
        NumericLiteral,

        /// <summary>
        /// A character literal.
        /// </summary>
        CharacterLiteral,

        /// <summary>
        /// A string literal.
        /// </summary>
        StringLiteral,

        /// <summary>
        /// A regular expression literal.
        /// </summary>
        RegularExpressionLiteral,

        /// <summary>
        /// A keyword.
        /// </summary>
        Keyword,

        /// <summary>
        /// A directive (e.g. #line).
        /// </summary>
        Directive,

        /// <summary>
        /// A punctuation character that has a specific meaning in a language.
        /// </summary>
        Operator,

        /// <summary>
        /// A token that operates as a separator between two language elements.
        /// </summary>
        Delimiter,

        /// <summary>
        /// An identifier (variable, $variable, @variable, @@variable, $variable$, function!, function?, [variable], i'variable', ...)
        /// </summary>
        Identifier,

        /// <summary>
        /// Braces, parenthesis, brackets.
        /// </summary>
        Grouping,

        /// <summary>
        /// Errors.
        /// </summary>
        Error,

        /// <summary>
        /// The start or continuation of an incomplete multi-line string literal
        /// </summary>
        IncompleteMultiLineStringLiteral,

        /// <summary>
        /// Special identifier that is built into the language.
        /// </summary>
        BuiltinIdentifier,

        LanguageDefined = 0x100
    }
}