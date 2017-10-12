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

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;
using TestUtilities.Mocks;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class CodeFormatterTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
        }

        [TestMethod, Priority(0)]
        public async Task TestCodeFormattingSelection() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello(self):
        method_end";

            string selection = "def say_hello .. method_end";

            string expected = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello( self ):
        method_end";

            var options = new CodeFormattingOptions() { 
                SpaceBeforeClassDeclarationParen = true, 
                SpaceWithinFunctionDeclarationParens = true 
            };

            await CodeFormattingTest(input, selection, expected, "    def say_hello .. method_end", options);
        }

        [TestMethod, Priority(0)]
        public async Task TestCodeFormattingEndOfFile() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello(self):
        method_end
";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = false,
                SpaceWithinFunctionDeclarationParens = false
            };

            await CodeFormattingTest(input, new Span(input.Length, 0), input, null, options);
        }

        [TestMethod, Priority(0)]
        public async Task TestCodeFormattingInMethodExpression() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello(self):
        method_end
";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = true,
                SpaceWithinFunctionDeclarationParens = true
            };

            await CodeFormattingTest(input, "method_end", input, null, options);
        }

        [TestMethod, Priority(0)]
        public async Task TestCodeFormattingStartOfMethodSelection() {
            var input = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello(self):
        method_end";

            string selection = "def say_hello";

            string expected = @"print('Hello World')

class SimpleTest(object):
    def test_simple_addition(self):
        pass

    def test_complex(self):
        pass

class Oar(object):
    def say_hello( self ):
        method_end";

            var options = new CodeFormattingOptions() {
                SpaceBeforeClassDeclarationParen = true,
                SpaceWithinFunctionDeclarationParens = true
            };

            await CodeFormattingTest(input, selection, expected, "    def say_hello .. method_end", options);
        }

        [TestMethod, Priority(0)]
        public async Task FormatDocument() {
            var input = @"fob('Hello World')";
            var expected = @"fob( 'Hello World' )";
            var options = new CodeFormattingOptions() { SpaceWithinCallParens = true };

            await CodeFormattingTest(input, new Span(0, input.Length), expected, null, options, false);
        }

        private static async Task CodeFormattingTest(string input, object selection, string expected, object expectedSelection, CodeFormattingOptions options, bool selectResult = true) {
            var fact = InterpreterFactoryCreator.CreateAnalysisInterpreterFactory(new Version(2, 7));
            var services = PythonToolsTestUtilities.CreateMockServiceProvider().GetEditorServices();
            using (var analyzer = await VsProjectAnalyzer.CreateForTests(services, fact)) {
                var buffer = new MockTextBuffer(input, PythonCoreConstants.ContentType, Path.Combine(TestData.GetTempPath(), "fob.py"));
                buffer.AddProperty(typeof(VsProjectAnalyzer), analyzer);
                var view = new MockTextView(buffer);
                var bi = services.GetBufferInfo(buffer);
                var entry = await analyzer.AnalyzeFileAsync(bi.Filename);
                Assert.AreEqual(entry, bi.TrySetAnalysisEntry(entry, null), "Failed to set analysis entry");
                entry.GetOrCreateBufferParser(services).AddBuffer(buffer);

                var selectionSpan = new SnapshotSpan(
                    buffer.CurrentSnapshot,
                    ExtractMethodTests.GetSelectionSpan(input, selection)
                );
                view.Selection.Select(selectionSpan, false);

                await analyzer.FormatCodeAsync(
                    selectionSpan,
                    view,
                    options,
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
