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
using System.Threading;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Document;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class FormattingUITests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ToggableOptionTest() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);

            PythonToolsPackage.Instance.SetFormattingOption("SpaceBeforeClassDeclarationParen", true);
            bool first = true;
            foreach (var expectedResult in new bool?[] { false, null, true }) {
                var spacingView = app.GetFormattingOptions("Spacing");
                var value = spacingView.WaitForItem("Class Definitions", "Space before the parenthesis in a class declaration");

                Mouse.MoveTo(value.GetClickablePoint());
                if (first) {
                    // first click selects node, but on subsequently bringing up the dialog
                    // the node will still have focus.
                    Mouse.Click(System.Windows.Input.MouseButton.Left); 
                    first = false;
                }
                Mouse.Click(System.Windows.Input.MouseButton.Left); // second click changes value

                Keyboard.Type(System.Windows.Input.Key.Enter);  // commit result

                app.WaitForDialogDismissed();

                Assert.AreEqual(
                    expectedResult,
                    PythonToolsPackage.Instance.GetFormattingOption("SpaceBeforeClassDeclarationParen")
                );
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FormatDocument() {
            FormattingTest("document.py", null, @"# the quick brown fox jumped over the slow lazy dog the quick brown fox jumped
# over the slow lazy dog
def f():
    pass

# short comment
def g():
    pass", new[] { new Span(0, 104) });
        }


        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FormatSelection() {
            FormattingTest("selection.py", new Span(0, 121), @"# the quick brown fox jumped over the slow lazy dog the quick brown fox jumped
# over the slow lazy dog
def f():
    pass

# short comment
def g():
    pass", new [] { new Span(0, 104) });
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void FormatReduceLines() {
            PythonToolsPackage.Instance.SetFormattingOption("SpacesAroundBinaryOperators", true);

            FormattingTest("linereduction.py", null, "(a + b + c + d + e + f)\r\n\r\n", new[] { new Span(0, 41) });
        }

        /// <summary>
        /// Runs a single formatting test
        /// </summary>
        /// <param name="filename">The filename of the document to perform formatting in (lives in FormattingTests.sln)</param>
        /// <param name="selection">The selection to format, or null if formatting the entire document</param>
        /// <param name="expectedText">The expected source code after the formatting</param>
        /// <param name="changedSpans">The spans which should be marked as changed in the buffer after formatting</param>
        private static void FormattingTest(string filename, Span? selection, string expectedText, Span[] changedSpans) {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\FormattingTests\FormattingTests.sln");
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);                
                var doc = app.GetDocument(item.Document.FullName);

                var aggFact = app.ComponentModel.GetService<IViewTagAggregatorFactoryService>();
                var changeTags = aggFact.CreateTagAggregator<ChangeTag>(doc.TextView);

                // format the selection or document
                if (selection == null) {
                    ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Edit.FormatDocument"));
                } else {
                    doc.Invoke(() => doc.TextView.Selection.Select(new SnapshotSpan(doc.TextView.TextBuffer.CurrentSnapshot, selection.Value), false));
                    ThreadPool.QueueUserWorkItem(x => VsIdeTestHostContext.Dte.ExecuteCommand("Edit.FormatSelection"));
                }

                // verify the contents are correct
                string actual = null;
                for (int i = 0; i < 100; i++) {
                    actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

                    if (expectedText == actual) {
                        break;
                    }
                    System.Threading.Thread.Sleep(100);
                }
                Assert.AreEqual(expectedText, actual);

                // verify the change tags are correct
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                var tags = changeTags.GetTags(
                    new SnapshotSpan(
                        doc.TextView.TextBuffer.CurrentSnapshot,
                        new Span(0, doc.TextView.TextBuffer.CurrentSnapshot.Length)
                    )
                );
                List<Span> result = new List<Span>();
                foreach (var tag in tags) {
                    result.Add(
                        new Span(
                            tag.Span.Start.GetPoint(doc.TextView.TextBuffer.CurrentSnapshot, PositionAffinity.Successor).Value.Position,
                            tag.Span.End.GetPoint(doc.TextView.TextBuffer.CurrentSnapshot, PositionAffinity.Successor).Value.Position
                        )
                    );
                }

                // dump the spans for creating tests easier
                foreach (var span in result) {
                    Console.WriteLine(span);
                }

                Assert.AreEqual(result.Count, changedSpans.Length);
                for (int i = 0; i < result.Count; i++) {
                    Assert.AreEqual(result[i], changedSpans[i]);
                }
            } finally {
                window.Document.Close(vsSaveChanges.vsSaveChangesNo);
            }
        }

    }
}
