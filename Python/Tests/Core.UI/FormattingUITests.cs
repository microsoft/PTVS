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

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Document;
using Microsoft.VisualStudio.Text.Tagging;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    public class FormattingUITests {
        private const string FormatDocumentMessage = "Format Document";
        private const string FormatSelectionMessage = "Format Selection - first line selected";
        private const string FormatSelectionNoSelectionMessage = "Format Selection - no selection, caret on first line";

        private PythonVersion Version => PythonPaths.Python37_x64 ?? PythonPaths.Python38_x64;

        public void FormatAutopep8(PythonVisualStudioApp app) {
            var slnPath = PrepareProject(app, Version, "autopep8");

            Console.WriteLine(FormatDocumentMessage);
            FormattingTest(app, slnPath, "document.py", null, @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven, twelve):
    pass


def g(): return 5 ** 2
", new[] { Span.FromBounds(0, 83), Span.FromBounds(93, 214) });

            Console.WriteLine(FormatSelectionMessage);
            FormattingTest(app, slnPath, "document.py", new Span(0, 85), @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven, twelve):
    pass
def g ():    return 5 ** 2
", new[] { Span.FromBounds(0, 83) });

            Console.WriteLine(FormatSelectionNoSelectionMessage);
            FormattingTest(app, slnPath, "document.py", new Span(0, 0), @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven, twelve):
    pass
def g ():    return 5 ** 2
", new[] { Span.FromBounds(0, 83) });
        }

        public void FormatBlack(PythonVisualStudioApp app) {
            var slnPath = PrepareProject(app, Version, "black");

            Console.WriteLine(FormatDocumentMessage);
            FormattingTest(app, slnPath, "document.py", null, @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven, twelve):
    pass


def g():
    return 5 ** 2
", new[] { Span.FromBounds(0, 81), Span.FromBounds(91, 182), Span.FromBounds(93, 219) });
        }

        public void FormatYapf(PythonVisualStudioApp app) {
            var slnPath = PrepareProject(app, Version, "yapf");

            Console.WriteLine(FormatDocumentMessage);
            FormattingTest(app, slnPath, "document.py", null, @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven,
      twelve):
    pass


def g():
    return 5**2
", new[] { Span.FromBounds(0, 90), Span.FromBounds(100, 231) });

            Console.WriteLine(FormatSelectionMessage);
            FormattingTest(app, slnPath, "document.py", new Span(0, 85), @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven,
      twelve):
    pass
def g ():    return 5 ** 2
", new[] { Span.FromBounds(0, 90) });

            Console.WriteLine(FormatSelectionNoSelectionMessage);
            FormattingTest(app, slnPath, "document.py", new Span(0, 0), @"def f(one, two, three, four, five, six, seven, eight, nine, ten, eleven,
      twelve):
    pass
def g ():    return 5 ** 2
", new[] { Span.FromBounds(0, 90) });
        }

        /// <summary>
        /// Runs a single formatting test
        /// </summary>
        /// <param name="filename">The filename of the document to perform formatting in (lives in FormattingTests.sln)</param>
        /// <param name="selection">The selection to format, or null if formatting the entire document</param>
        /// <param name="expectedText">The expected source code after the formatting</param>
        /// <param name="changedSpans">The spans which should be marked as changed in the buffer after formatting</param>
        private static void FormattingTest(
            PythonVisualStudioApp app,
            string slnPath,
            string filename,
            Span? selection,
            string expectedText,
            Span[] changedSpans) {

            var project = app.OpenProject(slnPath);
            var item = project.ProjectItems.Item(filename);
            var window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);

            var aggFact = app.ComponentModel.GetService<IViewTagAggregatorFactoryService>();
            var changeTags = aggFact.CreateTagAggregator<ChangeTag>(doc.TextView);

            // format the selection or document
            if (selection == null) {
                DoFormatDocument(app);
            } else {
                doc.Invoke(() => {
                    if (selection.Value.Length == 0) {
                        doc.TextView.Selection.Clear();
                        doc.TextView.Caret.MoveTo(new SnapshotPoint(doc.TextView.TextBuffer.CurrentSnapshot, selection.Value.Start));
                    } else {
                        doc.TextView.Selection.Select(new SnapshotSpan(doc.TextView.TextBuffer.CurrentSnapshot, selection.Value), false);
                    }
                });
                DoFormatSelection(app);
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

            app.Dte.Solution.Close(false);
        }

        private static string PrepareProject(PythonVisualStudioApp app, PythonVersion python, string formatter) {
            var slnPath = app.CopyProjectForTest(@"TestData\FormattingTests\FormattingTests.sln");
            var projFolder = Path.GetDirectoryName(slnPath);
            var projPath = Path.Combine(projFolder, "FormattingTests.pyproj");

            // The project file has a placeholder for the formatter which we must replace
            var projContents = File.ReadAllText(projPath);
            projContents = projContents.Replace("$$FORMATTER$$", formatter);
            File.WriteAllText(projPath, projContents);

            // The project references a virtual env in 'env' subfolder,
            // which we need to create before opening the project.
            python.CreateVirtualEnv(Path.Combine(projFolder, "env"), new[] { formatter });

            return slnPath;
        }

        private static void DoFormatSelection(VisualStudioApp app) {
            try {
                Task.Factory.StartNew(() => {
                    for (int i = 0; i < 3; i++) {
                        try {
                            // wait for the command to become available if it's not already
                            app.ExecuteCommand("Edit.FormatSelection");
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

        private static void DoFormatDocument(VisualStudioApp app) {
            try {
                Task.Factory.StartNew(() => {
                    for (int i = 0; i < 3; i++) {
                        try {
                            // wait for the command to become available if it's not already
                            app.ExecuteCommand("Edit.FormatDocument");
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
