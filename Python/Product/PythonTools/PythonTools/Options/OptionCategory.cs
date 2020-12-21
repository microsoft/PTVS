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
using Microsoft.PythonTools.Parsing;
using static Microsoft.PythonTools.Strings;

namespace Microsoft.PythonTools.Options {
    /// <summary>
    /// Represents a category of options.  Currently only used for formatting options.
    /// </summary>
    class OptionCategory {
        public readonly string Description;
        public readonly OptionInfo[] Options;
        private static Dictionary<CodeFormattingCategory, List<OptionInfo>> _cachedOptions = new Dictionary<CodeFormattingCategory, List<OptionInfo>>() {
            [CodeFormattingCategory.NewLines] = new List<OptionInfo> {
                //new IntegerOptionInfo(LinesBetweenLevelDeclarationsShort, nameof(CodeFormattingOptions.LinesBetweenLevelDeclarations), LinesBetweenLevelDeclarationsLong, "# Specifies the number of lines which would appear between top-level classes and functions", 2),
                //new IntegerOptionInfo(LinesBetweenMethodsInClassShort, nameof(CodeFormattingOptions.LinesBetweenMethodsInClass), LinesBetweenMethodsInClassLong, "# Specifies the number of lines between methods in classes", 1),
                //new BooleanOptionInfo(RemoveExtraLinesBetweenMethodsShort, nameof(CodeFormattingOptions.RemoveExtraLinesBetweenMethods), RemoveExtraLinesBetweenMethodsLong, "class C:\r\n    def f(): pass\r\n\r\n    def g(): pass", "class C:\r\n    def f(): pass\r\n\r\n\r\n    def g(): pass", false),
            },
            [CodeFormattingCategory.Classes] = new List<OptionInfo> {
                new TriStateOptionInfo(SpaceBeforeClassDeclarationParenShort, nameof(CodeFormattingOptions.SpaceBeforeClassDeclarationParen), SpaceBeforeClassDeclarationParenLong, "class X (object): pass", "class X(object): pass", false),
                new TriStateOptionInfo(SpaceWithinClassDeclarationParensShort, nameof(CodeFormattingOptions.SpaceWithinClassDeclarationParens), SpaceWithinClassDeclarationParensLong, "class X( object ): pass", "class X(object): pass", false),
                new TriStateOptionInfo(SpaceWithinEmptyBaseClassListShort, nameof(CodeFormattingOptions.SpaceWithinEmptyBaseClassList), SpaceWithinEmptyBaseClassListLong, "class X( ): pass", "class X(): pass", false)
            },
            [CodeFormattingCategory.Functions] = new List<OptionInfo> {
                new TriStateOptionInfo(SpaceBeforeFunctionDeclarationParenShort, nameof(CodeFormattingOptions.SpaceBeforeFunctionDeclarationParen), SpaceBeforeFunctionDeclarationParenLong, "def X (): pass", "def X(): pass", false),
                new TriStateOptionInfo(SpaceWithinFunctionDeclarationParensShort, nameof(CodeFormattingOptions.SpaceWithinFunctionDeclarationParens), SpaceWithinFunctionDeclarationParensLong, "def X( a, b ): pass", "def X(a, b): pass", false),
                new TriStateOptionInfo(SpaceWithinEmptyParameterListShort, nameof(CodeFormattingOptions.SpaceWithinEmptyParameterList), SpaceWithinEmptyParameterListLong, "def X( ): pass", "def X(): pass", false),
                new TriStateOptionInfo(SpaceAroundDefaultValueEqualsShort, nameof(CodeFormattingOptions.SpaceAroundDefaultValueEquals), SpaceAroundDefaultValueEqualsLong, "def X(a = 42): pass", "def X(a=42): pass", false),
                new TriStateOptionInfo(SpaceAroundAnnotationArrowShort, nameof(CodeFormattingOptions.SpaceAroundAnnotationArrow), SpaceAroundAnnotationArrowLong, "def X() -> 42: pass", "def X()->42: pass", true),
            },
            [CodeFormattingCategory.Spacing] = new List<OptionInfo> {
                new TriStateOptionInfo(SpaceBeforeCallParenShort, nameof(CodeFormattingOptions.SpaceBeforeCallParen), SpaceBeforeCallParenLong, "X ()", "X()", false),
                new TriStateOptionInfo(SpaceWithinEmptyCallArgumentListShort, nameof(CodeFormattingOptions.SpaceWithinEmptyCallArgumentList), SpaceWithinEmptyCallArgumentListLong, "X( )", "X()", false),
                new TriStateOptionInfo(SpaceWithinCallParensShort, nameof(CodeFormattingOptions.SpaceWithinCallParens), SpaceWithinCallParensLong, "X( a, b )", "X(a, b)", false),
                new TriStateOptionInfo(SpacesWithinParenthesisExpressionShort, nameof(CodeFormattingOptions.SpacesWithinParenthesisExpression), SpacesWithinParenthesisExpressionLong, "( a )", "(a)", false),
                new TriStateOptionInfo(SpaceWithinEmptyTupleExpressionShort, nameof(CodeFormattingOptions.SpaceWithinEmptyTupleExpression), SpaceWithinEmptyTupleExpressionLong, "( )", "()", false),
                new TriStateOptionInfo(SpacesWithinParenthesisedTupleExpressionShort, nameof(CodeFormattingOptions.SpacesWithinParenthesisedTupleExpression), SpacesWithinParenthesisedTupleExpressionLong, "( a, b )", "(a, b)", false),
                new TriStateOptionInfo(SpacesWithinEmptyListExpressionShort, nameof(CodeFormattingOptions.SpacesWithinEmptyListExpression), SpacesWithinEmptyListExpressionLong, "[ ]", "[]", false),
                new TriStateOptionInfo(SpacesWithinListExpressionShort, nameof(CodeFormattingOptions.SpacesWithinListExpression), SpacesWithinListExpressionLong, "[ a, b ]", "[a, b]", false),
                new TriStateOptionInfo(SpaceBeforeIndexBracketShort, nameof(CodeFormattingOptions.SpaceBeforeIndexBracket), SpaceBeforeIndexBracketLong, "x [i]", "x[i]", false),
                new TriStateOptionInfo(SpaceWithinIndexBracketsShort, nameof(CodeFormattingOptions.SpaceWithinIndexBrackets), SpaceWithinIndexBracketsLong, "x[ i ]", "x[i]", false),
            },
            [CodeFormattingCategory.Operators] = new List<OptionInfo> {
                new TriStateOptionInfo(SpacesAroundBinaryOperatorsShort, nameof(CodeFormattingOptions.SpacesAroundBinaryOperators), SpacesAroundBinaryOperatorsLong, "a + b", "a+b", true),
                new TriStateOptionInfo(SpacesAroundAssignmentOperatorShort, nameof(CodeFormattingOptions.SpacesAroundAssignmentOperator), SpacesAroundAssignmentOperatorLong, "a = b", "a=b", true),
            },
            [CodeFormattingCategory.Statements] = new List<OptionInfo> {
                new BooleanOptionInfo(ReplaceMultipleImportsWithMultipleStatementsShort, nameof(CodeFormattingOptions.ReplaceMultipleImportsWithMultipleStatements), ReplaceMultipleImportsWithMultipleStatementsLong, "import sys\r\nimport pickle", "import sys, pickle", true),
                new BooleanOptionInfo(RemoveTrailingSemicolonsShort, nameof(CodeFormattingOptions.RemoveTrailingSemicolons), RemoveTrailingSemicolonsLong, "x = 42", "x = 42;", true),
                new BooleanOptionInfo(BreakMultipleStatementsPerLineShort, nameof(CodeFormattingOptions.BreakMultipleStatementsPerLine), BreakMultipleStatementsPerLineLong, "x = 42\r\ny = 100", "x = 42; y = 100", true)
            },
            [CodeFormattingCategory.Wrapping] = new List<OptionInfo> {
                new BooleanOptionInfo(WrapCommentsShort, nameof(CodeFormattingOptions.WrapComments), WrapCommentsLong, WrapCommentsExample_On, WrapCommentsExample_Off, true),
                new IntegerOptionInfo(WrappingWidthShort, nameof(CodeFormattingOptions.WrappingWidth), WrappingWidthLong, WrappingWidthExample, 80)
            },
        };

        public OptionCategory(string description, params OptionInfo[] options) {
            Description = description;
            Options = options;
        }

        public static OptionInfo GetOption(string key) {
            foreach (var options in _cachedOptions.Values) {
                foreach (var option in options) {
                    if (string.Equals(option.Key, key, StringComparison.Ordinal)) {
                        return option;
                    }
                }
            }

            return null;
        }

        public static OptionInfo[] GetOptions(CodeFormattingCategory category)
            => _cachedOptions.TryGetValue(category, out var options) ? options.ToArray() : new OptionInfo[0];
    }
}
