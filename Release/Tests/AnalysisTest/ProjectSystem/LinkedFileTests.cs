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
using AnalysisTest.ProjectSystem;
using EnvDTE;
using Microsoft.PythonTools.Project;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using MSBuild = Microsoft.Build.Evaluation;

namespace AnalysisTest.UI {
    [TestClass]
    [DeploymentItem(@"..\\PythonTools\\CompletionDB\\", "CompletionDB")]
    [DeploymentItem(@"Python.VS.TestData\", "Python.VS.TestData")]
    public class LinkedFileTests {
        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameLinkedNode() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            // implicitly linked node
            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "ImplicitLinkedFile.py");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            try {
                app.Dte.ExecuteCommand("File.Rename");
                Assert.Fail("Should have failed to rename");
            } catch (Exception e) {
                Debug.WriteLine(e.ToString());
            }


            // explicitly linked node
            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "ExplicitDir","ExplicitLinkedFile.py");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNode() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFile.py");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            // item should have moved
            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFile.py");
            Assert.IsNotNull(projectNode);

            // file should be at the same location
            Assert.IsTrue(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MovedLinkedFile.py")));
            Assert.IsFalse(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MoveToFolder\\MovedLinkedFile.py")));

            // now move it back
            projectNode.SetFocus();
            app.Dte.ExecuteCommand("Edit.Cut");

            var originalFolder = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles");
            originalFolder.SetFocus();
            app.Dte.ExecuteCommand("Edit.Paste");

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFile.py");
            Assert.IsNotNull(projectNode);

            // and make sure we didn't mess up the path in the project file
            MSBuild.Project buildProject = new MSBuild.Project(Path.GetFullPath(@"Python.VS.TestData\LinkedFiles\LinkedFiles.pyproj"));
            bool found = false;
            foreach (var item in buildProject.GetItems("Compile")) {
                if (item.UnevaluatedInclude == "..\\MovedLinkedFile.py") {
                    found = true;
                    break;
                }
            }

            Assert.IsTrue(found);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpen() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var openWindow = project.ProjectItems.Item("MovedLinkedFileOpen.py").Open();
            Assert.IsNotNull(openWindow);

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFileOpen.py");
            
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFileOpen.py");
            Assert.IsNotNull(projectNode);

            Assert.IsTrue(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MovedLinkedFileOpen.py")));
            Assert.IsFalse(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MoveToFolder\\MovedLinkedFileOpen.py")));

            // window sholudn't have changed.
            Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpen.py"), openWindow);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeOpenEdited() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var openWindow = project.ProjectItems.Item("MovedLinkedFileOpenEdit.py").Open();
            Assert.IsNotNull(openWindow);

            var selection = ((TextSelection)openWindow.Selection);
            selection.SelectAll();
            selection.Delete();

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MovedLinkedFileOpenEdit.py");

            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "MoveToFolder", "MovedLinkedFileOpenEdit.py");
            Assert.IsNotNull(projectNode);

            Assert.IsTrue(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MovedLinkedFileOpenEdit.py")));
            Assert.IsFalse(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\MoveToFolder\\MovedLinkedFileOpenEdit.py")));

            // window sholudn't have changed.
            Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpenEdit.py"), openWindow);

            Assert.AreEqual(openWindow.Document.Saved, false);
            openWindow.Document.Save();

            Assert.AreEqual(new FileInfo(Path.GetFullPath(@"Python.VS.TestData\\MovedLinkedFileOpenEdit.py")).Length, (long)0);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveLinkedNodeFileExistsButNotInProject() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FileNotInProject.py");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Cut");

            var folderNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            folderNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Paste");

            // item should have moved
            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile", "FileNotInProject.py");
            Assert.IsNotNull(projectNode);

            // but it should be the linked file on disk outside of our project, not the file that exists on disk at the same location.
            var autoItem = project.ProjectItems.Item("FolderWithAFile").Collection.Item("FileNotInProject.py");
            Assert.AreEqual(autoItem.Properties.Item("FullPath").Value, Path.GetFullPath(@"Python.VS.TestData\\FileNotInProject.py"));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DeleteLinkedNode() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "DeletedLinkedFile.py");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            app.Dte.ExecuteCommand("Edit.Delete");

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "DeletedLinkedFile.py");
            Assert.AreEqual(null, projectNode);
            Assert.IsTrue(File.Exists(Path.GetFullPath(@"Python.VS.TestData\\DeletedLinkedFile.py")));
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LinkedFileInProjectIgnored() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "LinkedInModule.py");

            Assert.IsNull(projectNode);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateLink() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

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

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFile() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();

            var autoItem = project.ProjectItems.Item("SaveAsCreateFile.py");
            var node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, true);

            var itemWindow = autoItem.Open();

            autoItem.SaveAs(Path.GetFullPath(@"Python.VS.TestData\LinkedFiles\SaveAsCreateFile.py"));

            autoItem = project.ProjectItems.Item("SaveAsCreateFile.py");
            node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, false);
        }

        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveAsCreateFileNewDirectory() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();

            var autoItem = project.ProjectItems.Item("SaveAsCreateFileNewDirectory.py");
            var node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, true);

            var itemWindow = autoItem.Open();

            Directory.CreateDirectory(Path.GetFullPath(@"Python.VS.TestData\LinkedFiles\CreatedDirectory"));
            autoItem.SaveAs(Path.GetFullPath(@"Python.VS.TestData\LinkedFiles\CreatedDirectory\SaveAsCreateFileNewDirectory.py"));


            autoItem = project.ProjectItems.Item("CreatedDirectory").Collection.Item("SaveAsCreateFileNewDirectory.py");
            node = (HierarchyNode)autoItem.Properties.Item("Node").Value;
            Assert.AreEqual(node.IsLinkFile, false);
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItem() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\ExistingItem.py");
            addExistingDlg.AddLink();

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile", "ExistingItem.py");
            Assert.IsNotNull(projectNode);

            var searchPathNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Search Path", "..");
            Assert.IsNotNull(searchPathNode);
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndItemIsAlreadyLinked() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\FileNotInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "A project cannot have more than one link to the same file.", Path.GetFullPath(@"Python.VS.TestData\FileNotInProject.py"));
        }

        /// <summary>
        /// Adding a duplicate link to the same item.
        /// 
        /// Also because the linked file dir is "LinkedFilesDir" which is a substring of "LinkedFiles" (our project name)
        /// this verifies we deal with the project name string comparison correctly (including a \ at the end of the
        /// path).
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndLinkAlreadyExists() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Bar");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\SomeLinkedFile.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (when the file only exists on disk)
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskButNotInProject() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\ExistsOnDiskButNotInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (both in the project and on disk)
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsOnDiskAndInProject() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\ExistsOnDiskAndInProject.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (in the project, but not on disk)
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAndFileByNameExistsInProjectButNotOnDisk() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "FolderWithAFile");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();


            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\ExistsInProjectButNotOnDisk.py");
            addExistingDlg.AddLink();

            app.WaitForDialog();
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
        }

        /// <summary>
        /// Adding new linked item when the file lives in the project dir but not in the directory we selected
        /// Add Existing Item from.  We should add the file to the directory where it lives.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddExistingItemAsLinkButFileExistsInProjectDirectory() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo");
            Assert.IsNotNull(projectNode);
            projectNode.SetFocus();

            ThreadPool.QueueUserWorkItem(x => app.Dte.ExecuteCommand("Project.AddExistingItem"));

            var dialog = app.WaitForDialog();
            var addExistingDlg = new AddExistingItemDialog(dialog);
            addExistingDlg.FileName = Path.GetFullPath(@"Python.VS.TestData\LinkedFiles\Foo\AddExistingInProjectDirButNotInProject.py");
            addExistingDlg.AddLink();

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "AddExistingInProjectDirButNotInProject.py");
            Assert.IsNotNull(projectNode);
        }

        /// <summary>
        /// Reaming the file name in the Link attribute is ignored.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenamedLinkedFile() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "NewNameForLinkFile.py");
            Assert.IsNull(projectNode);

            projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "Foo", "RenamedLinkFile.py");
            Assert.IsNotNull(projectNode);
        }
        
        /// <summary>
        /// A link path outside of our project dir will result in the link being ignored.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void BadLinkPath() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

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
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedLinkIgnored() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "RootedLinkIgnored.py");
            Assert.IsNull(projectNode);
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(2), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RootedIncludeIgnored() {
            var project = DebugProject.OpenProject(@"Python.VS.TestData\LinkedFiles.sln");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var projectNode = window.FindItem("Solution 'LinkedFiles' (1 project)", "LinkedFiles", "RootedIncludeIgnored.py");
            Assert.IsNotNull(projectNode);
        }
    }
}
