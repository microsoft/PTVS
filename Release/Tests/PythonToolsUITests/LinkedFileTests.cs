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
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using EnvDTE;
using Microsoft.PythonTools.Project;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using MSBuild = Microsoft.Build.Evaluation;

namespace PythonToolsUITests {
    [TestClass]
    public class LinkedFileTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameLinkedNode() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // implicitly linked node
            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "ImplicitLinkedFile.py");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            try {
                app.Dte.ExecuteCommand("File.Rename");
                Assert.Fail("Should have failed to rename");
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }


            // explicitly linked node
            var explicitLinkedFile = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "ExplicitDir","ExplicitLinkedFile.py");
            Assert.IsNotNull(explicitLinkedFile, "explicitLinkedFile");
            explicitLinkedFile.SetFocus();

            try {
                app.Dte.ExecuteCommand("File.Rename");
                Assert.Fail("Should have failed to rename");
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }

            var autoItem = project.ProjectItems.Item("ImplicitLinkedFile.py");
            try {
                autoItem.Properties.Item("FileName").Value = "Foo";
                Assert.Fail("Should have failed to rename");
            } catch (TargetInvocationException tie) {
                Assert.AreEqual(tie.InnerException.GetType(), typeof(InvalidOperationException));
            }

            autoItem = project.ProjectItems.Item("ExplicitDir").Collection.Item("ExplicitLinkedFile.py");
            try {
                autoItem.Properties.Item("FileName").Value = "Foo";
                Assert.Fail("Should have failed to rename");
            } catch (TargetInvocationException tie) {
                Assert.AreEqual(tie.InnerException.GetType(), typeof(InvalidOperationException));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNode() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFile.py");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            // item should have moved
            var movedLinkedFile = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFile.py");
            Assert.IsNotNull(movedLinkedFile, "movedLinkedFile");

            // file should be at the same location
            Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\\MovedLinkedFile.py")));
            Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\\MoveToFolder\\MovedLinkedFile.py")));

            // now move it back
            movedLinkedFile.SetFocus();
            app.Dte.ExecuteCommand("Edit.Cut");

            var originalFolder = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles");
            originalFolder.SetFocus();
            app.Dte.ExecuteCommand("Edit.Paste");

            var movedLinkedFilePaste = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFile.py");
            Assert.IsNotNull(movedLinkedFilePaste, "movedLinkedFilePaste");

            // and make sure we didn't mess up the path in the project file
            MSBuild.Project buildProject = new MSBuild.Project(TestData.GetPath(@"TestData\LinkedFiles\LinkedFiles.pyproj"));
            bool found = false;
            foreach (var item in buildProject.GetItems("Compile")) {
                if (item.UnevaluatedInclude == "..\\MovedLinkedFile.py") {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpen() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var openWindow = project.ProjectItems.Item("MovedLinkedFileOpen.py").Open();
            Assert.IsNotNull(openWindow, "openWindow");

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFileOpen.py");
            
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            var movedLinkedFileOpen = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFileOpen.py");
            Assert.IsNotNull(movedLinkedFileOpen, "movedLinkedFileOpen");

            Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\\MovedLinkedFileOpen.py")));
            Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\\MoveToFolder\\MovedLinkedFileOpen.py")));

            // window sholudn't have changed.
            Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpen.py"), openWindow);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpenEdited() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var openWindow = project.ProjectItems.Item("MovedLinkedFileOpenEdit.py").Open();
            Assert.IsNotNull(openWindow, "openWindow");

            var selection = ((TextSelection)openWindow.Selection);
            selection.SelectAll();
            selection.Delete();

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFileOpenEdit.py");

            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            var movedLinkedFileOpenEdit = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFileOpenEdit.py");
            Assert.IsNotNull(movedLinkedFileOpenEdit, "movedLinkedFileOpenEdit");

            Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\\MovedLinkedFileOpenEdit.py")));
            Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\\MoveToFolder\\MovedLinkedFileOpenEdit.py")));

            // window sholudn't have changed.
            Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpenEdit.py"), openWindow);

            Assert.AreEqual(openWindow.Document.Saved, false);
            openWindow.Document.Save();

            Assert.AreEqual(new FileInfo(TestData.GetPath(@"TestData\\MovedLinkedFileOpenEdit.py")).Length, (long)0);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeFileExistsButNotInProject() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FileNotInProject.py");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            // item should have moved
            var fileNotInProject = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile", "FileNotInProject.py");
            Assert.IsNotNull(fileNotInProject, "fileNotInProject");

            // but it should be the linked file on disk outside of our project, not the file that exists on disk at the same location.
            var autoItem = project.ProjectItems.Item("FolderWithAFile").Collection.Item("FileNotInProject.py");
            Assert.AreEqual(TestData.GetPath(@"TestData\FileNotInProject.py"), autoItem.Properties.Item("FullPath").Value);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteLinkedNode() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "DeletedLinkedFile.py");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Delete");

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "DeletedLinkedFile.py");
            Assert.AreEqual(null, projectNode);
            Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\\DeletedLinkedFile.py")));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LinkedFileInProjectIgnored() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "LinkedInModule.py");

            Assert.IsNull(projectNode);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateLink() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();

            var autoItem = project.ProjectItems.Item("SaveAsCreateLink.py");
            var node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, false);

            var itemWindow = autoItem.Open();

            autoItem.SaveAs("..\\SaveAsCreateLink.py");


            autoItem = project.ProjectItems.Item("SaveAsCreateLink.py");
            node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, true);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFile() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();

            var autoItem = project.ProjectItems.Item("SaveAsCreateFile.py");
            var node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, true);

            var itemWindow = autoItem.Open();

            autoItem.SaveAs(TestData.GetPath(@"TestData\LinkedFiles\SaveAsCreateFile.py"));

            autoItem = project.ProjectItems.Item("SaveAsCreateFile.py");
            node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFileNewDirectory() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();

            var autoItem = project.ProjectItems.Item("SaveAsCreateFileNewDirectory.py");
            var node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, true);

            var itemWindow = autoItem.Open();

            Directory.CreateDirectory(TestData.GetPath(@"TestData\LinkedFiles\CreatedDirectory"));
            autoItem.SaveAs(TestData.GetPath(@"TestData\LinkedFiles\CreatedDirectory\SaveAsCreateFileNewDirectory.py"));


            autoItem = project.ProjectItems.Item("CreatedDirectory").Collection.Item("SaveAsCreateFileNewDirectory.py");
            node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, false);
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItem() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\ExistingItem.py");
            addExistingDlg.AddLink();

            var existingItem = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile", "ExistingItem.py");
            Assert.IsNotNull(existingItem, "existingItem");

            var searchPathNode = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Search Path", "..");
            Assert.IsNotNull(searchPathNode, "searchPathNode");
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndItemIsAlreadyLinked() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\FileNotInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "A project cannot have more than one link to the same file.", TestData.GetPath(@"TestData\FileNotInProject.py"));
        }

        /// <summary>
        /// Adding a duplicate link to the same item.
        /// 
        /// Also because the linked file dir is "LinkedFilesDir" which is a substring of "LinkedFiles" (our project name)
        /// this verifies we deal with the project name string comparison correctly (including a \ at the end of the
        /// path).
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndLinkAlreadyExists() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Bar");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\SomeLinkedFile.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (when the file only exists on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskButNotInProject() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\ExistsOnDiskButNotInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (both in the project and on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskAndInProject() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\ExistsOnDiskAndInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (in the project, but not on disk)
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsInProjectButNotOnDisk() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\ExistsInProjectButNotOnDisk.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when the file lives in the project dir but not in the directory we selected
        /// Add Existing Item from.  We should add the file to the directory where it lives.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAsLinkButFileExistsInProjectDirectory() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo");
            Assert.IsNotNull(projectNode, "projectNode");
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = TestData.GetPath(@"TestData\LinkedFiles\Foo\AddExistingInProjectDirButNotInProject.py");
            addExistingDlg.AddLink();

            var addExistingInProjectDirButNotInProject = window.WaitForItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "AddExistingInProjectDirButNotInProject.py");
            Assert.IsNotNull(addExistingInProjectDirButNotInProject, "addExistingInProjectDirButNotInProject");
        }

        /// <summary>
        /// Reaming the file name in the Link attribute is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenamedLinkedFile() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "NewNameForLinkFile.py");
            Assert.IsNull(projectNode);

            var renamedLinkFile = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "RenamedLinkFile.py");
            Assert.IsNotNull(renamedLinkFile, "renamedLinkFile");
        }
        
        /// <summary>
        /// A link path outside of our project dir will result in the link being ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BadLinkPath() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "..");
            Assert.IsNull(projectNode);

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "BadLinkPathFolder");
            Assert.IsNull(projectNode);
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedLinkIgnored() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "RootedLinkIgnored.py");
            Assert.IsNull(projectNode);
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedIncludeIgnored() {
            var project = DebuggerUITests.DebugProject.OpenProject(@"TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var rootedIncludeIgnored = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "RootedIncludeIgnored.py");
            Assert.IsNotNull(rootedIncludeIgnored, "rootedIncludeIgnored");
        }
    }
}
