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

using AnalysisTest.UI;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using TestUtilities;

namespace AnalysisTest.ProjectSystem {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class DjangoEditingTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion1() {
            InsertionTest("Insertion1.html.djt", 8, 10, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("operator", 71, 81, "{{ faoo }}"),
                new Classifcation("operator", 85, 94, "{{ foo }}"),
                new Classifcation("HTML Tag Delimiter", 96, 98, "</"),
                new Classifcation("HTML Element Name", 98, 102, "body"),
                new Classifcation("HTML Tag Delimiter", 102, 103, ">"),
                new Classifcation("HTML Tag Delimiter", 105, 107, "</"),
                new Classifcation("HTML Element Name", 107, 111, "html"),
                new Classifcation("HTML Tag Delimiter", 111, 112, ">")
            );

            InsertionTest("Insertion1.html.djt", 8, 10, "}aaa",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("operator", 71, 81, "{{ faoo }}"),
                new Classifcation("operator", 86, 95, "{{ foo }}"),
                new Classifcation("HTML Tag Delimiter", 97, 99, "</"),
                new Classifcation("HTML Element Name", 99, 103, "body"),
                new Classifcation("HTML Tag Delimiter", 103, 104, ">"),
                new Classifcation("HTML Tag Delimiter", 106, 108, "</"),
                new Classifcation("HTML Element Name", 108, 112, "html"),
                new Classifcation("HTML Tag Delimiter", 112, 113, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion2() {
            InsertionDeletionTest("Insertion2.html.djt", 9, 34, "{",
                new Classifcation[] {
                    new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                    new Classifcation("HTML Element Name", 1, 5, "html"),
                    new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                    new Classifcation("operator", 8, 15, "{{   }}"),
                    new Classifcation("HTML Tag Delimiter", 17, 18, "<"),
                    new Classifcation("HTML Element Name", 18, 22, "head"),
                    new Classifcation("HTML Tag Delimiter", 22, 24, "><"),
                    new Classifcation("HTML Element Name", 24, 29, "title"),
                    new Classifcation("HTML Tag Delimiter", 29, 32, "></"),
                    new Classifcation("HTML Element Name", 32, 37, "title"),
                    new Classifcation("HTML Tag Delimiter", 37, 40, "></"),
                    new Classifcation("HTML Element Name", 40, 44, "head"),
                    new Classifcation("HTML Tag Delimiter", 44, 45, ">"),
                    new Classifcation("HTML Tag Delimiter", 49, 50, "<"),
                    new Classifcation("HTML Element Name", 50, 54, "body"),
                    new Classifcation("HTML Tag Delimiter", 54, 55, ">"),
                    new Classifcation("HTML Tag Delimiter", 57, 58, "<"),
                    new Classifcation("HTML Element Name", 58, 64, "script"),
                    new Classifcation("HTML Tag Delimiter", 64, 65, ">"),
                    new Classifcation("HTML Tag Delimiter", 67, 69, "</"),
                    new Classifcation("HTML Element Name", 69, 75, "script"),
                    new Classifcation("HTML Tag Delimiter", 75, 76, ">"),
                    new Classifcation("operator", 96, 108, "{{ faoo aa}}"),
                    new Classifcation("operator", 113, 122, "{{ foo }}"),
                    new Classifcation("HTML Tag Delimiter", 124, 126, "</"),
                    new Classifcation("HTML Element Name", 126, 130, "body"),
                    new Classifcation("HTML Tag Delimiter", 130, 131, ">"),
                    new Classifcation("HTML Tag Delimiter", 133, 135, "</"),
                    new Classifcation("HTML Element Name", 135, 139, "html"),
                    new Classifcation("HTML Tag Delimiter", 139, 140, ">")
                },
                new Classifcation[]     {
                    new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                    new Classifcation("HTML Element Name", 1, 5, "html"),
                    new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                    new Classifcation("operator", 8, 15, "{{   }}"),
                    new Classifcation("HTML Tag Delimiter", 17, 18, "<"),
                    new Classifcation("HTML Element Name", 18, 22, "head"),
                    new Classifcation("HTML Tag Delimiter", 22, 24, "><"),
                    new Classifcation("HTML Element Name", 24, 29, "title"),
                    new Classifcation("HTML Tag Delimiter", 29, 32, "></"),
                    new Classifcation("HTML Element Name", 32, 37, "title"),
                    new Classifcation("HTML Tag Delimiter", 37, 40, "></"),
                    new Classifcation("HTML Element Name", 40, 44, "head"),
                    new Classifcation("HTML Tag Delimiter", 44, 45, ">"),
                    new Classifcation("HTML Tag Delimiter", 49, 50, "<"),
                    new Classifcation("HTML Element Name", 50, 54, "body"),
                    new Classifcation("HTML Tag Delimiter", 54, 55, ">"),
                    new Classifcation("HTML Tag Delimiter", 57, 58, "<"),
                    new Classifcation("HTML Element Name", 58, 64, "script"),
                    new Classifcation("HTML Tag Delimiter", 64, 65, ">"),
                    new Classifcation("HTML Tag Delimiter", 67, 69, "</"),
                    new Classifcation("HTML Element Name", 69, 75, "script"),
                    new Classifcation("HTML Tag Delimiter", 75, 76, ">"),
                    new Classifcation("operator", 96, 108, "{{ faoo aa}}"),
                    new Classifcation("operator", 113, 122, "{{ foo }}"),
                    new Classifcation("HTML Tag Delimiter", 124, 126, "</"),
                    new Classifcation("HTML Element Name", 126, 130, "body"),
                    new Classifcation("HTML Tag Delimiter", 130, 131, ">"),
                    new Classifcation("HTML Tag Delimiter", 133, 135, "</"),
                    new Classifcation("HTML Element Name", 135, 139, "html"),
                    new Classifcation("HTML Tag Delimiter", 139, 140, ">")
                }
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion3() {
            InsertionTest("Insertion3.html.djt", 2, 5, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("operator", 8, 13, "{{ }}")
            );

        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion4() {
            InsertionTest("Insertion4.html.djt", 1, 1, "{",
                new Classifcation("operator", 0, 12, "{{<html>\r\n}}")
            );

            InsertionTest("Insertion4.html.djt", 1, 2, "{",
                new Classifcation("operator", 0, 12, "{{<html>\r\n}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion5() {
            InsertionTest("Insertion5.html.djt", 1, 2, "#",
                new Classifcation("operator", 0, 13, "{#{<html>\r\n#}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion6() {
            InsertionTest("Insertion6.html.djt", 1, 4, "a",
                new Classifcation("operator", 4, 18, "{{<html>\r\n\r\n}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion7() {
            InsertionTest("Insertion7.html.djt", 1, 16, "{",
                new Classifcation("operator", 0, 12, "{{{  aaa{ }}"),
                new Classifcation("operator", 15, 30, "{{{<html>\r\n\r\n}}"),
                new Classifcation("HTML Tag Delimiter", 38, 39, "<"),
                new Classifcation("HTML Element Name", 39, 42, "foo"),
                new Classifcation("HTML Tag Delimiter", 42, 43, ">"),
                new Classifcation("operator", 49, 63, "{{<html>\r\n\r\n}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion8() {
            InsertionTest("Insertion8.html.djt", 2, 9, "}",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("operator", 8, 17, "{{ foo }}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion9() {
            InsertionTest("Insertion9.html.djt", 1, 7, "a",
                new Classifcation("operator", 4, 19, "{{a<html>\r\n\r\n}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Insertion10() {
            InsertionTest("Insertion10.html.djt", 7, 10, "a",
                new Classifcation("HTML Tag Delimiter", 0, 1, "<"),
                new Classifcation("HTML Element Name", 1, 5, "html"),
                new Classifcation("HTML Tag Delimiter", 5, 6, ">"),
                new Classifcation("HTML Tag Delimiter", 8, 9, "<"),
                new Classifcation("HTML Element Name", 9, 13, "head"),
                new Classifcation("HTML Tag Delimiter", 13, 15, "><"),
                new Classifcation("HTML Element Name", 15, 20, "title"),
                new Classifcation("HTML Tag Delimiter", 20, 23, "></"),
                new Classifcation("HTML Element Name", 23, 28, "title"),
                new Classifcation("HTML Tag Delimiter", 28, 31, "></"),
                new Classifcation("HTML Element Name", 31, 35, "head"),
                new Classifcation("HTML Tag Delimiter", 35, 36, ">"),
                new Classifcation("HTML Tag Delimiter", 40, 41, "<"),
                new Classifcation("HTML Element Name", 41, 45, "body"),
                new Classifcation("HTML Tag Delimiter", 45, 46, ">"),
                new Classifcation("HTML Tag Delimiter", 48, 49, "<"),
                new Classifcation("HTML Element Name", 49, 55, "script"),
                new Classifcation("HTML Tag Delimiter", 55, 56, ">"),
                new Classifcation("HTML Tag Delimiter", 58, 60, "</"),
                new Classifcation("HTML Element Name", 60, 66, "script"),
                new Classifcation("HTML Tag Delimiter", 66, 67, ">"),
                new Classifcation("operator", 72, 81, "{{ foo }}"),
                new Classifcation("operator", 84, 106, "{{ faoo }aaa {{ foo }}"),
                new Classifcation("HTML Tag Delimiter", 108, 110, "</"),
                new Classifcation("HTML Element Name", 110, 114, "body"),
                new Classifcation("HTML Tag Delimiter", 114, 115, ">"),
                new Classifcation("HTML Tag Delimiter", 117, 119, "</"),
                new Classifcation("HTML Element Name", 119, 123, "html"),
                new Classifcation("HTML Tag Delimiter", 123, 124, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Deletion1() {
            DeletionTest("Deletion1.html.djt", 1, 2, 1,
                new Classifcation("operator", 0, 14, "{{<html>\r\n\r\n}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 3, 1,
                new Classifcation("operator", 0, 14, "{{<html>\r\n\r\n}}")
            );

            DeletionTest("Deletion1.html.djt", 1, 4, 1,
                new Classifcation("operator", 0, 14, "{{<html>\r\n\r\n}}")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void Paste1() {
            PasteTest("Paste1.html.djt", 1, 2, "{{foo}}", "{{bazz}}",
                new Classifcation("HTML Tag Delimiter", 18, 19, "<"),
                new Classifcation("HTML Element Name", 19, 22, "foo"),
                new Classifcation("HTML Tag Delimiter", 22, 23, ">")
            );
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed1() {
            SelectAllAndDeleteTest("SelectAllMixed1.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed2() {
            SelectAllAndDeleteTest("SelectAllMixed2.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed3() {
            SelectAllAndDeleteTest("SelectAllMixed3.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllMixed4() {
            SelectAllAndDeleteTest("SelectAllMixed4.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllTag() {
            SelectAllAndDeleteTest("SelectAllTag.html.djt");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SelectAllText() {
            SelectAllAndDeleteTest("SelectAllText.html.djt");
        }

        private static void SelectAllAndDeleteTest(string filename) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);

            item.Invoke(() => {
                using (var edit = item.TextView.TextBuffer.CreateEdit()) {
                    edit.Delete(new Span(0, item.TextView.TextBuffer.CurrentSnapshot.Length));
                    edit.Apply();
                }
            });

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(spans);
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void DeletionTest(string filename, int line, int column, int deletionCount, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            for (int i = 0; i < deletionCount; i++) {
                Keyboard.Backspace();
            }

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void PasteTest(string filename, int line, int column, string selectionText, string pasteText, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;

            var selectionStart = snapshot.GetText().IndexOf(selectionText);
            item.Invoke(() => {
                item.TextView.Selection.Select(new SnapshotSpan(item.TextView.TextBuffer.CurrentSnapshot, new Span(selectionStart, selectionText.Length)), false);
                System.Windows.Clipboard.SetText(pasteText);
            });

            Keyboard.ControlV();

            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void InsertionTest(string filename, int line, int column, string insertionText, params Classifcation[] expected) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            Keyboard.Type(insertionText);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expected
            );
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static void InsertionDeletionTest(string filename, int line, int column, string insertionText, Classifcation[] expectedFirst, Classifcation[] expectedAfter) {
            Window window;
            var item = OpenDjangoProjectItem(filename, out window);
            item.MoveCaret(line, column);
            Keyboard.Type(insertionText);

            var snapshot = item.TextView.TextBuffer.CurrentSnapshot;
            var classifier = item.Classifier;
            var spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expectedFirst
            );

            for (int i = 0; i < insertionText.Length; i++) {
                Keyboard.Backspace();
            }

            spans = classifier.GetClassificationSpans(new SnapshotSpan(snapshot, 0, snapshot.Length));
            EditorTests.VerifyClassification(
                spans,
                expectedAfter
            );
            
            window.Close(vsSaveChanges.vsSaveChangesNo);
        }

        private static EditorWindow OpenDjangoProjectItem(string startItem, out Window window) {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\DjangoEditProject.sln", startItem);

            var item = project.ProjectItems.Item(startItem);
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            window = item.Open();
            window.Activate();
            var doc = app.GetDocument(item.Document.FullName);

            return doc;
        }
    }
}
