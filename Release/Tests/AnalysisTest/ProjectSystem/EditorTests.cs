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
using AnalysisTest.UI;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;

namespace AnalysisTest.ProjectSystem {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class EditorTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        #region Test Cases

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void UnregisteredFileExtensionEditor() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\UnregisteredFileExtension.sln");

            var item = project.ProjectItems.Item("Foo.unregfileext");
            var window = item.Open();
            window.Activate();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);
            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;

            // we shouldn't have opened this as a .py file, so we should have no classifications.
            var classifier = doc.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            Assert.AreEqual(spans.Count, 0);
        }

       
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutliningTest() {
            OutlineTest("Program.py", 
                new ExpectedTag(8, 18, "\r\n    pass"),
                new ExpectedTag(40, 50, "\r\n    pass"),
                new ExpectedTag(72, 82, "\r\n    pass"),
                new ExpectedTag(104, 131, "\r\n    pass\r\nelse:\r\n    pass"),
                new ExpectedTag(153, 185, "\r\n    pass\r\nelif True:\r\n    pass")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutlineNestedFuncDef() {
            OutlineTest("NestedFuncDef.py", 
                new ExpectedTag(8, 36, @"
    def g():
        pass"),
                new ExpectedTag(22, 36, @"
        pass"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OutliningBadForStatement() {
            // there should be no exceptions and no outlining when parsing a malformed for statement
            OutlineTest("BadForStatement.py");
        }

        private void OutlineTest(string filename, params ExpectedTag[] expected) {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\Outlining.sln");

            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);

            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            var tags = doc.GetTaggerAggregator<IOutliningRegionTag>(doc.TextView.TextBuffer).GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));                

            VerifyTags(doc.TextView.TextBuffer, tags, expected);
        }


        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassificationTest() {            
            VerifyClassification(GetClassifications("Program.py"),
                new Classifcation("comment", 0, 8, "#comment"),
                new Classifcation("whitespace", 8, 10, "\r\n"),
                new Classifcation("literal", 10, 11, "1"),
                new Classifcation("whitespace", 11, 13, "\r\n"),
                new Classifcation("string", 13, 18, "\"abc\""),
                new Classifcation("whitespace", 18, 20, "\r\n"),
                new Classifcation("keyword", 20, 23, "def"),
                new Classifcation("identifier", 24, 25, "f"),
                new Classifcation("Python grouping", 25, 27, "()"),
                new Classifcation("Python operator", 27, 28, ":"),
                new Classifcation("keyword", 29, 33, "pass"),
                new Classifcation("whitespace", 33, 35, "\r\n"),
                new Classifcation("string", 35, 46, "'abc\\\r\ndef'")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ClassificationMultiLineStringTest() {
            VerifyClassification(GetClassifications("MultiLineString.py"),
                new Classifcation("identifier", 0, 1, "x"),
                new Classifcation("Python operator", 38, 39, "="),
                new Classifcation("string", 40, 117, "'''\r\ncontents = open(%(filename)r, 'rb').read().replace(\"\\\\r\\\\n\", \"\\\\n\")\r\n'''")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SignaturesTest() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\Signatures.sln");

            var item = project.ProjectItems.Item("sigs.py");
            var window = item.Open();
            window.Activate();
            
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);


            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                doc.TextView.Caret.MoveTo(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, doc.TextView.TextBuffer.CurrentSnapshot.Length));
            }));

            Keyboard.Type("f(");

            var session = doc.WaitForSession<ISignatureHelpSession>();
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultiLineSignaturesTest() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\Signatures.sln");

            var item = project.ProjectItems.Item("multilinesigs.py");
            var window = item.Open();
            window.Activate();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(5 - 1).Start;
                doc.TextView.Caret.MoveTo(point);
            }));

            ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Edit.ParameterInfo"));
            

            var session = doc.WaitForSession<ISignatureHelpSession>();
            Assert.AreEqual("b", session.SelectedSignature.CurrentParameter.Name);
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionsCaseSensitive() {
            // http://pytools.codeplex.com/workitem/457
            var project = DebugProject.OpenProject(@"Python.VS.TestData\Completions.sln");

            var item = project.ProjectItems.Item("bar.py");
            var window = item.Open();
            window.Activate();

            Keyboard.Type("from foo import ba\r");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);

            doc.WaitForText("from foo import baz");
            Keyboard.Type("\r");
            
            Keyboard.Type("from foo import Ba\r");
            doc.WaitForText("from foo import baz\r\nfrom foo import Baz");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AutoIndent() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\AutoIndent.sln");            


            // http://pytools.codeplex.com/workitem/116
            AutoIndentTest(project, "def f():\rprint 'hi'\r\rdef inner(): pass←←←←←←←←←←←←←←←←←\r", @"def f():
    print 'hi'

    
    def inner(): pass");

            // http://pytools.codeplex.com/workitem/121
            AutoIndentTest(project, "x = {'a': [1, 2, 3],\r\r'b':42}", @"x = {'a': [1, 2, 3],

     'b':42}");

            AutoIndentTest(project, "if True:\rpass\r\r42\r\r", @"if True:
    pass

42

");

            AutoIndentTest(project, "def f():\rreturn\r\r42\r\r", @"def f():
    return

42

");

            AutoIndentTest(project, "if True: #foo\rpass\relse: #bar\rpass\r\r42\r\r", @"if True: #foo
    pass
else: #bar
    pass

42

");

            AutoIndentTest(project, "if True:\rraise Exception()\r\r42\r\r", @"if True:
    raise Exception()

42

");

            AutoIndentTest(project, "while True:\rcontinue\r\r42\r\r", @"while True:
    continue

42

");

            AutoIndentTest(project, "while True:\rbreak\r\r42\r\r", @"while True:
    break

42

");
            // http://pytools.codeplex.com/workitem/127
            AutoIndentTest(project, "print ('%s, %s' %\r(1, 2))", @"print ('%s, %s' %
       (1, 2))");

            // http://pytools.codeplex.com/workitem/125
            AutoIndentTest(project, "def f():\rx = (\r7)\rp", @"def f():
    x = (
         7)
    p");

            AutoIndentTest(project, "def f():\rassert False, \\\r'A message'\rp", @"def f():
    assert False, \
        'A message'
    p");

            // other tests...
            AutoIndentTest(project, "1 +\\\r2 +\\\r3 +\\\r4 + 5\r", @"1 +\
    2 +\
    3 +\
    4 + 5
");


            AutoIndentTest(project, "x = {42 :\r42}\rp", @"x = {42 :
     42}
p");

            AutoIndentTest(project, "def f():\rreturn (42,\r100)\r\rp", @"def f():
    return (42,
            100)

p");

            AutoIndentTest(project, "print ('a',\r'b',\r'c')\rp", @"print ('a',
       'b',
       'c')
p");

            AutoIndentTest(project, "foooo ('a',\r'b',\r'c')\rp", @"foooo ('a',
       'b',
       'c')
p");

            // http://pytools.codeplex.com/workitem/157
            AutoIndentTest(project, "def a():\rif b():\rif c():\rd()\rp", @"def a():
    if b():
        if c():
            d()
            p");

            AutoIndentTest(project, "a_list = [1, 2, 3]\rdef func():\rpass", @"a_list = [1, 2, 3]
def func():
    pass");

            AutoIndentTest(project, "class A:\rdef funcA(self, a):\rreturn a\r\rdef funcB(self):\rpass", @"class A:
    def funcA(self, a):
        return a

    def funcB(self):
        pass");

            AutoIndentTest(project, "print('abc')\rimport sys\rpass", @"print('abc')
import sys
pass");

            AutoIndentTest(project, "a_list = [1, 2, 3]\rimport os\rpass", @"a_list = [1, 2, 3]
import os
pass");

            AutoIndentTest(project, "class C:\rdef foo(self):\r'doc string'\rpass", @"class C:
    def foo(self):
        'doc string'
        pass");

            AutoIndentTest(project, "def g():\rfoo(15)\r\r\bfoo(1)\rpass", @"def g():
    foo(15)

foo(1)
pass");

            AutoIndentTest(project, "def m():\rif True:\rpass\relse:\rabc()\r\r\b\bm()\r\rm()\rpass", @"def m():
    if True:
        pass
    else:
        abc()

m()

m()
pass");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AutoIndentExisting() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\AutoIndent.sln");

            // http://pytools.codeplex.com/workitem/138
            AutoIndentExistingTest(project, "Decorator.py", 4, 4, @"class C:
    def f(self):
        pass

    
    @property
    def bar(self):
        pass");

            // http://pytools.codeplex.com/workitem/299
            AutoIndentExistingTest(project, "ClassAndFunc.py", 2, 4, @"class C:
    def f(self):
    
        pass");
        }

        /// <summary>
        /// Single auto indent test
        /// </summary>
        /// <param name="project">containting project</param>
        /// <param name="filename">filename in the project</param>
        /// <param name="line">zero-based line</param>
        /// <param name="column">zero based column</param>
        /// <param name="expectedText"></param>
        private static void AutoIndentExistingTest(Project project, string filename, int line, int column, string expectedText) {
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
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

        private static void AutoIndentTest(Project project, string typedText, string expectedText) {
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            Keyboard.Type(typedText);
            
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TypingTest() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\EditorTests.sln");

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
 });
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CompletionTests() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\EditorTests.sln");

            TypingTest(project, "BackslashCompletion.py", 2, 0, @"x = 42
x\
.conjugate", () => {
     Keyboard.Type(".con\t");     
 });
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

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);
            var textLine = doc.TextView.TextViewLines[line];

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                try {
                    doc.TextView.Caret.MoveTo(textLine.Start + column);
                } catch(Exception) {
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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OpenInvalidUnicodeFile() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\ErrorProjectUnicode.sln");
            var item = project.ProjectItems.Item("Program.py");
            EnvDTE.Window window = null;
            ThreadPool.QueueUserWorkItem(x => { window = item.Open(); });

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var dialog = app.WaitForDialog();

            VisualStudioApp.CheckMessageBox(UI.MessageBoxButton.Ok, "File Load", "Program.py", "ascii encoding");
            while (window == null) {
                System.Threading.Thread.Sleep(1000);
            }

            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);
            var text = doc.TextView.TextBuffer.CurrentSnapshot.GetText();
            Assert.AreNotEqual(text.IndexOf("????"), -1);
        }

        #endregion

        #region Helpers

        private void VerifyTags(ITextBuffer buffer, IEnumerable<IMappingTagSpan<IOutliningRegionTag>> tags, params ExpectedTag[] expected) {
            var ltags = new List<IMappingTagSpan<IOutliningRegionTag>>(tags);

            Assert.AreEqual(expected.Length, ltags.Count);

            for (int i = 0; i < ltags.Count; i++) {
                int start = ltags[i].Span.Start.GetInsertionPoint(x => x == buffer).Value.Position;
                int end = ltags[i].Span.End.GetInsertionPoint(x => x == buffer).Value.Position;
                Assert.AreEqual(expected[i].Start, start);
                Assert.AreEqual(expected[i].End, end);
                Assert.AreEqual(expected[i].Text, buffer.CurrentSnapshot.GetText(Span.FromBounds(start, end)));
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
            var project = DebugProject.OpenProject(@"Python.VS.TestData\Classification.sln");

            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();


            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var doc = app.GetDocument(item.Document.FullName);

            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            var classifier = doc.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            return spans;
        }

        private void VerifyClassification(IList<ClassificationSpan> spans, params Classifcation[] expected) {
            bool passed = false;
            try {
                Assert.AreEqual(expected.Length, spans.Count);

                for (int i = 0; i < spans.Count; i++) {
                    var curSpan = spans[i];
                    

                    int start = curSpan.Span.Start.Position;
                    int end = curSpan.Span.End.Position;

                    Assert.AreEqual(expected[i].Start, start);
                    Assert.AreEqual(expected[i].End, end);
                    Assert.AreEqual(expected[i].Text, curSpan.Span.GetText());
                    Assert.AreEqual(expected[i].ClassificationType, curSpan.ClassificationType.Classification);
                }
                passed = true;
            } finally {
                if (!passed) {
                    // output results for easy test creation...
                    Console.WriteLine(
                        String.Join(",\r\n", 
                            spans.Select(curSpan => 
                                String.Format("new Classifcation(\"{0}\", {1}, {2}, \"{3}\")",
                                    curSpan.ClassificationType.Classification,
                                    curSpan.Span.Start.Position,
                                    curSpan.Span.End.Position,
                                    FormatString(curSpan.Span.GetText())
                                )
                            )
                        )
                    );
                }
            }
        }

        private string FormatString(string p) {
            StringBuilder res = new StringBuilder();
            for (int i = 0; i < p.Length; i++) {
                switch (p[i]) {
                    case '\\': res.Append("\\\\"); break;
                    case '\n': res.Append("\\n"); break;
                    case '\r': res.Append("\\r"); break;
                    case '\t': res.Append("\\t"); break;
                    case '"': res.Append("\\\""); break;
                    default: res.Append(p[i]); break;
                }
            }
            return res.ToString();
        }

        private class Classifcation {
            public readonly int Start, End;
            public readonly string Text;
            public readonly string ClassificationType;

            public Classifcation(string classificationType, int start, int end, string text) {
                ClassificationType = classificationType;
                Start = start;
                End = end;
                Text = text;
            }
        }

        #endregion

    }
}
