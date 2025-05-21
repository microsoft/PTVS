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
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools;
using TestUtilities;
using TestUtilities.UI;

namespace PythonToolsUITests {
    public class ErrorListTaskListTests {
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

                // TODO: get_Priority and Category are not implemented by VS LSC (returns E_FAIL)
                Priority = VSTASKPRIORITY.TP_HIGH;
                Category = VSTASKCATEGORY.CAT_CODESENSE;
                ErrorCategory = null;

                //var priority = new VSTASKPRIORITY[1];
                //ErrorHandler.ThrowOnFailure(taskItem.get_Priority(priority));
                //Priority = priority[0];

                //var category = new VSTASKCATEGORY[1];
                //ErrorHandler.ThrowOnFailure(taskItem.Category(category));
                //Category = category[0];

                //var errorItem = taskItem as IVsErrorItem;
                //if (errorItem != null) {
                //    uint errorCategory;
                //    try {
                //        ErrorHandler.ThrowOnFailure(errorItem.GetCategory(out errorCategory));
                //        ErrorCategory = (__VSERRORCATEGORY)errorCategory;
                //    } catch (NotImplementedException) {
                //        ErrorCategory = null;
                //    }
                //} else {
                //    ErrorCategory = null;
                //}
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
                    app.ServiceProvider.GetUIThread().Invoke((Action)delegate {
                        items[j].NavigateTo();
                    });

                    // Wait for the document to be active and the caret to be at the expected position
                    var maxAttempts = 10;
                    var delay = 100; // ms
                    bool caretCorrect = false;
                    for (int attempt = 0; attempt < maxAttempts; attempt++) {
                        var doc = app.Dte.ActiveDocument;
                        if (doc != null &&
                            string.Compare(expectedItems[i].Document, doc.FullName, StringComparison.OrdinalIgnoreCase) == 0) {
                            var textDoc = (EnvDTE.TextDocument)doc.Object("TextDocument");
                            if (expectedItems[i].Line + 1 == textDoc.Selection.ActivePoint.Line &&
                                expectedItems[i].Column + 1 == textDoc.Selection.ActivePoint.DisplayColumn) {
                                caretCorrect = true;
                                break;
                            }
                        }
                        System.Threading.Thread.Sleep(delay);
                    }
                    Console.WriteLine($"Expected Line: {expectedItems[i].Line}, Column: {expectedItems[i].Column}");
                    if (app.Dte.ActiveDocument != null) {
                        var textDoc = (EnvDTE.TextDocument)app.Dte.ActiveDocument.Object("TextDocument");
                        Console.WriteLine($"Actual Line: {textDoc.Selection.ActivePoint.Line}, Column: {textDoc.Selection.ActivePoint.DisplayColumn}");
                    }
                    Assert.IsTrue(caretCorrect, "Caret did not move to the expected position after NavigateTo().");
                }
            }
        }

        /// <summary>
        /// Make sure errors in a file show up in the error list window.
        /// </summary>
        public void ErrorList(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProject.sln"));
            var projectNode = project.GetPythonProject();

            var expectedDocument = Path.Combine(projectNode.ProjectHome, "Program.py").Replace("C:", "c:");
            var expectedCategory = VSTASKCATEGORY.CAT_CODESENSE;
            var expectedItems = new[] {
                    new TaskItemInfo(expectedDocument, 2, 0, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "Unexpected indentation"),                    
                };

            app.OpenDocument(expectedDocument);
            app.OpenErrorList();

            TaskListTest(app, typeof(SVsErrorList), expectedItems, navigateTo: new[] { 0 });
        }

        /// <summary>
        /// Make sure task comments in a file show up in the task list window.
        /// </summary>
        public void CommentTaskList(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProject.sln"));
            var projectNode = project.GetPythonProject();

            var expectedDocument = Path.Combine(projectNode.ProjectHome, "Program.py").Replace("C:", "c:"); ;
            var expectedCategory = VSTASKCATEGORY.CAT_CODESENSE;
            var expectedItems = new[] {
                    new TaskItemInfo(expectedDocument, 4, 7, VSTASKPRIORITY.TP_HIGH, expectedCategory, null, "TODO 123"),                    
                };

            app.OpenDocument(expectedDocument);
            app.OpenTaskList();

            TaskListTest(app, typeof(SVsTaskList), expectedItems, navigateTo: new[] { 0 });
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        public void ErrorListAndTaskListAreClearedWhenProjectIsDeleted(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProjectDelete.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program.py"));

            app.OpenErrorList();
            //app.OpenTaskList();

            app.WaitForTaskListItems(typeof(SVsErrorList), 1);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 2);

            Console.WriteLine("Deleting project");
            app.Dte.Solution.Remove(project);

            app.WaitForTaskListItems(typeof(SVsErrorList), 0);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 0);
        }

        /// <summary>
        /// Make sure deleting a project clears the error list
        /// </summary>
        public void ErrorListAndTaskListAreClearedWhenProjectIsUnloaded(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProjectDelete.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program.py"));

            app.OpenErrorList();
            //app.OpenTaskList();

            app.WaitForTaskListItems(typeof(SVsErrorList), 1);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 2);

            IVsSolution solutionService = app.GetService<IVsSolution>(typeof(SVsSolution));
            Assert.IsNotNull(solutionService);

            IVsHierarchy selectedHierarchy;
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
            Assert.IsNotNull(selectedHierarchy);

            Console.WriteLine("Unloading project");
            app.ServiceProvider.GetUIThread().Invoke((Action)delegate {
                ErrorHandler.ThrowOnFailure(solutionService.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, selectedHierarchy, 0));
            });
            app.WaitForTaskListItems(typeof(SVsErrorList), 0);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 0);
        }

        /// <summary>
        /// Make sure deleting a project clears the error list when there are errors in multiple files
        /// 
        /// Take 2 of https://pytools.codeplex.com/workitem/1523
        /// </summary>
        public void ErrorListAndTaskListAreClearedWhenProjectWithMultipleFilesIsUnloaded(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProjectMultipleFiles.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program.py"));
            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program2.py"));

            app.OpenErrorList();
            //app.OpenTaskList();

            app.WaitForTaskListItems(typeof(SVsErrorList), 2);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 4);

            var solutionService = app.GetService<IVsSolution>(typeof(SVsSolution));
            Assert.IsNotNull(solutionService);

            IVsHierarchy selectedHierarchy;
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
            Assert.IsNotNull(selectedHierarchy);

            Console.WriteLine("Unloading project");
            app.ServiceProvider.GetUIThread().Invoke((Action)delegate {
                ErrorHandler.ThrowOnFailure(solutionService.CloseSolutionElement((uint)__VSSLNCLOSEOPTIONS.SLNCLOSEOPT_UnloadProject, selectedHierarchy, 0));
            });
            app.WaitForTaskListItems(typeof(SVsErrorList), 0);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 0);
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        public void ErrorListAndTaskListAreClearedWhenFileIsDeleted(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProjectDeleteFile.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program.py"));

            app.OpenErrorList();

            app.WaitForTaskListItems(typeof(SVsErrorList), 1);

            Console.WriteLine("Deleting file");
            project.ProjectItems.Item("Program.py").Delete();

            app.WaitForTaskListItems(typeof(SVsErrorList), 0);
            
        }

        /// <summary>
        /// Make sure deleting a file w/ errors clears the error list
        /// </summary>
        public void ErrorListAndTaskListAreClearedWhenOpenFileIsDeleted(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\ErrorProjectDeleteFile.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "Program.py"));

            app.OpenErrorList();
            //app.OpenTaskList();

            project.ProjectItems.Item("Program.py").Open();

            app.WaitForTaskListItems(typeof(SVsErrorList), 1);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 2);

            Console.WriteLine("Deleting file");
            project.ProjectItems.Item("Program.py").Delete();

            app.WaitForTaskListItems(typeof(SVsErrorList), 0);
            //app.WaitForTaskListItems(typeof(SVsTaskList), 0);
        }

        /// <summary>
        /// Make sure *.pyi files ignore the active Python version
        /// </summary>
        public void ErrorListEmptyForValidTypingFile(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\Typings.sln"));
            var projectNode = project.GetPythonProject();

            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "mymod.pyi"));
            app.OpenDocument(Path.Combine(projectNode.ProjectHome, "usermod.py"));

            app.OpenErrorList();

            var actual = app.WaitForTaskListItems(typeof(SVsErrorList), 1);
            Assert.AreEqual(1, actual.Count);
            ErrorHandler.ThrowOnFailure(actual[0].Document(out var doc));
            Assert.AreEqual("usermod.py", Path.GetFileName(doc));
        }
    }
}
