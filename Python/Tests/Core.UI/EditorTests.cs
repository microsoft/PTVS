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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using EnvDTE;
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Editor;
using pythontools::Microsoft.PythonTools.Intellisense;
using TestUtilities;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;

namespace PythonToolsUITests {
    public class EditorTests {
        //        public void CompletionsCaseSensitive(PythonVisualStudioApp app) {
        //            // http://pytools.codeplex.com/workitem/457
        //            var project = app.OpenProject(@"TestData\Completions.sln");

        //            var item = project.ProjectItems.Item("oar.py");
        //            var window = item.Open();
        //            window.Activate();

        //            var doc = app.GetDocument(item.Document.FullName);
        //            System.Threading.Thread.Sleep(1000);

        //            doc.Type("from fob import ba");
        //            using (doc.WaitForSession<ICompletionSession>()) {
        //                doc.Type("\r");
        //            }

        //            doc.WaitForText("from fob import baz");
        //            doc.Type("\r");

        //            doc.Type("from fob import Ba");
        //            using (doc.WaitForSession<ICompletionSession>()) {
        //                doc.Type("\r");
        //            }
        //            doc.WaitForText("from fob import baz\r\nfrom fob import Baz");
        //        }

        //        public void TypingTest(PythonVisualStudioApp app) {
        //            var project = app.OpenProject(@"TestData\EditorTests.sln");

        //            // http://pytools.codeplex.com/workitem/139
        //            TypingTest(app, project, "DecoratorOnFunction.py", 0, 0, @"@classmethod
        //def f(): pass
        //", () => {
        //                Keyboard.Type("\r");
        //                Keyboard.Type("↑");
        //                Keyboard.Type("@@");
        //                System.Threading.Thread.Sleep(5000);
        //                Keyboard.Backspace();
        //                Keyboard.Type("classmethod");
        //                System.Threading.Thread.Sleep(5000);
        //            });

        //            // http://pytools.codeplex.com/workitem/151
        //            TypingTest(app, project, "DecoratorInClass.py", 1, 4, @"class C:
        //    @classmethod
        //    def f(self):
        //        pass
        //", () => {
        //                Keyboard.Type("@");
        //                System.Threading.Thread.Sleep(5000);
        //                Keyboard.Type("classmethod");
        //                System.Threading.Thread.Sleep(5000);

        //                // VS Bug 
        //                // 72635 Exception occurrs and you're not prompted to save file when you close it while completion list is up. 
        //                Keyboard.Type(System.Windows.Input.Key.Escape);
        //            });
        //        }

        //        public void CompletionTests(PythonVisualStudioApp app) {
        //            var project = app.OpenProject(@"TestData\EditorTests.sln");

        //            TypingTest(app, project, "BackslashCompletion.py", 2, 0, @"x = 42
        //x\
        //.conjugate", () => {
        //                Keyboard.Type(".con\t");
        //            });
        //        }

        //        /// <summary>
        //        /// Single auto indent test
        //        /// </summary>
        //        /// <param name="project">containting project</param>
        //        /// <param name="filename">filename in the project</param>
        //        /// <param name="line">zero-based line</param>
        //        /// <param name="column">zero based column</param>
        //        /// <param name="expectedText"></param>
        //        private static void TypingTest(VisualStudioApp app, Project project, string filename, int line, int column, string expectedText, Action typing) {
        //            var item = project.ProjectItems.Item(filename);
        //            var window = item.Open();
        //            window.Activate();

        //            var doc = app.GetDocument(item.Document.FullName);
        //            doc.SetFocus();
        //            var textLine = doc.TextView.TextViewLines[line];

        //            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
        //                try {
        //                    doc.TextView.Caret.MoveTo(textLine.Start + column);
        //                    ((UIElement)doc.TextView).Focus();
        //                } catch (Exception) {
        //                    Debug.Fail("Bad position for moving caret");
        //                }
        //            }));

        //            doc.InvokeTask(() => doc.WaitForAnalysisAtCaretAsync());

        //            typing();

        //            string actual = null;
        //            for (int i = 0; i < 100; i++) {
        //                actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

        //                if (expectedText == actual) {
        //                    break;
        //                }
        //                System.Threading.Thread.Sleep(100);
        //            }
        //            Assert.AreEqual(expectedText, actual);
        //        }

        public void OpenInvalidUnicodeFile(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\ErrorProjectUnicode.sln");
            var item = project.ProjectItems.Item("Program.py");
            var windowTask = Task.Run(() => item.Open());

            app.CheckMessageBox(TestUtilities.MessageBoxButton.Ok, "File Load", "Program.py", "Unicode (UTF-8) encoding");

            var window = windowTask.Result;
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);
            var text = doc.TextView.TextBuffer.CurrentSnapshot.GetText();
            Console.WriteLine(string.Join(" ", text.Select(c => c < ' ' ? " .  " : string.Format(" {0}  ", c))));
            Console.WriteLine(string.Join(" ", text.Select(c => string.Format("{0:X04}", (int)c))));
            // Characters should have been replaced
            Assert.AreNotEqual(-1, text.IndexOf("\uFFFD\uFFFD\uFFFD\uFFFD", StringComparison.Ordinal));
        }

        public void IndentationInconsistencyWarning(PythonVisualStudioApp app) {
            //var oldSuppress = VsProjectAnalyzer.SuppressTaskProvider;
            //app.OnDispose(() => VsProjectAnalyzer.SuppressTaskProvider = oldSuppress);
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

        public void IndentationInconsistencyError(PythonVisualStudioApp app) {
            //var oldSuppress = VsProjectAnalyzer.SuppressTaskProvider;
            //app.OnDispose(() => VsProjectAnalyzer.SuppressTaskProvider = oldSuppress);
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

        public void IndentationInconsistencyIgnore(PythonVisualStudioApp app) {
            //var oldSuppress = VsProjectAnalyzer.SuppressTaskProvider;
            //app.OnDispose(() => VsProjectAnalyzer.SuppressTaskProvider = oldSuppress);
            var options = app.Options;
            var severity = options.IndentationInconsistencySeverity;
            options.IndentationInconsistencySeverity = Severity.Suppressed;
            app.OnDispose(() => options.IndentationInconsistencySeverity = severity);

            var project = app.OpenProject(@"TestData\InconsistentIndentation.sln");

            List<IVsTaskItem> items = app.WaitForErrorListItems(0);
            Assert.AreEqual(0, items.Count);
        }

        private static void SquiggleShowHide(PythonVisualStudioApp app, string document, Action test) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\MissingImport.sln"));

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
                test();
            } finally {
                wnd.Close();
            }
        }

        public void ImportPresent(PythonVisualStudioApp app) {
            SquiggleShowHide(app, "ImportPresent.py", () => {
                var items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);
            });
        }

        public void ImportSelf(PythonVisualStudioApp app) {
            SquiggleShowHide(app, "ImportSelf.py", () => {
                var items = app.WaitForErrorListItems(0);
                Assert.AreEqual(0, items.Count);
            });
        }

        public void ImportMissing(PythonVisualStudioApp app) {
            SquiggleShowHide(app, "ImportMissing.py", () => {
                var items = app.WaitForErrorListItems(1);
                Assert.AreEqual(1, items.Count);
                Assert.AreEqual(0, items[0].get_Text(out var text));
                Assert.IsTrue(text.Contains("AbsentModule"), text);
            });
        }

        public void ImportMissingThenAddFile(PythonVisualStudioApp app) {
            SquiggleShowHide(app, "ImportMissing.py", () => {
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
            });
        }
    }
}