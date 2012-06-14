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
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Input;
using AnalysisTest.UI;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.TestSccPackage;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using Keyboard = AnalysisTest.UI.Keyboard;
using Mouse = AnalysisTest.UI.Mouse;
using Path = System.IO.Path;

namespace AnalysisTest.ProjectSystem {
    [TestClass]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    [DeploymentItem("Binaries\\Win32\\Debug\\spam.dll")]
    public class UITests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeferredSaveWithDot() {
            // http://pytools.codeplex.com/workitem/623
            // enable deferred saving on projects
            var props = VsIdeTestHostContext.Dte.get_Properties("Environment", "ProjectsAndSolution");
            var prevValue = props.Item("SaveNewProjects").Value;
            props.Item("SaveNewProjects").Value = false;

            try {
                // now run the test
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var newProjDialog = app.FileNewProject();

                newProjDialog.FocusLanguageNode();

                var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
                consoleApp.SetFocus();
                newProjDialog.ProjectName = "Foo.Bar";
                newProjDialog.ClickOK();

                // wait for new solution to load...
                for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                    System.Threading.Thread.Sleep(1000);
                }

                ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("File.SaveAll"));

                var saveProjDialog = new SaveProjectDialog(app.WaitForDialog());
                saveProjDialog.Save();

                app.WaitForDialogDismissed();

                var fullname = app.Dte.Solution.FullName;
                app.Dte.Solution.Close(false);

                Directory.Delete(Path.GetDirectoryName(fullname), true);
            } finally {
                props.Item("SaveNewProjects").Value = prevValue;
            }
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AbsolutePaths() {
            var proj = File.ReadAllText(Path.Combine("Python.VS.TestData", "AbsolutePath", "AbsolutePath.pyproj"));
            proj = proj.Replace("[ABSPATH]", Path.GetFullPath(Path.Combine("Python.VS.TestData", "AbsolutePath")));
            File.WriteAllText(Path.Combine("Python.VS.TestData", "AbsolutePath", "AbsolutePath.pyproj"), proj);

            var project = DebugProject.OpenProject(@"Python.VS.TestData\AbsolutePath.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var programPy = window.WaitForItem("Solution 'AbsolutePath' (1 project)", "AbsolutePath", "Program.py");
            Assert.AreNotEqual(null, programPy);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyPasteFile() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var programPy = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program.py");
            
            programPy.SetFocus();

            Keyboard.ControlC();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program - Copy.py"));

            programPy.SetFocus();
            Keyboard.ControlC();
            Keyboard.ControlV();
            
            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program - Copy (2).py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddNewFolder() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");
            
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");
            projectNode.SetFocus();

            Keyboard.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.PressAndRelease(Key.Right);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.Type("MyNewFolder");
            Keyboard.PressAndRelease(Key.Enter);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "MyNewFolder"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddSearchPathRelativePath() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\AddSearchPaths.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'AddSearchPaths' (1 project)", "AddSearchPaths", "Search Path");
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddSearchPath"));

            var dialog = new SelectFolderDialog(app.WaitForDialog());
            dialog.FolderName = Path.Combine(Directory.GetCurrentDirectory(), "Python.VS.TestData", "Outlining");
            dialog.SelectFolder();

            app.Dte.ExecuteCommand("File.SaveAll");

            var text = File.ReadAllText(Path.Combine("Python.VS.TestData", "AddSearchPaths", "AddSearchPaths.pyproj"));
            Assert.IsTrue(text.Contains("<SearchPath>..\\Outlining\\</SearchPath>"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddNewFolderNested() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld");
            projectNode.SetFocus();

            Keyboard.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.PressAndRelease(Key.Right);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.Type("FolderX");
            Keyboard.PressAndRelease(Key.Enter);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "FolderX"));

            var folderNode = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "FolderX");
            folderNode.SetFocus();

            Keyboard.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.PressAndRelease(Key.Right);
            Keyboard.PressAndRelease(Key.D);
            Keyboard.Type("FolderY");
            Keyboard.PressAndRelease(Key.Enter);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "FolderX", "FolderY"));
            var innerFolderNode = window.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "FolderX", "FolderY");
            innerFolderNode.SetFocus();

            var newItem = project.ProjectItems.Item("FolderX").Collection.Item("FolderY").Collection.AddFromFile(
                Path.Combine(
                    System.IO.Directory.GetCurrentDirectory(),
                    "Python.VS.TestData",
                    "DebuggerProject",
                    "BreakpointTest.py"
                )
            );

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "FolderX", "FolderY", "BreakpointTest.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameProjectToExisting() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\RenameProjectTestUI.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'RenameProjectTestUI' (1 project)", "HelloWorld");
            
            // rename once, cancel renaming to existing file....
            projectNode.SetFocus();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("HelloWorldExisting");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            IntPtr dialog = app.WaitForDialog();

            VisualStudioApp.CheckMessageBox("HelloWorldExisting.pyproj", "overwrite");

            // rename again, don't cancel...
            projectNode.SetFocus();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("HelloWorldExisting");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            dialog = app.WaitForDialog();

            VisualStudioApp.CheckMessageBox(MessageBoxButton.Yes, "HelloWorldExisting.pyproj", "overwrite");

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'RenameProjectTestUI' (1 project)", "HelloWorldExisting"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameItemsTest() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\RenameItemsTestUI.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // find Program.py, send copy & paste, verify copy of file is there
            var projectNode = window.FindItem("Solution 'RenameItemsTestUI' (1 project)", "HelloWorld", "Program.py");

            // rename once, cancel renaming to existing file....
            projectNode.SetFocus();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("NewName.txt");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            IntPtr dialog = app.WaitForDialog();

            VisualStudioApp.CheckMessageBox(MessageBoxButton.Cancel, "file name extension");

            // rename again, don't cancel...
            projectNode.SetFocus();
            Keyboard.PressAndRelease(Key.F2);
            System.Threading.Thread.Sleep(100);

            Keyboard.Type("NewName.txt");
            System.Threading.Thread.Sleep(100);
            Keyboard.PressAndRelease(Key.Enter);

            dialog = app.WaitForDialog();

            VisualStudioApp.CheckMessageBox(MessageBoxButton.Yes, "file name extension");

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'RenameItemsTestUI' (1 project)", "HelloWorld", "NewName.txt"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossProjectCopy() {
            DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld2.sln", expectedProjects: 2);

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folderNode = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder3");
            folderNode.SetFocus();
            
            Keyboard.ControlC();

            var projectNode = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld");

            projectNode.SetFocus();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld", "TestFolder3"));
        }
        
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossProjectCutPaste() {
            DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld2.sln", expectedProjects: 2);

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folderNode = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder2");
            folderNode.SetFocus();

            Keyboard.ControlX();

            var projectNode = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld");

            projectNode.SetFocus();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld", "TestFolder2"));
            Assert.AreEqual(null, window.WaitForItemRemoved("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder2"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutPaste() {
            DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld2.sln", expectedProjects: 2);

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var subItem = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder", "SubItem.py");
            subItem.SetFocus();

            Keyboard.ControlX();

            var projectNode = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2");

            projectNode.SetFocus();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "SubItem.py"));
            Assert.AreEqual(null, window.WaitForItemRemoved("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder", "SubItem.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFolderOnToSelf() {
            DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld2.sln", expectedProjects: 2);

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder");
            folder.SetFocus();

            Keyboard.ControlC();

            folder.SetFocus();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder - Copy"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragDropTest() {
            DebugProject.OpenProject(@"Python.VS.TestData\DragDropTest.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem.py");
            var point = folder.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            var project = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest");
            point = project.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "SubItem.py"));
        }

        /// <summary>
        /// Drag a file onto another file in the same directory, nothing should happen
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragDropFileToFileTest() {
            DebugProject.OpenProject(@"Python.VS.TestData\DragDropTest.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem2.py");
            var point = folder.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            var project = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem3.py");
            point = project.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem2.py"));
            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem3.py"));
        }

        /// <summary>
        /// Drag a file onto it's containing folder, nothing should happen
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragDropFileToContainingFolderTest() {
            DebugProject.OpenProject(@"Python.VS.TestData\DragDropTest.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem2.py");
            var point = folder.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            var project = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder");
            point = project.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder", "SubItem2.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragLeaveTest() {
            DebugProject.OpenProject(@"Python.VS.TestData\DragDropTest.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var item = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder2", "SubItem.py");
            var project = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest");

            // click on SubItem.py
            var point = item.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            // move to project and hover
            var projectPoint = project.GetClickablePoint();
            Mouse.MoveTo(projectPoint);
            System.Threading.Thread.Sleep(500);

            // move back and release
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            Assert.AreNotEqual(null, window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder2", "SubItem.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragLeaveFolderTest() {
            DebugProject.OpenProject(@"Python.VS.TestData\DragDropTest.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder2", "SubFolder");
            var project = window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest");

            // click on SubItem.py
            var point = folder.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            // move to project and hover
            var projectPoint = project.GetClickablePoint();
            Mouse.MoveTo(projectPoint);
            System.Threading.Thread.Sleep(500);

            // move back and release
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            Assert.AreNotEqual(null, window.FindItem("Solution 'DragDropTest' (1 project)", "DragDropTest", "TestFolder2", "SubFolder"));
        }
        
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFolderInToSelf() {
            DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld2.sln", expectedProjects: 2);

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder");
            folder.SetFocus();
            Keyboard.ControlC();

            var subItem = window.FindItem("Solution 'HelloWorld2' (2 projects)", "HelloWorld2", "TestFolder", "SubItem.py");
            subItem.SetFocus();
            Keyboard.ControlV();
            VisualStudioApp.CheckMessageBox("Cannot copy 'TestFolder'. The destination folder is a subfolder of the source folder.");
        }
        
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultiSelectCopyAndPaste() {
            DebugProject.OpenProject(@"Python.VS.TestData\DebuggerProject.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folderNode = window.FindItem("Solution 'DebuggerProject' (1 project)", "DebuggerProject", "BreakAllTest.py");
            folderNode.SetFocus();

            Keyboard.Press(Key.LeftShift);
            Keyboard.PressAndRelease(Key.Down);
            Keyboard.PressAndRelease(Key.Down);
            Keyboard.Release(Key.LeftShift);
            Keyboard.ControlC();

            var projectNode = window.FindItem("Solution 'DebuggerProject' (1 project)", "DebuggerProject");

            projectNode.SetFocus();
            Keyboard.ControlV();

            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DebuggerProject' (1 project)", "DebuggerProject", "BreakAllTest - Copy.py"));
            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DebuggerProject' (1 project)", "DebuggerProject", "BreakpointTest - Copy.py"));
            Assert.AreNotEqual(null, window.WaitForItem("Solution 'DebuggerProject' (1 project)", "DebuggerProject", "BreakpointTest2 - Copy.py"));
        }

        /// <summary>
        /// Verify we get called w/ a project which does have source control enabled.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SourceControl() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            // close any projects before switching source control...
            VsIdeTestHostContext.Dte.Solution.Close();

            app.SelectSourceControlProvider("Test Source Provider");

            TestSccProvider.ExpectedAuxPath = "AuxPath";
            TestSccProvider.ExpectedLocalPath = "LocalPath";
            TestSccProvider.ExpectedProvider = "TestProvider";
            TestSccProvider.ExpectedProjectName = "HelloWorld";

            var project = DebugProject.OpenProject(@"Python.VS.TestData\SourceControl.sln");

            Assert.AreEqual(1, TestSccProvider.LoadedProjects.Count);

            TestSccProvider.ExpectedAuxPath = null;
            TestSccProvider.ExpectedLocalPath = null;
            TestSccProvider.ExpectedProvider = null;
            TestSccProvider.ExpectedProjectName = null;

            TestSccProvider.LoadedProjects.First().SccProject.SetSccLocation(
                "NewProjectName",
                "NewAuxPath",
                "NewLocalPath",
                "NewProvider"
            );

            app.Dte.Solution.Close();

            Assert.AreEqual(0, TestSccProvider.LoadedProjects.Count);
            if (TestSccProvider.Failures.Count != 0) {
                Assert.Fail(String.Join(Environment.NewLine, TestSccProvider.Failures));
            }

            app.SelectSourceControlProvider("None");
        }

        /// <summary>
        /// Verify the glyph change APIs update the glyphs appropriately
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SourceControlGlyphChanged() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            // close any projects before switching source control...
            VsIdeTestHostContext.Dte.Solution.Close();

            app.SelectSourceControlProvider("Test Source Provider");

            var project = DebugProject.OpenProject(@"Python.VS.TestData\SourceControl.sln");

            Assert.AreEqual(1, TestSccProvider.LoadedProjects.Count);
            var sccProject = TestSccProvider.LoadedProjects.First();
            Microsoft.TestSccPackage.FileInfo fileInfo = null;
            foreach (var curFile in sccProject.Files) {
                if (curFile.Key.EndsWith("Program.py")) {
                   fileInfo = curFile.Value;
                   break;
                }
            }
            Assert.AreNotEqual(null, fileInfo);

            fileInfo.GlyphChanged(VsStateIcon.STATEICON_CHECKEDOUTEXCLUSIVEOTHER);
            
            var programPy = project.ProjectItems.Item("Program.py");
            Assert.AreEqual(programPy.Properties.Item("SourceControlStatus").Value, "CHECKEDOUTEXCLUSIVEOTHER");

            fileInfo.StateIcon = VsStateIcon.STATEICON_READONLY;
            sccProject.AllGlyphsChanged();

            Assert.AreEqual(programPy.Properties.Item("SourceControlStatus").Value, "READONLY");

            app.Dte.Solution.Close();

            Assert.AreEqual(0, TestSccProvider.LoadedProjects.Count);
            if (TestSccProvider.Failures.Count != 0) {
                Assert.Fail(String.Join(Environment.NewLine, TestSccProvider.Failures));
            }

            app.SelectSourceControlProvider("None");
        }

        /// <summary>
        /// Verify we don't get called for a project which doesn't have source control enabled.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SourceControlNoControl() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            // close any projects before switching source control...
            VsIdeTestHostContext.Dte.Solution.Close();

            app.SelectSourceControlProvider("Test Source Provider");

            var project = DebugProject.OpenProject(@"Python.VS.TestData\NoSourceControl.sln");

            Assert.AreEqual(0, TestSccProvider.LoadedProjects.Count);

            app.Dte.Solution.Close();

            Assert.AreEqual(0, TestSccProvider.LoadedProjects.Count);
            if (TestSccProvider.Failures.Count != 0) {
                Assert.Fail(String.Join(Environment.NewLine, TestSccProvider.Failures));
            }

            app.SelectSourceControlProvider("None");
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void NewProject() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var newProjDialog = app.FileNewProject();

            newProjDialog.FocusLanguageNode();

            var consoleApp = newProjDialog.ProjectTypes.FindItem("Python Application");
            consoleApp.SetFocus();

            newProjDialog.ClickOK();

            // wait for new solution to load...
            for (int i = 0; i < 100 && app.Dte.Solution.Projects.Count == 0; i++) {
                System.Threading.Thread.Sleep(1000);
            }

            Assert.AreEqual(1, app.Dte.Solution.Projects.Count);
            
            Assert.AreNotEqual(null, app.Dte.Solution.Projects.Item(1).ProjectItems.Item(Path.GetFileNameWithoutExtension(app.Dte.Solution.FullName) + ".py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TransferItem() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\HelloWorld.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            string filename, basename;
            int i = 0;            
            do {
                i++;
                basename = "test" + i + " .py";
                filename = Path.Combine(Path.GetTempPath(), basename);
            } while (System.IO.File.Exists(filename));

            System.IO.File.WriteAllText(filename, "def f(): pass");

            var fileWindow = app.Dte.ItemOperations.OpenFile(filename);

            app.MoveCurrentFileToProject("HelloWorld");

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;
            Assert.AreNotEqual(null, window.WaitForItem("Solution 'HelloWorld' (1 project)", "HelloWorld", basename));

            Assert.AreEqual(fileWindow.Caption, basename);

            System.IO.File.Delete(filename);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAs() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\SaveAsUI.sln");
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            app.OpenSolutionExplorer();
            var solutionTree = app.SolutionExplorerTreeView;

            // open and edit the file
            var folderNode = solutionTree.FindItem("Solution 'SaveAsUI' (1 project)", "HelloWorld", "Program.py");
            folderNode.SetFocus();
            Keyboard.PressAndRelease(Key.Enter);
            
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
                        
            var selection = ((TextSelection)window.Selection);
            selection.SelectAll();
            selection.Delete();
            
            // save under a new file name
            var saveDialog = app.SaveAs();
            string oldName = saveDialog.FileName;
            saveDialog.FileName = "Program2.py";
            saveDialog.Save();

            Assert.AreNotEqual(null, solutionTree.WaitForItem("Solution 'SaveAsUI' (1 project)", "HelloWorld", "Program2.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ExtensionReference() {
            var curDir =System.IO.Directory.GetCurrentDirectory();
            var project = DebugProject.OpenProject(@"Python.VS.TestData\ExtensionReference.sln");
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);

            app.OpenSolutionExplorer();
            var solutionTree = app.SolutionExplorerTreeView;

            // open the solution, add a reference to our spam.dll Python extension module
            var folderNode = solutionTree.FindItem("Solution 'ExtensionReference' (1 project)", "ExtensionReference", "References");
            folderNode.SetFocus();
            Keyboard.PressAndRelease(Key.Apps); // context menu
            Keyboard.PressAndRelease(Key.R);    // Add Reference

            var dialog = new AddReferenceDialog(AutomationElement.FromHandle(app.WaitForDialog()));
            dialog.ActivateBrowseTab();

            dialog.BrowseFilename = Path.Combine(curDir, "spam.dll");
            dialog.ClickOK();

            app.WaitForDialogDismissed();

            System.Threading.Thread.Sleep(2000);
            
            // make sure the reference got added
            var spamItem = solutionTree.FindItem("Solution 'ExtensionReference' (1 project)", "ExtensionReference", "References", "spam.dll");
            Assert.IsNotNull(spamItem);

            // now open a file and make sure we get completions against the spam module
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            var doc = app.GetDocument(item.Document.FullName);
            
            doc.MoveCaret(doc.TextView.TextBuffer.CurrentSnapshot.GetLineFromLineNumber(1).Start);

            Keyboard.Type("spam.");
            
            System.Threading.Thread.Sleep(1000);
            var session = doc.WaitForSession<ICompletionSession>();
            
            var completion = session.CompletionSets.First().Completions.Select(x => x.InsertionText).FirstOrDefault(x => x == "quoxbar");
            Assert.IsNotNull(completion);

            // now clear the text we just typed
            Keyboard.Type(Key.Escape);
            for (int i = 0; i < 5; i++) {
                Keyboard.Type(Key.Back);
            }

            // remove the extension
            VsIdeTestHostContext.Dte.Solution.Projects.Item(1).ProjectItems.Item("References").ProjectItems.Item("spam.dll").Remove();
            System.Threading.Thread.Sleep(3000);

            // make sure it got removed
            spamItem = solutionTree.FindItem("Solution 'ExtensionReference' (1 project)", "ExtensionReference", "References", "spam.dll");
            Assert.AreEqual(spamItem, null);

            window.Activate();

            // and make sure we no longer offer completions on the spam module.
            Keyboard.Type("spam.");
            System.Threading.Thread.Sleep(1000);
            Assert.IsNull(doc.IntellisenseSessionStack.TopSession);
        }
    }
}
