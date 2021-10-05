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

using PriorityAttribute = Microsoft.VisualStudio.TestTools.UnitTesting.PriorityAttribute;

namespace PythonToolsTests
{
	[TestClass]
	public class AutoIndentTests
	{
		public static IContentType PythonContentType = new MockContentType("Python", new IContentType[0]);

		[ClassInitialize]
		public static void DoDeployment(TestContext context)
		{
			AssertListener.Initialize();
		}

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void GetIndentation()
		{
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

		[TestMethod, Priority(UnitTestPriority.P0)]
		public void GetLineIndentation()
		{
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

			AssertIndent("def f():\n    print 'hi'\n\n\ndef inner(): pass", 4, 4, version: PythonLanguageVersion.V27);
			AssertIndent("def f():\n    print 'hi'\n\n\ndef inner(): pass", 4, 4, version: PythonLanguageVersion.V36);

			AssertIndent("x = {  #comment\n\n    'a': [\n\n        1,\n\n        ],\n\n    'b':42\n    }", 2, 4);
			AssertIndent("x = {  #comment\n\n    'a': [\n\n        1,\n\n        ],\n\n    'b':42\n    }", 4, 8);
			AssertIndent("x = {  #comment\n\n    'a': [\n\n        1,\n\n        ],\n\n    'b':42\n    }", 6, 8);
			AssertIndent("x = {  #comment\n\n    'a': [\n\n        1,\n\n        ],\n\n    'b':42\n    }", 8, 4);
			AssertIndent("x = {  #comment\n\n    'a': [\n\n        1,\n\n        ],\n\n    'b':42\n    }", 10, 4);

			AssertIndent("def f():\n    assert False, \\\n        'A message'\n    p", 3, 8);
			AssertIndent("def f():\n    assert False, \\\n        'A message'\n    p", 4, 4);

			AssertIndent("def a():\n    if b():\n        if c():\n            d()\n            p", 2, 4);
			AssertIndent("def a():\n    if b():\n        if c():\n            d()\n            p", 3, 8);
			AssertIndent("def a():\n    if b():\n        if c():\n            d()\n            p", 4, 12);
			AssertIndent("def a():\n    if b():\n        if c():\n            d()\n            p", 5, 12);
		}

		private static void AssertIndent(string code, int lineNumber, int expected, int tabSize = 4, int indentSize = 4, PythonLanguageVersion version = PythonLanguageVersion.V36)
		{
			var buffer = new MockTextBuffer(code, PythonContentType);
			var view = new MockTextView(buffer);
			view.Options.SetOptionValue(DefaultOptions.IndentSizeOptionId, indentSize);
			view.Options.SetOptionValue(DefaultOptions.TabSizeOptionId, tabSize);

			var line = buffer.CurrentSnapshot.GetLineFromLineNumber(lineNumber - 1);
			var bi = PythonTextBufferInfo.ForBuffer(null, buffer);
			bi._defaultLanguageVersion = version;
			var actual = AutoIndent.GetLineIndentation(bi, line, view);
			Assert.AreEqual(expected, actual, line.GetText());
		}
	}
}
