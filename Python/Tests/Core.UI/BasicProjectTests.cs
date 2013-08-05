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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using EnvDTE;
using EnvDTE80;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools.Project.Automation;
using TestUtilities;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using VSLangProj;
using MessageBoxButton = TestUtilities.UI.MessageBoxButton;
using ST = System.Threading;

namespace PythonToolsUITests {
    [TestClass]
    public class BasicProjectTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            TestData.Deploy();
        }

        [TestCleanup]
        public void MyTestCleanup() {
            VsIdeTestHostContext.Dte.Solution.Close(false);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestSetDefaultInterpreter() {
            var props = VsIdeTestHostContext.Dte.get_Properties("Python Tools", "Interpreters");
            Assert.IsNotNull(props);

            var oldDefaultInterp = props.Item("DefaultInterpreter").Value;
            var oldDefaultVersion = props.Item("DefaultInterpreterVersion").Value;
            try {
                props.Item("DefaultInterpreter").Value = Guid.Empty;
                props.Item("DefaultInterpreterVersion").Value = "2.7";

                Assert.AreEqual(Guid.Empty, props.Item("DefaultInterpreter").Value);
                Assert.AreEqual("2.7", props.Item("DefaultInterpreterVersion").Value);
            } finally {
                props.Item("DefaultInterpreter").Value = oldDefaultInterp;
                props.Item("DefaultInterpreterVersion").Value = oldDefaultVersion;
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadPythonProject() {
            string fullPath = Path.GetFullPath(@"TestData\HelloWorld.sln");
            Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
            VsIdeTestHostContext.Dte.Solution.Open(fullPath);

            Assert.IsTrue(VsIdeTestHostContext.Dte.Solution.IsOpen, "The solution is not open");
            Assert.IsTrue(VsIdeTestHostContext.Dte.Solution.Projects.Count == 1, String.Format("Loading project resulted in wrong number of loaded projects, expected 1, received {0}", VsIdeTestHostContext.Dte.Solution.Projects.Count));

            var iter = VsIdeTestHostContext.Dte.Solution.Projects.GetEnumerator();
            iter.MoveNext();
            Project project = (Project)iter.Current;
            Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadFlavoredProject() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);            
            var project = app.OpenAndFindProject(@"TestData\FlavoredProject.sln");
            Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

            var catids = VsIdeTestHostContext.Dte.ObjectExtenders.GetContextualExtenderCATIDs();
            dynamic extender = project.Extender["WebApplication"];
            extender.StartWebServerOnDebug = true;
            extender.StartWebServerOnDebug = false;

            project.Save();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void SaveProjectAs() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

                AssertError<ArgumentNullException>(() => project.SaveAs(null));
                project.SaveAs(TestData.GetPath(@"TestData\TempFile.pyproj"));
                project.Save("");   // empty string means just save

                // try too long of a file
                try {
                    project.SaveAs("TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile.pyproj");
                    Assert.Fail();
                } catch (InvalidOperationException e) {
                    Assert.IsTrue(e.ToString().Contains("exceeds the maximum number of"));
                }

                // save to a new location
                try {
                    project.SaveAs("C:\\TempFile.pyproj");
                    Assert.Fail();
                } catch (UnauthorizedAccessException e) {
                    // Saving to a new location is now permitted, but this location will not succeed.
                    Assert.IsTrue(e.ToString().Contains("Access to the path 'C:\\TempFile.pyproj' is denied."));
                } //catch (InvalidOperationException e) {
                //    Assert.IsTrue(e.ToString().Contains("The project file can only be saved into the project location"));
                //}

                project.SaveAs(TestData.GetPath(@"TestData\TempFile.pyproj"));
                project.Save("");   // empty string means just save
                project.Delete();
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close(false);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void RenameProjectTest() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\RenameProjectTest.sln");

                // try it another way...
                project.Properties.Item("FileName").Value = "HelloWorld2.pyproj";
                Assert.AreEqual(project.Name, "HelloWorld2");

                // and yet another way...
                project.Name = "HelloWorld3";
                Assert.AreEqual(project.Name, "HelloWorld3");

                project.Name = "HelloWorld3";

                // invalid renames
                AssertError<InvalidOperationException>(() => project.Name = "");
                AssertError<InvalidOperationException>(() => project.Name = null);
                AssertError<InvalidOperationException>(() => project.Name = "TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile");
                AssertError<InvalidOperationException>(() => project.Name = "             ");
                AssertError<InvalidOperationException>(() => project.Name = "...............");
                var oldName = project.Name;
                project.Name = ".foo";
                Assert.AreEqual(project.Name, ".foo");
                project.Name = oldName;

                string projPath = TestData.GetPath(@"TestData\RenameProjectTest\HelloWorld3.pyproj");
                string movePath = TestData.GetPath(@"TestData\RenameProjectTest\HelloWorld_moved.pyproj");
                try {                    
                    File.Move(projPath, movePath);
                    AssertError<InvalidOperationException>(() => project.Name = "HelloWorld4");
                } finally {
                    File.Move(movePath, projPath);
                }

                try {
                    File.Copy(projPath, movePath);
                    AssertError<InvalidOperationException>(() => project.Name = "HelloWorld_moved");
                } finally {
                    File.Delete(movePath);
                }
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectAddItem() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");
                string fullPath = TestData.GetPath(@"TestData\HelloWorld.sln");                

                Assert.AreEqual(3, project.ProjectItems.Count);
                var item = project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"));
                
                Assert.AreEqual("LocalsTest.py", item.Properties.Item("FileName").Value);
                Assert.AreEqual(Path.Combine(Path.Combine(Path.GetDirectoryName(fullPath), "HelloWorld"), "LocalsTest.py"), item.Properties.Item("FullPath").Value);
                Assert.AreEqual(".py", item.Properties.Item("Extension").Value);

                Assert.IsTrue(item.Object is VSProjectItem);
                var vsProjItem = (VSProjectItem)item.Object;
                Assert.AreEqual(vsProjItem.DTE, VsIdeTestHostContext.Dte);
                Assert.AreEqual(vsProjItem.ContainingProject, project);
                Assert.AreEqual(vsProjItem.ProjectItem.ContainingProject, project);
                vsProjItem.ProjectItem.Open();
                Assert.AreEqual(true, vsProjItem.ProjectItem.IsOpen);
                Assert.AreEqual(true, vsProjItem.ProjectItem.Saved);
                vsProjItem.ProjectItem.Document.Close(vsSaveChanges.vsSaveChangesNo);
                Assert.AreEqual(false, vsProjItem.ProjectItem.IsOpen);
                Assert.AreEqual(VsIdeTestHostContext.Dte, vsProjItem.ProjectItem.DTE);

                Assert.AreEqual(4, project.ProjectItems.Count);

                // add an existing item
                project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\HelloWorld\Program.py"));

                Assert.AreEqual(4, project.ProjectItems.Count);
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectAddFolder() {
            try {
                string fullPath = TestData.GetPath(@"TestData\HelloWorld.sln");
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

                var folder = project.ProjectItems.AddFolder("Test\\Folder\\Name");
                var folder2 = project.ProjectItems.AddFolder("Test\\Folder\\Name2");

                // try again when it already exists
                AssertError<ArgumentException>(() => project.ProjectItems.AddFolder("Test"));

                Assert.AreEqual("Name", folder.Properties.Item("FileName").Value);
                Assert.AreEqual("Name", folder.Properties.Item("FolderName").Value);

                Assert.AreEqual(TestData.GetPath(@"TestData\HelloWorld\Test\Folder\Name\"), folder.Properties.Item("FullPath").Value);
                
                folder2.Properties.Item("FolderName").Value = "Name3";
                Assert.AreEqual("Name3", folder2.Name);
                folder2.Properties.Item("FileName").Value = "Name4";
                Assert.AreEqual("Name4", folder2.Name);

                AssertNotImplemented(() => folder.Open(""));
                AssertNotImplemented(() => folder.SaveAs(""));
                AssertNotImplemented(() => folder.Save());
                AssertNotImplemented(() => { var tmp = folder.IsOpen; });
                Assert.AreEqual(0, folder.Collection.Count);
                Assert.AreEqual(true, folder.Saved);

                Assert.AreEqual("{6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c}", folder.Kind);

                folder.ExpandView();

                folder.Delete();
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectAddFolderThroughUI() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\AddFolderExists.sln");
                var solutionExplorer = app.SolutionExplorerTreeView;

                var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)");
                var projectNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists");

                ProjectNewFolderWithName(app, solutionNode, projectNode, "A");

                var folderA = project.ProjectItems.Item("A");
                var folderANode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists", "A");

                Assert.AreEqual(TestData.GetPath("TestData\\AddFolderExists\\A\\"), folderA.Properties.Item("FullPath").Value);
                Assert.IsTrue(Directory.Exists(TestData.GetPath("TestData\\AddFolderExists\\A\\")));

                ProjectNewFolderWithName(app, solutionNode, folderANode, "B");

                var folderB = folderA.ProjectItems.Item("B");
                var folderBNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists", "A", "B");

                Assert.AreEqual(TestData.GetPath("TestData\\AddFolderExists\\A\\B\\"), folderB.Properties.Item("FullPath").Value);
                Assert.IsTrue(Directory.Exists(TestData.GetPath("TestData\\AddFolderExists\\A\\B\\")));

                ProjectNewFolderWithName(app, solutionNode, folderBNode, "C");

                var folderC = folderB.ProjectItems.Item("C");
                var folderCNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists", "A", "B", "C");

                // 817 & 836: Nested subfolders
                // Setting the wrong VirtualNodeName in FolderNode.FinishFolderAdd caused C's fullpath to be ...\AddFolderExists\B\C\
                // instead of ...\AddFolderExists\A\B\C\.
                Assert.AreEqual(TestData.GetPath("TestData\\AddFolderExists\\A\\B\\C\\"), folderC.Properties.Item("FullPath").Value);
                Assert.IsTrue(Directory.Exists(TestData.GetPath("TestData\\AddFolderExists\\A\\B\\C\\")));
            }
            finally
            {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAddExistingFolder() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\AddExistingFolder.sln");
            var solutionExplorer = app.SolutionExplorerTreeView;

            var projectNode = solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder");
            AutomationWrapper.Select(projectNode);

            var dialog = AddExistingFolder(app);
            Assert.AreEqual(dialog.Address, Path.GetFullPath(@"TestData\AddExistingFolder"));

            dialog.FolderName = Path.GetFullPath(@"TestData\AddExistingFolder\TestFolder");
            dialog.SelectFolder();

            Assert.AreNotEqual(solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder", "TestFolder"), null);
            Assert.AreNotEqual(solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder", "TestFolder", "TestFile.txt"), null);

            var subFolderNode = solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder", "SubFolder");
            AutomationWrapper.Select(subFolderNode);

            dialog = AddExistingFolder(app);
            Assert.AreEqual(dialog.Address, Path.GetFullPath(@"TestData\AddExistingFolder\SubFolder"));
            dialog.FolderName = Path.GetFullPath(@"TestData\AddExistingFolder\SubFolder\TestFolder2");
            dialog.SelectFolder();

            Assert.AreNotEqual(solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder", "SubFolder", "TestFolder2"), null);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAddExistingFolderDebugging() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\AddExistingFolder.sln");
            var window = project.ProjectItems.Item("Program.py").Open();
            window.Activate();

            var docWindow = app.GetDocument(window.Document.FullName);

            var solutionExplorer = app.SolutionExplorerTreeView;
            app.Dte.ExecuteCommand("Debug.Start");
            app.WaitForMode(dbgDebugMode.dbgRunMode);

            app.OpenSolutionExplorer();
            var projectNode = solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder");
            AutomationWrapper.Select(projectNode);

            try {
                VsIdeTestHostContext.Dte.ExecuteCommand("ProjectandSolutionContextMenus.Project.Add.Existingfolder");

                // try and dismiss the dialog if we successfully executed
                try {
                    var dialog = app.WaitForDialog();
                    Keyboard.Type(System.Windows.Input.Key.Escape);
                } finally {
                    Assert.Fail("Was able to add an existing folder");
                }
            } catch (COMException) {
            }
            app.Dte.ExecuteCommand("Debug.StopDebugging");
            app.WaitForMode(dbgDebugMode.dbgDesignMode);

            projectNode = solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder");
            AutomationWrapper.Select(projectNode);

            var addDialog = AddExistingFolder(app);
            Assert.AreEqual(addDialog.Address, Path.GetFullPath(@"TestData\AddExistingFolder"));

            addDialog.FolderName = Path.GetFullPath(@"TestData\AddExistingFolder\TestFolder");
            addDialog.SelectFolder();

            Assert.AreNotEqual(solutionExplorer.WaitForItem("Solution 'AddExistingFolder' (1 project)", "AddExistingFolder", "TestFolder"), null);
        }

        private static SelectFolderDialog AddExistingFolder(VisualStudioApp app) {
            ThreadPool.QueueUserWorkItem((_) => VsIdeTestHostContext.Dte.ExecuteCommand("ProjectandSolutionContextMenus.Project.Add.Python.Existingfolder"));
            var addFolderDialog = app.WaitForDialog();
            var dialog = new SelectFolderDialog(addFolderDialog);
            return dialog;
        }
        
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectBuild() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

                VsIdeTestHostContext.Dte.Solution.SolutionBuild.Build(true);
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectRenameAndDeleteItem() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\RenameItemsTest.sln");

                VsIdeTestHostContext.Dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);

                // invalid renames
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "              ");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "..............");
                project.ProjectItems.Item("ProgramX.py").Name = ".foo";
                project.ProjectItems.Item(".foo").Name = "ProgramX.py";
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "ProgramY.py");

                project.ProjectItems.Item("ProgramX.py").Name = "PrOgRaMX.py";
                project.ProjectItems.Item("ProgramX.py").Name = "ProgramX.py";

                project.ProjectItems.Item("ProgramX.py").Name = "Program2.py";

                bool foundProg2 = false;
                foreach (ProjectItem item in project.ProjectItems) {
                    Debug.Assert(item.Name != "ProgramX.py");
                    if (item.Name == "Program2.py") {
                        foundProg2 = true;
                    }
                }
                Assert.IsTrue(foundProg2);

                // rename using a different method...
                project.ProjectItems.Item("ProgramY.py").Properties.Item("FileName").Value = "Program3.py";
                bool foundProg3 = false;
                foreach (ProjectItem item in project.ProjectItems) {
                    Debug.Assert(item.Name != "ProgramY.py");
                    if (item.Name == "Program3.py") {
                        foundProg3 = true;
                    }
                }

                project.ProjectItems.Item("Program3.py").Remove();

                Assert.IsTrue(foundProg3);

                Assert.AreEqual(0, project.ProjectItems.Item("ProgramZ.py").ProjectItems.Count);
                AssertError<ArgumentNullException>(() => project.ProjectItems.Item("ProgramZ.py").SaveAs(null));
                // try Save As, this won't rename it in the project.
                project.ProjectItems.Item("ProgramZ.py").SaveAs("Program4.py");

                bool foundProgZ = false;
                foreach (ProjectItem item in project.ProjectItems) {
                    Debug.Assert(item.Name
                        != "Program4.py");
                    if (item.Name == "ProgramZ.py") {
                        foundProgZ = true;
                    }
                }
                Assert.IsTrue(foundProgZ);

                var newItem = project.ProjectItems.AddFromTemplate(((Solution2)VsIdeTestHostContext.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem2.py");
                newItem.Open();

                // save w/o filename, w/ filename that matches, and w/ wrong filename
                newItem.Save();
                newItem.Save("TemplateItem2.py");
                AssertError<InvalidOperationException>(() => newItem.Save("WrongFilename.py"));

                // rename something in a folder...
                project.ProjectItems.Item("SubFolder").ProjectItems.Item("SubItem.py").Name = "NewSubItem.py";

                project.ProjectItems.Item("ProgramDelete.py").Delete();

                // rename the folder
                project.ProjectItems.Item("SubFolder").Name = "SubFolderNew";
                Assert.AreEqual(project.ProjectItems.Item("SubFolderNew").Name, "SubFolderNew");
                project.Save();
                var projectFileContents = File.ReadAllText(project.FullName);
                Assert.AreNotEqual(-1, projectFileContents.IndexOf("\"SubFolderNew"), "Failed to find relative path for SubFolder");
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ChangeDefaultInterpreterProjectClosed() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            try {
                var model = (IComponentModel)VsIdeTestHostContext.ServiceProvider.GetService(typeof(SComponentModel));
                var service = model.GetService<IInterpreterOptionsService>();

                var currentDefault = service.DefaultInterpreter;

                app.SelectDefaultInterpreter(service.Interpreters.First(i => i != currentDefault).Description);

                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");
                VsIdeTestHostContext.Dte.Solution.Close();

                Assert.AreNotEqual(currentDefault, service.DefaultInterpreter);

                System.Threading.Thread.Sleep(1000);
                app.SelectDefaultInterpreter(currentDefault.Description);

                Assert.AreEqual(currentDefault, service.DefaultInterpreter);
            } finally {
                app.DismissAllDialogs();
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddTemplateItem() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

                project.ProjectItems.AddFromTemplate(((Solution2)VsIdeTestHostContext.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                bool foundItem = false;
                foreach (ProjectItem item in project.ProjectItems) {
                    if (item.Name == "TemplateItem.py") {
                        foundItem = true;
                    }
                }
                Assert.IsTrue(foundItem);
                Assert.AreEqual(false, project.Saved);
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAutomationProperties() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");
                
                int propCount = 0;
                foreach (Property prop in project.Properties) {
                    Assert.AreEqual(project.Properties.Item(propCount + 1).Value, project.Properties.Item(prop.Name).Value);
                    Assert.AreEqual(project.Properties.Item(propCount + 1).Value, project.Properties.Item(prop.Name).get_IndexedValue(null));
                    Assert.AreEqual(VsIdeTestHostContext.Dte, project.Properties.Item(propCount + 1).DTE);
                    Assert.AreEqual(0, project.Properties.Item(propCount + 1).NumIndices);
                    Assert.AreNotEqual(null, project.Properties.Item(propCount + 1).Parent);
                    Assert.AreEqual(null, project.Properties.Item(propCount + 1).Application);
                    Assert.AreNotEqual(null, project.Properties.Item(propCount + 1).Collection);
                    propCount++;
                }

                Assert.AreEqual(propCount, project.Properties.Count);

                Assert.AreEqual(project.Properties.DTE, VsIdeTestHostContext.Dte);
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestAutomationProject() {
            try {
                var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
                var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

                Assert.AreEqual("{888888a0-9f3d-457c-b088-3a5042f75d52}", project.Kind);
                // we don't yet expose a VSProject interface here, if we did we'd need tests for it, but it doesn't support
                // any functionality we care about/implement yet.
                Assert.AreEqual(null, project.Object);

                Assert.AreEqual(true, project.Saved);
                project.Saved = false;
                Assert.AreEqual(false, project.Saved);
                project.Saved = true;

                Assert.AreEqual(null, project.Globals);
                Assert.AreEqual("{c0000016-9ab0-4d58-80e6-54f29e8d3144}", project.ExtenderCATID);
                var extNames = project.ExtenderNames;
                Assert.AreEqual(typeof(string[]), extNames.GetType());
                Assert.AreEqual(0, ((string[])extNames).Length);
                Assert.AreEqual(null, project.ParentProjectItem);
                Assert.AreEqual(null, project.CodeModel);
                AssertError<ArgumentNullException>(() => project.get_Extender(null));
                AssertError<COMException>(() => project.get_Extender("DoesNotExist"));
                Assert.AreEqual(null, project.Collection);

                foreach (ProjectItem item in project.ProjectItems) {
                    Assert.AreEqual(item.Name, project.ProjectItems.Item(1).Name);
                    break;
                }

                Assert.AreEqual(VsIdeTestHostContext.Dte, project.ProjectItems.DTE);
                Assert.AreEqual(project, project.ProjectItems.Parent);
                Assert.AreEqual(null, project.ProjectItems.Kind);

                AssertError<ArgumentException>(() => project.ProjectItems.Item(-1));
                AssertError<ArgumentException>(() => project.ProjectItems.Item(0));
            } finally {
                VsIdeTestHostContext.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestProjectItemAutomation() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

            var item = project.ProjectItems.Item("Program.py");
            Assert.AreEqual(null, item.ExtenderNames);
            Assert.AreEqual(null, item.ExtenderCATID);
            Assert.AreEqual(null, item.SubProject);
            Assert.AreEqual("{6bb5f8ee-4483-11d3-8bcf-00c04f8ec28c}", item.Kind);
            Assert.AreEqual(null, item.ConfigurationManager);
            Assert.AreNotEqual(null, item.Collection.Item("Program.py"));
            AssertError<ArgumentOutOfRangeException>(() => item.get_FileNames(-1));
            AssertNotImplemented(() => item.Saved = false);


            AssertError<ArgumentException>(() => item.get_IsOpen("ThisIsNotTheGuidYoureLookingFor"));
            AssertError<ArgumentException>(() => item.Open("ThisIsNotTheGuidYoureLookingFor"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void TestRelativePaths() {
            // link to outside file should show up as top-level item
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\RelativePaths.sln");

            var item = project.ProjectItems.Item("Program.py");
            Assert.IsNotNull(item);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void ProjectConfiguration() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");
            
            project.ConfigurationManager.AddConfigurationRow("NewConfig", "Debug", true);
            project.ConfigurationManager.AddConfigurationRow("NewConfig2", "UnknownConfig", true);

            AssertError<ArgumentException>(() => project.ConfigurationManager.DeleteConfigurationRow(null));
            project.ConfigurationManager.DeleteConfigurationRow("NewConfig");
            project.ConfigurationManager.DeleteConfigurationRow("NewConfig2");
            
            var debug = project.ConfigurationManager.Item("Debug", "Any CPU");
            Assert.AreEqual(debug.IsBuildable, false);            

            Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.PlatformNames)[0]);
            Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.SupportedPlatforms)[0]);

            Assert.AreEqual(null, project.ConfigurationManager.ActiveConfiguration.Object);
            
            //var workingDir = project.ConfigurationManager.ActiveConfiguration.Properties.Item("WorkingDirectory");
            //Assert.AreEqual(".", workingDir);

            // not supported
            AssertError<COMException>(() => project.ConfigurationManager.AddPlatform("NewPlatform", "Any CPU", false));
            AssertError<COMException>(() => project.ConfigurationManager.DeletePlatform("NewPlatform"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DependentNodes() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\XamlProject.sln");

            Assert.AreNotEqual(null, project.ProjectItems.Item("Program.py").ProjectItems.Item("Program.xaml"));
            project.ProjectItems.Item("Program.py").Name = "NewProgram.py";
            
            Assert.AreNotEqual(null, project.ProjectItems.Item("NewProgram.py").ProjectItems.Item("NewProgram.xaml"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DotNetReferences() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\XamlProject.sln");

            var references = project.ProjectItems.Item("References");
            foreach (var pf in new[] { references.ProjectItems.Item("PresentationFramework"), references.ProjectItems.Item(1) }) {
                Assert.AreEqual("PresentationFramework", pf.Name);
                Assert.AreEqual(typeof(OAReferenceItem), pf.GetType());
                AssertError<InvalidOperationException>(() => pf.Delete());
                AssertError<InvalidOperationException>(() => pf.Open(""));
            }
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET project.  Makes sure we get completion after a build, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DotNetProjectReferences() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\ProjectReference\ProjectReference.sln", expectedProjects: 2, projectName: "PythonApplication");

            VsIdeTestHostContext.Dte.Solution.SolutionBuild.Build(WaitForBuildToFinish: true);
            var program = project.ProjectItems.Item("Program.py");
            var window = program.Open();
            window.Activate();

            var doc = app.GetDocument(program.Document.FullName);
            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "str");

            var lib = GetProject("ClassLibrary");
            var classFile = lib.ProjectItems.Item("Class1.cs");
            window = classFile.Open();
            window.Activate();
            doc = app.GetDocument(classFile.Document.FullName);
            
            doc.Invoke(() => {
                using (var edit = doc.TextView.TextBuffer.CreateEdit()) {
                    edit.Delete(0, doc.TextView.TextBuffer.CurrentSnapshot.Length);
                    edit.Insert(0, @"namespace ClassLibrary1
{
    public class Class1
    {
        public bool X
        {
            get { return true; }
        }
    }
}
");
                    edit.Apply();
                }
            });
            classFile.Save();

            // rebuild
            VsIdeTestHostContext.Dte.Solution.SolutionBuild.Build(WaitForBuildToFinish: true);

            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "bool");
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void DotNetAssemblyReferences() {
            CompileFile("ClassLibrary.cs", "ClassLibrary.dll");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);            
            var project = app.OpenAndFindProject(@"TestData\AssemblyReference\AssemblyReference.sln");
            
            var program = project.ProjectItems.Item("Program.py");
            var window = program.Open();
            window.Activate();

            System.Threading.Thread.Sleep(2000);

            var doc = app.GetDocument(program.Document.FullName);
            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "str");

            CompileFile("ClassLibraryBool.cs", "ClassLibrary.dll");

            System.Threading.Thread.Sleep(2000);

            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "bool");
        }


        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultipleDotNetAssemblyReferences() {
            CompileFile("ClassLibrary.cs", "ClassLibrary.dll");
            CompileFile("ClassLibrary2.cs", "ClassLibrary2.dll");

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\AssemblyReference\AssemblyReference.sln");

            var program = project.ProjectItems.Item("Program2.py");
            var window = program.Open();
            window.Activate();

            System.Threading.Thread.Sleep(2000);

            var doc = app.GetDocument(program.Document.FullName);
            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "str");
            Assert.AreEqual(GetVariableAnalysis("b", snapshot).Values.First().Description, "int");

            // verify getting signature help doesn't crash...  This used to crash because IronPython
            // used the empty path for an assembly and throws an exception.  We now handle the exception
            // in RemoteInterpreter.GetBuiltinFunctionDocumentation and RemoteInterpreter.GetPythonTypeDocumentation
            Assert.AreEqual(GetSignatures("Class1.Foo(", snapshot).Signatures.First().Documentation, "");

            // recompile one file, we should still have type info for both DLLs, with one updated
            CompileFile("ClassLibraryBool.cs", "ClassLibrary.dll");

            System.Threading.Thread.Sleep(2000);
            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "bool");
            Assert.AreEqual(GetVariableAnalysis("b", snapshot).Values.First().Description, "int");

            // recompile the 2nd file, we should then have updated types for both DLLs
            CompileFile("ClassLibrary2Char.cs", "ClassLibrary2.dll");
            System.Threading.Thread.Sleep(2000);

            Assert.AreEqual(GetVariableAnalysis("a", snapshot).Values.First().Description, "bool");
            Assert.AreEqual(GetVariableAnalysis("b", snapshot).Values.First().Description, "Char");
        }

        private static ExpressionAnalysis GetVariableAnalysis(string variable, ITextSnapshot snapshot) {
            var index = snapshot.GetText().IndexOf(variable + " =");
            var span = snapshot.CreateTrackingSpan(new Span(index, 1), SpanTrackingMode.EdgeInclusive);
            return snapshot.AnalyzeExpression(span);
        }

        private static SignatureAnalysis GetSignatures(string text, ITextSnapshot snapshot) {
            var index = snapshot.GetText().IndexOf(text);
            var span = snapshot.CreateTrackingSpan(new Span(index, text.Length), SpanTrackingMode.EdgeInclusive);
            return snapshot.GetSignatures(span);
        }

        private static void CompileFile(string file, string outname) {
            string loc = typeof(string).Assembly.Location;
            var psi = new ProcessStartInfo(Path.Combine(Path.GetDirectoryName(loc), "csc.exe"), "/target:library /out:" + outname + " " + file);
            psi.WorkingDirectory = TestData.GetPath(@"TestData\\AssemblyReference\\PythonApplication");
            var proc = System.Diagnostics.Process.Start(psi);
            proc.WaitForExit();
            Assert.AreEqual(proc.ExitCode, 0);
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MultiProjectAnalysis() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\MultiProjectAnalysis\MultiProjectAnalysis.sln", projectName: "PythonApplication", expectedProjects: 2);

            var program = project.ProjectItems.Item("Program.py");
            var window = program.Open();
            window.Activate();

            System.Threading.Thread.Sleep(2000);

            var doc = app.GetDocument(program.Document.FullName);
            var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
            var index = snapshot.GetText().IndexOf("a =");
            var span = snapshot.CreateTrackingSpan(new Span(index, 1), SpanTrackingMode.EdgeInclusive);
            var analysis = snapshot.AnalyzeExpression(span);
            Assert.AreEqual(analysis.Values.First().Description, "int");
        }

        private static Project GetProject(string name) {
            var iter = VsIdeTestHostContext.Dte.Solution.Projects.GetEnumerator();
            while (iter.MoveNext()) {
                if (((Project)iter.Current).Name == name) {
                    return (Project)iter.Current;
                }
            }
            return null;
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddFolderExists() {
            Directory.CreateDirectory(TestData.GetPath(@"TestData\\AddFolderExists\\X"));
            Directory.CreateDirectory(TestData.GetPath(@"TestData\\AddFolderExists\\Y"));

            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\AddFolderExists.sln");
            var solutionExplorer = app.SolutionExplorerTreeView;

            var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)");
            

            var projectNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists");

            ProjectNewFolder(app, solutionNode, projectNode);

            System.Threading.Thread.Sleep(1000);
            Keyboard.Type("."); // bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

#if DEV11_OR_LATER
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot contain any of the following characters");
#else
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, ". is an invalid filename");
#endif
            System.Threading.Thread.Sleep(1000);

            Keyboard.Type(".."); // another bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

#if DEV11_OR_LATER
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot contain any of the following characters");
#else
            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, ".. is an invalid filename");
#endif
            System.Threading.Thread.Sleep(1000);

            Keyboard.Type("Y"); // another bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

            VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "The folder Y already exists.");
            System.Threading.Thread.Sleep(1000);

            Keyboard.Type("X"); // directory exists, but is ok.
            Keyboard.Type(System.Windows.Input.Key.Enter);

            // item should be successfully added now.
            WaitForItem(project, "X");
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void AddFolderCopyAndPasteFile() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\AddFolderCopyAndPasteFile.sln");
            var solutionExplorer = app.SolutionExplorerTreeView;
            var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)");

            var projectNode = solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)", "AddFolderCopyAndPasteFile");

            var programNode = solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)", "AddFolderCopyAndPasteFile", "Program.py");
            Mouse.MoveTo(programNode.GetClickablePoint());
            Mouse.Click();
            Keyboard.ControlC();

            Keyboard.ControlV();
            System.Threading.Thread.Sleep(2000);

            // Make sure that copy/paste directly under the project node works:
            // http://pytools.codeplex.com/workitem/738
            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)", "AddFolderCopyAndPasteFile", "Program - Copy.py"));

            ProjectNewFolder(app, solutionNode, projectNode);

            System.Threading.Thread.Sleep(1000);
            Keyboard.Type("Foo");
            Keyboard.Type(System.Windows.Input.Key.Enter);

            WaitForItem(project, "Foo");

            Mouse.MoveTo(programNode.GetClickablePoint());
            Mouse.Click();
            Keyboard.ControlC();

            var folderNode = solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)", "AddFolderCopyAndPasteFile", "Foo");
            Mouse.MoveTo(folderNode.GetClickablePoint());
            Mouse.Click();

            Keyboard.ControlV();
            System.Threading.Thread.Sleep(2000);

            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'AddFolderCopyAndPasteFile' (1 project)", "AddFolderCopyAndPasteFile", "Foo", "Program.py"));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyAndPasteFolder() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\CopyAndPasteFolder.sln");
            var solutionExplorer = app.SolutionExplorerTreeView;
            var solutionNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)");

            var projectNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder");

            var folderNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "X");

            // paste to project node, make sure the files are there
            StringCollection paths = new StringCollection() {
                Path.Combine(Directory.GetCurrentDirectory(), "TestData", "CopiedFiles")
            };

            ToSTA(() => Clipboard.SetFileDropList(paths));

            Mouse.MoveTo(projectNode.GetClickablePoint());
            Mouse.Click();
            Keyboard.ControlV();

            Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "CopiedFiles"));
            Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "CopiedFiles", "SomeFile.py")));
            Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "CopiedFiles", "Foo", "SomeOtherFile.py")));

            Mouse.MoveTo(folderNode.GetClickablePoint());
            Mouse.Click();

            // paste to folder node, make sure the files are there
            ToSTA(() => Clipboard.SetFileDropList(paths));
            Keyboard.ControlV();

            System.Threading.Thread.Sleep(2000);

            Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "X", "CopiedFiles"));
            Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "X", "CopiedFiles", "SomeFile.py")));
            Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "X", "CopiedFiles", "Foo", "SomeOtherFile.py")));
        }

        private static void ToSTA(ST.ThreadStart code) {
            ST.Thread t = new ST.Thread(code);
            t.SetApartmentState(ST.ApartmentState.STA);
            t.Start();
            t.Join();
        }

        /// <summary>
        /// Verify we can copy a folder with multiple items in it.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void CopyFolderWithMultipleItems() {
            // http://mpfproj10.codeplex.com/workitem/11618
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\FolderMultipleItems.sln");
            var solutionExplorer = app.SolutionExplorerTreeView;
            var solutionNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' (1 project)");

            var projectNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' (1 project)", "FolderMultipleItems");

            var folderNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' (1 project)", "FolderMultipleItems", "A");

            Mouse.MoveTo(folderNode.GetClickablePoint());
            Mouse.Click();
            Keyboard.ControlC();

            Keyboard.ControlV();
            WaitForItem(project, "A - Copy");

            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'FolderMultipleItems' (1 project)", "FolderMultipleItems", "A - Copy", "a.py"));
            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'FolderMultipleItems' (1 project)", "FolderMultipleItems", "A - Copy", "b.py"));
        }

        /// <summary>
        /// Verify we can start the interactive window when focus in within solution explorer in one of our projects.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void OpenInteractiveFromSolutionExplorer() {
            // http://pytools.codeplex.com/workitem/765
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            var project = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

            var interpreterName = "Python 2.6";
            try {
                app.SelectDefaultInterpreter(interpreterName);
            } catch (System.Windows.Automation.ElementNotAvailableException) {
                try {
                    interpreterName = "Python 2.7";
                    app.SelectDefaultInterpreter(interpreterName);
                } catch (System.Windows.Automation.ElementNotAvailableException) {
                    Assert.Inconclusive("Test requires Python 2.6 or 2.7");
                }
            }
            
            var options = GetInteractiveOptions(interpreterName + " Interactive");

            options.InlinePrompts = true;
            options.UseInterpreterPrompts = false;
            options.PrimaryPrompt = ">>> ";
            options.SecondaryPrompt = "... ";

            var solutionExplorer = app.SolutionExplorerTreeView;

            var programNode = solutionExplorer.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program.py");


            Mouse.MoveTo(programNode.GetClickablePoint());
            Mouse.Click();

            // Press Alt-I to bring up the REPL
            Keyboard.PressAndRelease(System.Windows.Input.Key.I, System.Windows.Input.Key.LeftAlt);

            Keyboard.Type("print('hi')\r");
            var interactive = app.GetInteractiveWindow(interpreterName + " Interactive");
            interactive.WaitForTextEnd("hi", ">>> ");
        }

        protected static IPythonInteractiveOptions GetInteractiveOptions(string name) {
            Debug.Assert(name.EndsWith(" Interactive"));
            return ((IPythonOptions)VsIdeTestHostContext.Dte.GetObject("VsPython")).GetInteractiveOptions(name.Substring(0, name.Length - " Interactive".Length));
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void LoadProjectWithDuplicateItems() {
            var app = new VisualStudioApp(VsIdeTestHostContext.Dte);
            var solution = app.OpenAndFindProject(@"TestData\DuplicateItems.sln");

            var itemCount = new Dictionary<string, int>();

            CountNames(itemCount, solution.ProjectItems);

            CountIs(itemCount, "A", 1);
            CountIs(itemCount, "B", 1);
            CountIs(itemCount, "a.py", 1);
            CountIs(itemCount, "b.py", 1);
            CountIs(itemCount, "Program.py", 1);
            CountIs(itemCount, "HelloWorld.pyproj", 1);
            CountIs(itemCount, "HelloWorld.py", 0);     // not included because the actual name is Program.py
        }


#if DEV11_OR_LATER
        private static IEnumerable<EnvDTE.Window> GetOpenDocumentWindows(DTE dte) {
            foreach (var win in VsIdeTestHostContext.Dte.Windows.OfType<EnvDTE.Window>()) {
                if (win.Document != null) {
                    yield return win;
                }
            }
        }
        
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void PreviewFile() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            var solution = app.OpenAndFindProject(@"TestData\HelloWorld.sln");

            var dte = VsIdeTestHostContext.Dte;

            foreach (var win in GetOpenDocumentWindows(dte)) {
                if (win.Document != null) {
                    win.Close(vsSaveChanges.vsSaveChangesNo);
                }
            }
            
            Assert.AreEqual(0, GetOpenDocumentWindows(dte).Count());

            app.OpenSolutionExplorer();

            Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld").GetClickablePoint());
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, GetOpenDocumentWindows(dte).Count());
            
            Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program.py").GetClickablePoint());
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            try {
                app.WaitForDocument("Program.py");
            } catch (InvalidOperationException) {
                Assert.Fail("Document was not opened");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void PreviewMissingFile() {
            var app = new PythonVisualStudioApp(VsIdeTestHostContext.Dte);
            var solution = app.OpenAndFindProject(@"TestData\MissingFiles.sln");

            foreach (var win in GetOpenDocumentWindows(VsIdeTestHostContext.Dte)) {
                if (win.Document != null) {
                    win.Close(vsSaveChanges.vsSaveChangesNo);
                }
            }

            Assert.AreEqual(0, GetOpenDocumentWindows(VsIdeTestHostContext.Dte).Count());

            app.OpenSolutionExplorer();

            Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' (1 project)", "HelloWorld").GetClickablePoint());
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, GetOpenDocumentWindows(VsIdeTestHostContext.Dte).Count());

            Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' (1 project)", "HelloWorld", "Program2.py").GetClickablePoint());
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, GetOpenDocumentWindows(VsIdeTestHostContext.Dte).Count());
        }

#endif

        private static void CountIs(Dictionary<string, int> count, string key, int expected){
            int actual;
            if (!count.TryGetValue(key, out actual)) {
                actual = 0;
            }
            Assert.AreEqual(expected, actual, "count[" + key + "]");
        }

        private static void CountNames(Dictionary<string, int> count, ProjectItems items) {
            if (items == null) {
                return;
            }

            foreach (var item in items.OfType<ProjectItem>()) {
                if (!string.IsNullOrEmpty(item.Name)) {
                    int value;
                    if (!count.TryGetValue(item.Name, out value)) {
                        value = 0;
                    }
                    count[item.Name] = value + 1;
                }
                CountNames(count, item.ProjectItems);
            }
        }

        private static void ProjectNewFolder(VisualStudioApp app, System.Windows.Automation.AutomationElement solutionNode, System.Windows.Automation.AutomationElement projectNode) {
            // Project menu can take a little while to appear...
            for (int i = 0; i < 10; i++) {
                Mouse.MoveTo(projectNode.GetClickablePoint());
                Mouse.Click();
                projectNode.SetFocus();
                try {
                    app.Dte.ExecuteCommand("Project.NewFolder");
                    break;
                } catch {
                }

                Mouse.MoveTo(solutionNode.GetClickablePoint());
                Mouse.Click();
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void ProjectNewFolderWithName(VisualStudioApp app, System.Windows.Automation.AutomationElement solutionNode, System.Windows.Automation.AutomationElement projectNode, string name) {
            Mouse.MoveTo(projectNode.GetClickablePoint());
            Mouse.Click(System.Windows.Input.MouseButton.Right);

            System.Threading.Thread.Sleep(500);

            Keyboard.Type("d");
            Keyboard.PressAndRelease(System.Windows.Input.Key.Right);
            Keyboard.Type("d");

            System.Threading.Thread.Sleep(500);

            Keyboard.Type(name);
            Keyboard.Type("\n");

            System.Threading.Thread.Sleep(1000);
        }

        private static ProjectItem WaitForItem(Project project, string name) {
            bool found = false;
            ProjectItem item = null;
            for (int i = 0; i < 10; i++) {
                try {
                    item = project.ProjectItems.Item(name);
                    if (item != null) {
                        found = true;
                        break;
                    }
                } catch (ArgumentException) {
                }
                // wait for the edit to complete
                System.Threading.Thread.Sleep(1000);
            }
            Assert.IsTrue(found);
            return item;
        }

        private static void AssertNotImplemented(Action action) {
            AssertError<NotImplementedException>(action);
        }

        private static void AssertError<T>(Action action) where T : Exception {
            try {
                action();
                Assert.Fail();
            } catch (T) {
            }
        }
    }
}
