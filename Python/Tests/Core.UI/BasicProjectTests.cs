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

extern alias pythontools;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project.Automation;
using Microsoft.VisualStudioTools.VSTestHost;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using TestUtilities.UI.Python;
using VSLangProj;
using Keyboard = TestUtilities.UI.Keyboard;
using MessageBoxButton = TestUtilities.MessageBoxButton;
using Mouse = TestUtilities.UI.Mouse;
using ProcessOutput = pythontools::Microsoft.VisualStudioTools.Project.ProcessOutput;
using Thread = System.Threading.Thread;

namespace PythonToolsUITests {
    [TestClass]
    public class BasicProjectTests : SharedProjectTest {
        public static ProjectType PythonProject = ProjectTypes.First(x => x.ProjectExtension == ".pyproj");

        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void UserProjectFile() {
            using (var app = new VisualStudioApp()) {
                var project = app.CreateProject(
                    PythonVisualStudioApp.TemplateLanguageName,
                    PythonVisualStudioApp.PythonApplicationTemplate,
                    TestData.GetTempPath(),
                    "TestNewProject"
                );

                // Ensure that the user project is created
                app.ServiceProvider.GetUIThread().Invoke(() => project.GetPythonProject().SetUserProjectProperty("Test", "Value"));

                app.ExecuteCommand("File.SaveAll");

                var userFile = project.FullName + ".user";
                Assert.IsTrue(File.Exists(userFile), userFile + " does not exist on disk");
                var xml = Microsoft.Build.Construction.ProjectRootElement.Open(userFile);
                Assert.IsNotNull(xml);
                Assert.AreEqual("4.0", xml.ToolsVersion, "ToolsVersion should be '4.0'");
                Assert.AreEqual("Value", xml.Properties.Single(p => p.Name == "Test").Value, "Test property should be 'Value'");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestSetDefaultInterpreter() {
            using (var app = new VisualStudioApp()) {
                var props = app.Dte.get_Properties("Python Tools", "Interpreters");
                Assert.IsNotNull(props);

                var oldDefaultInterp = props.Item("DefaultInterpreter").Value;
                var oldDefaultVersion = props.Item("DefaultInterpreterVersion").Value;

                app.OnDispose(() => {
                    props.Item("DefaultInterpreter").Value = oldDefaultInterp;
                    props.Item("DefaultInterpreterVersion").Value = oldDefaultVersion;
                });

                props.Item("DefaultInterpreter").Value = Guid.Empty;
                props.Item("DefaultInterpreterVersion").Value = "2.7";

                Assert.AreEqual(Guid.Empty, props.Item("DefaultInterpreter").Value);
                Assert.AreEqual("2.7", props.Item("DefaultInterpreterVersion").Value);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LoadPythonProject() {
            using (var app = new VisualStudioApp()) {
                string fullPath = Path.GetFullPath(@"TestData\HelloWorld.sln");
                Assert.IsTrue(File.Exists(fullPath), "Can't find project file");
                app.OpenProject(fullPath);

                Assert.IsTrue(app.Dte.Solution.IsOpen, "The solution is not open");
                Assert.IsTrue(app.Dte.Solution.Projects.Count == 1, String.Format("Loading project resulted in wrong number of loaded projects, expected 1, received {0}", app.Dte.Solution.Projects.Count));

                var iter = app.Dte.Solution.Projects.GetEnumerator();
                Assert.IsTrue(iter.MoveNext());
                Project project = (Project)iter.Current;
                Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LoadFlavoredProject() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\FlavoredProject.sln");
                Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

                var catids = app.Dte.ObjectExtenders.GetContextualExtenderCATIDs();
                dynamic extender = project.Extender["WebApplication"];
                extender.StartWebServerOnDebug = true;
                extender.StartWebServerOnDebug = false;

                project.Save();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void SaveProjectAs() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                AssertError<ArgumentNullException>(() => project.SaveAs(null));
                project.SaveAs(TestData.GetPath(@"TestData\TempFile.pyproj"));
                project.Save("");   // empty string means just save

                // try too long of a file
                try {
                    project.SaveAs("TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile.pyproj");
                    Assert.Fail("Did not throw InvalidOperationException for long filename");
                } catch (InvalidOperationException e) {
                    Assert.IsTrue(e.ToString().Contains("exceeds the maximum number of"));
                }

                // save to a new location
                bool hasAdmin = false;
                try {
                    var path = "C:\\" + Guid.NewGuid().ToString("N");
                    File.WriteAllText(path, "");
                    File.Delete(path);
                    hasAdmin = true;
                } catch (UnauthorizedAccessException) {
                }

                // Skip this part if we have admin privileges
                if (!hasAdmin) {
                    try {
                    project.SaveAs("C:\\TempFile.pyproj");
                        Assert.Fail("Did not throw UnauthorizedAccessException for protected path");
                } catch (UnauthorizedAccessException e) {
                    // Saving to a new location is now permitted, but this location will not succeed.
                    Assert.IsTrue(e.ToString().Contains("Access to the path 'C:\\TempFile.pyproj' is denied."));
                } //catch (InvalidOperationException e) {
                //    Assert.IsTrue(e.ToString().Contains("The project file can only be saved into the project location"));
                //}
                }

                project.SaveAs(TestData.GetPath(@"TestData\TempFile.pyproj"));
                project.Save("");   // empty string means just save
                project.Delete();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void RenameProjectTest() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\RenameProjectTest.sln");

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
                project.Name = ".fob";
                Assert.AreEqual(project.Name, ".fob");
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
            }

        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectAddItem() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                string fullPath = TestData.GetPath(@"TestData\HelloWorld.sln");

                // "Python Environments", "References", "Search Paths", "Program.py"
                Assert.AreEqual(4, project.ProjectItems.Count);
                var item = project.ProjectItems.AddFromFileCopy(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"));

                Assert.AreEqual("LocalsTest.py", item.Properties.Item("FileName").Value);
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(fullPath), "HelloWorld", "LocalsTest.py"), item.Properties.Item("FullPath").Value);
                Assert.AreEqual(".py", item.Properties.Item("Extension").Value);

                Assert.IsTrue(item.Object is VSProjectItem);
                var vsProjItem = (VSProjectItem)item.Object;
                Assert.AreEqual(vsProjItem.ContainingProject, project);
                Assert.AreEqual(vsProjItem.ProjectItem.ContainingProject, project);
                vsProjItem.ProjectItem.Open();
                Assert.AreEqual(true, vsProjItem.ProjectItem.IsOpen);
                Assert.AreEqual(true, vsProjItem.ProjectItem.Saved);
                vsProjItem.ProjectItem.Document.Close(vsSaveChanges.vsSaveChangesNo);
                Assert.AreEqual(false, vsProjItem.ProjectItem.IsOpen);
                Assert.AreEqual(vsProjItem.DTE, vsProjItem.ProjectItem.DTE);

                Assert.AreEqual(5, project.ProjectItems.Count);

                // add an existing item
                project.ProjectItems.AddFromFileCopy(TestData.GetPath(@"TestData\HelloWorld\Program.py"));

                Assert.AreEqual(5, project.ProjectItems.Count);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectAddFolder() {
            string fullPath = TestData.GetPath(@"TestData\HelloWorld.sln");
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

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
                Assert.AreEqual(2, folder.Collection.Count);
                Assert.AreEqual(true, folder.Saved);

                Assert.AreEqual("{6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c}", folder.Kind);

                folder.ExpandView();

                folder.Delete();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectAddFolderThroughUI() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddFolderExists.sln");
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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAddExistingFolder() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddExistingFolder.sln");
                var solutionExplorer = app.SolutionExplorerTreeView;

                solutionExplorer.SelectProject(project);

                using (var dialog = SelectFolderDialog.AddExistingFolder(app)) {
                    Assert.AreEqual(dialog.Address, TestData.GetPath(@"TestData\AddExistingFolder"), ignoreCase: true);

                    dialog.FolderName = TestData.GetPath(@"TestData\AddExistingFolder\TestFolder");
                    dialog.SelectFolder();
                }

                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder"));
                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder", "TestFile.txt"));

                var subFolderNode = solutionExplorer.WaitForChildOfProject(project, "SubFolder");
                subFolderNode.Select();

                using (var dialog = SelectFolderDialog.AddExistingFolder(app)) {
                    Assert.AreEqual(dialog.Address, TestData.GetPath(@"TestData\AddExistingFolder\SubFolder"), ignoreCase: true);
                    dialog.FolderName = TestData.GetPath(@"TestData\AddExistingFolder\SubFolder\TestFolder2");
                    dialog.SelectFolder();
                }

                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "SubFolder", "TestFolder2"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAddExistingFolderDebugging() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddExistingFolder.sln");
                var window = project.ProjectItems.Item("Program.py").Open();
                window.Activate();

                var docWindow = app.GetDocument(window.Document.FullName);

                var solutionExplorer = app.SolutionExplorerTreeView;
                app.Dte.ExecuteCommand("Debug.Start");
                app.WaitForMode(dbgDebugMode.dbgRunMode);

                app.OpenSolutionExplorer();
                solutionExplorer.SelectProject(project);

                bool dialogWasCreated = false;
                try {
                    using (SelectFolderDialog.AddExistingFolder(app)) {
                        // Dialog will be dismissed automatically if it opened
                        dialogWasCreated = true;
                    }
                } catch (AssertFailedException) {
                    // Our DTE handling will fail the test, but we want to
                    // prevent that.
                }
                Assert.IsFalse(dialogWasCreated, "Was able to add an existing folder while debugging");

                app.Dte.ExecuteCommand("Debug.StopDebugging");
                app.WaitForMode(dbgDebugMode.dbgDesignMode);

                solutionExplorer.SelectProject(project);

                using (var addDialog = SelectFolderDialog.AddExistingFolder(app)) {
                    Assert.AreEqual(addDialog.Address, Path.GetFullPath(@"TestData\AddExistingFolder"), ignoreCase: true);

                    addDialog.FolderName = Path.GetFullPath(@"TestData\AddExistingFolder\TestFolder");
                    addDialog.SelectFolder();
                }

                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectBuild() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                app.Dte.Solution.SolutionBuild.Build(true);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectRenameAndDeleteItem() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\RenameItemsTest.sln");

                app.Dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);

                // invalid renames
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "              ");
                AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "..............");
                project.ProjectItems.Item("ProgramX.py").Name = ".fob";
                project.ProjectItems.Item(".fob").Name = "ProgramX.py";
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

                var newItem = project.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem2.py");
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
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ChangeDefaultInterpreterProjectClosed() {
            using (var app = new PythonVisualStudioApp()) {
                
                var service = app.InterpreterService;
                var original = service.DefaultInterpreter;
                using (var dis = new DefaultInterpreterSetter(service.Interpreters.FirstOrDefault(i => i != original))) {
                    var project = app.OpenProject(@"TestData\HelloWorld.sln");
                    app.Dte.Solution.Close();

                    Assert.AreNotEqual(dis.OriginalInterpreter, service.DefaultInterpreter);
                }

                Assert.AreEqual(original, service.DefaultInterpreter);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddTemplateItem() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                project.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                bool foundItem = false;
                foreach (ProjectItem item in project.ProjectItems) {
                    if (item.Name == "TemplateItem.py") {
                        foundItem = true;
                    }
                }
                Assert.IsTrue(foundItem);
                Assert.AreEqual(false, project.Saved);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAutomationProperties() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                int propCount = 0;
                foreach (Property prop in project.Properties) {
                    try {
                        Assert.AreEqual(project.Properties.Item(propCount + 1).Value, project.Properties.Item(prop.Name).Value);
                        Assert.AreEqual(project.Properties.Item(propCount + 1).Value, project.Properties.Item(prop.Name).get_IndexedValue(null));
                    } catch (NotImplementedException) {
                        // Different test for properties that are not implemented
                        try {
                            var value = project.Properties.Item(propCount + 1).Value;
                            Assert.Fail("Expected NotImplementedException");
                        } catch (NotImplementedException) {
                        }
                        try {
                            var value = project.Properties.Item(prop.Name).Value;
                            Assert.Fail("Expected NotImplementedException");
                        } catch (NotImplementedException) {
                        }
                    }

                    Assert.IsTrue(ComUtilities.IsSameComObject(app.Dte, project.Properties.Item(propCount + 1).DTE));
                    Assert.AreEqual(0, project.Properties.Item(propCount + 1).NumIndices);
                    Assert.IsNotNull(project.Properties.Item(propCount + 1).Parent);
                    Assert.IsNull(project.Properties.Item(propCount + 1).Application);
                    Assert.IsNotNull(project.Properties.Item(propCount + 1).Collection);
                    propCount++;
                }

                Assert.AreEqual(propCount, project.Properties.Count);

                Assert.IsTrue(ComUtilities.IsSameComObject(app.Dte, project.Properties.DTE));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestAutomationProject() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                Assert.AreEqual("{888888a0-9f3d-457c-b088-3a5042f75d52}", project.Kind, "project.Kind");
                // we don't yet expose a VSProject interface here, if we did we'd need tests for it, but it doesn't support
                // any functionality we care about/implement yet.
                Assert.IsNotNull(project.Object, "project.Object");
                Assert.IsInstanceOfType(project.Object, typeof(OAVSProject), "project.Object");

                Assert.IsTrue(project.Saved, "project.Saved");
                project.Saved = false;
                Assert.IsFalse(project.Saved, "project.Saved");
                project.Saved = true;

                Assert.IsNull(project.Globals, "project.Globals");
                Assert.AreEqual("{c0000016-9ab0-4d58-80e6-54f29e8d3144}", project.ExtenderCATID, "project.ExetenderCATID");
                var extNames = project.ExtenderNames;
                Assert.IsInstanceOfType(extNames, typeof(string[]), "project.ExtenderNames");
                Assert.AreEqual(0, ((string[])extNames).Length, "len(projectExtenderNames)");
                Assert.IsNull(project.ParentProjectItem, "project.ParentProjectItem");
                Assert.IsNull(project.CodeModel, "project.CodeModel");
                AssertError<ArgumentNullException>(() => project.get_Extender(null));
                AssertError<COMException>(() => project.get_Extender("DoesNotExist"));
                Assert.IsNull(project.Collection, "project.Collection");

                foreach (ProjectItem item in project.ProjectItems) {
                    Assert.AreEqual(item.Name, project.ProjectItems.Item(1).Name);
                    break;
                }

                Assert.IsTrue(ComUtilities.IsSameComObject(app.Dte, project.ProjectItems.DTE), "project.ProjectItems.DTE");
                Assert.AreEqual(project, project.ProjectItems.Parent, "project.ProjectItems.Parent");
                Assert.IsNull(project.ProjectItems.Kind, "project.ProjectItems.Kind");

                AssertError<ArgumentException>(() => project.ProjectItems.Item(-1));
                AssertError<ArgumentException>(() => project.ProjectItems.Item(0));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestProjectItemAutomation() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                var item = project.ProjectItems.Item("Program.py");
                Assert.IsNull(item.ExtenderNames);
                Assert.IsNull(item.ExtenderCATID);
                Assert.IsNull(item.SubProject);
                Assert.AreEqual("{6bb5f8ee-4483-11d3-8bcf-00c04f8ec28c}", item.Kind);
                Assert.IsNull(item.ConfigurationManager);
                Assert.IsNotNull(item.Collection.Item("Program.py"));
                AssertError<ArgumentOutOfRangeException>(() => item.get_FileNames(-1));
                AssertNotImplemented(() => item.Saved = false);


                AssertError<ArgumentException>(() => item.get_IsOpen("ThisIsNotTheGuidYoureLookingFor"));
                AssertError<ArgumentException>(() => item.Open("ThisIsNotTheGuidYoureLookingFor"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void TestRelativePaths() {
            // link to outside file should show up as top-level item
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\RelativePaths.sln");

                var item = project.ProjectItems.Item("Program.py");
                Assert.IsNotNull(item);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void ProjectConfiguration() {
            Assert.Fail("Test excluded because it crashes VS");

            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                project.ConfigurationManager.AddConfigurationRow("NewConfig", "Debug", true);
                project.ConfigurationManager.AddConfigurationRow("NewConfig2", "UnknownConfig", true);

                AssertError<ArgumentException>(() => project.ConfigurationManager.DeleteConfigurationRow(null));
                project.ConfigurationManager.DeleteConfigurationRow("NewConfig");
                project.ConfigurationManager.DeleteConfigurationRow("NewConfig2");

                var debug = project.ConfigurationManager.Item("Debug", "Any CPU");
                Assert.AreEqual(debug.IsBuildable, true);

                Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.PlatformNames)[0]);
                Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.SupportedPlatforms)[0]);

                Assert.IsNull(project.ConfigurationManager.ActiveConfiguration.Object);

                //var workingDir = project.ConfigurationManager.ActiveConfiguration.Properties.Item("WorkingDirectory");
                //Assert.AreEqual(".", workingDir);

                // not supported
                AssertError<COMException>(() => project.ConfigurationManager.AddPlatform("NewPlatform", "Any CPU", false));
                AssertError<COMException>(() => project.ConfigurationManager.DeletePlatform("NewPlatform"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DependentNodes() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\XamlProject.sln");

                Assert.IsNotNull(project.ProjectItems.Item("Program.py").ProjectItems.Item("Program.xaml"));
                project.ProjectItems.Item("Program.py").Name = "NewProgram.py";

                Assert.IsNotNull(project.ProjectItems.Item("NewProgram.py").ProjectItems.Item("NewProgram.xaml"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DotNetReferences() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\XamlProject.sln");

                var references = project.ProjectItems.Item("References");
                foreach (var pf in new[] { references.ProjectItems.Item("PresentationFramework"), references.ProjectItems.Item(1) }) {
                    Assert.AreEqual("PresentationFramework", pf.Name);
                    Assert.AreEqual(typeof(OAReferenceItem), pf.GetType());
                    AssertError<InvalidOperationException>(() => pf.Delete());
                    AssertError<InvalidOperationException>(() => pf.Open(""));
                }
            }
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET project.  Makes sure we get completion after a build, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DotNetProjectReferences() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ProjectReference\ProjectReference.sln", expectedProjects: 2, projectName: "PythonApplication");

                app.Dte.Solution.SolutionBuild.Build(WaitForBuildToFinish: true);
                var program = project.ProjectItems.Item("Program.py");
                var window = program.Open();
                window.Activate();

                var doc = app.GetDocument(program.Document.FullName);
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "str");

                var lib = app.GetProject("ClassLibrary");
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
                app.Dte.Solution.SolutionBuild.Build(WaitForBuildToFinish: true);

                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "bool");
            }
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DotNetAssemblyReferences() {
            CompileFile("ClassLibrary.cs", "ClassLibrary.dll");

            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AssemblyReference\AssemblyReference.sln");

                var program = project.ProjectItems.Item("Program.py");
                var window = program.Open();
                window.Activate();

                Thread.Sleep(2000); // allow time to reload the new DLL
                project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);

                var doc = app.GetDocument(program.Document.FullName);
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "str");

                CompileFile("ClassLibraryBool.cs", "ClassLibrary.dll");

                Thread.Sleep(2000); // allow time to reload the new DLL
                project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);

                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "bool");
            }
        }


        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void MultipleDotNetAssemblyReferences() {
            CompileFile("ClassLibrary.cs", "ClassLibrary.dll");
            CompileFile("ClassLibrary2.cs", "ClassLibrary2.dll");

            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AssemblyReference\AssemblyReference.sln");

                var program = project.ProjectItems.Item("Program2.py");
                System.Threading.Tasks.Task.Run(() => {
                    var window = program.Open();
                    window.Activate();
                });

                project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);

                var doc = app.GetDocument(program.Document.FullName);
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "str");
                AssertUtil.ContainsExactly(GetVariableDescriptions("b", snapshot), "int");

                // verify getting signature help doesn't crash...  This used to crash because IronPython
                // used the empty path for an assembly and throws an exception.  We now handle the exception
                // in RemoteInterpreter.GetBuiltinFunctionDocumentation and RemoteInterpreter.GetPythonTypeDocumentation
                AssertUtil.ContainsExactly(
                    GetSignatures(app, "Class1.Fob(", snapshot).Signatures.Select(s => s.Documentation),
                    ""
                );

                // recompile one file, we should still have type info for both DLLs, with one updated
                CompileFile("ClassLibraryBool.cs", "ClassLibrary.dll");

                Thread.Sleep(2000); // allow time to reload the new DLL
                project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);
                
                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "bool");
                AssertUtil.ContainsExactly(GetVariableDescriptions("b", snapshot), "int");

                // recompile the 2nd file, we should then have updated types for both DLLs
                CompileFile("ClassLibrary2Char.cs", "ClassLibrary2.dll");

                Thread.Sleep(2000); // allow time to reload the new DLL
                project.GetPythonProject().GetAnalyzer().WaitForCompleteAnalysis(_ => true);

                AssertUtil.ContainsExactly(GetVariableDescriptions("a", snapshot), "bool");
                AssertUtil.ContainsExactly(GetVariableDescriptions("b", snapshot), "Char");
            }
        }

        private static ExpressionAnalysis GetVariableAnalysis(string variable, ITextSnapshot snapshot) {
            var index = snapshot.GetText().IndexOf(variable + " =");
            var span = snapshot.CreateTrackingSpan(new Span(index, 1), SpanTrackingMode.EdgeInclusive);
            return snapshot.AnalyzeExpression(VSTestContext.ServiceProvider, span);
        }

        private static IEnumerable<string> GetVariableDescriptions(string variable, ITextSnapshot snapshot) {
            return GetVariableAnalysis(variable, snapshot).Values.Select(v => v.Description);
        }

        private static SignatureAnalysis GetSignatures(VisualStudioApp app, string text, ITextSnapshot snapshot) {
            var index = snapshot.GetText().IndexOf(text);
            var span = snapshot.CreateTrackingSpan(new Span(index, text.Length), SpanTrackingMode.EdgeInclusive);
            return snapshot.GetSignatures(app.ServiceProvider, span);
        }

        private static void CompileFile(string file, string outname) {
            string loc = typeof(string).Assembly.Location;
            using (var proc = ProcessOutput.Run(
                Path.Combine(Path.GetDirectoryName(loc), "csc.exe"),
                new[] { "/nologo", "/target:library", "/out:" + outname, file },
                TestData.GetPath(@"TestData\AssemblyReference\PythonApplication"),
                null,
                false,
                null
            )) {
                Console.WriteLine("Executing {0}", proc.Arguments);
                proc.Wait();
                Console.WriteLine("Standard output:");
                foreach (var line in proc.StandardOutputLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine();
                Console.WriteLine("Standard error:");
                foreach (var line in proc.StandardErrorLines) {
                    Console.WriteLine(line);
                }
                Console.WriteLine();

                Assert.AreEqual(0, proc.ExitCode);
            }
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void MultiProjectAnalysis() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\MultiProjectAnalysis\MultiProjectAnalysis.sln", projectName: "PythonApplication", expectedProjects: 2);

                var program = project.ProjectItems.Item("Program.py");
                var window = program.Open();
                window.Activate();

                Thread.Sleep(2000);

                var doc = app.GetDocument(program.Document.FullName);
                var snapshot = doc.TextView.TextBuffer.CurrentSnapshot;
                var index = snapshot.GetText().IndexOf("a =");
                var span = snapshot.CreateTrackingSpan(new Span(index, 1), SpanTrackingMode.EdgeInclusive);
                var analysis = snapshot.AnalyzeExpression(app.ServiceProvider, span);
                Assert.AreEqual(analysis.Values.First().Description, "int");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CProjectReference() {
            var pythons = PythonPaths.Versions.Where(p => p.Version.Is3x() && !p.Isx64).Reverse().Take(2).ToList();
            if (pythons.Count != 2) {
                pythons = PythonPaths.Versions.Where(p => p.Version.Is3x() && p.Isx64).Reverse().Take(2).ToList();
            }
            Assert.AreEqual(2, pythons.Count, "Two different Python 3 interpreters required");
            var buildPython = pythons[0];
            var testPython = pythons[1];
            buildPython.AssertInstalled();
            testPython.AssertInstalled();

            var vcproj = TestData.GetPath(@"TestData\ProjectReference\NativeModule\NativeModule.vcxproj");
            File.WriteAllText(vcproj, File.ReadAllText(vcproj)
                .Replace("$(PYTHON_INCLUDE)", Path.Combine(buildPython.PrefixPath, "include"))
                .Replace("$(PYTHON_LIB)", Path.Combine(buildPython.PrefixPath, "libs"))
            );

            using (var app = new PythonVisualStudioApp())
            using (var dis = app.SelectDefaultInterpreter(buildPython)) {
                var project = app.OpenProject(@"TestData\ProjectReference\CProjectReference.sln", projectName: "PythonApplication2", expectedProjects: 2);

                var sln = app.GetService<IVsSolution4>(typeof(SVsSolution));
                sln.EnsureSolutionIsLoaded((uint)__VSBSLFLAGS.VSBSLFLAGS_None);

                app.Dte.Solution.SolutionBuild.Clean(true);
                app.Dte.Solution.SolutionBuild.Build(true);

                Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\ProjectReference\Debug\native_module.pyd")), ".pyd was not created");

                string[] searchPaths = null;
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    searchPaths = (project.GetPythonProject() as IPythonProject).GetSearchPaths().ToArray();
                });
                AssertUtil.ContainsExactly(searchPaths, TestData.GetPath(@"TestData\ProjectReference\Debug\"));
                
                var pyproj = project.GetPythonProject();
                var interp = pyproj.GetInterpreter();
                Assert.IsNotNull(interp.ImportModule("native_module"), "module was not loaded");

                using (var evt = new AutoResetEvent(false)) {
                    pyproj.ProjectAnalyzerChanged += (s, e) => { try { evt.Set(); } catch { } };
                    dis.SetDefault(app.InterpreterService.FindInterpreter(testPython.Id, testPython.Version.ToVersion()));
                    Assert.IsTrue(evt.WaitOne(10000), "Timed out waiting for analyzer change");
                }

                interp = pyproj.GetInterpreter();
                for (int retries = 10; retries > 0 && interp.ImportModule("native_module") == null; --retries) {
                    Thread.Sleep(500);
                }
                Assert.IsNotNull(interp.ImportModule("native_module"), "module was not reloadod");
            }
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddFolderExists() {
            try {
                // Ensure X does not exist, otherwise we won't be able to create
                // it and pass the test.
                Directory.Delete(TestData.GetPath(@"TestData\AddFolderExists\X"), true);
            } catch { }
            Directory.CreateDirectory(TestData.GetPath(@"TestData\AddFolderExists\Y"));

            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddFolderExists.sln");
                var solutionExplorer = app.OpenSolutionExplorer();

                var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)");


                var projectNode = solutionExplorer.FindItem("Solution 'AddFolderExists' (1 project)", "AddFolderExists");

                ProjectNewFolder(app, solutionNode, projectNode);

                Thread.Sleep(1000);
                Keyboard.Type("."); // bad filename
                Keyboard.Type(System.Windows.Input.Key.Enter);

#if DEV14_OR_LATER
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot:", "be '.' or '..'");
#elif DEV11_OR_LATER
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot contain any of the following characters");
#else
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, ". is an invalid filename");
#endif
                Thread.Sleep(1000);

                Keyboard.Type(".."); // another bad filename
                Keyboard.Type(System.Windows.Input.Key.Enter);

#if DEV14_OR_LATER
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot:", "be '.' or '..'");
#elif DEV11_OR_LATER
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot contain any of the following characters");
#else
                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, ".. is an invalid filename");
#endif
                Thread.Sleep(1000);

                Keyboard.Type("Y"); // another bad filename
                Keyboard.Type(System.Windows.Input.Key.Enter);

                VisualStudioApp.CheckMessageBox(MessageBoxButton.Ok, "The folder Y already exists.");
                Thread.Sleep(1000);

                Keyboard.Type("X"); // directory exists, but is ok.
                Keyboard.Type(System.Windows.Input.Key.Enter);

                // item should be successfully added now.
                WaitForItem(project, "X");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddFolderCopyAndPasteFile() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddFolderCopyAndPasteFile.sln");
                var solutionExplorer = app.OpenSolutionExplorer();

                solutionExplorer.FindChildOfProject(project, "Program.py").Select();
                app.ExecuteCommand("Edit.Copy");
                app.ExecuteCommand("Edit.Paste");

                // Make sure that copy/paste directly under the project node works:
                // http://pytools.codeplex.com/workitem/738
                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "Program - Copy.py"));

                solutionExplorer.SelectProject(project);

                app.ExecuteCommand("Project.NewFolder");
                app.OpenSolutionExplorer();
                Thread.Sleep(1000);
                Keyboard.Type("Fob");
                Keyboard.Type(Key.Enter);

                solutionExplorer.FindChildOfProject(project, "Program.py").Select();
                app.ExecuteCommand("Edit.Copy");

                var folder = solutionExplorer.WaitForChildOfProject(project, "Fob");
                Assert.IsNotNull(folder);
                folder.Select();

                app.ExecuteCommand("Edit.Paste");

                Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "Fob", "Program.py"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CopyAndPasteFolder() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\CopyAndPasteFolder.sln");
                var solutionExplorer = app.OpenSolutionExplorer();
                var solutionNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)");

                var projectNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder");

                var folderNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "X");

                // paste to project node, make sure the files are there
                StringCollection paths = new StringCollection() {
                    Path.Combine(Directory.GetCurrentDirectory(), "TestData", "CopiedFiles")
                };

                ClipboardSetFileDropList(paths);

                Mouse.MoveTo(projectNode.GetClickablePoint());
                Mouse.Click();
                Keyboard.ControlV();

                Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "CopiedFiles"));
                Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "CopiedFiles", "SomeFile.py")));
                Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "CopiedFiles", "Fob", "SomeOtherFile.py")));

                Mouse.MoveTo(folderNode.GetClickablePoint());
                Mouse.Click();

                // paste to folder node, make sure the files are there
                ClipboardSetFileDropList(paths);
                Keyboard.ControlV();

                Thread.Sleep(2000);

                Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' (1 project)", "CopyAndPasteFolder", "X", "CopiedFiles"));
                Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "X", "CopiedFiles", "SomeFile.py")));
                Assert.IsTrue(File.Exists(Path.Combine("TestData", "CopyAndPasteFolder", "X", "CopiedFiles", "Fob", "SomeOtherFile.py")));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddFromFileInSubDirectory() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\AddExistingFolder.sln");
                string fullPath = TestData.GetPath(@"TestData\AddExistingFolder.sln");

                Assert.AreEqual(5, project.ProjectItems.Count);
                Assert.AreEqual(8, app.OpenSolutionExplorer().ExpandAll());

                var item = project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\AddExistingFolder\TestFolder\TestFile.txt"));

                Assert.IsNotNull(item);
                Assert.AreEqual("TestFile.txt", item.Properties.Item("FileName").Value);
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(fullPath), "AddExistingFolder", "TestFolder", "TestFile.txt"), item.Properties.Item("FullPath").Value);

                Assert.AreEqual(6, project.ProjectItems.Count);
                // Two more items, because we've added the file and its folder
                Assert.AreEqual(10, app.OpenSolutionExplorer().ExpandAll());

                var folder = project.ProjectItems.Item("TestFolder");
                Assert.IsNotNull(folder);
                Assert.IsNotNull(folder.ProjectItems.Item("TestFile.txt"));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddFromFileOutsideOfProject() {
            using (var app = new VisualStudioApp()) {
                var prevSetting = app.GetService<PythonToolsService>().GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles;
                app.OnDispose(() => app.GetService<PythonToolsService>().GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles = prevSetting);
                app.GetService<PythonToolsService>().GeneralOptions.UpdateSearchPathsWhenAddingLinkedFiles = false;

                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                // "Python Environments", "References", "Search Paths", "Program.py"
                Assert.AreEqual(4, project.ProjectItems.Count);
                Assert.AreEqual(7, app.OpenSolutionExplorer().ExpandAll());

                try {
                    File.Delete(TestData.GetPath(@"TestData\HelloWorld\LocalsTest.py"));
                } catch {
                }
                var item = project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"));

                Assert.IsNotNull(item);
                Assert.AreEqual(5, project.ProjectItems.Count);
                Assert.AreEqual(8, app.OpenSolutionExplorer().ExpandAll());

                Assert.AreEqual("LocalsTest.py", item.Properties.Item("FileName").Value);

                Assert.AreEqual(true, item.Properties.Item("IsLinkFile").Value);
                Assert.AreEqual(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"), item.Properties.Item("FullPath").Value);
            }
        }

        private static void ClipboardSetFileDropList(StringCollection paths) {
            Exception exception = null;
            var thread = new Thread(p => {
                try {
                    Clipboard.SetFileDropList((StringCollection)p);
                } catch (Exception ex) {
                    exception = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start(paths);
            if (!thread.Join(TimeSpan.FromSeconds(60.0))) {
                thread.Abort();
                Assert.Fail("Failed to set file list on clipboard because the thread timed out.");
            }
            if (exception != null) {
                Assert.Fail("Exception occurred while setting file drop list:{0}{1}", Environment.NewLine, exception);
            }
        }

        /// <summary>
        /// Verify we can copy a folder with multiple items in it.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CopyFolderWithMultipleItems() {
            // http://mpfproj10.codeplex.com/workitem/11618
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\FolderMultipleItems.sln");
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
        }

        /// <summary>
        /// Verify we can start the interactive window when focus in within solution explorer in one of our projects.
        /// </summary>
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void OpenInteractiveFromSolutionExplorer() {
            // http://pytools.codeplex.com/workitem/765
            var python = PythonPaths.Python26 ?? PythonPaths.Python27;
            python.AssertInstalled();

            using (var app = new PythonVisualStudioApp())
            using (var dis = app.SelectDefaultInterpreter(python)) {
                var interpreterName = dis.CurrentDefault.Description;
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                var replService = (IPythonOptions)app.Dte.GetObject("VsPython");
                Assert.IsNotNull(replService, "Unable to get VsPython object");
                var options = replService.GetInteractiveOptions(interpreterName);

                options.InlinePrompts = true;
                options.UseInterpreterPrompts = false;
                options.PrimaryPrompt = ">>> ";
                options.SecondaryPrompt = "... ";

                var solutionExplorer = app.OpenSolutionExplorer();

                var programNode = solutionExplorer.WaitForChildOfProject(project, "Program.py");
                programNode.Select();

                // Press Alt-I to bring up the REPL
                Keyboard.PressAndRelease(System.Windows.Input.Key.I, System.Windows.Input.Key.LeftAlt);

                Keyboard.Type("print('hi')\r");
                var interactive = app.GetInteractiveWindow(interpreterName + " Interactive");
                interactive.WaitForTextEnd("hi", ">>> ");
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LoadProjectWithDuplicateItems() {
            using (var app = new VisualStudioApp()) {
                var solution = app.OpenProject(@"TestData\DuplicateItems.sln");

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
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void EnvironmentVariablesWithDebugging() {
            var filename = Path.Combine(TestData.GetTempPath(), Path.GetRandomFileName());
            Console.WriteLine("Temp file is: {0}", filename);
            var code = String.Format(@"
from os import environ
f = open('{0}', 'w')
f.write(environ['fob'] + environ['oar'] + environ['baz'])
f.close()
while True: pass
", filename.Replace("\\", "\\\\"));

            var project = new ProjectDefinition("EnvironmentVariables", PythonProject,
                Compile("main", code),
                Property(PythonConstants.EnvironmentSetting, "fob=1\noar=2;3\r\nbaz=4"),
                Property(CommonConstants.StartupFile, "main.py")
            );

            using (var solution = project.Generate().ToVs()) {
                solution.ExecuteCommand("Debug.Start");
                solution.WaitForMode(dbgDebugMode.dbgRunMode);

                for (int i = 0; i < 10 && !File.Exists(filename); i++) {
                    System.Threading.Thread.Sleep(1000);
                }
                Assert.IsTrue(File.Exists(filename), "environment variables not written out");
                solution.ExecuteCommand("Debug.StopDebugging");

                Assert.AreEqual(
                    File.ReadAllText(filename),
                    "12;34"
                );
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void EnvironmentVariablesWithoutDebugging() {
            var filename = Path.Combine(TestData.GetTempPath(), Path.GetRandomFileName());
            Console.WriteLine("Temp file is: {0}", filename);
            var code = String.Format(@"
from os import environ
f = open('{0}', 'w')
f.write(environ['fob'] + environ['oar'] + environ['baz'])
f.close()
while True: pass
", filename.Replace("\\", "\\\\"));

            var project = new ProjectDefinition("EnvironmentVariables", PythonProject,
                Compile("main", code),
                Property(PythonConstants.EnvironmentSetting, "fob=1\noar=2;3\r\nbaz=4"),
                Property(CommonConstants.StartupFile, "main.py")
            );

            using (var solution = project.Generate().ToVs()) {
                solution.ExecuteCommand("Debug.StartWithoutDebugging");

                for (int i = 0; i < 10 && !File.Exists(filename); i++) {
                    System.Threading.Thread.Sleep(1000);
                }
                Assert.IsTrue(File.Exists(filename), "environment variables not written out");

                Assert.AreEqual(
                    File.ReadAllText(filename),
                    "12;34"
                );
            }
        }

#if DEV11_OR_LATER
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void PreviewFile() {
            using (var app = new PythonVisualStudioApp()) {
                var solution = app.OpenProject(@"TestData\HelloWorld.sln");

                foreach (var win in app.OpenDocumentWindows) {
                    win.Close(vsSaveChanges.vsSaveChangesNo);
                }

                Assert.AreEqual(0, app.OpenDocumentWindows.Count());

                app.OpenSolutionExplorer();

                Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld").GetClickablePoint());
                Mouse.Click();

                app.WaitForNoDialog(TimeSpan.FromSeconds(5));
                Assert.AreEqual(0, app.OpenDocumentWindows.Count());

                Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' (1 project)", "HelloWorld", "Program.py").GetClickablePoint());
                Mouse.Click();

                app.WaitForNoDialog(TimeSpan.FromSeconds(5));
                try {
                    app.WaitForDocument("Program.py");
                } catch (InvalidOperationException) {
                    Assert.Fail("Document was not opened");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void PreviewMissingFile() {
            using (var app = new PythonVisualStudioApp()) {
                var solution = app.OpenProject(@"TestData\MissingFiles.sln");

                foreach (var win in app.OpenDocumentWindows) {
                    win.Close(vsSaveChanges.vsSaveChangesNo);
                }

                Assert.AreEqual(0, app.OpenDocumentWindows.Count());

                app.OpenSolutionExplorer();

                Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' (1 project)", "HelloWorld").GetClickablePoint());
                Mouse.Click();

                app.WaitForNoDialog(TimeSpan.FromSeconds(5));
                Assert.AreEqual(0, app.OpenDocumentWindows.Count());

                Mouse.MoveTo(app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' (1 project)", "HelloWorld", "Program2.py").GetClickablePoint());
                Mouse.Click();

                app.WaitForNoDialog(TimeSpan.FromSeconds(5));
                Assert.AreEqual(0, app.OpenDocumentWindows.Count());
            }
        }

#endif

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CopyFullPath() {
            foreach (var projectType in ProjectTypes) {
                var def = new ProjectDefinition(
                    "HelloWorld",
                    projectType,
                    Compile("server"),
                    Folder("IncFolder", isExcluded: false),
                    Folder("ExcFolder", isExcluded: true),
                    Compile("app", isExcluded: true),
                    Compile("missing", isMissing: true)
                );

                using (var solution = def.Generate().ToVs()) {
                    var projectDir = Path.GetDirectoryName(solution.GetProject("HelloWorld").FullName);

                    CheckCopyFullPath(solution,
                                      solution.WaitForItem("HelloWorld", "IncFolder"),
                                      projectDir + "\\IncFolder\\");
                    var excFolder = solution.WaitForItem("HelloWorld", "ExcFolder");
                    if (excFolder == null) {
                        solution.SelectProject(solution.GetProject("HelloWorld"));
                        solution.ExecuteCommand("Project.ShowAllFiles");
                        excFolder = solution.WaitForItem("HelloWorld", "ExcFolder");
                    }
                    CheckCopyFullPath(solution, excFolder, projectDir + "\\ExcFolder\\");
                    CheckCopyFullPath(solution,
                                      solution.WaitForItem("HelloWorld", "server" + def.ProjectType.CodeExtension),
                                      projectDir + "\\server" + def.ProjectType.CodeExtension);
                    CheckCopyFullPath(solution,
                                      solution.WaitForItem("HelloWorld", "app" + def.ProjectType.CodeExtension),
                                      projectDir + "\\app" + def.ProjectType.CodeExtension);
                    CheckCopyFullPath(solution,
                                      solution.WaitForItem("HelloWorld", "missing" + def.ProjectType.CodeExtension),
                                      projectDir + "\\missing" + def.ProjectType.CodeExtension);
                }
            }
        }

        private void CheckCopyFullPath(IVisualStudioInstance vs, ITreeNode element, string expected) {
            string clipboardText = "";
            Console.WriteLine("Checking CopyFullPath on:{0}", expected);
            AutomationWrapper.Select(element);
            VSTestContext.DTE.ExecuteCommand("Python.CopyFullPath");

            var app = ((VisualStudioInstance)vs).App;
            app.ServiceProvider.GetUIThread().Invoke(() => clipboardText = System.Windows.Clipboard.GetText());

            Assert.AreEqual(expected, clipboardText);
        }


        private static void CountIs(Dictionary<string, int> count, string key, int expected) {
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
                Thread.Sleep(1000);
            }
        }

        private static void ProjectNewFolderWithName(VisualStudioApp app, System.Windows.Automation.AutomationElement solutionNode, System.Windows.Automation.AutomationElement projectNode, string name) {
            Mouse.MoveTo(projectNode.GetClickablePoint());
            Mouse.Click(System.Windows.Input.MouseButton.Right);

            Thread.Sleep(500);

            Keyboard.Type("d");
            Keyboard.PressAndRelease(System.Windows.Input.Key.Right);
            Keyboard.Type("d");

            Thread.Sleep(500);

            Keyboard.Type(name);
            Keyboard.Type("\n");

            Thread.Sleep(1000);
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
                Thread.Sleep(1000);
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
