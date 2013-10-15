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
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class EditorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        #region Test Cases

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void UnregisteredFileExtensionEditor() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\UnregisteredFileExtension.sln");

                var item = project.ProjectItems.Item("Foo.unregfileext");
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;

                // we shouldn't have opened this as a .py file, so we should have no classifications.
                var classifier = doc.Classifier;
                var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                Assert.AreEqual(spans.Count, 0);
            }
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutliningTest() {
            OutlineTest("Program.py",
                new ExpectedTag(8, 18, "\r\n    pass"),
                new ExpectedTag(40, 50, "\r\n    pass"),
                new ExpectedTag(72, 82, "\r\n    pass"),
                new ExpectedTag(104, 131, "\r\n    pass\r\nelse:\r\n    pass"),
                new ExpectedTag(153, 185, "\r\n    pass\r\nelif True:\r\n    pass"),
                new ExpectedTag(228, 248, "\r\n    print('hello')"),
                new ExpectedTag(267, 287, "\r\n    print('hello')")
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutlineNestedFuncDef() {
            OutlineTest("NestedFuncDef.py",
                new ExpectedTag(8, 36, @"
    def g():
        pass"),
                new ExpectedTag(22, 36, @"
        pass"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutliningBadForStatement() {
            // there should be no exceptions and no outlining when parsing a malformed for statement
            OutlineTest("BadForStatement.py");
        }

        private void OutlineTest(string filename, params ExpectedTag[] expected) {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\Outlining.sln");

                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                var tags = doc.GetTaggerAggregator<IOutliningRegionTag>(doc.TextView.TextBuffer).GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));

                VerifyTags(doc.TextView.TextBuffer, tags, expected);
            }
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassificationTest() {
            Classification.Verify(GetClassifications("Program.py"),
                new Classification("comment", 0, 8, "#comment"),
                new Classification("whitespace", 8, 10, "\r\n"),
                new Classification("literal", 10, 11, "1"),
                new Classification("whitespace", 11, 13, "\r\n"),
                new Classification("string", 13, 18, "\"abc\""),
                new Classification("whitespace", 18, 20, "\r\n"),
                new Classification("keyword", 20, 23, "def"),
                new Classification("identifier", 24, 25, "f"),
                new Classification("Python grouping", 25, 27, "()"),
                new Classification("Python operator", 27, 28, ":"),
                new Classification("keyword", 29, 33, "pass"),
                new Classification("whitespace", 33, 35, "\r\n"),
                new Classification("string", 35, 46, "'abc\\\r\ndef'"),
                new Classification("whitespace", 46, 50, "\r\n\r\n"),
                new Classification("identifier", 50, 53, "foo"),
                new Classification("Python operator", 54, 55, "="),
                new Classification("string", 56, 72, "'ONE \\\r\n    ONE'"),
                new Classification("Python operator", 73, 74, "+"),
                new Classification("identifier", 75, 87, "message_text"),
                new Classification("Python operator", 88, 89, "+"),
                new Classification("string", 90, 113, "'TWOXXXXXXXXXXXX\\\r\nTWO'"),
                new Classification("whitespace", 113, 115, "\r\n")
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassificationMultiLineStringTest() {
            Classification.Verify(GetClassifications("MultiLineString.py"),
                new Classification("identifier", 0, 1, "x"),
                new Classification("Python operator", 38, 39, "="),
                new Classification("string", 40, 117, "'''\r\ncontents = open(%(filename)r, 'rb').read().replace(\"\\\\r\\\\n\", \"\\\\n\")\r\n'''")
            );
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/749
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassificationMultiLineStringTest2() {
            Classification.Verify(GetClassifications("MultiLineString2.py"),
                new Classification("string", 0, 15, "'''\r\nfoo bar'''"),
                new Classification("Python operator", 40, 41, "+"),
                new Classification("string", 45, 125, "''')\r\n\r\n__visualstudio_debugger_init()\r\ndel __visualstudio_debugger_init\r\naaa'''")
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SignaturesTest() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\Signatures.sln");

                var item = project.ProjectItems.Item("sigs.py");
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);


                ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                    doc.TextView.Caret.MoveTo(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, doc.TextView.TextBuffer.CurrentSnapshot.Length));
                }));

                Keyboard.Type("f(");

                using (var sh = doc.WaitForSession<ISignatureHelpSession>()) {
                    var session = sh.Session;
                    Assert.AreEqual("a", session.SelectedSignature.CurrentParameter.Name);

                    Keyboard.Type("b=");

                    WaitForCurrentParameter(session, "b");
                    Assert.AreEqual("b", session.SelectedSignature.CurrentParameter.Name);
                    window.Activate();

                    Keyboard.Type("42,");

                    WaitForNoCurrentParameter(session);
                    Assert.AreEqual(null, session.SelectedSignature.CurrentParameter);

                    Keyboard.Backspace();
                    WaitForCurrentParameter(session);
                    Assert.AreEqual("b", session.SelectedSignature.CurrentParameter.Name);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultiLineSignaturesTest() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\Signatures.sln");

                var item = project.ProjectItems.Item("multilinesigs.py");
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                    var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(5 - 1).Start;
                    doc.TextView.Caret.MoveTo(point);
                }));

                ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Edit.ParameterInfo"));


                using (var sh = doc.WaitForSession<ISignatureHelpSession>()) {
                    Assert.AreEqual("b", sh.Session.SelectedSignature.CurrentParameter.Name);
                }
            }
        }

        private static void WaitForCurrentParameter(ISignatureHelpSession session, string name) {
            for (int i = 0; i < 10; i++) {
                if (session.SelectedSignature.CurrentParameter != null && session.SelectedSignature.CurrentParameter.Name == name) {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void WaitForNoCurrentParameter(ISignatureHelpSession session) {
            for (int i = 0; i < 10; i++) {
                if (session.SelectedSignature.CurrentParameter == null) {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void WaitForCurrentParameter(ISignatureHelpSession session) {
            for (int i = 0; i < 10; i++) {
                if (session.SelectedSignature.CurrentParameter != null) {
                    break;
                }
                System.Threading.Thread.Sleep(1000);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionsCaseSensitive() {
            // http://pytools.codeplex.com/workitem/457
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\Completions.sln");

                var item = project.ProjectItems.Item("bar.py");
                var window = item.Open();
                window.Activate();

                Keyboard.Type("from foo import ba\r");

                var doc = app.GetDocument(item.Document.FullName);

                doc.WaitForText("from foo import baz");
                Keyboard.Type("\r");

                Keyboard.Type("from foo import Ba\r");
                doc.WaitForText("from foo import baz\r\nfrom foo import Baz");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AutoIndent() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\AutoIndent.sln");


                // http://pytools.codeplex.com/workitem/116
                AutoIndentTest(app, project, "def f():\rprint 'hi'\r\rdef inner(): pass←←←←←←←←←←←←←←←←←\r", @"def f():
    print 'hi'

    
    def inner(): pass");

                // http://pytools.codeplex.com/workitem/121
                AutoIndentTest(app, project, "x = {'a': [1, 2, 3],\r\r'b':42}", @"x = {'a': [1, 2, 3],

     'b':42}");

                AutoIndentTest(app, project, "x = {  #comment\r'a': [\r1,\r2,\r3\r],\r\r'b':42\r}", @"x = {  #comment
    'a': [
        1,
        2,
        3
        ],

    'b':42
    }");

                AutoIndentTest(app, project, "if True:\rpass\r\r42\r\r", @"if True:
    pass

42

");

                AutoIndentTest(app, project, "def f():\rreturn\r\r42\r\r", @"def f():
    return

42

");

                AutoIndentTest(app, project, "if True: #foo\rpass\relse: #bar\rpass\r\r42\r\r", @"if True: #foo
    pass
else: #bar
    pass

42

");

                AutoIndentTest(app, project, "if True:\rraise Exception()\r\r42\r\r", @"if True:
    raise Exception()

42

");

                AutoIndentTest(app, project, "while True:\rcontinue\r\r42\r\r", @"while True:
    continue

42

");

                AutoIndentTest(app, project, "while True:\rbreak\r\r42\r\r", @"while True:
    break

42

");
                // http://pytools.codeplex.com/workitem/127
                AutoIndentTest(app, project, "print ('%s, %s' %\r(1, 2))", @"print ('%s, %s' %
       (1, 2))");

                // http://pytools.codeplex.com/workitem/125
                AutoIndentTest(app, project, "def f():\rx = (\r7)\rp", @"def f():
    x = (
        7)
    p");

                AutoIndentTest(app, project, "def f():\rassert False, \\\r'A message'\rp", @"def f():
    assert False, \
        'A message'
    p");

                // other tests...
                AutoIndentTest(app, project, "1 +\\\r2 +\\\r3 +\\\r4 + 5\r", @"1 +\
    2 +\
    3 +\
    4 + 5
");


                AutoIndentTest(app, project, "x = {42 :\r42}\rp", @"x = {42 :
     42}
p");

                AutoIndentTest(app, project, "def f():\rreturn (42,\r100)\r\rp", @"def f():
    return (42,
            100)

p");

                AutoIndentTest(app, project, "print ('a',\r'b',\r'c')\rp", @"print ('a',
       'b',
       'c')
p");

                AutoIndentTest(app, project, "foooo ('a',\r'b',\r'c')\rp", @"foooo ('a',
       'b',
       'c')
p");

                // http://pytools.codeplex.com/workitem/157
                AutoIndentTest(app, project, "def a():\rif b():\rif c():\rd()\rp", @"def a():
    if b():
        if c():
            d()
            p");

                AutoIndentTest(app, project, "a_list = [1, 2, 3]\rdef func():\rpass", @"a_list = [1, 2, 3]
def func():
    pass");

                AutoIndentTest(app, project, "class A:\rdef funcA(self, a):\rreturn a\r\rdef funcB(self):\rpass", @"class A:
    def funcA(self, a):
        return a

    def funcB(self):
        pass");

                AutoIndentTest(app, project, "print('abc')\rimport sys\rpass", @"print('abc')
import sys
pass");

                AutoIndentTest(app, project, "a_list = [1, 2, 3]\rimport os\rpass", @"a_list = [1, 2, 3]
import os
pass");

                AutoIndentTest(app, project, "class C:\rdef foo(self):\r'doc string'\rpass", @"class C:
    def foo(self):
        'doc string'
        pass");

                AutoIndentTest(app, project, "def g():\rfoo(15)\r\r\bfoo(1)\rpass", @"def g():
    foo(15)

foo(1)
pass");

                AutoIndentTest(app, project, "def m():\rif True:\rpass\relse:\rabc()\r\r\b\bm()\r\rm()\rpass", @"def m():
    if True:
        pass
    else:
        abc()

m()

m()
pass");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AutoIndentExisting() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\AutoIndent.sln");

                // http://pytools.codeplex.com/workitem/138
                AutoIndentExistingTest(app, project, "Decorator.py", 4, 4, @"class C:
    def f(self):
        pass

    
    @property
    def bar(self):
        pass");

                // http://pytools.codeplex.com/workitem/299
                AutoIndentExistingTest(app, project, "ClassAndFunc.py", 2, 4, @"class C:
    def f(self):
    
        pass");
            }
        }

        /// <summary>
        /// Single auto indent test
        /// </summary>
        /// <param name="project">containting project</param>
        /// <param name="filename">filename in the project</param>
        /// <param name="line">zero-based line</param>
        /// <param name="column">zero based column</param>
        /// <param name="expectedText"></param>
        private static void AutoIndentExistingTest(VisualStudioApp app, Project project, string filename, int line, int column, string expectedText) {
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);
            var textLine = doc.TextView.TextViewLines[line];

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                doc.TextView.Caret.MoveTo(textLine.Start + column);
            }));

            Keyboard.Type("\r");

            string actual = null;
            for (int i = 0; i < 100; i++) {
                actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

                if (expectedText == actual) {
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }
            Assert.AreEqual(actual, expectedText);
        }

        private static void AutoIndentTest(VisualStudioApp app, Project project, string typedText, string expectedText) {
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            Keyboard.Type(typedText);

            var doc = app.GetDocument(item.Document.FullName);

            string actual = null;
            for (int i = 0; i < 100; i++) {
                actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

                if (expectedText == actual) {
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }
            Assert.AreEqual(expectedText, actual);

            window.Document.Close(vsSaveChanges.vsSaveChangesNo);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TypingTest() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\EditorTests.sln");

                // http://pytools.codeplex.com/workitem/139
                TypingTest(project, "DecoratorOnFunction.py", 0, 0, @"@classmethod
def f(): pass
", () => {
     Keyboard.Type("\r");
     Keyboard.Type("↑");
     Keyboard.Type("@@");
     System.Threading.Thread.Sleep(5000);
     Keyboard.Backspace();
     Keyboard.Type("classmethod");
     System.Threading.Thread.Sleep(5000);
 });

                // http://pytools.codeplex.com/workitem/151
                TypingTest(project, "DecoratorInClass.py", 1, 4, @"class C:
    @classmethod
    def f(self):
        pass
", () => {
     Keyboard.Type("@");
     System.Threading.Thread.Sleep(5000);
     Keyboard.Type("classmethod");
     System.Threading.Thread.Sleep(5000);

     // VS Bug 
     // 72635 Exception occurrs and you're not prompted to save file when you close it while completion list is up. 
     Keyboard.Type(System.Windows.Input.Key.Escape);
 });
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionTests() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\EditorTests.sln");

                TypingTest(project, "BackslashCompletion.py", 2, 0, @"x = 42
x\
.conjugate", () => {
               Keyboard.Type(".con\t");
           });
            }
        }

        /// <summary>
        /// Single auto indent test
        /// </summary>
        /// <param name="project">containting project</param>
        /// <param name="filename">filename in the project</param>
        /// <param name="line">zero-based line</param>
        /// <param name="column">zero based column</param>
        /// <param name="expectedText"></param>
        private static void TypingTest(Project project, string filename, int line, int column, string expectedText, Action typing) {
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var doc = app.GetDocument(item.Document.FullName);
                var textLine = doc.TextView.TextViewLines[line];

                ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                    try {
                        doc.TextView.Caret.MoveTo(textLine.Start + column);
                    } catch (Exception) {
                        Debug.Fail("Bad position for moving caret");
                    }
                }));

                typing();

                string actual = null;
                for (int i = 0; i < 100; i++) {
                    actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

                    if (expectedText == actual) {
                        break;
                    }
                    System.Threading.Thread.Sleep(100);
                }
                Assert.AreEqual(actual, expectedText);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OpenInvalidUnicodeFile() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\ErrorProjectUnicode.sln");
                var item = project.ProjectItems.Item("Program.py");
                EnvDTE.Window window = null;
                ThreadPool.QueueUserWorkItem(x => { window = item.Open(); });

                var dialog = app.WaitForDialog();

                VisualStudioApp.CheckMessageBox(TestUtilities.UI.MessageBoxButton.Ok, "File Load", "Program.py", "ascii encoding");
                while (window == null) {
                    System.Threading.Thread.Sleep(1000);
                }

                window.Activate();
                var doc = app.GetDocument(item.Document.FullName);
                var text = doc.TextView.TextBuffer.CurrentSnapshot.GetText();
                Assert.AreNotEqual(text.IndexOf("????"), -1);
            }
        }

        #endregion

        #region Helpers

        private void VerifyTags(ITextBuffer buffer, IEnumerable<IMappingTagSpan<IOutliningRegionTag>> tags, params ExpectedTag[] expected) {
            var ltags = new List<IMappingTagSpan<IOutliningRegionTag>>(tags);

            foreach (var tag in ltags) {
                int start = tag.Span.Start.GetInsertionPoint(x => x == buffer).Value.Position;
                int end = tag.Span.End.GetInsertionPoint(x => x == buffer).Value.Position;
                Console.WriteLine("new ExpectedTag({0}, {1}, \"{2}\"),",
                    start,
                    end,
                    Classification.FormatString(buffer.CurrentSnapshot.GetText(Span.FromBounds(start, end)))
                );
            }
            Assert.AreEqual(expected.Length, ltags.Count);

            for (int i = 0; i < ltags.Count; i++) {
                int start = ltags[i].Span.Start.GetInsertionPoint(x => x == buffer).Value.Position;
                int end = ltags[i].Span.End.GetInsertionPoint(x => x == buffer).Value.Position;
                Assert.AreEqual(expected[i].Start, start);
                Assert.AreEqual(expected[i].End, end);
                Assert.AreEqual(expected[i].Text, buffer.CurrentSnapshot.GetText(Span.FromBounds(start, end)));
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

        private static IList<ClassificationSpan> GetClassifications(string filename) {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = app.OpenAndFindProject(@"TestData\Classification.sln");

                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                var classifier = doc.Classifier;
                var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                return spans;
            }

        #endregion

        }

    }
}