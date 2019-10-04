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
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudioTools;
using pythontools::Microsoft.PythonTools;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;

namespace PythonToolsUITests {
    public class BraceCompletionUITests {
        public void BraceCompletion(PythonVisualStudioApp app) {
            var project = LoadProject(app);

            // Braces get auto completed
            AutoBraceCompletionTest(app, project, "foo(", "foo()");
            AutoBraceCompletionTest(app, project, "foo[", "foo[]");
            AutoBraceCompletionTest(app, project, "foo{", "foo{}");
            AutoBraceCompletionTest(app, project, "\"foo", "\"foo\"");
            AutoBraceCompletionTest(app, project, "'foo", "'foo'");
        }

        public void BraceCompletionEndBracesSkipped(PythonVisualStudioApp app) {
            var project = LoadProject(app);

            // End braces get skipped
            AutoBraceCompletionTest(app, project, "foo(bar)", "foo(bar)");
            AutoBraceCompletionTest(app, project, "foo[bar]", "foo[bar]");
            AutoBraceCompletionTest(app, project, "foo{bar}", "foo{bar}");
            AutoBraceCompletionTest(app, project, "\"foo\"", "\"foo\"");
            AutoBraceCompletionTest(app, project, "'foo'", "'foo'");
            AutoBraceCompletionTest(app, project, "foo({[\"\"]})", "foo({[\"\"]})");
        }

        public void BraceCompletionInsideCommentsAndStrings(PythonVisualStudioApp app) {
            var project = LoadProject(app);

            // Quotes do not get autocompleted in comments and strings
            AutoBraceCompletionTest(app, project, "#\"", "#\"");
            AutoBraceCompletionTest(app, project, "#'", "#'");
            AutoBraceCompletionTest(app, project, "\"'", "\"'\"");
            AutoBraceCompletionTest(app, project, "'\"", "'\"'");

            // Braces get autocompleted in comments and strings
            AutoBraceCompletionTest(app, project, "\"foo(", "\"foo()\"");
            AutoBraceCompletionTest(app, project, "#foo(", "#foo()");

            // Triple quotes
            AutoBraceCompletionTest(app, project, "'''", "'''");
            AutoBraceCompletionTest(app, project, "\"\"\"", "\"\"\"");
        }

        private static Project LoadProject(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AutomaticBraceCompletion.sln");

            bool oldState = SetBraceCompletionPreference(app, true);
            app.OnDispose(() => SetBraceCompletionPreference(app, oldState));

            return project;
        }

        private static void AutoBraceCompletionTest(VisualStudioApp app, Project project, string typedText, string expectedText) {
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

        private static bool SetBraceCompletionPreference(VisualStudioApp app, bool enable) {
            return app.GetService<UIThreadBase>().Invoke(() => {
                var mgr = app.GetService<IVsTextManager4>(typeof(SVsTextManager));
                LANGPREFERENCES3[] langPrefs = { new LANGPREFERENCES3() };

                langPrefs[0].guidLang = GuidList.guidPythonLanguageServiceGuid;
                ErrorHandler.ThrowOnFailure(mgr.GetUserPreferences4(null, langPrefs, null));
                bool old = langPrefs[0].fBraceCompletion != 0;
                langPrefs[0].fBraceCompletion = enable ? 1u : 0u;
                ErrorHandler.ThrowOnFailure(mgr.SetUserPreferences4(null, langPrefs, null));
                return old;
            });
        }
    }
}
