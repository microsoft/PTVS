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
using System.IO;
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using TestUtilities.UI.Python;

namespace PythonToolsUITests {
    [TestClass]
    public class ErrorListTaskListTests : SharedProjectTest {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        internal struct TaskItemInfo {
            public int Line, Column;
            public string Document, Message;
            public VSTASKCATEGORY Category;
            public VSTASKPRIORITY Priority;
            public __VSERRORCATEGORY? ErrorCategory;

            public TaskItemInfo(string document, int line, int column, VSTASKPRIORITY priority, VSTASKCATEGORY category, __VSERRORCATEGORY? errorCategory, string message) {
                Document = document;
                Line = line;
                Column = column;
                Priority = priority;
                Category = category;
                ErrorCategory = errorCategory;
                Message = message;
            }

            public TaskItemInfo(IVsTaskItem taskItem) {
                ErrorHandler.ThrowOnFailure(taskItem.Document(out Document));
                ErrorHandler.ThrowOnFailure(taskItem.get_Text(out Message));
                ErrorHandler.ThrowOnFailure(taskItem.Line(out Line));
                ErrorHandler.ThrowOnFailure(taskItem.Column(out Column));

                var priority = new VSTASKPRIORITY[1];
                ErrorHandler.ThrowOnFailure(taskItem.get_Priority(priority));
                Priority = priority[0];

                var category = new VSTASKCATEGORY[1];
                ErrorHandler.ThrowOnFailure(taskItem.Category(category));
                Category = category[0];

                var errorItem = taskItem as IVsErrorItem;
                if (errorItem != null) {
                    uint errorCategory;
                    ErrorHandler.ThrowOnFailure(errorItem.GetCategory(out errorCategory));
                    ErrorCategory = (__VSERRORCATEGORY)errorCategory;
                } else {
                    ErrorCategory = null;
                }
            }

            public override string ToString() {
                var errorCategory = ErrorCategory != null ? "__VSERRORCATEGORY." + ErrorCategory : "null";
                return string.Format("new TaskItemInfo(\"{0}\", {1}, {2}, VSTASKPRIORITY.{3}, VSTASKCATEGORY.{4}, {5}, \"{6}\")",
                    Document, Line, Column, Priority, Category, errorCategory, Message);
            }
        }

        internal void TaskListTest(VisualStudioApp app, Type taskListService, IList<TaskItemInfo> expectedItems, int[] navigateTo = null) {
            var items = app.WaitForTaskListItems(taskListService, expectedItems.Count);
            var actualItems = items.Select(item => new TaskItemInfo(item)).ToList();

            Assert.AreEqual(expectedItems.Count, actualItems.Count);
            AssertUtil.ContainsExactly(actualItems, expectedItems.ToSet());

            if (navigateTo != null) {
                foreach (var i in navigateTo) {
                    Console.WriteLine("Trying to navigate to " + expectedItems[i]);

                    var j = actualItems.IndexOf(expectedItems[i]);
                    Assert.IsTrue(j >= 0);
                    app.ServiceProvider.GetUIThread().Invoke((Action)delegate { items[j].NavigateTo(); });

                    var doc = app.Dte.ActiveDocument;
                    Assert.IsNotNull(doc);
                    Assert.AreEqual(expectedItems[i].Document, doc.FullName);

                    var textDoc = (EnvDTE.TextDocument)doc.Object("TextDocument");
                    Assert.AreEqual(expectedItems[i].Line + 1, textDoc.Selection.ActivePoint.Line);
                    Assert.AreEqual(expectedItems[i].Column + 1, textDoc.Selection.ActivePoint.DisplayColumn);
                }
            }
        }

        /// <summary>
        /// Make sure errors in a file show up in the error list window.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ErrorList() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProject.sln");
                var projectNode = project.GetPythonProject();

                var expectedDocument = Path.Combine(projectNode.ProjectHome, "Program.py");
                var expectedCategory = VSTASKCATEGORY.CAT_BUILDCOMPILE;
                var expectedItems = new[] {
                    new TaskItemInfo(expectedDocument, 2, 8, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected indent"),
                    new TaskItemInfo(expectedDocument, 2, 13, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token '('"),
                    new TaskItemInfo(expectedDocument, 2, 30, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token ')'"),
                    new TaskItemInfo(expectedDocument, 2, 31, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token '<newline>'"),
                    new TaskItemInfo(expectedDocument, 3, 0, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token '<NL>'"),
                    new TaskItemInfo(expectedDocument, 3, 0, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token '<dedent>'"),
                    new TaskItemInfo(expectedDocument, 4, 0, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "unexpected token 'pass'"),
                };

                TaskListTest(app, typeof(SVsErrorList), expectedItems, navigateTo: new[] { 0, 1, 2, 3, 4, 5, 6 });
            }
        }

        /// <summary>
        /// Make sure task comments in a file show up in the task list window.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CommentTaskList() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProject.sln");
                var projectNode = project.GetPythonProject();

                var expectedDocument = Path.Combine(projectNode.ProjectHome, "Program.py");
                var expectedCategory = VSTASKCATEGORY.CAT_COMMENTS;
                var expectedItems = new[] {
                    new TaskItemInfo(expectedDocument, 4, 5, VSTASKPRIORITY.TP_NORMAL, expectedCategory, null, "TODO 123"),
                    new TaskItemInfo(expectedDocument, 5, 0, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "456 UnresolvedMergeConflict"),
                };

                TaskListTest(app, typeof(SVsTaskList), expectedItems, navigateTo: new[] { 0, 1 });
            }
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ErrorListAndTaskListAreClearedWhenProjectIsDeleted() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProjectDelete.sln");

                app.WaitForTaskListItems(typeof(SVsErrorList), 7);
                app.WaitForTaskListItems(typeof(SVsTaskList), 2);

                Console.WriteLine("Deleting project");
                app.Dte.Solution.Remove(project);

                app.WaitForTaskListItems(typeof(SVsErrorList), 0);
                app.WaitForTaskListItems(typeof(SVsTaskList), 0);
            }
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ErrorListAndTaskListAreClearedWhenProjectIsUnloaded() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProjectDelete.sln");

                app.WaitForTaskListItems(typeof(SVsErrorList), 7);
                app.WaitForTaskListItems(typeof(SVsTaskList), 2);

                IVsSolution solutionService = app.GetService<IVsSolution>(typeof(SVsSolution));
                Assert.IsNotNull(solutionService);

                IVsHierarchy selectedHierarchy;
                ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
                Assert.IsNotNull(selectedHierarchy);

                Console.WriteLine("Unloading project");
                ErrorHandler.ThrowOnFailure(solutionService.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, selectedHierarchy, 0));

                app.WaitForTaskListItems(typeof(SVsErrorList), 0);
                app.WaitForTaskListItems(typeof(SVsTaskList), 0);
            }
        }

        /// <summary>
        /// Make sure deleting a project clears the error list when there are errors in multiple files
        /// 
        /// Take 2 of https://pytools.codeplex.com/workitem/1523
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ErrorListAndTaskListAreClearedWhenProjectWithMultipleFilesIsUnloaded() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProjectMultipleFiles.sln");

                app.WaitForTaskListItems(typeof(SVsErrorList), 14);
                app.WaitForTaskListItems(typeof(SVsTaskList), 4);

                var solutionService = app.GetService<IVsSolution>(typeof(SVsSolution));
                Assert.IsNotNull(solutionService);

                IVsHierarchy selectedHierarchy;
                ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
                Assert.IsNotNull(selectedHierarchy);

                Console.WriteLine("Unloading project");
                ErrorHandler.ThrowOnFailure(solutionService.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, selectedHierarchy, 0));

                app.WaitForTaskListItems(typeof(SVsErrorList), 0);
                app.WaitForTaskListItems(typeof(SVsTaskList), 0);
            }
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ErrorListAndTaskListAreClearedWhenFileIsDeleted() {
            using (var app = new PythonVisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ErrorProjectDeleteFile.sln");

                app.WaitForTaskListItems(typeof(SVsErrorList), 7);
                app.WaitForTaskListItems(typeof(SVsTaskList), 2);

                Console.WriteLine("Deleting file");
                project.ProjectItems.Item("Program.py").Delete();

                app.WaitForTaskListItems(typeof(SVsErrorList), 0);
                app.WaitForTaskListItems(typeof(SVsTaskList), 0);
            }
        }
    }
}
