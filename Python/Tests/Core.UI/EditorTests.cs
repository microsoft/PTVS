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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class EditorTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        #region Test Cases

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UnregisteredFileExtensionEditor() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\UnregisteredFileExtension.sln");

                var item = project.ProjectItems.Item("Fob.unregfileext");
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
        [HostType("VSTestHost")]
        public void OutliningTest() {
            OutlineTest("Program.py",
                new ExpectedTag(9, 64, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(86, 142, "\r\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(165, 220, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(306, 361, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(243, 298, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(384, 439, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(452, 507, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(551, 606, "\n    print('hello')\r\n    print('world')\r\n    print('!')"),
                new ExpectedTag(626, 681, "\n    print('hello')\r\n    print('world')\r\n    print('!')")
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void OutlineNestedFuncDef() {
            OutlineTest("NestedFuncDef.py",
                new ExpectedTag(9, 90, "\n    def g():\r\n        print('hello')\r\n        print('world')\r\n        print('!')"),
                new ExpectedTag(23, 90, "\n        print('hello')\r\n        print('world')\r\n        print('!')"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void OutliningBadForStatement() {
            // there should be no exceptions and no outlining when parsing a malformed for statement
            OutlineTest("BadForStatement.py");
        }

        private void OutlineTest(string filename, params ExpectedTag[] expected) {
            using (var app = new VisualStudioApp()) {

                var prevOption = app.GetService<PythonToolsService>().AdvancedOptions.EnterOutliningModeOnOpen;
                try {
                    app.GetService<PythonToolsService>().AdvancedOptions.EnterOutliningModeOnOpen = true;

                    var project = app.OpenProject(@"TestData\Outlining.sln");

                    var item = project.ProjectItems.Item(filename);
                    var window = item.Open();
                    window.Activate();

                    var doc = app.GetDocument(item.Document.FullName);

                    var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                    var tags = doc.GetTaggerAggregator<IOutliningRegionTag>(doc.TextView.TextBuffer).GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length));

                    VerifyTags(doc.TextView.TextBuffer, tags, expected);
                } finally {
                    app.GetService<PythonToolsService>().AdvancedOptions.EnterOutliningModeOnOpen = prevOption;
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
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
                new Classification("identifier", 50, 53, "fob"),
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
        [HostType("VSTestHost")]
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
        [HostType("VSTestHost")]
        public void ClassificationMultiLineStringTest2() {
            Classification.Verify(GetClassifications("MultiLineString2.py"),
                new Classification("string", 0, 15, "'''\r\nfob oar'''"),
                new Classification("Python operator", 40, 41, "+"),
                new Classification("string", 45, 125, "''')\r\n\r\n__visualstudio_debugger_init()\r\ndel __visualstudio_debugger_init\r\naaa'''")
            );
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void SignaturesTest() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\Signatures.sln");

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
        [HostType("VSTestHost")]
        public void MultiLineSignaturesTest() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\Signatures.sln");

                var item = project.ProjectItems.Item("multilinesigs.py");
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                    var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(5 - 1).Start;
                    doc.TextView.Caret.MoveTo(point);
                }));

                app.ExecuteCommand("Edit.ParameterInfo");

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
        [HostType("VSTestHost")]
        public void CompletionsCaseSensitive() {
            // http://pytools.codeplex.com/workitem/457
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\Completions.sln");

                var item = project.ProjectItems.Item("oar.py");
                var window = item.Open();
                window.Activate();

                Keyboard.Type("from fob import ba\r");

                var doc = app.GetDocument(item.Document.FullName);

                doc.WaitForText("from fob import baz");
                Keyboard.Type("\r");

                Keyboard.Type("from fob import Ba\r");
                doc.WaitForText("from fob import baz\r\nfrom fob import Baz");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AutoIndent() {
            using (var app = new VisualStudioApp()) {
                var prevSetting = app.GetService<PythonToolsService>().AdvancedOptions.AddNewLineAtEndOfFullyTypedWord;
                app.OnDispose(() => app.GetService<PythonToolsService>().AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = prevSetting);
                app.GetService<PythonToolsService>().AdvancedOptions.AddNewLineAtEndOfFullyTypedWord = true;

                var project = app.OpenProject(@"TestData\AutoIndent.sln");


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

                AutoIndentTest(app, project, "if True: #fob\rpass\relse: #oar\rpass\r\r42\r\r", @"if True: #fob
    pass
else: #oar
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

                AutoIndentTest(app, project, "class C:\rdef fob(self):\r'doc string'\rpass", @"class C:
    def fob(self):
        'doc string'
        pass");

                AutoIndentTest(app, project, "def g():\rfob(15)\r\r\bfob(1)\rpass", @"def g():
    fob(15)

fob(1)
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
        [HostType("VSTestHost")]
        public void AutoIndentExisting() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AutoIndent.sln");

                // http://pytools.codeplex.com/workitem/138
                AutoIndentExistingTest(app, project, "Decorator.py", 4, 4, @"class C:
    def f(self):
        pass

    
    @property
    def oar(self):
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
        [HostType("VSTestHost")]
        public void TypingTest() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\EditorTests.sln");

                // http://pytools.codeplex.com/workitem/139
                TypingTest(app, project, "DecoratorOnFunction.py", 0, 0, @"@classmethod
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
                TypingTest(app, project, "DecoratorInClass.py", 1, 4, @"class C:
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
        [HostType("VSTestHost")]
        public void CompletionTests() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\EditorTests.sln");

                TypingTest(app, project, "BackslashCompletion.py", 2, 0, @"x = 42
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
        private static void TypingTest(VisualStudioApp app, Project project, string filename, int line, int column, string expectedText, Action typing) {
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();

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

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void OpenInvalidUnicodeFile() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProjectUnicode.sln");
                var item = project.ProjectItems.Item("Program.py");
                var windowTask = Task.Run(() => item.Open());

                VisualStudioApp.CheckMessageBox(TestUtilities.MessageBoxButton.Ok, "File Load", "Program.py", "ascii encoding");

                var window = windowTask.Result;
                window.Activate();
                var doc = app.GetDocument(item.Document.FullName);
                var text = doc.TextView.TextBuffer.CurrentSnapshot.GetText();
                // Characters should not have been replaced
                Assert.AreEqual(-1, text.IndexOf("????"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void IndentationInconsistencyWarning() {
            using (var app = new PythonVisualStudioApp()) {
                var options = app.Options;
                var severity = options.IndentationInconsistencySeverity;
                options.IndentationInconsistencySeverity = Severity.Warning;
                app.OnDispose(() => options.IndentationInconsistencySeverity = severity);

                var project = app.OpenProject(@"TestData\InconsistentIndentation.sln");

                var items = app.WaitForErrorListItems(1);
                Assert.AreEqual(1, items.Count);

                VSTASKPRIORITY[] pri = new VSTASKPRIORITY[1];
                ErrorHandler.ThrowOnFailure(items[0].get_Priority(pri));
                Assert.AreEqual(VSTASKPRIORITY.TP_NORMAL, pri[0]);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void IndentationInconsistencyError() {
            using (var app = new PythonVisualStudioApp()) {
                var options = app.Options;
                var severity = options.IndentationInconsistencySeverity;
                options.IndentationInconsistencySeverity = Severity.Error;
                app.OnDispose(() => options.IndentationInconsistencySeverity = severity);

                var project = app.OpenProject(@"TestData\InconsistentIndentation.sln");

                var items = app.WaitForErrorListItems(1);
                Assert.AreEqual(1, items.Count);

                VSTASKPRIORITY[] pri = new VSTASKPRIORITY[1];
                ErrorHandler.ThrowOnFailure(items[0].get_Priority(pri));
                Assert.AreEqual(VSTASKPRIORITY.TP_HIGH, pri[0]);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void IndentationInconsistencyIgnore() {
            using (var app = new PythonVisualStudioApp()) {
                var options = app.Options;
                var severity = options.IndentationInconsistencySeverity;
                options.IndentationInconsistencySeverity = Severity.Ignore;
                app.OnDispose(() => options.IndentationInconsistencySeverity = severity);

                var project = app.OpenProject(@"TestData\InconsistentIndentation.sln");

                List<IVsTaskItem> items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);
            }
        }

        private static void SquiggleShowHide(string document, Action<PythonVisualStudioApp> test) {
            using (var app = new PythonVisualStudioApp()) {
                UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = true;
                app.OnDispose(() => UnresolvedImportSquiggleProvider._alwaysCreateSquiggle = false);


                var project = app.OpenProject(@"TestData\MissingImport.sln");

                var editorWindows = app.Dte.Windows
                    .OfType<EnvDTE.Window>()
                    .Where(w => w.Kind == "Editor")
                    .ToArray();
                foreach (var w in editorWindows) {
                    w.Close(vsSaveChanges.vsSaveChangesNo);
                }

                var wnd = project.ProjectItems.Item(document).Open();
                wnd.Activate();
                try {
                    test(app);
                } finally {
                    wnd.Close();
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Squiggle")]
        [HostType("VSTestHost")]
        public void ImportPresent() {
            SquiggleShowHide("ImportPresent.py", app => {
                var items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);
            });
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Squiggle")]
        [HostType("VSTestHost")]
        public void ImportSelf() {
            SquiggleShowHide("ImportSelf.py", app => {
                var items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);
            });
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Squiggle")]
        [HostType("VSTestHost")]
        public void ImportMissingThenAddThenExcludeFile() {
            SquiggleShowHide("ImportMissing.py", app => {
                string text;
                var items = app.WaitForErrorListItems(1);
                Assert.AreEqual(1, items.Count);
                Assert.AreEqual(0, items[0].get_Text(out text));
                Assert.IsTrue(text.Contains("AbsentModule"), text);

                var sln2 = (EnvDTE80.Solution2)app.Dte.Solution;
                var project = app.Dte.Solution.Projects.Item(1);
                project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\MissingImport\AbsentModule.py"));

                items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);

                project.ProjectItems.Item("AbsentModule.py").Remove();
                items = app.WaitForErrorListItems(1);
                Assert.AreEqual(1, items.Count);
                Assert.AreEqual(0, items[0].get_Text(out text));
                Assert.IsTrue(text.Contains("AbsentModule"), text);
            });
        }

        [TestMethod, Priority(0), TestCategory("Core"), TestCategory("Squiggle")]
        [HostType("VSTestHost")]
        public void ImportPresentThenAddThenRemoveReference() {
            var python = PythonPaths.Versions.LastOrDefault(p => p.Version.Is3x() && !p.Isx64);
            python.AssertInstalled();

            var vcproj = TestData.GetPath(@"TestData\ProjectReference\NativeModule\NativeModule.vcxproj");
            File.WriteAllText(vcproj, File.ReadAllText(vcproj)
                .Replace("$(PYTHON_INCLUDE)", Path.Combine(python.PrefixPath, "include"))
                .Replace("$(PYTHON_LIB)", Path.Combine(python.PrefixPath, "libs"))
            );

            using (var app = new PythonVisualStudioApp())
            using (app.SelectDefaultInterpreter(python)) {
                var project = app.OpenProject(@"TestData\ProjectReference\CProjectReference.sln", projectName: "PythonApplication2", expectedProjects: 2);

                var wnd = project.ProjectItems.Item("Program.py").Open();
                wnd.Activate();
                try {
                    app.Dte.Solution.SolutionBuild.Clean(true);

                    string text;
                    var items = app.WaitForErrorListItems(1);
                    Assert.AreEqual(1, items.Count);
                    Assert.AreEqual(0, items[0].get_Text(out text));
                    Assert.IsTrue(text.Contains("native_module"), text);

                    app.Dte.Solution.SolutionBuild.Build(true);

                    items = app.WaitForErrorListItems(0);
                    Assert.AreEqual(0, items.Count);
                } finally {
                    wnd.Close();
                }
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
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\Classification.sln");

                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();

                var doc = app.GetDocument(item.Document.FullName);

                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                var classifier = doc.Classifier;
                var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
                return spans;
            }
        }

        #endregion

    }
}