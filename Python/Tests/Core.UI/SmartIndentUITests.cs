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

extern alias pythontools;
extern alias util;
using System;
using System.Text.RegularExpressions;
using System.Windows;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;
using vsIndentStyle = Microsoft.VisualStudio.TextManager.Interop.vsIndentStyle;

namespace PythonToolsUITests {
    public class SmartIndentUITests {
        public void SmartIndent(PythonVisualStudioApp app) {
            var project = LoadProject(app);

            SmartIndentTest(app, project, "def f():\rprint 'hi'\r\rdef inner(): pass←←←←←←←←←←←←←←←←←\r", @"def f():
            print 'hi'


            def inner(): pass");

            SmartIndentTest(app, project, "x = {'a': [1, 2, 3],\r\r'b':42}", @"x = {'a': [1, 2, 3],

             'b':42}");

            SmartIndentTest(app, project, "x = {  #comment\r'a': [\r1,\r2,\r3\r],\r\r'b':42\r}", @"x = {  #comment
            'a': [
                1,
                2,
                3
                ],

            'b':42
            }");

            SmartIndentTest(app, project, "if True:\rpass\r\r42\r\r", @"if True:
            pass

        42

        ");

            SmartIndentTest(app, project, "def f():\rreturn\r\r42\r\r", @"def f():
            return

        42

        ");

            SmartIndentTest(app, project, "if True: #fob\rpass\relse: #oar\rpass\r\r42\r\r", @"if True: #fob
            pass
        else: #oar
            pass

        42

        ");

            SmartIndentTest(app, project, "if True:\rraise Exception()\r\r42\r\r", @"if True:
            raise Exception()

        42

        ");

            SmartIndentTest(app, project, "while True:\rcontinue\r\r42\r\r", @"while True:
            continue

        42

        ");

            SmartIndentTest(app, project, "while True:\rbreak\r\r42\r\r", @"while True:
            break

        42

        ");

            SmartIndentTest(app, project, "print ('%s, %s' %\r(1, 2))", @"print ('%s, %s' %
               (1, 2))");

            SmartIndentTest(app, project, "def f():\rx = (\r7)\rp", @"def f():
            x = (
                7)
            p");

            SmartIndentTest(app, project, "def f():\rassert False, \\\r'A message'\rp", @"def f():
            assert False, \
                'A message'
            p");

            // other tests...
            SmartIndentTest(app, project, "1 +\\\r2 +\\\r3 +\\\r4 + 5\r", @"1 +\
            2 +\
            3 +\
            4 + 5
        ");


            SmartIndentTest(app, project, "x = {42 :\r42}\rp", @"x = {42 :
             42}
        p");

            SmartIndentTest(app, project, "def f():\rreturn (42,\r100)\r\rp", @"def f():
            return (42,
                    100)

        p");

            SmartIndentTest(app, project, "print ('a',\r'b',\r'c')\rp", @"print ('a',
               'b',
               'c')
        p");

            SmartIndentTest(app, project, "foooo ('a',\r'b',\r'c')\rp", @"foooo ('a',
               'b',
               'c')
        p");

            SmartIndentTest(app, project, "def a():\rif b():\rif c():\rd()\rp", @"def a():
            if b():
                if c():
                    d()
                    p");

            SmartIndentTest(app, project, "a_list = [1, 2, 3]\rdef func():\rpass", @"a_list = [1, 2, 3]
        def func():
            pass");

            SmartIndentTest(app, project, "class A:\rdef funcA(self, a):\rreturn a\r\rdef funcB(self):\rpass", @"class A:
            def funcA(self, a):
                return a

            def funcB(self):
                pass");

            SmartIndentTest(app, project, "print('abc')\rimport sys\rpass", @"print('abc')
        import sys
        pass");

            SmartIndentTest(app, project, "a_list = [1, 2, 3]\rimport sys\rpass", @"a_list = [1, 2, 3]
        import sys
        pass");

            SmartIndentTest(app, project, "class C:\rdef fob(self):\r'doc string'\rpass", @"class C:
            def fob(self):
                'doc string'
                pass");

            SmartIndentTest(app, project, "def g():\rfob(15)\r\r\bfob(1)\rpass", @"def g():
            fob(15)

        fob(1)
        pass");

            SmartIndentTest(app, project, "def m():\rif True:\rpass\relse:\rabc()\r\r\b\bm()\r\rm()\rpass", @"def m():
            if True:
                pass
            else:
                abc()

        m()

        m()
        pass");
        }

        public void SmartIndentExisting(PythonVisualStudioApp app) {
            var project = LoadProject(app);

            SmartIndentExistingTest(app, project, "Decorator.py", 4, 4, @"class C:
            def f(self):
                pass


            @property
            def oar(self):
                pass");

            // New expected behaviors are:
            //      def f():                def f():
            //      |   pass                   |pass
            //
            //                    become
            //      def f():                def f():
            //      
            //      pass                        pass
            SmartIndentExistingTest(app, project, "ClassAndFunc.py", 2, 4, @"class C:
            def f(self):

            pass");

            SmartIndentExistingTest(app, project, "ClassAndFunc.py", 2, 8, @"class C:
            def f(self):

                pass");
        }

        private static void SmartIndentExistingTest(VisualStudioApp app, Project project, string filename, int line, int column, string expectedText) {
            var item = project.ProjectItems.Item(filename);
            if (item.IsOpen) {
                item.Document.ActiveWindow?.Close();
            }
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);
            doc.SetFocus();
            var textLine = doc.TextView.TextViewLines[line];

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                doc.TextView.Caret.MoveTo(textLine.Start + column);
                ((UIElement)doc.TextView).Focus();
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
            Assert.AreEqual(expectedText, actual);
        }

        private static void SmartIndentTest(VisualStudioApp app, Project project, string typedText, string expectedText) {
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            expectedText = Regex.Replace(expectedText, "^\\s+$", "", RegexOptions.Multiline);

            var doc = app.GetDocument(item.Document.FullName);
            doc.SetFocus();

            // A little extra time for things to load, because VS...
            System.Threading.Thread.Sleep(1000);

            Keyboard.Type(typedText);

            string actual = null;
            for (int i = 0; i < 100; i++) {
                actual = Regex.Replace(doc.TextView.TextBuffer.CurrentSnapshot.GetText(),
                    "^\\s+$", "", RegexOptions.Multiline);

                if (expectedText == actual) {
                    break;
                }
                System.Threading.Thread.Sleep(100);
            }
            Assert.AreEqual(expectedText, actual);

            window.Document.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static Project LoadProject(PythonVisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\AutoIndent.sln"));

            var oldState = SetIndentPreference(app, vsIndentStyle.vsIndentStyleSmart);
            app.OnDispose(() => SetIndentPreference(app, oldState));

            return project;
        }

        private static vsIndentStyle SetIndentPreference(VisualStudioApp app, vsIndentStyle style) {
            return app.GetService<UIThreadBase>().Invoke(() => {
                var mgr = app.GetService<IVsTextManager4>(typeof(SVsTextManager));
                LANGPREFERENCES3[] langPrefs = { new LANGPREFERENCES3() };

                langPrefs[0].guidLang = CommonGuidList.guidPythonLanguageServiceGuid;
                ErrorHandler.ThrowOnFailure(mgr.GetUserPreferences4(null, langPrefs, null));
                var old = langPrefs[0].IndentStyle;
                langPrefs[0].IndentStyle = style;
                ErrorHandler.ThrowOnFailure(mgr.SetUserPreferences4(null, langPrefs, null));
                return old;
            });
        }
    }
}
