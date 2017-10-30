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

using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;
using TestUtilities.Mocks;

namespace PythonToolsTests {
    [TestClass]
    public class AutoIndentTests {
        public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);

        [TestMethod, Priority(0)]
        public void GetIndentation() {
            Assert.AreEqual(0, AutoIndent.GetIndentation("", 4));
            Assert.AreEqual(0, AutoIndent.GetIndentation("p", 4));
            Assert.AreEqual(4, AutoIndent.GetIndentation("    ", 4));
            Assert.AreEqual(4, AutoIndent.GetIndentation("    p", 4));
            Assert.AreEqual(3, AutoIndent.GetIndentation("   p", 4));
            Assert.AreEqual(5, AutoIndent.GetIndentation("     p", 4));
            Assert.AreEqual(4, AutoIndent.GetIndentation("\tp", 4));
            Assert.AreEqual(5, AutoIndent.GetIndentation("\t p", 4));
            Assert.AreEqual(5, AutoIndent.GetIndentation(" \tp", 4));
            Assert.AreEqual(6, AutoIndent.GetIndentation(" \t p", 4));
        }

        [TestMethod, Priority(0)]
        public void GetLineIndentation() {
            AssertIndent("pass\n", 2, 0);
            AssertIndent("def f():\n", 2, 4);
            AssertIndent("f(x,\n", 2, 2);
            AssertIndent("f(\n", 2, 4);
            AssertIndent("[\n", 2, 4);
            AssertIndent("{\n", 2, 4);
            AssertIndent("         [\n", 2, 13);
            AssertIndent("[[[[[[[[[[\n", 2, 4);
            AssertIndent("         [x,\n", 2, 10);
            AssertIndent("[[[[[[[[[[x,\n", 2, 10);

            AssertIndent("def f():\n    print('hi')\n\n", 3, 4);
            AssertIndent("def f():\n    pass\n\n", 3, 0);

            AssertIndent("abc = {'x': [\n\n  ['''str''',\n\n]],\n\n    }", 2, 4);
            AssertIndent("abc = {'x': [\n\n  ['''str''',\n\n]],\n\n    }", 4, 3);
            AssertIndent("abc = {'x': [\n\n  ['''str''',\n\n]],\n\n    }", 6, 7);
        }

        private static void AssertIndent(string code, int lineNumber, int expected, int tabSize = 4, int indentSize = 4) {
            var buffer = new MockTextBuffer(code, PythonContentType);
            var view = new MockTextView(buffer);
            view.Options.SetOptionValue(DefaultOptions.IndentSizeOptionId, indentSize);
            view.Options.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

            var line = buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber - 1);
            var actual = AutoIndent.GetLineIndentation(PythonTextBufferInfo.ForBuffer(null, buffer), line, view);
            Assert.AreEqual(expected, actual, line.GetText());
        }
    }
}
