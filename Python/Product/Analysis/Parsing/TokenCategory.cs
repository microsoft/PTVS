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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Parsing {

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