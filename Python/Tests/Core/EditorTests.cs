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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Intellisense;
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
        [TestMethod, Priority(0)]
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
                new ExpectedTag(29, 77, "\r\nif param: \r\n    print('hello')\r\n    #endregion"),
                new ExpectedTag(160, 269, "\r\nclass someClass:\r\n    def this( self ):\r\n        return self._hidden_variable * 2\r\n#    endregion someClass"));
        }

        [TestMethod, Priority(0)]
        public void OutlineUnbalancedRegions() {
            string content = @"#region
#endregion
#endregion
#region
#region";

            SnapshotRegionTest(content,
                new ExpectedTag(7, 19, "\r\n#endregion"));
        }

        #endregion Outlining Regions

        #region Outlining Cells

        [TestMethod, Priority(0)]
        public void OutlineCells() {
            string content = @"pass
#%% cell 1
pass

#%% empty cell
#%%cell2

pass

# Preceding comment
# In[7]: IPython tag
pass

";

            SnapshotCellTest(content,
                new ExpectedTag(16, 22, "\r\npass"),
                new ExpectedTag(50, 58, "\r\n\r\npass"),
                new ExpectedTag(81, 109, "\r\n# In[7]: IPython tag\r\npass")
            );
        }

        #endregion

        #region Outline Compound Statements

        [TestMethod, Priority(0)]
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
                new ExpectedTag(149, 205, "\r\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(9, 65, "\r\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(84, 140, "\r\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(223, 291, "\r\n    param:\r\n    print('hello')\r\n    print('world')\r\n    print('!')"));
        }

        [TestMethod, Priority(0)]
        public void OutlineWhile() {
            string content = @"while b and c and d \
    and e \
    and f:
    print('hello')";

            SnapshotOutlineTest(content,
               new ExpectedTag(21, 66, "\r\n    and e \\\r\n    and f:\r\n    print('hello')"));
        }

        [TestMethod, Priority(0)]
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
                new ExpectedTag(11, 64, "\r\n    1,\r\n    2,\r\n    3,\r\n    4\r\n]:\r\n    print('for')"),
                new ExpectedTag(73, 133, "\r\n    print('final')\r\n    print('final')\r\n    print('final')"),
                new ExpectedTag(158, 215, "\r\n    print('for2')\r\n    print('for2')\r\n    print('for2')"));
        }

        [TestMethod, Priority(0)]
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
                new ExpectedTag(6, 43, " \r\n    print('try')\r\n    print('try')"),
                new ExpectedTag(62, 110, "\r\n    print('TypeError')\r\n    print('TypeError')"),
                new ExpectedTag(129, 177, "\r\n    print('NameError')\r\n    print('NameError')"),
                new ExpectedTag(232, 276, "\r\n    print('finally')\r\n    print('finally')"),
                new ExpectedTag(184, 222, "\r\n    print('else')\r\n    print('else')"),
                new ExpectedTag(284, 323, " \r\n    print('try2')\r\n    print('try2')"),
                new ExpectedTag(333, 379, "\r\n    print('finally2')\r\n    print('finally2')"));
        }

        [TestMethod, Priority(0)]
        public void OutlineWith() {
            string content = @"with open('file.txt') as f:
    line = f.readline()
    print(line)";

            SnapshotOutlineTest(content,
                new ExpectedTag(27, 69, "\r\n    line = f.readline()\r\n    print(line)"));
        }

        [TestMethod, Priority(0)]
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
                new ExpectedTag(33, 134, "\r\n    print('f')\r\n    def g(a, \r\n          b, \r\n          c):\r\n        print('g')\r\n        print('g')"),
                new ExpectedTag(94, 134, "\r\n        print('g')\r\n        print('g')"));
        }

        [TestMethod, Priority(0)]
        public void OutlineClassDef() {
            string content = @"class SomeClass:
    def this( self ):
        return self";

            SnapshotOutlineTest(content,
                new ExpectedTag(16, 60, "\r\n    def this( self ):\r\n        return self"));
        }

        [TestMethod, Priority(0)]
        public void OutlineDecorated() {
            string content = @"
@decorator_stmt(a,
               b,
               c)";

            SnapshotOutlineTest(content,
                new ExpectedTag(20, 58, "\r\n               b,\r\n               c)"));
        }

        #endregion Outline Compound Statements

        #region Outlining Statements

        [TestMethod, Priority(0)]
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
                new ExpectedTag(7, 25, "\r\n     2,\r\n     3]"),
                new ExpectedTag(32, 123, "\r\n        [2,\r\n         3,\r\n         5, 6, 7,\r\n         9,\r\n         10],\r\n        4\r\n    ]"),
                new ExpectedTag(45, 104, "\r\n         3,\r\n         5, 6, 7,\r\n         9,\r\n         10]"));
        }

        [TestMethod, Priority(0)]
        public void OutlineTuple() {
            string content = @"( 'value1', 
  'value2',
  'value3')";

            SnapshotOutlineTest(content,
                new ExpectedTag(12, 38, "\r\n  'value2',\r\n  'value3')"));
        }

        [TestMethod, Priority(0)]
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
                new ExpectedTag(24, 283, 
                    "\r\n        \"hello\":\"world\",\"hello\":[1,\r\n" + 
                    "                                 2,3,4,\r\n" + 
                    "                                 5],\r\n" + 
                    "        \"hello\":\"world\",\r\n" + 
                    "        \"check\": (\"tuple1\",\r\n" + 
                    "                  \"tuple2\"," + 
                    "\r\n                  \"tuple3\"," +
                    "\r\n                  \"tuple4\")}"),
                new ExpectedTag(61, 139, "\r\n                                 2,3,4,\r\n                                 5]"),
                new ExpectedTag(195, 282, "\r\n                  \"tuple2\",\r\n                  \"tuple3\",\r\n                  \"tuple4\")"));
        }

        [TestMethod, Priority(0)]
        public void OutlineParenthesesExpression() {
            string content = @"
(   'abc'
    'def'
    'qrt'
    'quox'
)";

            SnapshotOutlineTest(content,
                new ExpectedTag(11, 48, "\r\n    'def'\r\n    'qrt'\r\n    'quox'\r\n)"));
        }

        [TestMethod, Priority(0)]
        public void OutlineCallExpression() {
            string content = @"function_call(arg1,
              arg2,
              arg3)";

            SnapshotOutlineTest(content,
                new ExpectedTag(19, 61, "\r\n              arg2,\r\n              arg3)"));
        }

        [TestMethod, Priority(0)]
        public void OutlineFromImportStatement() {
            string content = @"from sys \
import argv \
as c";

            SnapshotOutlineTest(content,
                new ExpectedTag(10, 31, "\r\nimport argv \\\r\nas c"));
        }

        [TestMethod, Priority(0)]
        public void OutlineSetExpression() {
            string content = @"{1,
 2,
 3}";

            SnapshotOutlineTest(content,
                new ExpectedTag(3, 13, "\r\n 2,\r\n 3}"));
        }

        [TestMethod, Priority(0)]
        public void OutlineConstantExpression() {
            string content = @"'''this
is
a
multiline
string'''";

            SnapshotOutlineTest(content,
                new ExpectedTag(7, 36, "\r\nis\r\na\r\nmultiline\r\nstring'''"));
        }

        private void SnapshotOutlineTest(string fileContents, params ExpectedTag[] expected) {
            var snapshot = new TestUtilities.Mocks.MockTextSnapshot(new TestUtilities.Mocks.MockTextBuffer(fileContents), fileContents);
            var ast = Parser.CreateParser(new TextSnapshotToTextReader(snapshot), PythonLanguageVersion.V34).ParseFile();
            var walker = new OutliningWalker(ast);
            ast.Walk(walker);
            var protoTags = walker.GetTags();

            var tags = protoTags.Select(x =>
                OutliningTaggerProvider.OutliningTagger.GetTagSpan(
                    snapshot,
                    x.startIndex,
                    x.endIndex,
                    x.headerIndex
                )
            );
            VerifyTags(snapshot, tags, expected);
        }

        private void SnapshotRegionTest(string fileContents, params ExpectedTag[] expected) {
            var snapshot = new TestUtilities.Mocks.MockTextSnapshot(new TestUtilities.Mocks.MockTextBuffer(fileContents), fileContents);
            var ast = Parser.CreateParser(new TextSnapshotToTextReader(snapshot), PythonLanguageVersion.V34).ParseFile();
            var tags = Microsoft.PythonTools.OutliningTaggerProvider.OutliningTagger.ProcessRegionTags(snapshot, default(CancellationToken));
            VerifyTags(snapshot, tags, expected);
        }

        private void SnapshotCellTest(string fileContents, params ExpectedTag[] expected) {
            var snapshot = new TestUtilities.Mocks.MockTextSnapshot(new TestUtilities.Mocks.MockTextBuffer(fileContents), fileContents);
            var ast = Parser.CreateParser(new TextSnapshotToTextReader(snapshot), PythonLanguageVersion.V34).ParseFile();
            var tags = Microsoft.PythonTools.OutliningTaggerProvider.OutliningTagger.ProcessCellTags(snapshot, default(CancellationToken));
            VerifyTags(snapshot, tags, expected);
        }

        #endregion Outlining Statements

        #region REPL prompt removal

        [TestMethod, Priority(0)]
        public void RemoveReplPrompts() {
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts(">>>", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts(">>> ", null));
            Assert.AreEqual("    ", ReplPromptHelpers.RemovePrompts(">>>     ", null));
            Assert.AreEqual("pass", ReplPromptHelpers.RemovePrompts(">>> pass", null));
            Assert.AreEqual(" pass", ReplPromptHelpers.RemovePrompts(">>>  pass", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("...", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("... ", null));
            Assert.AreEqual("    ", ReplPromptHelpers.RemovePrompts("...     ", null));
            Assert.AreEqual("pass", ReplPromptHelpers.RemovePrompts("... pass", null));
            Assert.AreEqual(" pass", ReplPromptHelpers.RemovePrompts("...  pass", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("In[1]:", null));
            Assert.AreEqual("    ", ReplPromptHelpers.RemovePrompts("In [ 2 ]  :     ", null));
            Assert.AreEqual("pass", ReplPromptHelpers.RemovePrompts("In [ 2 ]  : pass", null));
            Assert.AreEqual(" pass", ReplPromptHelpers.RemovePrompts("In [ 2 ]  :  pass", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("...:", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("    ...:", null));
            Assert.AreEqual("", ReplPromptHelpers.RemovePrompts("  ...: ", null));
            Assert.AreEqual("    ", ReplPromptHelpers.RemovePrompts("  ...:     ", null));
            Assert.AreEqual("pass", ReplPromptHelpers.RemovePrompts("  ...: pass", null));
            Assert.AreEqual(" pass", ReplPromptHelpers.RemovePrompts("  ...:  pass", null));

            Assert.AreEqual(@"x = 1
print(x)
if True:
    pass


print(x)
1

if True:
    print(x)

1".Replace("\r\n", "\n"), ReplPromptHelpers.RemovePrompts(@">>> x = 1
>>> print(x)
>>> if True:
...     pass
...

In [2]: print(x)
1

In [3]: if True:
   ...:     print(x)
   ...: 
1", "\n"));
        }

        #endregion

        private static StringLiteralCompletionList.EntryInfo MakeEntryInfo(string rootpath, string filename, string insertionText = null, string fullpath = null, bool? isFile = null) {
            var realIsFile = isFile ?? !string.IsNullOrEmpty(Path.GetExtension(filename));
            return new StringLiteralCompletionList.EntryInfo {
                Tooltip = fullpath ?? Path.Combine(rootpath, filename),
                InsertionText = insertionText ?? (Path.Combine(rootpath, filename) + (realIsFile ? "" : "\\")),
                Caption = filename,
                IsFile = realIsFile
            };
        }

        [TestMethod, Priority(0)]
        public void StringCompletionFileEntries() {
            var cwd = TestData.GetPath("TestData");
            var user = TestData.GetPath("TestData\\Databases");

            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo(cwd + "\\AbsolutePaths", cwd, user),
                MakeEntryInfo(cwd, "AbsolutePath"),
                MakeEntryInfo(cwd, "AbsolutePath.sln"),
                MakeEntryInfo(cwd, "HelloWorld"),
                MakeEntryInfo(cwd, "HelloWorld.sln")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo(cwd + "\\AbsolutePath\\", cwd, user),
                MakeEntryInfo(cwd + "\\AbsolutePath", "AbsolutePath.pyproj")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo("./AbsolutePaths", cwd, user),
                MakeEntryInfo(cwd, "AbsolutePath", ".\\AbsolutePath\\"),
                MakeEntryInfo(cwd, "AbsolutePath.sln", ".\\AbsolutePath.sln"),
                MakeEntryInfo(cwd, "HelloWorld", ".\\HelloWorld\\"),
                MakeEntryInfo(cwd, "HelloWorld.sln", ".\\HelloWorld.sln")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo(".\\Ab", cwd, user),
                MakeEntryInfo(cwd, "AbsolutePath", ".\\AbsolutePath\\"),
                MakeEntryInfo(cwd, "AbsolutePath.sln", ".\\AbsolutePath.sln"),
                MakeEntryInfo(cwd, "HelloWorld", ".\\HelloWorld\\"),
                MakeEntryInfo(cwd, "HelloWorld.sln", ".\\HelloWorld.sln")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo("~/Ab", cwd, user),
                MakeEntryInfo(user, "V27", "~\\V27\\"),
                MakeEntryInfo(user, "Readme.txt", "~\\Readme.txt")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo("~\\Ab", cwd, user),
                MakeEntryInfo(user, "V27", "~\\V27\\"),
                MakeEntryInfo(user, "Readme.txt", "~\\Readme.txt")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo("~\\V27\\", cwd, user),
                MakeEntryInfo(user + "\\V27", "ntpath.idb", "~\\V27\\ntpath.idb"),
                MakeEntryInfo(user + "\\V27", "os.idb", "~\\V27\\os.idb")
            );
            AssertUtil.ContainsAtLeast(
                StringLiteralCompletionList.GetEntryInfo("Ab", cwd, user)
            );
        }

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