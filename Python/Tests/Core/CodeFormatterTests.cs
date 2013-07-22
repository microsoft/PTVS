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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Interpreter.Default;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Refactoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class CodeFormatterTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        [TestMethod, Priority(0)]
        public void TestCodeFormattingSelection() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello(self):
        method_end";

            string selection = "def say_hello .. method_end";

            string expected = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello( self ):
        method_end";

            var options = new CodeFormattingOptions() { 
                SpaceBeforeClassDeclarationParen = true, 
                SpaceWithinFunctionDeclarationParens = true 
            };

            CodeFormattingTest(input, selection, expected, "    def say_hello .. method_end", options);
        }

        [TestMethod, Priority(0)]
        public void TestCodeFormattingEndOfFile() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello(self):
        method_end
";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = true,
                SpaceWithinFunctionDeclarationParens = true
            };

            CodeFormattingTest(input, new Span(input.Length, 0), input, null, options);
        }

        [TestMethod, Priority(0)]
        public void TestCodeFormattingInMethodExpression() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello(self):
        method_end
";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = true,
                SpaceWithinFunctionDeclarationParens = true
            };

            CodeFormattingTest(input, "method_end", input, null, options);
        }

        [TestMethod, Priority(0)]
        public void TestCodeFormattingStartOfMethodSelection() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello(self):
        method_end";

            string selection = "def say_hello";

            string expected = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Bar(object):
    def say_hello( self ):
        method_end";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = true,
                SpaceWithinFunctionDeclarationParens = true
            };

            CodeFormattingTest(input, selection, expected, "    def say_hello .. method_end", options);
        }

        [TestMethod, Priority(0)]
        public void FormatDocument() {
            var input = @"foo('Hello World')";
            var expected = @"foo( 'Hello World' )";
            var options = new CodeFormattingOptions() { SpaceWithinCallParens = true };

            CodeFormattingTest(input, new Span(0, input.Length), expected, null, options, false);
        }

        private static void CodeFormattingTest(string input, object selection, string expected, object expectedSelection, CodeFormattingOptions options, bool selectResult = true) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
            using (var analyzer = new VsProjectAnalyzer(fact, new[] { fact }, new MockErrorProviderFactory())) {
                var buffer = new MockTextBuffer(input);
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var view = new MockTextView(buffer);
                var selectionSpan = new SnapshotSpan(
                    buffer.CurrentSnapshot,
                    ExtractMethodTests.GetSelectionSpan(input, selection)
                );
                view.Selection.Select(selectionSpan, false);

                new CodeFormatter(view, options).FormatCode(
                    selectionSpan,
                    selectResult
                );

                Assert.AreEqual(expected, view.TextBuffer.CurrentSnapshot.GetText());
                if (expectedSelection != null) {
                    Assert.AreEqual(
                        ExtractMethodTests.GetSelectionSpan(expected, expectedSelection),
                        view.Selection.StreamSelectionSpan.SnapshotSpan.Span
                    );
                }
            }
        }
    }
}
