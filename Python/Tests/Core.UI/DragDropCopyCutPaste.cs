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
using System.Windows;
using System.Windows.Input;
using EnvDTE;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Mouse = TestUtilities.UI.Mouse;

namespace PythonToolsUITests {
    [TestClass]
    public class DragDropCopyCutPaste {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        /// <summary>
        /// Cut item, paste into folder, paste into top-level, 2nd paste shouldn’t do anything
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultiPaste() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\MultiPaste.sln");

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var server = window.WaitForItem("Solution 'MultiPaste' (1 project)", "HelloWorld", "python.py");
                var server2 = window.WaitForItem("Solution 'MultiPaste' (1 project)", "HelloWorld", "python2.py");

                var point = server.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Click(MouseButton.Left);

                Keyboard.Press(Key.LeftShift);
                try {
                    point = server2.GetClickablePoint();
                    Mouse.MoveTo(point);
                    Mouse.Click(MouseButton.Left);
                } finally {
                    Keyboard.Release(Key.LeftShift);
                }

                Keyboard.ControlC();

                // https://pytools.codeplex.com/workitem/1144
                var folder = window.WaitForItem("Solution 'MultiPaste' (1 project)", "HelloWorld", "SubFolder");
                AutomationWrapper.Select(folder);
                Keyboard.ControlV();

                // paste once, multiple items should be pasted
                Assert.IsNotNull(window.WaitForItem("Solution 'MultiPaste' (1 project)", "HelloWorld", "SubFolder", "python.py"));
                Assert.IsNotNull(window.WaitForItem("Solution 'MultiPaste' (1 project)", "HelloWorld", "SubFolder", "python2.py"));

                AutomationWrapper.Select(folder);
                Keyboard.ControlV();

                // paste again, we should get the replace prompts...

                var dialog = new OverwriteFileDialog(app.WaitForDialog());
                dialog.Cancel();

                // https://pytools.codeplex.com/workitem/1154
                // and we shouldn't get a second dialog after cancelling...
                app.WaitForDialogDismissed();
            }
        }

        /// <summary>
        /// Cut item, paste into folder, paste into top-level, 2nd paste shouldn’t do anything
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutPastePasteItem() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutPastePasteItem.py");
                AutomationWrapper.Select(file);

                Keyboard.ControlX();

                AutomationWrapper.Select(folder);
                Keyboard.ControlV();
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder", "CutPastePasteItem.py");

                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                System.Threading.Thread.Sleep(1000);

                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutPastePasteItem.py");
            }
        }

        /// <summary>
        /// Cut item, rename it, paste into top-level, check error message
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutRenamePaste() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutRenamePaste", "CutRenamePaste.py");

                AutomationWrapper.Select(file);
                Keyboard.ControlX();

                AutomationWrapper.Select(file);
                Keyboard.Type(Key.F2);
                Keyboard.Type("CutRenamePasteNewName");
                Keyboard.Type(Key.Enter);

                System.Threading.Thread.Sleep(1000);
                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The source URL 'CutRenamePaste.py' could not be found.");
            }
        }

        /// <summary>
        /// Cut item, rename it, paste into top-level, check error message
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutDeletePaste() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutDeletePaste", "CutDeletePaste.py");

                AutomationWrapper.Select(file);
                Keyboard.ControlX();

                File.Delete(@"TestData\DragDropCopyCutPaste\CutDeletePaste\CutDeletePaste.py");

                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The item 'CutDeletePaste.py' does not exist in the project directory. It may have been moved, renamed or deleted.");

                Assert.IsNotNull(window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutDeletePaste", "CutDeletePaste.py"));
            }
        }

        /// <summary>
        /// Adds a new folder which fits exactly w/ no space left in the path name
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFileToFolderTooLong() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = OpenLongFileNameProject(app, 24);
                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find server.py, send copy & paste, verify copy of file is there
                var projectNode = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN");
                AutomationWrapper.Select(projectNode);

                Keyboard.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
                Keyboard.PressAndRelease(Key.D);
                Keyboard.PressAndRelease(Key.Right);
                Keyboard.PressAndRelease(Key.D);
                Keyboard.Type("01234567891");
                Keyboard.PressAndRelease(Key.Enter);

                var folderNode = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN", "01234567891");
                Assert.IsNotNull(folderNode);

                var serverNode = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN", "python.py");
                AutomationWrapper.Select(serverNode);
                Keyboard.ControlC();
                Keyboard.ControlV();

                var serverCopy = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN", "python - Copy.py");
                Assert.IsNotNull(serverCopy);

                AutomationWrapper.Select(serverCopy);
                Keyboard.ControlC();

                AutomationWrapper.Select(folderNode);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The filename is too long.");
            }
        }

        /// <summary>
        /// Adds a new folder which fits exactly w/ no space left in the path name
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutFileToFolderTooLong() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                var project = OpenLongFileNameProject(app, 24);
                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                // find server.py, send copy & paste, verify copy of file is there
                var projectNode = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN");
                AutomationWrapper.Select(projectNode);

                Keyboard.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
                Keyboard.PressAndRelease(Key.D);
                Keyboard.PressAndRelease(Key.Right);
                Keyboard.PressAndRelease(Key.D);
                Keyboard.Type("01234567891");
                Keyboard.PressAndRelease(Key.Enter);

                var folderNode = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN", "01234567891");
                Assert.IsNotNull(folderNode);

                var serverNode = window.FindItem("Solution 'LongFileNames' (1 project)", "LFN", "python.py");
                AutomationWrapper.Select(serverNode);
                Keyboard.ControlC();
                Keyboard.ControlV();

                var serverCopy = window.WaitForItem("Solution 'LongFileNames' (1 project)", "LFN", "python - Copy.py");
                Assert.IsNotNull(serverCopy);

                AutomationWrapper.Select(serverCopy);
                Keyboard.ControlX();

                AutomationWrapper.Select(folderNode);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The filename is too long.");
            }
        }

        internal static Project OpenLongFileNameProject(VisualStudioApp app, int spaceRemaining = 30) {
            string testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            int targetPathLength = 260 - spaceRemaining - "\\LongFileNames\\".Length;
            testDir = testDir + new string('X', targetPathLength - testDir.Length);
            Console.WriteLine("Creating long file name project ({0}) at: {1}", testDir.Length, testDir);

            Directory.CreateDirectory(testDir);
            File.Copy(TestData.GetPath(@"TestData\LongFileNames.sln"), Path.Combine(testDir, "LongFileNames.sln"));
            File.Copy(TestData.GetPath(@"TestData\LFN.pyproj"), Path.Combine(testDir, "LFN.pyproj"));

            CopyDirectory(TestData.GetPath(@"TestData\LongFileNames"), Path.Combine(testDir, "LongFileNames"));

            return app.OpenAndFindProject(Path.Combine(testDir, "LongFileNames.sln"));
        }

        private static void CopyDirectory(string source, string dest) {
            Directory.CreateDirectory(dest);

            foreach (var file in Directory.GetFiles(source)) {
                var target = Path.Combine(dest, Path.GetFileName(file));
                Console.WriteLine("Copying {0} to {1}", file, target);
                File.Copy(file, target);
            }

            foreach (var dir in Directory.GetDirectories(source)) {
                Console.WriteLine("Copying dir {0} to {1}", dir, Path.Combine(dest, dir));
                CopyDirectory(dir, Path.Combine(dest, dir));
            }
        }

        /// <summary>
        /// Cut folder, rename it, paste into top-level, check error message
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutRenamePasteFolder() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutRenamePaste", "CutRenamePasteFolder");
                AutomationWrapper.Select(file);
                Keyboard.ControlX();

                Keyboard.Type(Key.F2);
                Keyboard.Type("CutRenamePasteFolderNewName");
                Keyboard.Type(Key.Enter);
                System.Threading.Thread.Sleep(1000);

                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The source URL 'CutRenamePasteFolder' could not be found.");
            }
        }

        /// <summary>
        /// Copy a file node, drag and drop a different file, paste the node, should succeed
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopiedBeforeDragPastedAfterDrop() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                Assert.AreNotEqual(null, project);
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedBeforeDragPastedAfterDrop.py");
                Assert.AreNotEqual(null, file);
                var draggedFile = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragAndDroppedDuringCopy.py");
                Assert.AreNotEqual(null, draggedFile);
                var dragFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragDuringCopyDestination");
                Assert.AreNotEqual(null, dragFolder);

                AutomationWrapper.Select(file);
                Keyboard.ControlC();

                AutomationWrapper.Select(draggedFile);

                Mouse.MoveTo(draggedFile.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(dragFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder");
                AutomationWrapper.Select(folder);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder", "CopiedBeforeDragPastedAfterDrop.py");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedBeforeDragPastedAfterDrop.py");
            }
        }

        /// <summary>
        /// Copy a file node from project, drag and drop node from other project, should get copy, not move
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragToAnotherProject() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var draggedFile = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "!Source", "DraggedToOtherProject.py");
                var destProject = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1");
                AutomationWrapper.Select(draggedFile);

                var point = draggedFile.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Down(MouseButton.Left);

                Mouse.MoveTo(destProject.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "DraggedToOtherProject.py");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "!Source", "DraggedToOtherProject.py");
            }
        }

        /// <summary>
        /// Cut folder, paste onto itself, should report an error that the destination is the same as the source
        ///     Cannot move 'X'. The destination folder is the same as the source folder.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutFolderPasteOnSelf() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var cutFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderPasteOnSelf");
                AutomationWrapper.Select(cutFolder);

                Keyboard.ControlX();
                Keyboard.ControlV();
                VisualStudioApp.CheckMessageBox("Cannot move 'CutFolderPasteOnSelf'. The destination folder is the same as the source folder.");

                AssertFolderExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderPasteOnSelf");
                AssertFolderDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderPasteOnSelf - Copy");
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragFolderOntoSelf() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var draggedFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderOntoSelf");
                AutomationWrapper.Select(draggedFolder);

                var point = draggedFolder.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(new Point(point.X + 1, point.Y + 1));

                Mouse.Up(MouseButton.Left);

                AssertFolderExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderOntoSelf");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderOntoSelf", "File.py");
                AssertFolderDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderOntoSelf - Copy");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderOntoSelf", "File - Copy.py");
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragFolderOntoChild() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var draggedFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "ParentFolder");
                var childFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "ParentFolder", "ChildFolder");
                AutomationWrapper.Select(draggedFolder);

                var point = draggedFolder.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(childFolder.GetClickablePoint());

                Mouse.Up(MouseButton.Left);

                VisualStudioApp.CheckMessageBox("Cannot move 'ParentFolder'. The destination folder is a subfolder of the source folder.");
                app.WaitForDialogDismissed();

                draggedFolder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "ParentFolder");
                Assert.IsNotNull(draggedFolder);
                childFolder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "ParentFolder", "ChildFolder");
                Assert.IsNotNull(childFolder);
                var parentInChildFolder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "ParentFolder", "ChildFolder", "ParentFolder");
                Assert.IsNull(parentInChildFolder);
            }
        }

        /// <summary>
        /// Move a file to a location where a file with the name now already exists.  We should get an overwrite
        /// dialog, and after answering yes to overwrite the file should be moved.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutFileReplace() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "MoveDupFilename", "Foo", "Python1.py");
                Assert.AreNotEqual(null, file);
                var dest = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "MoveDupFilename");
                Assert.AreNotEqual(null, dest);

                AutomationWrapper.Select(file);

                Keyboard.ControlX();
                AutomationWrapper.Select(dest);

                Keyboard.ControlV();

                var dialog = new OverwriteFileDialog(app.WaitForDialog());
                dialog.Yes();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "MoveDupFilename", "Python1.py");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "MoveDupFilename", "Foo", "Python1.py");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutFolderAndFile() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder", "CutFolderAndFile.py");
                var dest = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                Mouse.MoveTo(folder.GetClickablePoint());
                Mouse.Click(MouseButton.Left);
                try {
                    Keyboard.Press(Key.LeftShift);
                    Mouse.MoveTo(file.GetClickablePoint());
                    Mouse.Click(MouseButton.Left);
                } finally {
                    Keyboard.Release(Key.LeftShift);
                }

                Keyboard.ControlX();
                AutomationWrapper.Select(dest);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolder", "CutFolderAndFile.py");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder");
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        ///     Cannot move 'CutFilePasteSameLocation.py'. The destination folder is the same as the source folder.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CutFilePasteSameLocation() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");
                var cutFile = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFilePasteSameLocation.py");
                AutomationWrapper.Select(cutFile);

                Keyboard.ControlX();
                AutomationWrapper.Select(project);

                Keyboard.ControlV();
                VisualStudioApp.CheckMessageBox("Cannot move 'CutFilePasteSameLocation.py'. The destination folder is the same as the source folder.");

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFilePasteSameLocation.py");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CutFilePasteSameLocation - Copy.py");
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        ///     Cannot move 'DragFolderAndFileToSameFolder'. The destination folder is the same as the source folder.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragFolderAndFileOntoSelf() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderAndFileOntoSelf");
                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragFolderAndFileOntoSelf", "File.py");

                Mouse.MoveTo(folder.GetClickablePoint());
                Mouse.Click(MouseButton.Left);
                try {
                    Keyboard.Press(Key.LeftShift);
                    Mouse.MoveTo(file.GetClickablePoint());
                    Mouse.Click(MouseButton.Left);
                } finally {
                    Keyboard.Release(Key.LeftShift);
                }

                Mouse.MoveTo(file.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(folder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                VisualStudioApp.CheckMessageBox("Cannot move 'DragFolderAndFileOntoSelf'. The destination folder is the same as the source folder.");
            }
        }

        /// <summary>
        /// Add folder from another project, folder contains items on disk which are not in the project, only items in the project should be added.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFolderFromAnotherHierarchy() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CopiedFolderWithItemsNotInProject");
                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                AutomationWrapper.Select(folder);
                Keyboard.ControlC();

                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Class.cs");

                AssertFolderExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Class.cs");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Text.txt");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyDeletePaste() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyDeletePaste", "CopyDeletePaste.py");
                var project = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                AutomationWrapper.Select(file);
                Keyboard.ControlC();

                AutomationWrapper.Select(file);
                Keyboard.Type(Key.Delete);
                app.WaitForDialog();

                Keyboard.Type("\r");

                AutomationWrapper.Select(project);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The source URL 'CopyDeletePaste.py' could not be found.");
            }
        }

        /// <summary>
        /// Drag file from another hierarchy into folder in our hierarchy, item should be added
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossHierarchyFileDragAndDrop() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyFileDragAndDrop.cs");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DropFolder");

                Mouse.MoveTo(folder.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(destFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DropFolder", "CrossHierarchyFileDragAndDrop.cs");
            }
        }

        /// <summary>
        /// Drag file from another hierarchy into folder in our hierarchy, item should be added
        ///     Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DuplicateFolderName() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DuplicateFolderName");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DuplicateFolderNameTarget");

                AutomationWrapper.Select(folder);
                Keyboard.ControlX();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.");

                // try again with drag and drop, which defaults to move
                Mouse.MoveTo(folder.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(destFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                VisualStudioApp.CheckMessageBox("Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.");
            }
        }

        /// <summary>
        /// Copy file from another hierarchy into folder in our hierarchy, item should be added
        ///     Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyDuplicateFolderName() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyDuplicateFolderName");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget");

                AutomationWrapper.Select(folder);
                Keyboard.ControlC();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                try {
                    var dialog = new OverwriteFileDialog(app.WaitForDialog());
                    Assert.IsTrue(dialog.Text.Contains("This folder already contains a folder called 'CopyDuplicateFolderName'"), "wrong text in overwrite dialog");
                    dialog.No();
                } finally {
                    app.DismissAllDialogs();
                }

                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget", "CopyDuplicateFolderName", "Python1.py");
            }
        }

        /// <summary>
        /// Cut item from one project, paste into another project, item should be removed from original project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossHierarchyCut() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCut.cs");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                AutomationWrapper.Select(file);
                Keyboard.ControlX();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCut.cs");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCut.cs");
            }
        }

        /// <summary>
        /// Cut item from one project, paste into another project, item should be removed from original project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossHierarchyCopy() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCopy.cs");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                AutomationWrapper.Select(file);
                Keyboard.ControlC();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCopy.cs");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCopy.cs");
            }
        }

        /// <summary>
        /// Cut an item from our project, paste into another project, item should be removed from our project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReverseCrossHierarchyCut() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCut.py");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1");

                AutomationWrapper.Select(file);
                Keyboard.ControlX();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCut.py");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCut.py");
            }
        }

        /// <summary>
        /// Copy an item from our project, paste into another project, item should be removed from our project
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ReverseCrossHierarchyCopy() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCopy.py");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1");

                AutomationWrapper.Select(file);
                Keyboard.ControlC();

                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyCopy.py");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyCopy.py");
            }
        }

        /// <summary>
        /// Cut an item from our project, drag and drop item from other project into ours.
        /// 
        /// Should result in a copy into our project, not a move.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CrossHierarchyDragDropAfterCut() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var cutFile = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyDragDropAfterCut.py");
                var draggedFile = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyDragDropAfterCut.cs");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                AutomationWrapper.Select(cutFile);
                Keyboard.ControlX();

                Mouse.MoveTo(draggedFile.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(destFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "CrossHierarchyDragDropAfterCut.cs");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CrossHierarchyDragDropAfterCut.cs");
            }
        }

        /// <summary>
        /// Drag item from our project to other project, copy
        /// Drag item from other project to our project, still copy back
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DoubleCrossHierarchyMove() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "!Source", "DoubleCrossHierarchy.py");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1");

                AutomationWrapper.Select(file);
                Mouse.MoveTo(file.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(destFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "DoubleCrossHierarchy.py");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "!Source", "DoubleCrossHierarchy.py");

                file = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "DoubleCrossHierarchy.cs");
                destFolder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                Mouse.MoveTo(file.GetClickablePoint());
                Mouse.Down(MouseButton.Left);
                Mouse.MoveTo(destFolder.GetClickablePoint());
                Mouse.Up(MouseButton.Left);

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DoubleCrossHierarchy.cs");
                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "DoubleCrossHierarchy.cs");
            }
        }

        /// <summary>
        /// Drag item from another project, drag same item again, prompt to overwrite, say yes, only one item should be in the hierarchy
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DragTwiceAndOverwrite() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                for (int i = 0; i < 2; i++) {
                    var file = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "ConsoleApplication1", "DragTwiceAndOverwrite.cs");
                    var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste");

                    Mouse.MoveTo(file.GetClickablePoint());
                    Mouse.Down(MouseButton.Left);
                    Mouse.MoveTo(destFolder.GetClickablePoint());
                    Mouse.Up(MouseButton.Left);
                }

                var dialog = new OverwriteFileDialog(app.WaitForDialog());
                Assert.IsTrue(dialog.Text.Contains("A file with the name 'DragTwiceAndOverwrite.cs' already exists."), "wrong text");
                dialog.Yes();

                AssertFileExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragTwiceAndOverwrite.cs");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "DragTwiceAndOverwrite - Copy.cs");
            }
        }

        /// <summary>
        /// Drag item from another project, drag same item again, prompt to overwrite, say yes, only one item should be in the hierarchy
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFolderMissingItem() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyFolderMissingItem");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder");

                AutomationWrapper.Select(folder);
                Keyboard.ControlC();
                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                // make sure no dialogs pop up
                VisualStudioApp.CheckMessageBox("The item 'Python1.py' does not exist in the project directory. It may have been moved, renamed or deleted.");

                AssertFolderExists(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "CopyFolderMissingItem");
                AssertFolderDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder", "CopyFolderMissingItem");
                AssertFileDoesntExist(window, "Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder", "Python1.py");
            }
        }

        /// <summary>
        /// Copy missing file
        /// 
        /// https://pytools.codeplex.com/workitem/1141
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyPasteMissingFile() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "MissingFile.py");
                var destFolder = window.WaitForItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder");

                AutomationWrapper.Select(folder);
                Keyboard.ControlC();
                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("The item 'MissingFile.py' does not exist in the project directory. It may have been moved, renamed or deleted.");
            }
        }

        /// <summary>
        /// Copy missing file
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveFolderExistingFile() {
            using (var app = new VisualStudioApp(VsIdeTestHostContext.Dte)) {
                app.OpenAndFindProject(@"TestData\DragDropCopyCutPaste.sln", expectedProjects: 2);

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "FolderCollision");
                var destFolder = window.FindItem("Solution 'DragDropCopyCutPaste' (2 projects)", "DragDropCopyCutPaste", "PasteFolder");

                AutomationWrapper.Select(folder);
                Keyboard.ControlX();
                AutomationWrapper.Select(destFolder);
                Keyboard.ControlV();

                VisualStudioApp.CheckMessageBox("Unable to add 'FolderCollision'. A file with that name already exists.");
            }
        }

        private static void AssertFileExists(SolutionExplorerTree window, params string[] path) {
            Assert.IsNotNull(window.WaitForItem(path), "Item not found in solution explorer" + String.Join("\\", path));

            var basePath = Path.Combine("TestData");
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsTrue(File.Exists(basePath), "File doesn't exist: " + basePath);
            switch (Path.GetExtension(path[path.Length - 1]).ToLower()) {
                case ".py":
                    Assert.AreEqual(File.ReadAllText(basePath).ToLower(), ("#" + path[path.Length - 1]).ToLower());
                    break;
                case ".cs":
                    Assert.IsTrue(File.ReadAllText(basePath).ToLower().StartsWith(("//" + path[path.Length - 1]).ToLower()), "wrong file " + basePath);
                    break;
            }
        }

        private static void AssertFileDoesntExist(SolutionExplorerTree window, params string[] path) {
            Assert.IsNull(window.FindItem(path), "Item exists in solution explorer: " + String.Join("\\", path));

            var basePath = Path.Combine("TestData");
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsFalse(File.Exists(basePath), "File exists: " + basePath);
        }

        private static void AssertFolderExists(SolutionExplorerTree window, params string[] path) {
            Assert.IsNotNull(window.WaitForItem(path), "Item not found in solution explorer" + String.Join("\\", path));

            var basePath = Path.Combine("TestData");
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsTrue(Directory.Exists(basePath), "File doesn't exist: " + basePath);
        }

        private static void AssertFolderDoesntExist(SolutionExplorerTree window, params string[] path) {
            Assert.IsNull(window.FindItem(path), "Item exists in solution explorer: " + String.Join("\\", path));

            var basePath = Path.Combine("TestData");
            for (int i = 1; i < path.Length; i++) {
                basePath = Path.Combine(basePath, path[i]);
            }
            Assert.IsFalse(Directory.Exists(basePath), "File exists: " + basePath);
        }
    }
}
