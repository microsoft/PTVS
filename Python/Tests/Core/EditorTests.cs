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
using System.Collections.Generic;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.Python;

namespace PythonToolsTests {
    [TestClass]
    public class EditorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        #region Test Cases

        #region Outlining Regions
        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineRegions() {
            string content = @"print('Hello World')
#region
if param: 
    print('hello')
    #endregion
elif not param:
    print('world')
else:
    print('!')

#  region someClass
class someClass:
    def this( self ):
        return self._hidden_variable * 2
#    endregion someClass";

            SnapshotRegionTest(content,
                new ExpectedTag(30, 77, "\nif param: \r\n    print('hello')\r\n    #endregion"),
                new ExpectedTag(161, 269, "\nclass someClass:\r\n    def this( self ):\r\n        return self._hidden_variable * 2\r\n#    endregion someClass"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineUnbalancedRegions() {
            string content = @"#region
#endregion
#endregion
#region
#region";

            SnapshotRegionTest(content,
                new ExpectedTag(8, 19, "\n#endregion"));
        }

        #endregion Outlining Regions

        #region Outline Compound Statements

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineIf() {
            string content = @"if param:
    print('hello')
    print('world')
    print('!')

elif not param:
    print('hello')
    print('world')
    print('!')

else:
    print('hello')
    print('world')
    print('!')

if param and \
    param:
    print('hello')
    print('world')
    print('!')";

            SnapshotOutlineTest(content,
                new ExpectedTag(150, 205, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(10, 65, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(85, 140, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(224, 291, "\n    param:\r\n    print('hello')\r\n    print('world')\r\n    print('!')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineWhile() {
            string content = @"while b and c and d \
    and e \
    and f:
    print('hello')";

            SnapshotOutlineTest(content,
               new ExpectedTag(22, 66, "\n    and e \\\r\n    and f:\r\n    print('hello')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineFor() {
            string content = @"for x in [ 
    1,
    2,
    3,
    4
]:
    print('for')

else:
    print('final')
    print('final')
    print('final')


for x in [1,2,3,4]:
    print('for2')
    print('for2')
    print('for2')";
            SnapshotOutlineTest(content,
                new ExpectedTag(12, 64, "\n    1,\r\n    2,\r\n    3,\r\n    4\r\n]:\r\n    print('for')"),
                new ExpectedTag(74, 133, "\n    print('final')\r\n    print('final')\r\n    print('final')"),
                new ExpectedTag(159, 215, "\n    print('for2')\r\n    print('for2')\r\n    print('for2')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineTry() {
            string content = @"
try: 
    print('try')
    print('try')
except TypeError:
    print('TypeError')
    print('TypeError')
except NameError:
    print('NameError')
    print('NameError')
else:
    print('else')
    print('else')
finally:
    print('finally')
    print('finally')

try: 
    print('try2')
    print('try2')
finally:
    print('finally2')
    print('finally2')";

            SnapshotOutlineTest(content,
                new ExpectedTag(8, 43, "\n    print('try')\r\n    print('try')"),
                new ExpectedTag(63, 110, "\n    print('TypeError')\r\n    print('TypeError')"),
                new ExpectedTag(130, 177, "\n    print('NameError')\r\n    print('NameError')"),
                new ExpectedTag(233, 276, "\n    print('finally')\r\n    print('finally')"),
                new ExpectedTag(185, 222, "\n    print('else')\r\n    print('else')"),
                new ExpectedTag(286, 323, "\n    print('try2')\r\n    print('try2')"),
                new ExpectedTag(334, 379, "\n    print('finally2')\r\n    print('finally2')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineWith() {
            string content = @"with open('file.txt') as f:
    line = f.readline()
    print(line)";

            SnapshotOutlineTest(content,
                new ExpectedTag(28, 69, "\n    line = f.readline()\r\n    print(line)"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineFuncDef() {
            string content = @"@decorator_stmt_made_up
def f():
    print('f')
    def g(a, 
          b, 
          c):
        print('g')
        print('g')";

            SnapshotOutlineTest(content,
                new ExpectedTag(34, 134, "\n    print('f')\r\n    def g(a, \r\n          b, \r\n          c):\r\n        print('g')\r\n        print('g')"),
                new ExpectedTag(65, 134, "\n          b, \r\n          c):\r\n        print('g')\r\n        print('g')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineClassDef() {
            string content = @"class SomeClass:
    def this( self ):
        return self";

            SnapshotOutlineTest(content,
                new ExpectedTag(16, 60, "\r\n    def this( self ):\r\n        return self"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineDecorated() {
            string content = @"
@decorator_stmt(a,
               b,
               c)";

            SnapshotOutlineTest(content,
                new ExpectedTag(21, 58, "\n               b,\r\n               c)"));
        }

        #endregion Outline Compound Statements

        #region Outlining Statements

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineLists() {
            string content = @"a = [1,
     2,
     3]

[1,
        [2,
         3,
         5, 6, 7,
         9,
         10],
        4
    ]
";

            SnapshotOutlineTest(content,
                new ExpectedTag(8, 25, "\n     2,\r\n     3]"),
                new ExpectedTag(33, 123, "\n        [2,\r\n         3,\r\n         5, 6, 7,\r\n         9,\r\n         10],\r\n        4\r\n    ]"),
                new ExpectedTag(46, 104, "\n         3,\r\n         5, 6, 7,\r\n         9,\r\n         10]"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineTuple() {
            string content = @"( 'value1', 
  'value2',
  'value3')";

            SnapshotOutlineTest(content,
                new ExpectedTag(13, 38, "\n  'value2',\r\n  'value3')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineDictionary() {
            string content = @"dict = {""hello"":""world"",
        ""hello"":""world"",""hello"":[1,
                                 2,3,4,
                                 5],
        ""hello"":""world"",
        ""check"": (""tuple1"",
                  ""tuple2"",
                  ""tuple3"",
                  ""tuple4"")}";

            SnapshotOutlineTest(content,
                new ExpectedTag(25, 283, 
                    "\n        \"hello\":\"world\",\"hello\":[1,\r\n" + 
                    "                                 2,3,4,\r\n" + 
                    "                                 5],\r\n" + 
                    "        \"hello\":\"world\",\r\n" + 
                    "        \"check\": (\"tuple1\",\r\n" + 
                    "                  \"tuple2\"," + 
                    "\r\n                  \"tuple3\"," +
                    "\r\n                  \"tuple4\")}"),
                new ExpectedTag(62, 139, "\n                                 2,3,4,\r\n                                 5]"),
                new ExpectedTag(196, 282, "\n                  \"tuple2\",\r\n                  \"tuple3\",\r\n                  \"tuple4\")"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineParanthesesExpression() {
            string content = @"
(   'abc'
    'def'
    'qrt'
    'quox'
)";

            SnapshotOutlineTest(content,
                new ExpectedTag(12, 48, "\n    'def'\r\n    'qrt'\r\n    'quox'\r\n)"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineCallExpression() {
            string content = @"function_call(arg1,
              arg2,
              arg3)";

            SnapshotOutlineTest(content,
                new ExpectedTag(20, 61, "\n              arg2,\r\n              arg3)"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineFromImportStatement() {
            string content = @"from sys \
import argv \
as c";

            SnapshotOutlineTest(content,
                new ExpectedTag(11, 31, "\nimport argv \\\r\nas c"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        public void OutlineSetExpression() {
            string content = @"{1,
 2,
 3}";

            SnapshotOutlineTest(content,
                new ExpectedTag(4, 13, "\n 2,\r\n 3}"));
        }

        private void SnapshotOutlineTest(string fileContents, params ExpectedTag[] expected) {
            var snapshot = new TestUtilities.Mocks.MockTextSnapshot(new TestUtilities.Mocks.MockTextBuffer(fileContents), fileContents);
            var ast = Parser.CreateParser(new TextSnapshotToTextReader(snapshot), PythonLanguageVersion.V34).ParseFile();
            var tags = Microsoft.PythonTools.OutliningTaggerProvider.OutliningTagger.ProcessOutliningTags(ast, snapshot);
            VerifyTags(snapshot, tags, expected);
        }

        private void SnapshotRegionTest(string fileContents, params ExpectedTag[] expected) {
            var snapshot = new TestUtilities.Mocks.MockTextSnapshot(new TestUtilities.Mocks.MockTextBuffer(fileContents), fileContents);
            var ast = Parser.CreateParser(new TextSnapshotToTextReader(snapshot), PythonLanguageVersion.V34).ParseFile();
            var tags = Microsoft.PythonTools.OutliningTaggerProvider.OutliningTagger.ProcessRegionTags(snapshot);
            VerifyTags(snapshot, tags, expected);
        }

        #endregion Outlining Statements

        #endregion Test Cases

        #region Helpers

        private void VerifyTags(ITextSnapshot snapshot, IEnumerable<ITagSpan<IOutliningRegionTag>> tags, params ExpectedTag[] expected) {
            var ltags = new List<ITagSpan<IOutliningRegionTag>>(tags);

            // Print this out so we can easily update the tests if things change.
            foreach (var tag in ltags) {
                int start = tag.Span.Start.Position;
                int end = tag.Span.End.Position;
                Console.WriteLine("new ExpectedTag({0}, {1}, \"{2}\"),",
                    start,
                    end,
                    Classification.FormatString(snapshot.GetText(Span.FromBounds(start, end)))
                );
            }

            Assert.AreEqual(expected.Length, ltags.Count);

            for (int i = 0; i < ltags.Count; i++) {
                int start = ltags[i].Span.Start.Position;
                int end = ltags[i].Span.End.Position;
                Assert.AreEqual(expected[i].Start, start);
                Assert.AreEqual(expected[i].End, end);
                Assert.AreEqual(expected[i].Text, snapshot.GetText(Span.FromBounds(start, end)));
                Assert.AreEqual(ltags[i].Tag.IsImplementation, true);
            }
        }

        private class ExpectedTag {
            public readonly int Start, End;
            public readonly string Text;

            public ExpectedTag(int start, int end, string text) {
                Start = start;
                End = end;
                Text = text;
            }
        }

        #endregion
    }
}