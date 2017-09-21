extern alias analysis;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using analysis::Microsoft.PythonTools.Parsing;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Document;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudioTools.VSTestHost;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    //[TestClass]
    public class FormattingUITests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void ToggleableOptionTest() {
            using (var app = new PythonVisualStudioApp()) {
                app.PythonToolsService.SetFormattingOption("SpaceBeforeClassDeclarationParen", true);
                foreach (var expectedResult in new bool?[] { false, null, true }) {
                    using (var dialog = ToolsOptionsDialog.FromDte(app)) {
                        dialog.SelectedView = "Text Editor/Python/Formatting/Spacing";
                        var spacingView = FormattingOptionsTreeView.FromDialog(dialog);

                        var value = spacingView.WaitForItem(
                            "Class Definitions",
                            "Insert space between a class declaration's name and bases list"
                        );
                        Assert.IsNotNull(value, "Did not find item");

                        value.SetFocus();
                        Mouse.MoveTo(value.GetClickablePoint());
                        Mouse.Click(System.Windows.Input.MouseButton.Left);

                        dialog.OK();

                        Assert.AreEqual(
                            expectedResult,
                            app.PythonToolsService.GetFormattingOption("SpaceBeforeClassDeclarationParen")
                        );
                    }
                }
            }
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FormatDocument() {
            FormattingTest("document.py", null, @"# the quick brown fox jumped over the slow lazy dog the quick brown fox jumped
# over the slow lazy dog
def f():
    pass

# short comment
def g():
    pass", new[] { Span.FromBounds(0, 78), Span.FromBounds(80, 186) }, null, null);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FormatAsyncDocument() {
            FormattingTest("async.py", null, @"async  def f(x):
    async  for  i in await  x:
        pass
    # comment before
    async   with x:
        pass
    [  x   async for x  in await    x]", new Span[] { }, null, null, new Version(3, 6));
        }


        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FormatSelection() {
            FormattingTest("selection.py", new Span(0, 121), @"# the quick brown fox jumped over the slow lazy dog the quick brown fox jumped
# over the slow lazy dog
def f():
    pass

# short comment
def g():
    pass", new[] { Span.FromBounds(0, 78), Span.FromBounds(80, 186) }, null, null);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FormatSelectionNoSelection() {
            FormattingTest("selection2.py", new Span(5, 0), @"x=1

y=2

z=3", new Span[0], null, null);
        }

        //[TestMethod, Priority(1)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void FormatReduceLines() {
            FormattingTest(
                "linereduction.py",
                null,
                "(a + b + c + d + e + f)\r\n",
                new[] { new Span(0, 23), Span.FromBounds(25, 50) },
                s => {
                    var v = s.GetFormattingOption("SpacesAroundBinaryOperators");
                    s.SetFormattingOption("SpacesAroundBinaryOperators", true);
                    return v;
                },
                (s, v) => s.SetFormattingOption("SpacesAroundBinaryOperators", v)
            );
        }

        /// <summary>
        /// Runs a single formatting test
        /// </summary>
        /// <param name="filename">The filename of the document to perform formatting in (lives in FormattingTests.sln)</param>
        /// <param name="selection">The selection to format, or null if formatting the entire document</param>
        /// <param name="expectedText">The expected source code after the formatting</param>
        /// <param name="changedSpans">The spans which should be marked as changed in the buffer after formatting</param>
        private static void FormattingTest(
            string filename,
            Span? selection,
            string expectedText,
            Span[] changedSpans,
            Func<PythonToolsService, object> updateSettings,
            Action<PythonToolsService, object> revertSettings,
            Version version = null
        ) {
            using (var app = new PythonVisualStudioApp())
            using (version == null ? null : app.SelectDefaultInterpreter(PythonPaths.Versions.FirstOrDefault(v => v.Version.ToVersion() >= version))) {
                var o = updateSettings?.Invoke(app.PythonToolsService);
                if (revertSettings != null) {
                    app.OnDispose(() => revertSettings(app.PythonToolsService, o));
                }

                var project = app.OpenProject(@"TestData\FormattingTests\FormattingTests.sln");
                var item = project.ProjectItems.Item(filename);
                var window = item.Open();
                window.Activate();
                var doc = app.GetDocument(item.Document.FullName);

                var aggFact = app.ComponentModel.GetService<IViewTagAggregatorFactoryService>();
                var changeTags = aggFact.CreateTagAggregator<ChangeTag>(doc.TextView);

                // format the selection or document
                if (selection == null) {
                    DoFormatDocument();
                } else {
                    doc.Invoke(() => doc.TextView.Selection.Select(new SnapshotSpan(doc.TextView.TextBuffer.CurrentSnapshot, selection.Value), false));
                    DoFormatSelection();
                }

                // verify the contents are correct
                string actual = null;
                int steady = 50;
                for (int i = 0; i < 100; i++) {
                    actual = doc.TextView.TextBuffer.CurrentSnapshot.GetText();

                    if (expectedText == actual) {
                        if (--steady <= 0) {
                            break;
                        }
                    } else {
                        steady = 50;
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
            }
        }

        private static void DoFormatSelection() {
            try {
                Task.Factory.StartNew(() => {
                    for (int i = 0; i < 3; i++) {
                        try {
                            // wait for the command to become available if it's not already
                            VSTestContext.DTE.ExecuteCommand("Edit.FormatSelection");
                            return;
                        } catch {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                    throw new Exception();
                }).Wait();
            } catch {
                Assert.Fail("Failed to format selection");
            }
        }

        private static void DoFormatDocument() {
            try {
                Task.Factory.StartNew(() => {
                    for (int i = 0; i < 3; i++) {
                        try {
                            // wait for the command to become available if it's not already
                            VSTestContext.DTE.ExecuteCommand("Edit.FormatDocument");
                            return;
                        } catch {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                    throw new Exception();
                }).Wait();
            } catch {
                Assert.Fail("Failed to format document");
            }
        }
    }
}
