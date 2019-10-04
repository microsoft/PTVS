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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Automation;
using System.Windows.Input;
using EnvDTE;
using Microsoft.PythonTools;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;
using static pythontools::Microsoft.PythonTools.Extensions;
using Keyboard = util::TestUtilities.UI.Keyboard;
using MessageBoxButton = TestUtilities.MessageBoxButton;
using Mouse = util::TestUtilities.UI.Mouse;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    //[TestClass]
    public class UITests {
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DeferredSaveWithDot(PythonVisualStudioApp app) {
            string fullname;
            // http://pytools.codeplex.com/workitem/623
            // enable deferred saving on projects
            var props = app.Dte.get_Properties("Environment", "ProjectsAndSolution");
            var prevValue = props.Item("SaveNewProjects").Value;
            props.Item("SaveNewProjects").Value = false;
            app.OnDispose(() => { props.Item("SaveNewProjects").Value = prevValue; });


            using (var newProjDialog = app.FileNewProject()) {
                newProjDialog.FocusLanguageNode();

                var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
                consoleApp.Select();
                newProjDialog.ProjectName = "Fob.Oar";
                newProjDialog.OK();
            }

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            using (var saveDialog = AutomationDialog.FromDte(app, "File.SaveAll")) {
                saveDialog.ClickButtonAndClose("Save");
            }

            fullname = app.Dte.Solution.FullName;
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AbsolutePaths(PythonVisualStudioApp app) {
            var proj = File.ReadAllText(TestData.GetPath(@"TestData\AbsolutePath\AbsolutePath.pyproj"));
            proj = proj.Replace("[ABSPATH]", TestData.GetPath(@"TestData\AbsolutePath"));
            File.WriteAllText(TestData.GetPath(@"TestData\AbsolutePath\AbsolutePath.pyproj"), proj);

            var project = app.OpenProject(@"TestData\AbsolutePath.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var programPy = window.WaitForChildOfProject(project, "Program.py");
            Assert.IsNotNull(programPy);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CopyPasteFile(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var programPy = window.FindItem("Solution 'HelloWorld' (1 of 1 project)", "HelloWorld", "Program.py");

            AutomationWrapper.Select(programPy);

            Keyboard.ControlC();
            Keyboard.ControlV();

            Assert.IsNotNull(window.WaitForItem("Solution 'HelloWorld' (1 of 1 project)", "HelloWorld", "Program - Copy.py"));

            AutomationWrapper.Select(programPy);
            Keyboard.ControlC();
            Keyboard.ControlV();

            Assert.IsNotNull(window.WaitForItem("Solution 'HelloWorld' (1 of 1 project)", "HelloWorld", "Program - Copy (2).py"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddNewFolder(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            app.OpenSolutionExplorer().SelectProject(project);

            app.ExecuteCommand("Project.NewFolder");
            Keyboard.Type("MyNewFolder");
            Keyboard.PressAndRelease(Key.Enter);

            app.OpenSolutionExplorer().WaitForChildOfProject(project, "MyNewFolder");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddSearchPathRelativePath(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddSearchPaths.sln");

            app.OpenSolutionExplorer().SelectProject(project);

            using (var dialog = SelectFolderDialog.AddFolderToSearchPath(app)) {
                dialog.FolderName = TestData.GetPath(@"TestData\Outlining");
                dialog.SelectFolder();
            }
            System.Threading.Thread.Sleep(1000);
            app.ExecuteCommand("File.SaveAll");

            var text = File.ReadAllText(TestData.GetPath(@"TestData\AddSearchPaths\AddSearchPaths.pyproj"));
            string actual = Regex.Match(text, @"<SearchPath>.*</SearchPath>", RegexOptions.Singleline).Value;
            Assert.AreEqual("<SearchPath>..\\Outlining\\</SearchPath>", actual);
        }

        //[TestMethod, Priority(0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        //public void LoadSearchPath(PythonVisualStudioApp app) {
        //    var project = app.OpenProject(@"TestData\LoadSearchPaths.sln");

        //    // Ensure we complete analysis. VS may crash if the invalid
        //    // path is not handled correctly.
        //    project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);

        //    var tree = app.OpenSolutionExplorer();

        //    const string sln = "Solution 'LoadSearchPaths' (1 project)";
        //    const string proj = "LoadSearchPaths";
        //    var sp = Strings.SearchPaths;

        //    // Entered in file as ..\AddSearchPaths\
        //    var path1 = tree.WaitForItem(sln, proj, sp, "..\\AddSearchPaths");
        //    Assert.IsNotNull(path1, "Could not find ..\\AddSearchPaths");

        //    // Entered in file as ..\HelloWorld
        //    var path2 = tree.WaitForItem(sln, proj, sp, "..\\HelloWorld");
        //    Assert.IsNotNull(path2, "Could not find ..\\HelloWorld");

        //    // Entered in file as ..\LoadSearchPaths\NotHere\..\ - resolves to .\
        //    var path3 = tree.WaitForItem(sln, proj, sp, ".");
        //    Assert.IsNotNull(path3, "Could not find .");

        //    // Entered in file as .\NotHere\
        //    var path4 = tree.WaitForItem(sln, proj, sp, "NotHere");
        //    Assert.IsNotNull(path4, "Could not find NotHere");
        //    Assert.AreEqual("NotHere", path4.Current.Name);

        //    AutomationWrapper.Select(path4);
        //    app.ExecuteCommand("Edit.Delete"); // should not prompt, https://pytools.codeplex.com/workitem/1233
        //    Assert.IsNull(tree.WaitForItemRemoved(sln, proj, sp, "NotHere"));

        //    // Entered in file as Invalid*Search?Path
        //    var path5 = tree.WaitForItem(sln, proj, sp, "Invalid*Search?Path");
        //    Assert.IsNotNull(path5, "Could not find Invalid*Search?Path");
        //    Assert.AreEqual(path5.Current.Name, "Invalid*Search?Path");

        //    AutomationWrapper.Select(path5);
        //    app.ExecuteCommand("Edit.Delete");
        //    Assert.IsNull(tree.WaitForItemRemoved(sln, proj, sp, "Invalid*Search?Path"));

        //    // Ensure NotHere hasn't come back
        //    path4 = tree.WaitForItem(sln, proj, sp, "NotHere");
        //    Assert.IsNull(path4, "NotHere came back");
        //}

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddNewFolderNested(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            app.OpenSolutionExplorer().SelectProject(project);

            app.ExecuteCommand("Project.NewFolder");
            Keyboard.Type("FolderX");
            Keyboard.PressAndRelease(Key.Enter);

            var folderNode = app.OpenSolutionExplorer().WaitForChildOfProject(project, "FolderX");
            folderNode.Select();

            app.ExecuteCommand("Project.NewFolder");
            Keyboard.Type("FolderY");
            Keyboard.PressAndRelease(Key.Enter);

            var innerFolderNode = app.OpenSolutionExplorer().WaitForChildOfProject(project, "FolderX", "FolderY");
            innerFolderNode.Select();

            var newItem = project.ProjectItems.Item("FolderX").ProjectItems.Item("FolderY").ProjectItems.AddFromFile(
                TestData.GetPath(@"TestData\DebuggerProject\BreakpointTest.py")
            );

            app.OpenSolutionExplorer().WaitForChildOfProject(project, "FolderX", "FolderY", "BreakpointTest.py");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void RenameProjectToExisting(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\RenameProjectTestUI.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'RenameProjectTestUI' (1 of 1 project)", "HelloWorld");

            // rename once, cancel renaming to existing file....
            AutomationWrapper.Select(projectNode);
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("HelloWorldExisting");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            IntPtr dialog = app.WaitForDialog();

            app.CheckMessageBox("HelloWorldExisting.pyproj", "overwrite");

            // rename again, don't cancel...
            AutomationWrapper.Select(projectNode);
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("HelloWorldExisting");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            dialog = app.WaitForDialog();

            app.CheckMessageBox(MessageBoxButton.Yes, "HelloWorldExisting.pyproj", "overwrite");

            Assert.IsNotNull(window.WaitForItem("Solution 'RenameProjectTestUI' (1 of 1 project)", "HelloWorldExisting"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void RenameItemsTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\RenameItemsTestUI.sln");

            var window = app.OpenSolutionExplorer();

            // find Program.py, send copy & paste, verify copy of file is there
            var node = window.FindChildOfProject(project, "Program.py");

            // rename once, cancel renaming to existing file....
            node.Select();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.A, Key.LeftCtrl);

            Keyboard.Type("NewName.txt");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            IntPtr dialog = app.WaitForDialog();

            app.CheckMessageBox(MessageBoxButton.Cancel, "file name extension");

            // rename again, don't cancel...
            node.Select();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.A, Key.LeftCtrl);

            Keyboard.Type("NewName.txt");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            dialog = app.WaitForDialog();

            app.CheckMessageBox(MessageBoxButton.Yes, "file name extension");

            Assert.IsNotNull(window.WaitForChildOfProject(project, "NewName.txt"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CrossProjectCopy(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\HelloWorld2.sln", expectedProjects: 2);
            var proj1 = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld2");
            var proj2 = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld");

            var window = app.OpenSolutionExplorer();

            window.FindChildOfProject(proj1, "TestFolder3").Select();
            app.ExecuteCommand("Edit.Copy");

            window.SelectProject(proj2);
            app.ExecuteCommand("Edit.Paste");

            Assert.IsNotNull(window.WaitForChildOfProject(proj1, "TestFolder3"));
            Assert.IsNotNull(window.WaitForChildOfProject(proj2, "TestFolder3"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CrossProjectCutPaste(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\HelloWorld2.sln", expectedProjects: 2);
            var proj1 = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld2");
            var proj2 = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld");

            var window = app.OpenSolutionExplorer();

            window.FindChildOfProject(proj1, "TestFolder2").Select();
            app.ExecuteCommand("Edit.Cut");

            window.SelectProject(proj2);
            app.ExecuteCommand("Edit.Paste");

            Assert.IsNotNull(window.WaitForChildOfProject(proj2, "TestFolder2"));
            Assert.IsNull(window.WaitForChildOfProjectRemoved(proj1, "TestFolder2"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CutPaste(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\HelloWorld2.sln", expectedProjects: 2);
            var proj = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld2");

            var window = app.OpenSolutionExplorer();

            window.FindChildOfProject(proj, "TestFolder", "SubItem.py").Select();

            app.ExecuteCommand("Edit.Cut");

            window.SelectProject(proj);
            app.ExecuteCommand("Edit.Paste");

            Assert.IsNotNull(window.WaitForChildOfProject(proj, "SubItem.py"));
            Assert.IsNull(window.WaitForChildOfProjectRemoved(proj, "TestFolder", "SubItem.py"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CopyFolderOnToSelf(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\HelloWorld2.sln", expectedProjects: 2);
            var proj = app.Dte.Solution.Projects.Cast<Project>().Single(p => p.Name == "HelloWorld2");

            var window = app.OpenSolutionExplorer();

            try {
                // Remove the destination folder in case a previous test has
                // created it.
                Directory.Delete(TestData.GetPath(@"TestData\HelloWorld2\TestFolder - Copy"), true);
            } catch {
            }

            window.FindChildOfProject(proj, "TestFolder").Select();
            app.ExecuteCommand("Edit.Copy");

            window.FindChildOfProject(proj, "TestFolder").Select();
            app.ExecuteCommand("Edit.Paste");

            Assert.IsNotNull(window.WaitForChildOfProject(proj, "TestFolder - Copy"));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DragDropTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\DragDropTest.sln");

            var window = app.OpenSolutionExplorer();

            var folder = window.FindChildOfProject(project, "TestFolder", "SubItem.py");
            var point = folder.Element.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            point = window.FindChildOfProject(project).Element.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            window.WaitForChildOfProject(project, "SubItem.py");
        }

        /// <summary>
        /// Drag a file onto another file in the same directory, nothing should happen
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DragDropFileToFileTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\DragDropTest.sln");

            var window = app.OpenSolutionExplorer();

            Mouse.MoveTo(window.FindChildOfProject(project, "TestFolder", "SubItem2.py").Element.GetClickablePoint());
            Mouse.Down(MouseButton.Left);

            Mouse.MoveTo(window.FindChildOfProject(project, "TestFolder", "SubItem3.py").Element.GetClickablePoint());
            Mouse.Up(MouseButton.Left);

            using (var dlg = AutomationDialog.WaitForDialog(app)) { }
            window.WaitForChildOfProject(project, "TestFolder", "SubItem2.py");
            window.WaitForChildOfProject(project, "TestFolder", "SubItem3.py");
        }

        /// <summary>
        /// Drag a file onto it's containing folder, dialog should appear
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DragDropFileToContainingFolderTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\DragDropTest.sln");
            var window = app.OpenSolutionExplorer();

            Mouse.MoveTo(window.FindChildOfProject(project, "TestFolder", "SubItem2.py").Element.GetClickablePoint());
            Mouse.Down(MouseButton.Left);

            Mouse.MoveTo(window.FindChildOfProject(project, "TestFolder").Element.GetClickablePoint());
            Mouse.Up(MouseButton.Left);

            using (var dlg = AutomationDialog.WaitForDialog(app)) { }
            window.WaitForChildOfProject(project, "TestFolder", "SubItem2.py");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DragLeaveTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\DragDropTest.sln");
            var window = app.OpenSolutionExplorer();

            // click on SubItem.py
            var point = window.FindChildOfProject(project, "TestFolder2", "SubItem.py").Element.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            // move to project and hover
            Mouse.MoveTo(window.FindChildOfProject(project).Element.GetClickablePoint());
            System.Threading.Thread.Sleep(500);

            // move back and release
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            window.WaitForChildOfProject(project, "TestFolder2", "SubItem.py");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DragLeaveFolderTest(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\DragDropTest.sln");
            var window = app.OpenSolutionExplorer();

            // click on SubItem.py
            var point = window.FindChildOfProject(project, "TestFolder2", "SubFolder").Element.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            // move to project and hover
            Mouse.MoveTo(window.FindChildOfProject(project).Element.GetClickablePoint());
            System.Threading.Thread.Sleep(500);

            // move back and release
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            window.WaitForChildOfProject(project, "TestFolder2", "SubFolder");
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void CopyFolderInToSelf(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\HelloWorld2.sln", expectedProjects: 2);

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'HelloWorld2' (2 of 2 projects)", "HelloWorld2", "TestFolder");
            AutomationWrapper.Select(folder);
            Keyboard.ControlC();

            try {
                // Remove the destination folder in case a previous test has
                // created it.
                Directory.Delete(TestData.GetPath(@"TestData\HelloWorld2\TestFolder - Copy"), true);
            } catch {
            }

            var subItem = window.FindItem("Solution 'HelloWorld2' (2 of 2 projects)", "HelloWorld2", "TestFolder", "SubItem.py");
            AutomationWrapper.Select(subItem);
            Keyboard.ControlV();

            var item = window.WaitForItem("Solution 'HelloWorld2' (2 of 2 projects)", "HelloWorld2", "TestFolder - Copy", "SubItem.py");
            if (item == null) {
                AutomationWrapper.DumpElement(window.Element);
                Assert.Fail("Did not find TestFolder - Copy");
            }
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void MultiSelectCopyAndPaste(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\DebuggerProject.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var files = new[] { "BreakAllTest.py", "BreakpointTest.py", "BreakpointTest2.py" };
            bool anySelected = false;
            foreach (var f in files) {
                var node = window.FindItem("Solution 'DebuggerProject' (1 of 1 project)", "DebuggerProject", f);
                Assert.IsNotNull(node, f + " not found in DebuggerProject");
                if (anySelected) {
                    ((SelectionItemPattern)node.GetCurrentPattern(SelectionItemPattern.Pattern)).AddToSelection();
                } else {
                    node.Select();
                    anySelected = true;
                }
            }
            Keyboard.ControlC();

            var projectNode = window.FindItem("Solution 'DebuggerProject' (1 of 1 project)", "DebuggerProject");

            AutomationWrapper.Select(projectNode);
            Keyboard.ControlV();

            foreach (var f in files.Select(f => f.Remove(f.LastIndexOf('.')) + " - Copy" + f.Substring(f.LastIndexOf('.')))) {
                Assert.IsNotNull(
                    window.WaitForItem("Solution 'DebuggerProject' (1 of 1 project)", "DebuggerProject", f),
                    f + " not found after copying");
            }
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/1222
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoMultiSelectContextMenu(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\DjangoApplication.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var manageNode = window.FindItem("Solution 'DjangoApplication' (1 of 1 project)", "DjangoApplication", "manage.py");
            Mouse.MoveTo(manageNode.GetClickablePoint());
            Mouse.Click(MouseButton.Left);

            Keyboard.Press(Key.LeftShift);
            Keyboard.PressAndRelease(Key.Down);
            Keyboard.Release(Key.LeftShift);

            Mouse.MoveTo(manageNode.GetClickablePoint());
            Mouse.Click(MouseButton.Right);

            Keyboard.Type("j"); // Exclude from Project
            Assert.IsNull(window.WaitForItemRemoved("Solution 'DjangoApplication' (1 of 1 project)", "DjangoApplication", "manage.py"));
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/1223
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void DjangoIncludeInProject(PythonVisualStudioApp app) {
            app.OpenProject(@"TestData\DjangoApplication.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            app.Dte.ExecuteCommand("Project.ShowAllFiles"); // start showing all

            var folderNode = window.WaitForItem("Solution 'DjangoApplication' (1 of 1 project)", "DjangoApplication", "Folder");
            Mouse.MoveTo(folderNode.GetClickablePoint());
            Mouse.Click(MouseButton.Right);

            Keyboard.Type("j"); // Exclude from Project
            app.Dte.ExecuteCommand("Project.ShowAllFiles"); // stop showing all

            Assert.IsNull(window.WaitForItemRemoved("Solution 'DjangoApplication' (1 of 1 project)", "DjangoApplication", "notinproject.py"));
            Assert.IsNotNull(window.WaitForItem("Solution 'DjangoApplication' (1 of 1 project)", "DjangoApplication", "Folder", "test.py"));
        }

        /// <summary>
        /// http://pytools.codeplex.com/workitem/1223
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddItemPreviousSiblingNotVisible(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddItemPreviousSiblingNotVisible.sln");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.WaitForItem("Solution 'AddItemPreviousSiblingNotVisible' (1 of 1 project)", "HelloWorld");
            Assert.IsNotNull(projectNode);
            AutomationWrapper.Select(projectNode);

            var solutionService = app.GetService<IVsSolution>(typeof(SVsSolution));
            Assert.IsNotNull(solutionService);

            IVsHierarchy selectedHierarchy;
            ErrorHandler.ThrowOnFailure(solutionService.GetProjectOfUniqueName(project.UniqueName, out selectedHierarchy));
            Assert.IsNotNull(selectedHierarchy);
            HierarchyEvents events = new HierarchyEvents();
            uint cookie;
            selectedHierarchy.AdviseHierarchyEvents(events, out cookie);

            using (var newItem = NewItemDialog.FromDte(app)) {
                var pythonFile = newItem.ProjectTypes.FindItem("Empty Python File");
                pythonFile.Select();
                newItem.FileName = "zmodule1.py";
                newItem.OK();
            }

            var test2 = window.WaitForItem("Solution 'AddItemPreviousSiblingNotVisible' (1 of 1 project)", "HelloWorld", "zmodule1.py");
            Assert.IsNotNull(test2);

            selectedHierarchy.UnadviseHierarchyEvents(cookie);

            object caption;
            ErrorHandler.ThrowOnFailure(
                selectedHierarchy.GetProperty(
                    events.SiblingPrev,
                    (int)__VSHPROPID.VSHPROPID_Caption,
                    out caption
                )
            );

            Assert.AreEqual("Program.py", caption);
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1251
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddExistingItem(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddExistingItem.sln");

            app.OpenSolutionExplorer().SelectProject(project);

            using (var addExistingDlg = AddExistingItemDialog.FromDte(app)) {
                addExistingDlg.FileName = TestData.GetPath(@"TestData\AddExistingItem\Program2.py");
                addExistingDlg.Add();
            }
            app.OpenSolutionExplorer().WaitForChildOfProject(project, "Program2.py");
        }

        /// <summary>
        /// https://pytools.codeplex.com/workitem/1221
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void AddNewFileOverwritingExistingFileNotInProject(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddExistingItem.sln");

            app.OpenSolutionExplorer().SelectProject(project);

            const string filename = "Program2.py";

            using (var addItemDlg = NewItemDialog.FromDte(app)) {
                var pythonFile = addItemDlg.ProjectTypes.FindItem("Empty Python File");
                pythonFile.Select();
                addItemDlg.FileName = filename;
                addItemDlg.OK();
            }

            app.CheckMessageBox(
                MessageBoxButton.Yes,
                "A file named", filename, "already exists. Do you want to overwrite it?"
            );

            app.OpenSolutionExplorer().WaitForChildOfProject(project, "Program2.py");
        }


        class HierarchyEvents : IVsHierarchyEvents {
            public uint SiblingPrev;

            #region IVsHierarchyEvents Members

            public int OnInvalidateIcon(IntPtr hicon) {
                return VSConstants.S_OK;
            }

            public int OnInvalidateItems(uint itemidParent) {
                return VSConstants.S_OK;
            }

            public int OnItemAdded(uint itemidParent, uint itemidSiblingPrev, uint itemidAdded) {
                SiblingPrev = itemidSiblingPrev;
                return VSConstants.S_OK;
            }

            public int OnItemDeleted(uint itemid) {
                return VSConstants.S_OK;
            }

            public int OnItemsAppended(uint itemidParent) {
                return VSConstants.S_OK;
            }

            public int OnPropertyChanged(uint itemid, int propid, uint flags) {
                return VSConstants.S_OK;
            }

            #endregion
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void NewProject(PythonVisualStudioApp app) {
            using (var newProjDialog = NewProjectDialog.FromDte(app)) {
                newProjDialog.FocusLanguageNode();

                var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
                consoleApp.Select();

                newProjDialog.OK();
            }

            // wait for new solution to load...
            for (int i = 0; i < 10 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);

            var project = app.Dte.Solution.Projects.Item(1);
            Console.WriteLine("Project.Name: {0}", project.Name ?? "(null)");

            var itemName = Path.ChangeExtension(project.Name, ".py");
            Console.WriteLine("Expected item: {0}", itemName);

            Console.WriteLine("Items:");
            foreach (var item in project.ProjectItems.OfType<ProjectItem>()) {
                Console.WriteLine("  {0}", item.Name ?? "(null)");
            }

            Assert.IsNotNull(project.ProjectItems.Item(itemName));
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void TransferItem(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            string filename, basename;
            int i = 0;
            do {
                i++;
                basename = "test" + i + " .py";
                filename = Path.Combine(TestData.GetTempPath(), basename);
            } while (System.IO.File.Exists(filename));

            System.IO.File.WriteAllText(filename, "def f(): pass");

            var fileWindow = app.Dte.ItemOperations.OpenFile(filename);

            using (var dialog = ChooseLocationDialog.FromDte(app)) {
                dialog.SelectProject("HelloWorld");
                dialog.OK();
            }

            app.OpenSolutionExplorer().WaitForChildOfProject(project, basename);

            Assert.AreEqual(basename, fileWindow.Caption);

            System.IO.File.Delete(filename);
        }

        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void SaveAs(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\SaveAsUI.sln");

            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            var selection = ((TextSelection)window.Selection);
            selection.SelectAll();
            selection.Delete();

            // save under a new file name
            using (var saveDialog = SaveDialog.FromDte(app)) {
                Assert.AreEqual(item.FileNames[0], saveDialog.FileName);
                saveDialog.FileName = "Program2.py";
                saveDialog.WaitForInputIdle();
                saveDialog.Save();
            }

            Assert.IsNotNull(app.OpenSolutionExplorer().WaitForChildOfProject(project, "Program2.py"));
        }

        /// <summary>
        /// Verifies non-member items don't show in in find all files
        /// 
        /// https://pytools.codeplex.com/workitem/1277
        /// </summary>
        //[TestMethod, Priority(0)]
        [HostType("VSTestHost"), TestCategory("Installed")]
        public void TestSearchExcludedFiles(PythonVisualStudioApp app) {
            var project = app.OpenProject(@"TestData\FindInAllFiles.sln");
            Assert.IsNotNull(project);

            var find = app.Dte.Find;

            find.Target = vsFindTarget.vsFindTargetSolution;
            find.FindWhat = "THIS_TEXT_IS_NOT_ANYWHERE_ELSE";
            var results = find.Execute();
            Assert.AreEqual(results, vsFindResult.vsFindResultNotFound);
        }

    }
}
