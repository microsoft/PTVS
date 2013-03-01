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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities.Mocks;

namespace AnalysisTests {
    /// <summary>
    /// Test cases to verify that the parser successfully preserves all information for round tripping source code.
    /// </summary>
    [TestClass]
    public class ParserRoundTripTest {
        [TestMethod, Priority(0)]
        public void TestCodeFormattingOptions() {
            /* Function Definitions */
            // SpaceAroundDefaultValueEquals
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = true }, "def f(a = 2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = false }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = false }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = true }, "def f(a = 2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a=2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = null }, "def f(a=2): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 2): pass", new CodeFormattingOptions() { SpaceAroundDefaultValueEquals = null }, "def f(a = 2): pass");

            // SpaceBeforeMethodDeclarationParen
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = true }, "def f (): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = true }, "def f (): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = null }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f (): pass", new CodeFormattingOptions() { SpaceBeforeFunctionDeclarationParen = null }, "def f (): pass");

            // SpaceWithinEmptyArgumentList
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = true }, "def f( ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = true }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = false }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = false }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f( ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyParameterList = null }, "def f( a ): pass");

            // SpaceWithinMethodDeclarationParens
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( a, b ): pass");
            TestOneString(PythonLanguageVersion.V33, "def f(*, a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = true }, "def f( *, a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a, b ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V33, "def f( *, a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = false }, "def f(*, a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V33, "def f(*, a): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f(*, a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f( a, b ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( a, b ): pass");
            TestOneString(PythonLanguageVersion.V33, "def f( *, a ): pass", new CodeFormattingOptions() { SpaceWithinFunctionDeclarationParens = null }, "def f( *, a ): pass");

            // SpaceAroundAnnotationArrow
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = true }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = false }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f() -> 42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f() -> 42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()->42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f()->42: pass");
            TestOneString(PythonLanguageVersion.V33, "def f()  ->  42: pass", new CodeFormattingOptions() { SpaceAroundAnnotationArrow = null }, "def f()  ->  42: pass");

            // SpaceBeforeClassDeclarationParen
            TestOneString(PythonLanguageVersion.V27, "class foo(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = true }, "class foo (): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = true }, "class foo (): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = false }, "class foo(): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = false }, "class foo(): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = null }, "class foo(): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo (): pass", new CodeFormattingOptions() { SpaceBeforeClassDeclarationParen = null }, "class foo (): pass");

            // SpaceWithinEmptyBaseClassListList
            TestOneString(PythonLanguageVersion.V27, "class foo(): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = true }, "class foo( ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = true }, "class foo(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = false }, "class foo(): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = false }, "class foo( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class foo(): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(a): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class foo(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class foo( ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a ): pass", new CodeFormattingOptions() { SpaceWithinEmptyBaseClassList = null }, "class foo( a ): pass");

            // SpaceWithinClassDeclarationParens
            TestOneString(PythonLanguageVersion.V27, "class foo(a): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = true }, "class foo( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(a, b): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = true }, "class foo( a, b ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = false }, "class foo(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a, b ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = false }, "class foo(a, b): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(a): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class foo(a): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo(a, b): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class foo(a, b): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class foo( a ): pass");
            TestOneString(PythonLanguageVersion.V27, "class foo( a, b ): pass", new CodeFormattingOptions() { SpaceWithinClassDeclarationParens = null }, "class foo( a, b ): pass");

            /* Calls */
            // SpaceBeforeCallParen
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = false }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = false }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f  (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = true }, "f (a)");
            TestOneString(PythonLanguageVersion.V27, "f(a)", new CodeFormattingOptions() { SpaceBeforeCallParen = null }, "f(a)");
            TestOneString(PythonLanguageVersion.V27, "f (a)", new CodeFormattingOptions() { SpaceBeforeCallParen = null }, "f (a)");

            // SpaceWithinEmptyCallArgumentList
            TestOneString(PythonLanguageVersion.V27, "foo()", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = true }, "foo( )");
            TestOneString(PythonLanguageVersion.V27, "foo(a)", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = true }, "foo(a)");
            TestOneString(PythonLanguageVersion.V27, "foo( )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = false }, "foo()");
            TestOneString(PythonLanguageVersion.V27, "foo( a )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = false }, "foo( a )");
            TestOneString(PythonLanguageVersion.V27, "foo()", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "foo()");
            TestOneString(PythonLanguageVersion.V27, "foo(a)", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "foo(a)");
            TestOneString(PythonLanguageVersion.V27, "foo( )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "foo( )");
            TestOneString(PythonLanguageVersion.V27, "foo( a )", new CodeFormattingOptions() { SpaceWithinEmptyCallArgumentList = null }, "foo( a )");

            // SpaceWithinCallParens
            TestOneString(PythonLanguageVersion.V27, "foo(a)", new CodeFormattingOptions() { SpaceWithinCallParens = true }, "foo( a )");
            TestOneString(PythonLanguageVersion.V27, "foo(a, b)", new CodeFormattingOptions() { SpaceWithinCallParens = true }, "foo( a, b )");
            TestOneString(PythonLanguageVersion.V27, "foo( a )", new CodeFormattingOptions() { SpaceWithinCallParens = false }, "foo(a)");
            TestOneString(PythonLanguageVersion.V27, "foo( a, b )", new CodeFormattingOptions() { SpaceWithinCallParens = false }, "foo(a, b)");
            TestOneString(PythonLanguageVersion.V27, "foo(a)", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "foo(a)");
            TestOneString(PythonLanguageVersion.V27, "foo(a, b)", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "foo(a, b)");
            TestOneString(PythonLanguageVersion.V27, "foo( a )", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "foo( a )");
            TestOneString(PythonLanguageVersion.V27, "foo( a, b )", new CodeFormattingOptions() { SpaceWithinCallParens = null }, "foo( a, b )");

            /* Index Expressions */
            // SpaceWithinIndexBrackets
            TestOneString(PythonLanguageVersion.V27, "foo[a]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = true }, "foo[ a ]");
            TestOneString(PythonLanguageVersion.V27, "foo[a, b]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = true }, "foo[ a, b ]");
            TestOneString(PythonLanguageVersion.V27, "foo[ a ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = false }, "foo[a]");
            TestOneString(PythonLanguageVersion.V27, "foo[ a, b ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = false }, "foo[a, b]");
            TestOneString(PythonLanguageVersion.V27, "foo[a]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "foo[a]");
            TestOneString(PythonLanguageVersion.V27, "foo[a, b]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "foo[a, b]");
            TestOneString(PythonLanguageVersion.V27, "foo[ a ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "foo[ a ]");
            TestOneString(PythonLanguageVersion.V27, "foo[ a, b ]", new CodeFormattingOptions() { SpaceWithinIndexBrackets = null }, "foo[ a, b ]");

            // SpaceBeforeIndexBracket
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = false }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = false }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f  [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = true }, "f [a]");
            TestOneString(PythonLanguageVersion.V27, "f[a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = null }, "f[a]");
            TestOneString(PythonLanguageVersion.V27, "f [a]", new CodeFormattingOptions() { SpaceBeforeIndexBracket = null }, "f [a]");

            /* Other */
            // SpacesWithinParenthesisExpression
            TestOneString(PythonLanguageVersion.V27, "(a)", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = true }, "( a )");
            TestOneString(PythonLanguageVersion.V27, "( a )", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = false }, "(a)");
            TestOneString(PythonLanguageVersion.V27, "(a)", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = null }, "(a)");
            TestOneString(PythonLanguageVersion.V27, "( a )", new CodeFormattingOptions() { SpacesWithinParenthesisExpression = null }, "( a )");

            // WithinEmptyTupleExpression
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = true }, "( )");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = true }, "( )");
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = false }, "()");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = false }, "()");
            TestOneString(PythonLanguageVersion.V27, "()", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = null }, "()");
            TestOneString(PythonLanguageVersion.V27, "( )", new CodeFormattingOptions() { SpaceWithinEmptyTupleExpression = null }, "( )");

            // WithinParenthesisedTupleExpression
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a,b )");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = true }, "( a,b )");
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = false }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "(a,)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "(a,)");
            TestOneString(PythonLanguageVersion.V27, "(a,b)", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "(a,b)");
            TestOneString(PythonLanguageVersion.V27, "( a, )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "( a, )");
            TestOneString(PythonLanguageVersion.V27, "( a,b )", new CodeFormattingOptions() { SpacesWithinParenthesisedTupleExpression = null }, "( a,b )");

            // WithinEmptyListExpression
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = true }, "[ ]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = true }, "[ ]");
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = false }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = false }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = null }, "[]");
            TestOneString(PythonLanguageVersion.V27, "[ ]", new CodeFormattingOptions() { SpacesWithinEmptyListExpression = null }, "[ ]");

            // WithinListExpression
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a,b ]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = true }, "[ a,b ]");
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = false }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[a,]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[a,]");
            TestOneString(PythonLanguageVersion.V27, "[a,b]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[a,b]");
            TestOneString(PythonLanguageVersion.V27, "[ a, ]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[ a, ]");
            TestOneString(PythonLanguageVersion.V27, "[ a,b ]", new CodeFormattingOptions() { SpacesWithinListExpression = null }, "[ a,b ]");

            // SpacesAroundBinaryOperators
            foreach (var op in new[] { "+", "-", "/", "//", "*", "%", "**", "<<", ">>", "&", "|", "^", "<", ">", "<=", ">=", "!=", "<>" }) {
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa" + op + "bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa" + op + "bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa  " + op + "  bb");
            }

            foreach (var op in new[] { "is", "in", "is not", "not in" }) {
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = true }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = false }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa " + op + " bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa " + op + " bb");
                TestOneString(PythonLanguageVersion.V27, "aa  " + op + "  bb", new CodeFormattingOptions() { SpacesAroundBinaryOperators = null }, "aa  " + op + "  bb");
            }

            // SpacesAroundAssignmentOperator
            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = true }, "x = y = 2");

            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = false }, "x=y=2");

            TestOneString(PythonLanguageVersion.V27, "x = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x = 2");
            TestOneString(PythonLanguageVersion.V27, "x=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x  =  2");
            TestOneString(PythonLanguageVersion.V27, "x = y = 2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x = y = 2");
            TestOneString(PythonLanguageVersion.V27, "x=y=2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x=y=2");
            TestOneString(PythonLanguageVersion.V27, "x  =  y  =  2", new CodeFormattingOptions() { SpacesAroundAssignmentOperator = null }, "x  =  y  =  2");

            /* Statements */
            // ReplaceMultipleImportsWithMultipleStatements
            TestOneString(PythonLanguageVersion.V27, "import foo", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import foo");
            TestOneString(PythonLanguageVersion.V27, "import foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import foo\r\nimport bar");
            TestOneString(PythonLanguageVersion.V27, "\r\n\r\n\r\nimport foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "\r\n\r\n\r\nimport foo\r\nimport bar");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    import foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "def f():\r\n    import foo\r\n    import bar");
            TestOneString(PythonLanguageVersion.V27, "import foo as quox, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import foo as quox\r\nimport bar");
            TestOneString(PythonLanguageVersion.V27, "import   foo,  bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import   foo\r\nimport  bar");
            TestOneString(PythonLanguageVersion.V27, "import foo  as  quox, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = true }, "import foo  as  quox\r\nimport bar");

            TestOneString(PythonLanguageVersion.V27, "import foo", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import foo");
            TestOneString(PythonLanguageVersion.V27, "import foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import foo, bar");
            TestOneString(PythonLanguageVersion.V27, "\r\n\r\n\r\nimport foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "\r\n\r\n\r\nimport foo, bar");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    import foo, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "def f():\r\n    import foo, bar");
            TestOneString(PythonLanguageVersion.V27, "import foo as quox, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import foo as quox, bar");
            TestOneString(PythonLanguageVersion.V27, "import   foo,  bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import   foo,  bar");
            TestOneString(PythonLanguageVersion.V27, "import foo  as  quox, bar", new CodeFormattingOptions() { ReplaceMultipleImportsWithMultipleStatements = false }, "import foo  as  quox, bar");

            // RemoveTrailingSemicolons
            TestOneString(PythonLanguageVersion.V27, "x = 42;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x = 42  ;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x = 42;  y = 100;", new CodeFormattingOptions() { RemoveTrailingSemicolons = true }, "x = 42;  y = 100");
            TestOneString(PythonLanguageVersion.V27, "x = 42;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42;");
            TestOneString(PythonLanguageVersion.V27, "x = 42  ;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42  ;");
            TestOneString(PythonLanguageVersion.V27, "x = 42;  y = 100;", new CodeFormattingOptions() { RemoveTrailingSemicolons = false }, "x = 42;  y = 100;");

            // BreakMultipleStatementsPerLine
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, "x = 42\r\ny = 100");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, "def f():\r\n    x = 42\r\n    y = 100");
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100;", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, "x = 42\r\ny = 100;");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    x = 42; y = 100;", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true }, "def f():\r\n    x = 42\r\n    y = 100;");
            TestOneString(PythonLanguageVersion.V27, "x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true, RemoveTrailingSemicolons = true }, "x = 42\r\ny = 100");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    x = 42; y = 100", new CodeFormattingOptions() { BreakMultipleStatementsPerLine = true, RemoveTrailingSemicolons = true }, "def f():\r\n    x = 42\r\n    y = 100");
        }

        [TestMethod, Priority(0)]
        public void TestReflowComment() {
            var commentTestCases = new[] { 
                new {
                    Before = "  # Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.\r\n",
                    After =  "  # Beautiful is better than ugly.  Explicit is better than implicit.  Simple\r\n  # is better than complex.  Complex is better than complicated.\r\n"
                },
                new { 
                    Before = "## Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.\r\n",
                    After =  "## Beautiful is better than ugly.  Explicit is better than implicit.  Simple is\r\n## better than complex.  Complex is better than complicated.\r\n"
                },
                new {
                    Before = "############# Beautiful is better than ugly. Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.\r\n",
                    After =  "############# Beautiful is better than ugly.  Explicit is better than implicit.\r\n############# Simple is better than complex.  Complex is better than\r\n############# complicated.\r\n"
                },
                new {
                    Before = "  # Beautiful is better than ugly.\r\n  # import foo\r\n  # Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.\r\n",
                    After =  "  # Beautiful is better than ugly.\r\n  # import foo\r\n  # Explicit is better than implicit.  Simple is better than complex.  Complex\r\n  # is better than complicated.\r\n"
                },
                new {
                    Before = "  #\r\n  #   Beautiful is better than ugly.\r\n  #   import foo\r\n  #   Explicit is better than implicit. Simple is better than complex. Complex is better than complicated.\r\n",
                    After =  "  #\r\n  #   Beautiful is better than ugly.\r\n  #   import foo\r\n  #   Explicit is better than implicit.  Simple is better than complex.\r\n  #   Complex is better than complicated.\r\n"
                }
            };

            foreach (var preceedingText in commentTestCases) {
                Console.WriteLine("----");
                Console.WriteLine(preceedingText.Before);

                var allSnippets =
                    _snippets2x.Select(text => new { Text = text, Version = PythonLanguageVersion.V27 }).Concat(
                    _snippets3x.Select(text => new { Text = text, Version = PythonLanguageVersion.V33 }));

                foreach (var testCase in allSnippets) {
                    Console.WriteLine(testCase);

                    TestOneString(
                        testCase.Version,
                        preceedingText.Before + testCase.Text,
                        new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 80 },
                        preceedingText.After + testCase.Text
                    );
                }
            }

            // TODO: Comments inside of various groupings (base classes, etc...)
            foreach (var preceedingText in commentTestCases) {
                Console.WriteLine("----");
                Console.WriteLine(preceedingText.Before);

                foreach (var testCase in _insertionSnippets) {
                    Console.WriteLine(testCase);

                    var input = testCase.Replace("[INSERT]", preceedingText.Before);
                    var output = testCase.Replace("[INSERT]", preceedingText.After);

                    TestOneString(
                        PythonLanguageVersion.V27,
                        input,
                        new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 80 },
                        output
                    );
                }
            }
        }

        [TestMethod, Priority(0)]
        public void TestReflowComment2() {
            foreach (var optionValue in new bool?[] { true, false, null }) {
                var options = new CodeFormattingOptions() {
                    SpaceWithinClassDeclarationParens = optionValue,
                    SpaceWithinEmptyBaseClassList = optionValue,
                    SpaceWithinFunctionDeclarationParens = optionValue,
                    SpaceWithinEmptyParameterList = optionValue,
                    SpaceAroundDefaultValueEquals = optionValue,
                    SpaceBeforeCallParen = optionValue,
                    SpaceWithinEmptyCallArgumentList = optionValue,
                    SpaceWithinCallParens = optionValue,
                    SpacesWithinParenthesisExpression = optionValue,
                    SpaceWithinEmptyTupleExpression = optionValue,
                    SpacesWithinParenthesisedTupleExpression = optionValue,
                    SpacesWithinEmptyListExpression = optionValue,
                    SpacesWithinListExpression = optionValue,
                    SpaceBeforeIndexBracket = optionValue,
                    SpaceWithinIndexBrackets = optionValue,
                    SpacesAroundBinaryOperators = optionValue,
                    SpacesAroundAssignmentOperator = optionValue,
                };

                foreach (var testCase in _commentInsertionSnippets) {
                    Console.WriteLine(testCase);

                    var parser = Parser.CreateParser(
                        new StringReader(testCase.Replace("[INSERT]", "# comment here")), 
                        PythonLanguageVersion.V27, 
                        new ParserOptions() { Verbatim = true }
                    );
                    var ast = parser.ParseFile();
                    var newCode = ast.ToCodeString(ast, options);
                    Console.WriteLine(newCode);
                    Assert.IsTrue(newCode.IndexOf("# comment here") != -1);
                }
            }
        }

        /// <summary>
        /// Verify trailing \ doesn't mess up comments
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestReflowComment3() {
            var code = @"def f():
    if a and \
        b:
            print('hi')";

            var parser = Parser.CreateParser(
                new StringReader(code),
                PythonLanguageVersion.V27,
                new ParserOptions() { Verbatim = true }
            );

            var ast = parser.ParseFile();
            var newCode = ast.ToCodeString(ast, new CodeFormattingOptions() { WrapComments = true, WrappingWidth = 20 });
            Assert.AreEqual(newCode, code);
        }

        static readonly string[] _commentInsertionSnippets = new[] {
            "class C(a, [INSERT]\r\n    b): pass",
            "class C( [INSERT]\r\n    ): pass", 
            "def f(a, [INSERT]\r\n    b): pass",
            "def f( [INSERT]\r\n    ): pass", 
            "def f(a = [INSERT]\r\n    42): pass",
            "g( f [INSERT]\r\n    (42))",
            "f( [INSERT]\r\n    )",
            "f( a, [INSERT]\r\n     )",
            "f([INSERT]\r\n   a)",
            "([INSERT]\r\n    a)",
            "(a [INSERT]\r\n    )",
            "(\r\n    [INSERT]\r\n)",
            "([INSERT]\r\n 1, 2, 3)",
            "(1,2,3[INSERT]\r\n)",
            "[[INSERT]\r\n]",
            "[[INSERT]\r\n1,2,3]",
            "[1,2,3\r\n[INSERT]\r\n]",
            "(x [INSERT]\r\n[42])",
            "x[[INSERT]\r\n42]",
            "x[42\r\n[INSERT]\r\n]",
            "(a +[INSERT]\r\nb)",
            "(a[INSERT]\r\n+b)",
        };

        static readonly string[] _insertionSnippets = new[] {
            "if True:\r\n    pass\r\n[INSERT]else:\r\n    pass",
            "if True:\r\n    pass\r\n[INSERT]elif True:\r\n    pass",
            "try:\r\n    pass\r\n[INSERT]finally:\r\n    pass",
            "try:\r\n    pass\r\n[INSERT]except:\r\n    pass",
            "try:\r\n    pass\r\n[INSERT]except Exception:\r\n    pass",
            "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\n[INSERT]else:\r\n    pass",
            "while True:\r\n    pass\r\n[INSERT]else:\r\n    pass",
            "for x in [1,2,3]:\r\n    pass\r\n[INSERT]else:\r\n    pass",
            /*@"(1, [INSERT]
               2,
               3)"*/
        };

        static readonly string[] _snippets2x = new[] { 
                // expressions
                "a",
                "a()",
                "a[42]",
                "a + b",
                "+a",
                "-a",
                "a and b",
                "a or b",
                "`foo`",
                "42",
                "'abc'",
                "42 if True else False",
                "{}",
                "[]",
                "[x for x in abc]",
                "(x for x in abc)",
                "lambda x: 2",
                "a.b",
                "(a)",
                "()",
                "(1, 2, 3)",
                "1, 2, 3",
                "yield 42",

                // statements
                "assert True",
                "x = 42",
                "x += 42",
                "break",
                "continue",
                "def f(): pass",
                "class C: pass",
                "del x",
                "pass",
                "exec 'hello'",
                "for i in xrange(42): pass",
                "import foo",
                "from foo import bar",
                "global x",
                "if True: pass",
                "print abc",
                "raise Exception()",
                "return abc",
                "try:\r\n    pass\r\nexcept:\r\n    pass",
                "while True:\r\n    pass",
                "with abc: pass",
                "@property\r\ndef f(): pass",
            };

        static readonly string[] _snippets3x = new[] { "nonlocal foo" };

        /// <summary>
        /// Verifies that the proceeding white space is consistent across all nodes.
        /// </summary>
        [TestMethod, Priority(0)]
        public void TestStartWhiteSpace() {
            foreach (var preceedingText in new[] { "#foo\r\n" }) {
                var allSnippets = 
                    _snippets2x.Select(text => new { Text = text, Version = PythonLanguageVersion.V27 }).Concat(
                    _snippets3x.Select(text => new { Text = text, Version = PythonLanguageVersion.V33 }));
                
                foreach (var testCase in allSnippets) {
                    var exprText = testCase.Text;
                    string code = preceedingText + exprText;
                    Console.WriteLine(code);

                    var parser = Parser.CreateParser(new StringReader(code), testCase.Version, new ParserOptions() { Verbatim = true });
                    var ast = parser.ParseFile();
                    Statement stmt = ((SuiteStatement)ast.Body).Statements[0];
                    if (stmt is ExpressionStatement) {
                        var expr = ((ExpressionStatement)stmt).Expression;

                        Assert.AreEqual(preceedingText.Length, expr.StartIndex);
                        Assert.AreEqual(preceedingText.Length + exprText.Length, expr.EndIndex);
                        Assert.AreEqual(preceedingText, expr.GetLeadingWhiteSpace(ast));
                    } else {
                        Assert.AreEqual(preceedingText.Length, stmt.StartIndex);
                        Assert.AreEqual(preceedingText.Length + exprText.Length, stmt.EndIndex);
                        Assert.AreEqual(preceedingText, stmt.GetLeadingWhiteSpace(ast));
                    }
                }
            }
        }

        [TestMethod, Priority(0)]
        public void TestExpressions() {            
            // TODO: Trailing white space tests
            // Unary Expressions
            TestOneString(PythonLanguageVersion.V27, "x=~42");
            TestOneString(PythonLanguageVersion.V27, "x=-42");
            TestOneString(PythonLanguageVersion.V27, "x=+42");
            TestOneString(PythonLanguageVersion.V27, "x=not 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   ~    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   -    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   +    42");
            TestOneString(PythonLanguageVersion.V27, "x  =   not    42");

            // Constant Expressions
            TestOneString(PythonLanguageVersion.V27, "\r\n42");
            TestOneString(PythonLanguageVersion.V27, "42");
            TestOneString(PythonLanguageVersion.V27, "'abc'");
            TestOneString(PythonLanguageVersion.V27, "\"abc\"");
            TestOneString(PythonLanguageVersion.V27, "'''abc'''");
            TestOneString(PythonLanguageVersion.V27, "\"\"\"abc\"\"\"");
            TestOneString(PythonLanguageVersion.V27, "x = - 1");
            TestOneString(PythonLanguageVersion.V27, "x = -1");
            TestOneString(PythonLanguageVersion.V27, "x = - 2147483648");
            TestOneString(PythonLanguageVersion.V27, "x = -2147483648");

            // Conditional Expressions
            TestOneString(PythonLanguageVersion.V27, "1 if True else 2");
            TestOneString(PythonLanguageVersion.V27, "1  if   True    else     2");

            // Generator expressions
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, " (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc)");
            TestOneString(PythonLanguageVersion.V27, "f(x for x in abc if abc >= 42)");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       )");
            TestOneString(PythonLanguageVersion.V27, "f (  x   for    x     in      abc       if        abc        >=          42          )");
            TestOneString(PythonLanguageVersion.V27, "x(a for a,b in x)");
            TestOneString(PythonLanguageVersion.V27, "x  (   a    for     a      ,       b        in        x          )");

            // Lambda Expressions
            TestOneString(PythonLanguageVersion.V27, "lambda x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda x, y: x, y");
            TestOneString(PythonLanguageVersion.V27, "lambda x = 42: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda **x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda *x, **y: x");
            TestOneString(PythonLanguageVersion.V27, "lambda : 42");
            TestOneString(PythonLanguageVersion.V30, "lambda *, x: x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   :    x");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   ,    y     :      x       ,        y");
            TestOneString(PythonLanguageVersion.V27, "lambda  x   =    42     :      x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  **   x    :     x");
            TestOneString(PythonLanguageVersion.V27, "lambda  *   x    ,     **      y       :        x");
            TestOneString(PythonLanguageVersion.V27, "lambda  :   42");
            TestOneString(PythonLanguageVersion.V30, "lambda  *   ,    x     :      x");

            // List Comprehensions
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc, baz]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in (abc, baz)]");
            TestOneString(PythonLanguageVersion.V27, "[x for x in abc if abc >= 42]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       ]");
            TestOneString(PythonLanguageVersion.V27, " [  x   for    x     in      abc       if        abc        >=          42          ]");
            TestOneString(PythonLanguageVersion.V27, "[v for k,v in x]");
            TestOneString(PythonLanguageVersion.V27, "  [v   for    k     ,      v       in        x         ]");
            TestOneString(PythonLanguageVersion.V27, "[v for (k,v) in x]");
            TestOneString(PythonLanguageVersion.V27, "  [   v    for     (      k       ,        v          )          in           x             ]");

            // Set comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x   for    x     in      abc       if        abc        >=          42          }");

            // Dict Comprehensions
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc}");
            TestOneString(PythonLanguageVersion.V27, "{x:x for x in abc if abc >= 42}");
            TestOneString(PythonLanguageVersion.V27, " {  x        :         x   for    x     in      abc       }");
            TestOneString(PythonLanguageVersion.V27, " {  x           :            x   for    x     in      abc       if        abc        >=          42          }");

            // Backquote Expression
            TestOneString(PythonLanguageVersion.V27, "`42`");
            TestOneString(PythonLanguageVersion.V27, " `42`");
            TestOneString(PythonLanguageVersion.V27, " `42  `");

            // Call Expression
            TestOneString(PythonLanguageVersion.V27, "x(abc)");
            TestOneString(PythonLanguageVersion.V27, "x(abc = 42)");
            TestOneString(PythonLanguageVersion.V27, "x(*abc)");
            TestOneString(PythonLanguageVersion.V27, "x(**abc)");
            TestOneString(PythonLanguageVersion.V27, "x(*foo, **bar)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c)");
            TestOneString(PythonLanguageVersion.V27, "x(a, b, c, d = 42)");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   )");
            TestOneString(PythonLanguageVersion.V27, "x (  abc   =    42     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   abc    )");
            TestOneString(PythonLanguageVersion.V27, "x (  **   abc     )");
            TestOneString(PythonLanguageVersion.V27, "x (  *   foo    ,     **      bar       )");
            TestOneString(PythonLanguageVersion.V27, "x (  a,   b,    c     )");
            TestOneString(PythonLanguageVersion.V27, "x (  a   ,    b     ,      c       ,        d         =           42           )");
            TestOneString(PythonLanguageVersion.V27, "x(abc,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    ,     )");
            TestOneString(PythonLanguageVersion.V27, "x(abc=42,)");
            TestOneString(PythonLanguageVersion.V27, "x  (   abc    =     42      ,       )");

            // Member Expression
            TestOneString(PythonLanguageVersion.V27, "foo.bar");
            TestOneString(PythonLanguageVersion.V27, "foo .bar");
            TestOneString(PythonLanguageVersion.V27, "foo. bar");
            TestOneString(PythonLanguageVersion.V27, "foo .  bar");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    x = foo.__bar");

            // Parenthesis expression
            TestOneString(PythonLanguageVersion.V27, "(42)");
            TestOneString(PythonLanguageVersion.V27, "( 42  )");
            TestOneString(PythonLanguageVersion.V27, " (  42   )");

            // Starred expression
            TestOneString(PythonLanguageVersion.V30, "*a, b = c, d");
            TestOneString(PythonLanguageVersion.V30, "*a, b, c = d, e, f");
            TestOneString(PythonLanguageVersion.V30, "*               a ,  b   ,    c     =      d       ,        e         ,          f");
            TestOneString(PythonLanguageVersion.V30, "(            *               a ,  b   ,    c     )             =      (              d       ,        e         ,          f              )");
            TestOneString(PythonLanguageVersion.V30, "[            *               a ,  b   ,    c     ]             =      [              d       ,        e         ,          f              ]");
            
            // Index expression
            TestOneString(PythonLanguageVersion.V27, "x[42]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42]");
            
            TestOneString(PythonLanguageVersion.V27, "x [42]");
            TestOneString(PythonLanguageVersion.V27, "x [42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [42,23]");
            TestOneString(PythonLanguageVersion.V27, "x[ 42 ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   ,    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:23:100]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    23     :      100       ]");
            TestOneString(PythonLanguageVersion.V27, "x[42:]");
            TestOneString(PythonLanguageVersion.V27, "x[42::]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    ]");
            TestOneString(PythonLanguageVersion.V27, "x [  42   :    :     ]");
            TestOneString(PythonLanguageVersion.V27, "x[::]");
            TestOneString(PythonLanguageVersion.V27, "x  [   :    :     ]");

            // or expression
            TestOneString(PythonLanguageVersion.V27, "1 or 2");
            TestOneString(PythonLanguageVersion.V27, "1  or   2");

            // and expression
            TestOneString(PythonLanguageVersion.V27, "1 and 2");
            TestOneString(PythonLanguageVersion.V27, "1  and   2");

            // binary expression
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**", "<", ">", "<=", ">=", "==", "!=", "<>" }) {
                TestOneString(PythonLanguageVersion.V27, "1 " + op + "2");
                TestOneString(PythonLanguageVersion.V27, "1"+ op + "2");
                TestOneString(PythonLanguageVersion.V27, "1" + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            foreach (var op in new[] { "is", "is not", "in", "not in" }) {
                // TODO: All of these should pass in the binary expression case once we have error handling working
                TestOneString(PythonLanguageVersion.V27, "1 " + op + " 2");
                TestOneString(PythonLanguageVersion.V27, "1  " + op + "   2");
            }

            // yield expression
            TestOneString(PythonLanguageVersion.V27, "yield 1");
            TestOneString(PythonLanguageVersion.V27, "yield 1, 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2");
            TestOneString(PythonLanguageVersion.V27, "yield 1  , 2,");
            TestOneString(PythonLanguageVersion.V27, "yield 1 ,  2   ,");
            TestOneString(PythonLanguageVersion.V27, "yield");
            TestOneString(PythonLanguageVersion.V27, "yield None");
            TestOneString(PythonLanguageVersion.V27, "yield 1 == 2");
            TestOneString(PythonLanguageVersion.V27, "yield lambda: 42");
            TestOneString(PythonLanguageVersion.V27, "yield 42, ");

            // yield from expression
            TestOneString(PythonLanguageVersion.V33, "yield from foo");
            TestOneString(PythonLanguageVersion.V33, "yield from  foo");
            TestOneString(PythonLanguageVersion.V33, "yield  from foo");
            TestOneString(PythonLanguageVersion.V33, "yield  from  foo");
            TestOneString(PythonLanguageVersion.V33, "x  =  yield  from  foo");

            // tuples
            TestOneString(PythonLanguageVersion.V27, "(1, 2, 3)");
            TestOneString(PythonLanguageVersion.V27, "(1, 2,  3)");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      )");
            TestOneString(PythonLanguageVersion.V27, "( 1  ,   2    ,     3      ,       )");
            
            // list expressions
            TestOneString(PythonLanguageVersion.V27, "[1, 2, 3]");
            TestOneString(PythonLanguageVersion.V27, "[1, 2,  3]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ]");
            TestOneString(PythonLanguageVersion.V27, "[ 1  ,   2    ,     3      ,       ]");
            TestOneString(PythonLanguageVersion.V27, "[abc, foo and bar]");
            TestOneString(PythonLanguageVersion.V27, "[foo if True else bar]");

            // set expressions
            TestOneString(PythonLanguageVersion.V27, "{1, 2, 3}");
            TestOneString(PythonLanguageVersion.V27, "{1, 2,  3}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  ,   2    ,     3      ,       }");

            // dict expressions
            TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4}");
            TestOneString(PythonLanguageVersion.V27, "{1 :2, 2  :3,  3:  4}");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           }");
            TestOneString(PythonLanguageVersion.V27, "{ 1  :   2    ,     2      :       3,        3         :          4           ,            }");

            // Error cases:
            //TestOneString(PythonLanguageVersion.V27, "{1:2, 2 :3, 3: 4]");
        }

        [TestMethod, Priority(0)]
        public void TestMangledPrivateName() {
            TestOneString(PythonLanguageVersion.V27, @"class C:
    def f(__a):
        pass
"); 
            TestOneString(PythonLanguageVersion.V27, @"class C:
    class __D:
        pass
");


            TestOneString(PythonLanguageVersion.V27, @"class C:
    import __abc
    import __foo, __bar
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    from sys import __abc
    from sys import __foo, __bar
    from __sys import __abc
");

            TestOneString(PythonLanguageVersion.V27, @"class C:
    global __X
");

            TestOneString(PythonLanguageVersion.V30, @"class C:
    nonlocal __X
");
        }

        [TestMethod, Priority(0)]
        public void TestComments() {

            TestOneString(PythonLanguageVersion.V27, @"x = foo(
        r'abc'                                # comments
        r'def'                                # are spanning across
                                              # a string plus
                                              # which might make life
                                              # difficult if we don't
        r'ghi'                                # handle it properly
        )");

            TestOneString(PythonLanguageVersion.V27, "#foo\r\npass");
            TestOneString(PythonLanguageVersion.V27, "#foo\r\n\r\npass"); 
            TestOneString(PythonLanguageVersion.V27, "#foo");

        }

        [TestMethod, Priority(0)]
        public void TestWhiteSpaceAfterDocString() {
            TestOneString(PythonLanguageVersion.V27, @"'''hello

this is some documentation
'''

import foo");
        }

        [TestMethod, Priority(0)]
        public void TestBinaryFiles() {
            var filename = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");
            TestOneString(PythonLanguageVersion.V27, filename);
        }

        [TestMethod, Priority(0)]
        public void TestErrors() {
            TestOneString(PythonLanguageVersion.V30, ":   ...");

            // Index Expression
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], .");
            TestOneString(PythonLanguageVersion.V27, "x[[val, val, ...], [val, val, ...], ..");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, "while X !=2 :\r\n");

            // Lambda Expression

            TestOneString(PythonLanguageVersion.V27, "lambda");
            TestOneString(PythonLanguageVersion.V27, "lambda :");
            TestOneString(PythonLanguageVersion.V27, "lambda pass");
            TestOneString(PythonLanguageVersion.V27, "lambda : pass"); 
            TestOneString(PythonLanguageVersion.V27, "lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda a, b, quote");
            TestOneString(PythonLanguageVersion.V27, "lambda, X+Y Z");
            TestOneString(PythonLanguageVersion.V30, "[x for x in abc if lambda, X+Y Z");

            // print statement
            TestOneString(PythonLanguageVersion.V27, "print >>sys.stderr, \\\r\n");
            TestOneString(PythonLanguageVersion.V27, "print pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, ");
            TestOneString(PythonLanguageVersion.V27, "print >>pass, pass");
            TestOneString(PythonLanguageVersion.V27, "print >>pass pass");

            // Import statement
            TestOneString(PythonLanguageVersion.V27, "import X as");

            // From Import statement
            TestOneString(PythonLanguageVersion.V27, "from _struct import");
            TestOneString(PythonLanguageVersion.V27, "from _io import (DEFAULT_BUFFER_SIZE");
            TestOneString(PythonLanguageVersion.V27, "from x import y as");
            TestOneString(PythonLanguageVersion.V27, "from ... import ...");

            // Parenthesis Expression
            TestOneString(PythonLanguageVersion.V27, "(\r\n(x");
            TestOneString(PythonLanguageVersion.V27, "(\r\n(");            

            TestOneString(PythonLanguageVersion.V27, "m .b'");
            TestOneString(PythonLanguageVersion.V27, "m . b'");
            TestOneString(PythonLanguageVersion.V27, "x y import");
            TestOneString(PythonLanguageVersion.V27, "x y global");

            TestOneString(PythonLanguageVersion.V27, "x[..., ]");

            TestOneString(PythonLanguageVersion.V27, "(a for x y");
            TestOneString(PythonLanguageVersion.V27, "x(a for x y");
            TestOneString(PythonLanguageVersion.V27, "[a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a for x y");
            TestOneString(PythonLanguageVersion.V27, "{a:v for x y");

            TestOneString(PythonLanguageVersion.V27, ":   ");
            TestOneString(PythonLanguageVersion.V27, "from the");
            TestOneString(PythonLanguageVersion.V27, "when not None");
            TestOneString(PythonLanguageVersion.V27, "for x and y");

            // conditional expression
            TestOneString(PythonLanguageVersion.V27, "e if x y z");
            TestOneString(PythonLanguageVersion.V27, "e if x y");
            TestOneString(PythonLanguageVersion.V27, "e if x");
            TestOneString(PythonLanguageVersion.V27, "e if x pass");

            TestOneString(PythonLanguageVersion.V27, ", 'hello'\r\n        self");
            TestOneString(PythonLanguageVersion.V27, "http://xkcd.com/353/\")");
            TestOneString(PythonLanguageVersion.V27, "�g�\r��\r���\r��\r���\r���\r��\rt4�\r*V�\roA�\r\t�\r�$�\r\t.�\r�t�\r�q�\r�H�\r�|");
            TestOneString(PythonLanguageVersion.V27, "\r\t.�\r�t�\r�q�\r");
            TestOneString(PythonLanguageVersion.V27, "\r\t�\r�$�\r\t.�\r");
            TestOneString(PythonLanguageVersion.V27, "�\r�$�\r\t.�\r�t");
            TestOneString(PythonLanguageVersion.V27, "\r\n.\r\n");
            
            TestOneString(PythonLanguageVersion.V27, "abc\r\n.\r\n");

            // Dictionary Expressions
            TestOneString(PythonLanguageVersion.V27, "{");
            TestOneString(PythonLanguageVersion.V27, @"X = { 42 : 100,
");
            TestOneString(PythonLanguageVersion.V27, @"s.
    X = { 23   : 42,
");
            TestOneString(PythonLanguageVersion.V27, "{x:y");
            TestOneString(PythonLanguageVersion.V27, "{x:y, z:x");
            TestOneString(PythonLanguageVersion.V27, "{x");
            TestOneString(PythonLanguageVersion.V27, "{x, y");
            TestOneString(PythonLanguageVersion.V27, "{x:y for x in abc");
            TestOneString(PythonLanguageVersion.V27, "{x for x in abc");
            TestOneString(PythonLanguageVersion.V27, @")
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"]
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"}
    X = { 42 : 100,
          100 : 200,
");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100 ");
            TestOneString(PythonLanguageVersion.V27, @"{ 42: 100, 100, 200:30 } ");
            TestOneString(PythonLanguageVersion.V27, @"{ 100, 100:30, 200 } ");


            // generator comprehensions and calls
            TestOneString(PythonLanguageVersion.V27, "x(");
            TestOneString(PythonLanguageVersion.V27, "x(for x in abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc");
            TestOneString(PythonLanguageVersion.V27, "x(abc, ");
            TestOneString(PythonLanguageVersion.V27, "x(pass");

            // lists and list comprehensions
            TestOneString(PythonLanguageVersion.V27, "[");
            TestOneString(PythonLanguageVersion.V27, "[abc");
            TestOneString(PythonLanguageVersion.V27, "[abc,");
            TestOneString(PythonLanguageVersion.V27, "[for x in abc");
            TestOneString(PythonLanguageVersion.V27, "[b for b in");

            TestOneString(PythonLanguageVersion.V27, "x[");
            TestOneString(PythonLanguageVersion.V27, "x[abc");
            TestOneString(PythonLanguageVersion.V27, "x[abc,");
            TestOneString(PythonLanguageVersion.V27, "x[abc:");

            // backquote expression
            TestOneString(PythonLanguageVersion.V27, "`foo");

            // constant expressions
            TestOneString(PythonLanguageVersion.V27, "'\r");
            TestOneString(PythonLanguageVersion.V27, @"'abc' 24 : q");
            TestOneString(PythonLanguageVersion.V27, @"u'abc' 24 : q");

            // bad tokens
            TestOneString(PythonLanguageVersion.V27, "!x");
            TestOneString(PythonLanguageVersion.V27, "$aü");
            TestOneString(PythonLanguageVersion.V27, "0399");
            TestOneString(PythonLanguageVersion.V27, "0o399");
            TestOneString(PythonLanguageVersion.V27, "0399L");
            TestOneString(PythonLanguageVersion.V27, "0399j");
            
            // calls
            TestOneString(PythonLanguageVersion.V27, "x(42 = 42)");

            // for statement
            TestOneString(PythonLanguageVersion.V27, "for pass\r\nin abc: pass");
            TestOneString(PythonLanguageVersion.V27, "for pass in abc: pass");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\nabc");
            TestOneString(PythonLanguageVersion.V27, "for pass in");

            // class defs
            TestOneString(PythonLanguageVersion.V30, "class(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V30, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V30, "class X(=");
            TestOneString(PythonLanguageVersion.V30, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, "class(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, int: pass");
            TestOneString(PythonLanguageVersion.V27, "class X(object, pass");
            TestOneString(PythonLanguageVersion.V27, "class X(=");
            TestOneString(PythonLanguageVersion.V27, "class X(pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    x = foo.42");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.42\r\n    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.[]\r\n    def f(self): pass");
            TestOneString(PythonLanguageVersion.V27, "class 42");
            TestOneString(PythonLanguageVersion.V30, "class");
            TestOneString(PythonLanguageVersion.V27, "@foo\r\nclass 42");

            // func defs
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, *x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, **x");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, x = 2");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *, (a, b)");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *,");
            TestOneString(PythonLanguageVersion.V30, "def f(A, *)");

            TestOneString(PythonLanguageVersion.V27, "def f(x, *, ): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 + 2: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42: pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42, )): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((42 pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, 42)): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(42 = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(pass = pass): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(= = =): pass");
            TestOneString(PythonLanguageVersion.V27, "def f");
            TestOneString(PythonLanguageVersion.V27, "def");
            TestOneString(PythonLanguageVersion.V27, " @@");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, **");
            TestOneString(PythonLanguageVersion.V27, "def X(abc, *");
            TestOneString(PythonLanguageVersion.V27, @"@foo(
def f(): pass");


            // misc malformed expressions
            TestOneString(PythonLanguageVersion.V27, "1 + :");
            TestOneString(PythonLanguageVersion.V27, "abc.2");
            TestOneString(PythonLanguageVersion.V27, "abc 1L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j");
            TestOneString(PythonLanguageVersion.V27, "abc.2.3");
            TestOneString(PythonLanguageVersion.V27, "abc 1L 2L");
            TestOneString(PythonLanguageVersion.V27, "abc 0j 1j");

            // global / nonlocal statements
            TestOneString(PythonLanguageVersion.V27, "global abc, baz,"); // trailing comma not allowed
            TestOneString(PythonLanguageVersion.V27, "nonlocal abc");           // nonlocal not supported before 3.0
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz,"); // trailing comma not allowed

            // assert statements
            TestOneString(PythonLanguageVersion.V27, "assert");

            // while statements
            TestOneString(PythonLanguageVersion.V27, "while True:\r\n    break\r\nelse:\r\npass");

            // if statements
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelif False:\r\n    pass\r\n    else:\r\n    pass");

            // try/except
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   pass\r\finally    :     pass");
        }

        [TestMethod, Priority(0)]
        public void TestExplicitLineJoin() {
            TestOneString(PythonLanguageVersion.V27, @"foo(4 + \
                    5)");
        }

        [TestMethod, Priority(0)]
        public void TestStatements() {
            // TODO: Vary all of these tests by putting the test case in a function def
            // TODO: Vary all of these tests by adding trailing comments                        
            TestOneString(PythonLanguageVersion.V27, "def _process_result(self, (i");

            // Empty Statement
            TestOneString(PythonLanguageVersion.V27, "pass");
            
            // Break Statement
            TestOneString(PythonLanguageVersion.V27, "break");
            
            // Continue Statement
            TestOneString(PythonLanguageVersion.V27, "continue");

            // Non local statement
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc");
            TestOneString(PythonLanguageVersion.V30, "nonlocal abc, baz");
            TestOneString(PythonLanguageVersion.V30, "nonlocal  abc   ,    baz");

            // Global Statement
            TestOneString(PythonLanguageVersion.V27, "global abc");
            TestOneString(PythonLanguageVersion.V27, "global abc, baz");
            TestOneString(PythonLanguageVersion.V27, "global  abc   ,    baz");

            // Return Statement
            TestOneString(PythonLanguageVersion.V27, "return");
            TestOneString(PythonLanguageVersion.V27, "return 42");
            TestOneString(PythonLanguageVersion.V27, "return 42,");
            TestOneString(PythonLanguageVersion.V27, "return 42,43");
            TestOneString(PythonLanguageVersion.V27, "return  42   ,    43");

            // Del Statement
            TestOneString(PythonLanguageVersion.V27, "del");
            TestOneString(PythonLanguageVersion.V27, "del abc");
            TestOneString(PythonLanguageVersion.V27, "del abc,");
            TestOneString(PythonLanguageVersion.V27, "del abc,baz");
            TestOneString(PythonLanguageVersion.V27, "del  abc   ,    baz     ,");

            // Raise Statement
            TestOneString(PythonLanguageVersion.V27, "raise");
            TestOneString(PythonLanguageVersion.V27, "raise foo");
            TestOneString(PythonLanguageVersion.V27, "raise foo, bar");
            TestOneString(PythonLanguageVersion.V27, "raise foo, bar, baz");
            TestOneString(PythonLanguageVersion.V30, "raise foo from bar");
            TestOneString(PythonLanguageVersion.V27, "raise  foo");
            TestOneString(PythonLanguageVersion.V27, "raise  foo   ,    bar");
            TestOneString(PythonLanguageVersion.V27, "raise  foo   ,    bar     ,      baz");
            TestOneString(PythonLanguageVersion.V30, "raise  foo   from    bar");

            // Assert Statement
            TestOneString(PythonLanguageVersion.V27, "assert foo");
            TestOneString(PythonLanguageVersion.V27, "assert foo, bar");
            TestOneString(PythonLanguageVersion.V27, "assert  foo");
            TestOneString(PythonLanguageVersion.V27, "assert  foo   ,    bar");

            // Import Statement
            TestOneString(PythonLanguageVersion.V27, "import sys");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo, itertools");
            TestOneString(PythonLanguageVersion.V27, "import sys as foo, itertools as i");
            TestOneString(PythonLanguageVersion.V27, "import  sys");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo     ,       itertools");
            TestOneString(PythonLanguageVersion.V27, "import  sys   as    foo     ,       itertools       as        i");
            TestOneString(PythonLanguageVersion.V27, "import X, Y, Z, A as B");

            // From Import Statement
            TestOneString(PythonLanguageVersion.V27, "from sys import *");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver as wv");
            TestOneString(PythonLanguageVersion.V27, "from sys import winver as wv, stdin as si");
            TestOneString(PythonLanguageVersion.V27, "from sys import (winver)");
            TestOneString(PythonLanguageVersion.V27, "from sys import (winver,)");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    *");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver     as      wv");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    winver     as      wv       ,        stdin         as           si");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver      )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       as       wv        )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       as       wv        ,         stdin          as          si           )");
            TestOneString(PythonLanguageVersion.V27, "from  sys   import    (     winver       ,        )");
            TestOneString(PythonLanguageVersion.V27, "from xyz import A, B, C, D, E");


            // Assignment statement
            TestOneString(PythonLanguageVersion.V27, "x = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   42");
            TestOneString(PythonLanguageVersion.V27, "x = abc = 42");
            TestOneString(PythonLanguageVersion.V27, "x  =   abc    =     42");
            TestOneString(PythonLanguageVersion.V30, "def f():\r\n     a = True");

            // Augmented Assignment Statement
            foreach (var op in new[] { "+", "-", "*", "/", "//", "%", "&", "|", "^", "<<", ">>", "**"}) {
                TestOneString(PythonLanguageVersion.V27, "x " + op + "= 42");
                TestOneString(PythonLanguageVersion.V27, "x  " + op + "   42");
            }

            // Exec Statement
            TestOneString(PythonLanguageVersion.V27, "exec 'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l");
            TestOneString(PythonLanguageVersion.V27, "exec 'abc' in l, g");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l");
            TestOneString(PythonLanguageVersion.V27, "exec  'abc'   in    l     ,      g");

            // Print Statement
            TestOneString(PythonLanguageVersion.V27, "print foo");
            TestOneString(PythonLanguageVersion.V27, "print foo, bar");
            TestOneString(PythonLanguageVersion.V27, "print foo,");
            TestOneString(PythonLanguageVersion.V27, "print foo, bar,"); 
            TestOneString(PythonLanguageVersion.V27, "print >> dest");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo, bar");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo,");
            TestOneString(PythonLanguageVersion.V27, "print >> dest, foo, bar,");
            TestOneString(PythonLanguageVersion.V27, "print  foo");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,    bar");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,");
            TestOneString(PythonLanguageVersion.V27, "print  foo   ,    bar     ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,       bar");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,");
            TestOneString(PythonLanguageVersion.V27, "print  >>   dest    ,     foo      ,       bar        ,");
            TestOneString(PythonLanguageVersion.V27, "print l1==l");


            // For Statement
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n    pass\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n\r\n    pass\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for i in xrange(10):\r\n    break\r\nelse:\r\n    pass");
            
            TestOneString(PythonLanguageVersion.V27, "for (i), (j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (i, j) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for i,j in xrange(10): pass");
            TestOneString(PythonLanguageVersion.V27, "for ((i, j)) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for (((i), (j))) in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [i, j] in x.items(): print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for [[i], [j]] in x.items(): print(i, j)");

            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :      pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n    pass \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n\r\n    pass \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  i   in    xrange(10)    :\r\n    break\r\nelse     :      \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "for  (i), (j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (i, j)   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  i,j    in    xrange(10)     :      pass");
            TestOneString(PythonLanguageVersion.V27, "for  ((i, j))   in    x.items()     :       print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  (((i), (j)))   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [i, j]   in    x.items()     :      print(i, j)");
            TestOneString(PythonLanguageVersion.V27, "for  [[i], [j]]   in    x.items()     :      print(i, j)");
            
            // While Statement
            TestOneString(PythonLanguageVersion.V27, "while True: break");
            TestOneString(PythonLanguageVersion.V27, "while True: break\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "while True:\r\n    break\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "while  True   :    break");
            TestOneString(PythonLanguageVersion.V27, "while  True   :    break\r\nelse     : pass");
            TestOneString(PythonLanguageVersion.V27, "while  True:\r\n    break   \r\nelse    :     \r\n    pass");

            // If Statement
            TestOneString(PythonLanguageVersion.V27, "if True: pass");
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if True:\r\n    pass\r\nelif False:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :\r\n    pass\r\nelse    :     \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "if  True   :\r\n    pass\r\nelif     False     :\r\n    pass      \r\nelse       :        \r\n    pass");

            // Suite Statement
            TestOneString(PythonLanguageVersion.V27, "abc;foo;bar");
            TestOneString(PythonLanguageVersion.V27, "abc  ;   foo    ;     bar");
            TestOneString(PythonLanguageVersion.V27, "abc;foo\r\n\r\nbar;baz");
            TestOneString(PythonLanguageVersion.V27, "abc  ;   foo    \r\n\r\nbar     ;      baz");
            TestOneString(PythonLanguageVersion.V27, "foo;");
            TestOneString(PythonLanguageVersion.V27, "def f():\r\n    if True:\r\n        foo;\r\n     bar");
            TestOneString(PythonLanguageVersion.V27, @"def f(x):
    length = x
    if length == 0:
        pass
");
            TestOneString(PythonLanguageVersion.V27, @"def f():
    try:
        return 42
    except Exception:
        pass");

            // With Statement
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, "with abc as bar: pass");
            TestOneString(PythonLanguageVersion.V27, "with foo, bar: pass");
            TestOneString(PythonLanguageVersion.V27, "with foo as f, bar as b: pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   : pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   as    bar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   ,    bar     :      pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   as    f     ,       bar       as       b        :          pass");
            TestOneString(PythonLanguageVersion.V27, "with abc: pass");
            TestOneString(PythonLanguageVersion.V27, "with abc as bar:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with foo, bar:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with foo as f, bar as b:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  abc   as    bar     :  \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   ,    bar     :  \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "with  foo   as    f     ,       bar       as       b        :  \r\n    pass");
            
            // Try Statement
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError, e: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept TypeError as e: pass\r\nexcept Exception: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nelse: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception, e: pass\r\nelse: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nexcept Exception as e: pass\r\nelse: pass\r\nfinally: pass");

            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError, e:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept TypeError as e:\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nelse:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept: pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept: pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception:\r\n    pass\r\nelse: pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception, e:\r\n    pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nexcept Exception as e:\r\n    pass\r\nelse:\r\n    pass\r\nfinally:\r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :     pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,     e      :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as    e      :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    :     pass      \r\nexcept        Exception        :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    ,     e        :          pass\r\nexcept           Exception            :            pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   TypeError    as    e        :          pass\r\nexcept           Exception             :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass     \r\nelse      :         pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :      pass     \r\nelse       :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,     e      :       pass        \r\nelse         :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as     e      :       pass        \r\nelse         :              pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass     \r\nfinally      :       pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :      pass       \r\nfinally       :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,      e       :       pass\r\nfinally         :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as      e       :       pass\r\nfinally         :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   :    pass\r\nelse     :      pass       \r\nfinally        :        pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    :     pass      \r\nelse       :        pass         \r\nfinally          :           pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    ,    e:     pass      \r\nelse       :       pass         \r\nfinally          :          pass");
            TestOneString(PythonLanguageVersion.V27, "try:  pass\r\nexcept   Exception    as    e:     pass      \r\nelse       :       pass         \r\nfinally          :          pass");

            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,        e         :          \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as        e          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       :        \r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       ,        e         :\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       TypeError       as        e         :\r\n    pass\r\nexcept Exception:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :\r\n    pass    \r\nelse        :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :        \r\n    pass\r\nelse        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,         e         :\r\n    pass\r\nelse          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as       e        :\r\n    pass\r\nelse          :          \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      : pass      \r\nfinally       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception      :       \r\n    pass\r\nfinally          :\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,        e          :\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as        e         :\r\n    pass\r\nfinally           :            \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept      :        pass        \r\nelse          :          \r\n    pass\r\nfinally           :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       :\r\n    pass\r\nelse        :         pass\r\nfinally          :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       ,       e         :\r\n    pass\r\nelse           :             \r\n    pass\r\nfinally             :               \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass     \r\nexcept       Exception       as       e        :\r\n    pass\r\nelse           :\r\n    pass\r\nfinally          :              \r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "try: pass\r\nfinally: pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   pass\r\nfinally    :     pass");
            TestOneString(PythonLanguageVersion.V27, "try:\r\n    pass\r\nfinally:\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "try  :   \r\n    pass\r\nfinally    :     \r\n    pass");

            // Class Definition
            TestOneString(PythonLanguageVersion.V27, "class C: pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar, ): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo, ): pass"); 
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar): pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar, baz = 42): pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       )        :        pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       **        bar         )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,      **        bar         ,          )           :            pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      )       :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      ,       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       )        :         pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       ,        baz         =          42           )           :             pass");

            TestOneString(PythonLanguageVersion.V27, "class C: \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class C(object, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(object, metaclass=42): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(*foo, **bar, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(**foo, ): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar): \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class C(foo = bar, baz = 42): \r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "class  C   :    \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    )     :      \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object): \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  C   (    object      ,       )      : \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    object      ,       metaclass        =         42          )           : \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      )       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       )        :        \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,       **        bar         )          :           \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    *     foo      ,      **        bar         ,          )           :            \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      )       :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    **     foo      ,       )        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       )        :         \r\n    pass");
            TestOneString(PythonLanguageVersion.V30, "class  C   (    foo     =      bar       ,        baz         =          42           )           :             \r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class Foo(int if y else object):\r\n    pass");
            TestOneString(PythonLanguageVersion.V27, "class  Foo   (    int     if      y      else       object         )         :\r\n    pass");

            TestOneString(PythonLanguageVersion.V27, "@foo\r\nclass C: pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   \r\nclass    C     :       pass");

            // Function Definition
            TestOneString(PythonLanguageVersion.V27, "def f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a = 42): pass");
            TestOneString(PythonLanguageVersion.V27, "def f(a, b): pass");
            TestOneString(PythonLanguageVersion.V30, "def f(a, b) -> foo: pass");
            TestOneString(PythonLanguageVersion.V27, "def f(*a, **b): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    )     :       pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     )      :        pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     =       42        )          :           pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    a     ,       b          )          :           pass");
            TestOneString(PythonLanguageVersion.V30, "def  f   (    a     ,       b        )         ->          foo           :            pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    *     a      ,        **        b         )          :           pass");
            TestOneString(PythonLanguageVersion.V27, "@foo\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo.bar\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo(2)\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@foo.bar(2)\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   \r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   .    bar\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   (    2     )\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "@  foo   .    bar     (      2       )\r\ndef f(): pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         )      )       :        pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, b)): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a         ,      b)       )         :           pass");
            TestOneString(PythonLanguageVersion.V27, "def f((a, (b, c))): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f   (    (     a      ,       (         b          ,          c            )             )              )              :                pass");

            TestOneString(PythonLanguageVersion.V27, "@foo\r\n\r\ndef f(): pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @foo.__bar\r\n    def f(self): pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    def __f(self): pass");

            TestOneString(PythonLanguageVersion.V27, "def f(a,): pass");
            TestOneString(PythonLanguageVersion.V27, "def  f(   a    ,     )      :       pass");

            TestOneString(PythonLanguageVersion.V27, "class C:\r\n    @property\r\n    def foo(self): return 42");

        }

        [TestMethod, Priority(0)]
        public void StdLibTest() {
            var versions = new[] { 
                new { Path = "C:\\Python25\\Lib", Version = PythonLanguageVersion.V25 },
                new { Path = "C:\\Python26\\Lib", Version = PythonLanguageVersion.V26 },
                new { Path = "C:\\Python27\\Lib", Version = PythonLanguageVersion.V27 },
                
                new { Path = "C:\\Python30\\Lib", Version = PythonLanguageVersion.V30 },
                new { Path = "C:\\Python31\\Lib", Version = PythonLanguageVersion.V31 },
                new { Path = "C:\\Python32\\Lib", Version = PythonLanguageVersion.V32 },
                new { Path = "C:\\Python33\\Lib", Version = PythonLanguageVersion.V33 } 
            };

            foreach (var version in versions) {
                Console.WriteLine("Testing version {0} {1}", version.Version, version.Path);
                int ran = 0, succeeded = 0;
                string[] files;
                try {
                    files = Directory.GetFiles(version.Path);
                } catch (DirectoryNotFoundException) {
                    continue;
                }

                foreach (var file in files) {
                    try {
                        if (file.EndsWith(".py")) {
                            ran++;
                            TestOneFile(file, version.Version);
                            succeeded++;
                        }
                    } catch (Exception e) {
                        Console.WriteLine(e);
                        Console.WriteLine("Failed: {0}", file);
                        break;
                    }
                }

                Assert.AreEqual(ran, succeeded);
            }
        }

        private static void TestOneFile(string filename, PythonLanguageVersion version) {
            var originalText = File.ReadAllText(filename);

            TestOneString(version, originalText);
        }

        internal static void TestOneString(PythonLanguageVersion version, string originalText, CodeFormattingOptions format = null, string expected = null, bool recurse = true) {
            bool hadExpected = true;
            if (expected == null) {
                expected = originalText;
                hadExpected = false;
            }
            var parser = Parser.CreateParser(new StringReader(originalText), version, new ParserOptions() { Verbatim = true });
            var ast = parser.ParseFile();

            string output;
            try {
                if (format == null) {
                    output = ast.ToCodeString(ast);
                } else {
                    output = ast.ToCodeString(ast, format);
                }
            } catch(Exception e) {
                Console.WriteLine("Failed to convert to code: {0}\r\n{1}", originalText, e);
                Assert.Fail();
                return;
            }

            const int contextSize = 50;
            for (int i = 0; i < expected.Length && i < output.Length; i++) {
                if (expected[i] != output[i]) {
                    // output some context
                    StringBuilder x = new StringBuilder();
                    StringBuilder y = new StringBuilder();
                    StringBuilder z = new StringBuilder();
                    for (int j = Math.Max(0, i - contextSize); j < Math.Min(Math.Max(expected.Length, output.Length), i + contextSize); j++) {
                        if (j < expected.Length) {
                            x.AppendRepr(expected[j]);
                        }
                        if (j < output.Length) {
                            y.AppendRepr(output[j]);
                        }
                        if (j == i) {
                            z.Append("^");
                        } else {
                            z.Append(" ");
                        }
                    }

                    Console.WriteLine("Mismatch context at {0}:", i);
                    Console.WriteLine("Expected: {0}", x.ToString());
                    Console.WriteLine("Got     : {0}", y.ToString());
                    Console.WriteLine("Differs : {0}", z.ToString());

                    if (recurse) {
                        // Try and automatically get a minimal repro if we can...
                        if (!hadExpected) {
                            try {
                                for (int j = i; j >= 0; j--) {
                                    TestOneString(version, originalText.Substring(j), format, null, false);
                                }
                            } catch {
                            }
                        }
                    } else {
                        Console.WriteLine("-----");
                        Console.WriteLine(expected);
                        Console.WriteLine("-----");
                    }

                    Assert.AreEqual(expected[i], output[i], String.Format("Characters differ at {0}, got {1}, expected {2}", i, output[i], expected[i]));
                }
            }

            if (expected.Length != output.Length) {
                Console.WriteLine("Original: {0}", expected.ToString());
                Console.WriteLine("New     : {0}", output.ToString());
            }
            Assert.AreEqual(expected.Length, output.Length);
        }        
    }
}
