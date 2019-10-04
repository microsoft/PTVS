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
using System.Windows;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;

namespace PythonToolsUITests {
    public class SignatureHelpUITests {
        public void Signatures(PythonVisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\Signatures.sln"));

            var item = project.ProjectItems.Item("sigs.py");
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);

            doc.SetFocus();

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                doc.TextView.Caret.MoveTo(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, doc.TextView.TextBuffer.CurrentSnapshot.Length));
                ((UIElement)doc.TextView).Focus();
            }));

            //doc.WaitForAnalysisAtCaretAsync().WaitAndUnwrapExceptions();

            Keyboard.Type("f");
            Keyboard.Type(System.Windows.Input.Key.Escape); // for now, to dismiss completion list
            System.Threading.Thread.Sleep(500);
            Keyboard.Type("(");

            using (var sh = doc.WaitForSession<ISignatureHelpSession>()) {
                var session = sh.Session;
                Assert.IsNotNull(session, "No session active");
                Assert.IsNotNull(session.SelectedSignature, "No signature selected");

                WaitForCurrentParameter(session, "a");
                Assert.AreEqual("a", session.SelectedSignature.CurrentParameter.Name);
                window.Activate();

                System.Threading.Thread.Sleep(500);
                Keyboard.Type("1,");

                WaitForCurrentParameter(session, "b");
                Assert.AreEqual("b", session.SelectedSignature.CurrentParameter.Name);
                window.Activate();

                System.Threading.Thread.Sleep(500);
                Keyboard.Type("2,");

                WaitForCurrentParameter(session, "c");
                Assert.AreEqual("c", session.SelectedSignature.CurrentParameter.Name);
                window.Activate();

                System.Threading.Thread.Sleep(500);
                Keyboard.Type("3,");

                WaitForCurrentParameter(session, "d");
                Assert.AreEqual("d", session.SelectedSignature.CurrentParameter.Name);
                window.Activate();

                System.Threading.Thread.Sleep(500);
                Keyboard.Type("4,");

                WaitForNoCurrentParameter(session);
                Assert.AreEqual(null, session.SelectedSignature.CurrentParameter);

                System.Threading.Thread.Sleep(500);
                Keyboard.Press(System.Windows.Input.Key.Left);
                WaitForCurrentParameter(session);
                Assert.AreEqual("d", session.SelectedSignature.CurrentParameter.Name);

                //Keyboard.Backspace();
                //WaitForCurrentParameter(session);
                //Assert.AreEqual("d", session.SelectedSignature.CurrentParameter.Name);
            }
        }

        public void SignaturesByName(PythonVisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\Signatures.sln"));

            var item = project.ProjectItems.Item("sigs.py");
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);

            doc.SetFocus();

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                doc.TextView.Caret.MoveTo(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, doc.TextView.TextBuffer.CurrentSnapshot.Length));
                ((UIElement)doc.TextView).Focus();
            }));

            //doc.WaitForAnalysisAtCaretAsync().WaitAndUnwrapExceptions();

            Keyboard.Type("f");
            System.Threading.Thread.Sleep(500);
            Keyboard.Type("(");

            using (var sh = doc.WaitForSession<ISignatureHelpSession>()) {
                var session = sh.Session;
                Assert.IsNotNull(session, "No session active");
                Assert.IsNotNull(session.SelectedSignature, "No signature selected");

                WaitForCurrentParameter(session, "a");
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

        public void SignaturesMultiLine(PythonVisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\Signatures.sln"));

            var item = project.ProjectItems.Item("multilinesigs.py");
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);
            doc.SetFocus();

            ((UIElement)doc.TextView).Dispatcher.Invoke((Action)(() => {
                var point = doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(5 - 1).Start;
                doc.TextView.Caret.MoveTo(point);
                ((UIElement)doc.TextView).Focus();
            }));

            //doc.WaitForAnalysisAtCaretAsync().WaitAndUnwrapExceptions();

            app.ExecuteCommand("Edit.ParameterInfo");

            using (var sh = doc.WaitForSession<ISignatureHelpSession>()) {
                var session = sh.Session;
                Assert.IsNotNull(session, "No session active");
                Assert.IsNotNull(session.SelectedSignature, "No signature selected");

                WaitForCurrentParameter(session);
                Assert.AreEqual("b", session.SelectedSignature.CurrentParameter.Name);
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
    }
}
