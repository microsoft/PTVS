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
extern alias vsinterpreters;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.Project;
using pythontools::Microsoft.PythonTools;
using pythontools::Microsoft.PythonTools.Project;
using pythontools::Microsoft.VisualStudioTools;
using pythontools::Microsoft.VisualStudioTools.Project.Automation;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.SharedProject;
using TestUtilities.UI.Python;
using util::TestUtilities.UI;
using vsinterpreters::Microsoft.PythonTools.Interpreter;
using VSLangProj;
using static Microsoft.VisualStudioTools.UIThreadExtensions;
using DefaultInterpreterSetter = TestUtilities.UI.DefaultInterpreterSetter;
using Keyboard = util::TestUtilities.UI.Keyboard;
using MessageBoxButton = TestUtilities.MessageBoxButton;
using Mouse = util::TestUtilities.UI.Mouse;
using Strings = Microsoft.PythonTools.Strings;
using Task = System.Threading.Tasks.Task;
using Thread = System.Threading.Thread;

namespace PythonToolsUITests {
    public class BasicProjectTests {
        public void TemplateDirectories(VisualStudioApp app) {
            var languageName = PythonVisualStudioApp.TemplateLanguageName;

            var sln = (Solution2)app.Dte.Solution;

            foreach (var templateName in new[] {
                PythonVisualStudioApp.PythonApplicationTemplate,
                PythonVisualStudioApp.BottleWebProjectTemplate,
                PythonVisualStudioApp.DjangoWebProjectTemplate
            }) {
                var templatePath = sln.GetProjectTemplate(templateName, languageName);
                Assert.IsTrue(
                    File.Exists(templatePath) || Directory.Exists(templatePath),
                    string.Format("Cannot find template '{0}' for language '{1}'", templateName, languageName)
                );
                Console.WriteLine("Found {0} at {1}", templateName, templatePath);
            }
        }

        public void UserProjectFile(VisualStudioApp app) {
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

        public void SetDefaultInterpreter(PythonVisualStudioApp app) {
            //var python2 = PythonPaths.Python27_x64 ?? PythonPaths.Python27;
            var python3 = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            //python2.AssertInstalled();
            python3.AssertInstalled();
            var interpreterIds = new string[] { python3.Id };

            var service = app.ComponentModel.GetService<IInterpreterOptionsService>();
            Assert.IsNotNull(service, "Failed to get IInterpreterOptionsService");
            var registry = app.ComponentModel.GetService<IInterpreterRegistryService>();
            Assert.IsNotNull(registry, "Failed to get IInterpreterRegistryService");
            var oldDefaultInterp = service.DefaultInterpreterId;

            app.OnDispose(() => {
                service.DefaultInterpreterId = oldDefaultInterp;
            });

            using (var mre = new ManualResetEvent(false)) {
                EventHandler onChange = (o, e) => mre.SetIfNotDisposed();
                service.DefaultInterpreterChanged += onChange;
                try {
                    foreach (var fact in registry.Interpreters.Where(fact => interpreterIds.IndexOf(fact.Configuration.Id) >= 0)) {
                        service.DefaultInterpreterId = fact.Configuration.Id;
                        Assert.IsTrue(mre.WaitOne(500));
                        mre.Reset();
                        var actual = service.DefaultInterpreter;
                        Assert.AreSame(fact, actual, $"{fact.Configuration.Id} is not {actual?.Configuration.Id ?? "(null)"}");
                    }
                } finally {
                    service.DefaultInterpreterChanged -= onChange;
                }
            }
        }

        public void LoadPythonProject(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            Assert.IsTrue(app.Dte.Solution.IsOpen, "The solution is not open");
            Assert.AreEqual(1, app.Dte.Solution.Projects.Count, $"Loading project resulted in wrong number of loaded projects, expected 1, received {app.Dte.Solution.Projects.Count}");

            Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");
        }

        public void LoadPythonProjectWithNoConfigurations(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\NoConfigurations\HelloWorld.pyproj");

            Assert.IsTrue(app.Dte.Solution.IsOpen, "The solution is not open");
            Assert.AreEqual(1, app.Dte.Solution.Projects.Count, $"Loading project resulted in wrong number of loaded projects, expected 1, received {app.Dte.Solution.Projects.Count}");

            Assert.AreEqual("HelloWorld.pyproj", Path.GetFileName(project.FileName), "Wrong project file name");

            // An exception here may cause the test to fail
            var value = (string)project.Properties.Item("CommandLineArguments").Value;
            Assert.AreEqual("expected", value);
        }

        public void SaveProjectAs(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var slnDir = PathUtils.GetParent(sln);
            var project = app.OpenProject(sln);

            AssertError<ArgumentNullException>(() => project.SaveAs(null));
            project.SaveAs(Path.Combine(slnDir, "TempFile.pyproj"));
            project.Save("");   // empty string means just save

            // try too long of a file
            try {
                project.SaveAs("TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile.pyproj");
                Assert.Fail("Did not throw InvalidOperationException for long filename");
            } catch (InvalidOperationException e) {
                Assert.IsTrue(e.ToString().Contains("exceeds the maximum number of"));
            }
        }

        public void RenameProjectTest(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\RenameProjectTest.sln");
            var project = app.OpenProject(sln);

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

            string projPath = Path.Combine(Path.GetDirectoryName(sln), "RenameProjectTest", "HelloWorld3.pyproj");
            string movePath = Path.Combine(Path.GetDirectoryName(sln), "RenameProjectTest", "HelloWorld_moved.pyproj");
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

        public void ProjectAddItem(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var project = app.OpenProject(sln);

            // "Python Environments", "References", "Search Paths", "Program.py"
            Assert.AreEqual(4, project.ProjectItems.Count);
            var item = project.ProjectItems.AddFromFileCopy(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"));

            Assert.AreEqual("LocalsTest.py", item.Properties.Item("FileName").Value);
            Assert.AreEqual(Path.Combine(Path.GetDirectoryName(sln), "HelloWorld", "LocalsTest.py"), item.Properties.Item("FullPath").Value);
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
            project.ProjectItems.AddFromFileCopy(Path.Combine(PathUtils.GetParent(sln), "HelloWorld", "Program.py"));

            Assert.AreEqual(5, project.ProjectItems.Count);
        }

        public void ProjectAddFolder(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var project = app.OpenProject(sln);

            var folder = project.ProjectItems.AddFolder("Test\\Folder\\Name");
            var folder2 = project.ProjectItems.AddFolder("Test\\Folder\\Name2");

            // try again when it already exists
            AssertError<ArgumentException>(() => project.ProjectItems.AddFolder("Test"));

            Assert.AreEqual("Name", folder.Properties.Item("FileName").Value);
            Assert.AreEqual("Name", folder.Properties.Item("FolderName").Value);

            Assert.AreEqual(
                Path.Combine(Path.GetDirectoryName(sln), "HelloWorld", "Test", "Folder", "Name"),
                CommonUtils.TrimEndSeparator((string)folder.Properties.Item("FullPath").Value)
            );

            folder2.Properties.Item("FolderName").Value = "Name3";
            Assert.AreEqual("Name3", folder2.Name);
            folder2.Properties.Item("FileName").Value = "Name4";
            Assert.AreEqual("Name4", folder2.Name);

            AssertNotImplemented(() => folder.Open(""));
            AssertNotImplemented(() => folder.SaveAs(""));
            AssertNotImplemented(() => folder.Save());
            AssertNotImplemented(() => { var tmp = folder.IsOpen; });
            Assert.AreEqual(2, folder.Collection.Count);
            Assert.IsTrue(folder.Saved);

            Assert.AreEqual("{6bb5f8ef-4483-11d3-8bcf-00c04f8ec28c}", folder.Kind);

            folder.ExpandView();

            folder.Delete();
        }

        public void ProjectAddFolderThroughUI(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\AddFolderExists.sln");
            var slnDir = Path.GetDirectoryName(sln);
            var project = app.OpenProject(sln);
            var solutionExplorer = app.SolutionExplorerTreeView;

            var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderExists' ");
            var projectNode = solutionExplorer.FindItem("Solution 'AddFolderExists' ", "AddFolderExists");

            ProjectNewFolderWithName(app, solutionNode, projectNode, "A");

            var folderA = project.ProjectItems.Item("A");
            var folderANode = solutionExplorer.FindItem("Solution 'AddFolderExists' ", "AddFolderExists", "A");

            var expectedA = Path.Combine(slnDir, "AddFolderExists", "A");
            Assert.AreEqual(expectedA, CommonUtils.TrimEndSeparator((string)folderA.Properties.Item("FullPath").Value));
            Assert.IsTrue(Directory.Exists(expectedA));

            ProjectNewFolderWithName(app, solutionNode, folderANode, "B");

            var folderB = folderA.ProjectItems.Item("B");
            var folderBNode = solutionExplorer.FindItem("Solution 'AddFolderExists' ", "AddFolderExists", "A", "B");

            var expectedB = Path.Combine(slnDir, "AddFolderExists", "A", "B");
            Assert.AreEqual(expectedB, CommonUtils.TrimEndSeparator((string)folderB.Properties.Item("FullPath").Value));
            Assert.IsTrue(Directory.Exists(expectedB));

            ProjectNewFolderWithName(app, solutionNode, folderBNode, "C");

            var folderC = folderB.ProjectItems.Item("C");
            var folderCNode = solutionExplorer.FindItem("Solution 'AddFolderExists' ", "AddFolderExists", "A", "B", "C");

            // 817 & 836: Nested subfolders
            // Setting the wrong VirtualNodeName in FolderNode.FinishFolderAdd caused C's fullpath to be ...\AddFolderExists\B\C\
            // instead of ...\AddFolderExists\A\B\C\.
            var expectedC = Path.Combine(slnDir, "AddFolderExists", "A", "B", "C");
            Assert.AreEqual(expectedC, CommonUtils.TrimEndSeparator((string)folderC.Properties.Item("FullPath").Value));
            Assert.IsTrue(Directory.Exists(expectedC));
        }

        public void ProjectRenameFolder(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var project = app.OpenProject(sln);

            var folder = project.ProjectItems.AddFolder("Test\\Folder\\Name");
            folder.Name = "Renamed";

            project.Save();

            // Verify that after rename, the path in .pyproj is relative
            var projText = File.ReadAllText(project.FullName);
            AssertUtil.Contains(projText, "Folder Include=\"Test\\Folder\\Renamed\\\"");
        }

        public void AddExistingFolder(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\AddExistingFolder.sln");
            var project = app.OpenProject(sln);
           
            var solutionExplorer = app.OpenSolutionExplorer();
            solutionExplorer.SelectProject(project);

            using (var dialog = SelectFolderDialog.AddExistingFolder(app)) {
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(sln), "AddExistingFolder"), dialog.Address, ignoreCase: true);

                dialog.FolderName = Path.Combine(Path.GetDirectoryName(sln), "AddExistingFolder", "TestFolder");
                dialog.SelectFolder();
            }
            solutionExplorer.ExpandAll();

            Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder"));
            Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder", "TestFile.txt"));

            var subFolderNode = solutionExplorer.WaitForChildOfProject(project, "SubFolder");
            subFolderNode.Select();

            using (var dialog = SelectFolderDialog.AddExistingFolder(app)) {
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(sln), "AddExistingFolder", "SubFolder"), dialog.Address, ignoreCase: true);
                dialog.FolderName = Path.Combine(Path.GetDirectoryName(sln), "AddExistingFolder", "SubFolder", "TestFolder2");
                dialog.SelectFolder();
            }

            Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "SubFolder", "TestFolder2"));
        }

        public void AddExistingFolderWhileDebugging(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\AddExistingFolder.sln");
            var project = app.OpenProject(sln);
            var window = project.ProjectItems.Item("Program.py").Open();
            window.Activate();

            var docWindow = app.GetDocument(window.Document.FullName);

            var solutionExplorer = app.OpenSolutionExplorer();
            solutionExplorer.SelectProject(project);

            app.Dte.ExecuteCommand("Debug.Start");
            app.WaitForMode(dbgDebugMode.dbgRunMode);

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
                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(sln), "AddExistingFolder"), addDialog.Address, ignoreCase: true);

                addDialog.FolderName = TestData.GetPath(@"TestData\AddExistingFolder\TestFolder");
                addDialog.SelectFolder();
            }

            solutionExplorer.ExpandAll();


            Assert.IsNotNull(solutionExplorer.WaitForChildOfProject(project, "TestFolder"));
        }

        public void ProjectBuild(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            app.Dte.Solution.SolutionBuild.Build(true);
        }

        public void ProjectRenameAndDeleteItem(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\RenameItemsTest.sln");
            var project = app.OpenProject(sln);

            app.Dte.Documents.CloseAll(vsSaveChanges.vsSaveChangesNo);

            // invalid renames
            AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "");
            AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "TempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFileTempFile");
            AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "              ");
            AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "..............");
            project.ProjectItems.Item("ProgramX.py").Name = ".fob";
            project.ProjectItems.Item(".fob").Name = "ProgramX.py";
            AssertError<InvalidOperationException>(() => project.ProjectItems.Item("ProgramX.py").Name = "ProgramY.py");

            var progXpyc = project.ProjectItems.Item("ProgramX.py").FileNames[0] + "c";
            var progXpyo = project.ProjectItems.Item("ProgramX.py").FileNames[0] + "o";
            Assert.IsTrue(File.Exists(progXpyc), "Expected " + progXpyc);
            Assert.IsTrue(File.Exists(progXpyo), "Expected " + progXpyo);

            project.ProjectItems.Item("ProgramX.py").Name = "PrOgRaMX.py";
            project.ProjectItems.Item("ProgramX.py").Name = "ProgramX.py";

            project.ProjectItems.Item("ProgramX.py").Name = "Program2.py";

            bool foundProg2 = false;
            foreach (ProjectItem item in project.ProjectItems) {
                Assert.AreNotEqual("ProgramX.py", item.Name);
                if (item.Name == "Program2.py") {
                    foundProg2 = true;
                }
            }
            Assert.IsTrue(foundProg2);

            Assert.IsFalse(File.Exists(progXpyc), "Did not expect " + progXpyc);
            Assert.IsFalse(File.Exists(progXpyo), "Did not expect " + progXpyo);
            var prog2pyc = project.ProjectItems.Item("Program2.py").FileNames[0] + "c";
            var prog2pyo = project.ProjectItems.Item("Program2.py").FileNames[0] + "o";
            Assert.IsTrue(File.Exists(prog2pyc), "Expected " + prog2pyc);
            Assert.IsTrue(File.Exists(prog2pyo), "Expected " + prog2pyo);


            // rename using a different method...
            var progYpyc = project.ProjectItems.Item("ProgramY.py").FileNames[0] + "c";

            project.ProjectItems.Item("ProgramY.py").Properties.Item("FileName").Value = "Program3.py";
            bool foundProg3 = false;
            foreach (ProjectItem item in project.ProjectItems) {
                Assert.AreNotEqual("ProgramY.py", item.Name);
                if (item.Name == "Program3.py") {
                    foundProg3 = true;
                }
            }

            var prog3pyc = project.ProjectItems.Item("Program3.py").FileNames[0] + "c";

            Assert.IsTrue(File.Exists(progYpyc), "Expected " + progYpyc);
            Assert.IsTrue(File.Exists(prog3pyc), "Expected " + prog3pyc);
            Assert.AreEqual("Program3.pyc", File.ReadAllText(prog3pyc), "Program3.pyc should not have changed");

            project.ProjectItems.Item("Program3.py").Remove();
            Assert.IsTrue(File.Exists(prog3pyc), "Expected " + prog3pyc);

            Assert.IsTrue(foundProg3);

            Assert.AreEqual(0, project.ProjectItems.Item("ProgramZ.py").ProjectItems.Count);
            AssertError<ArgumentNullException>(() => project.ProjectItems.Item("ProgramZ.py").SaveAs(null));
            // try Save As, this won't rename it in the project.
            project.ProjectItems.Item("ProgramZ.py").SaveAs("Program4.py");

            bool foundProgZ = false;
            foreach (ProjectItem item in project.ProjectItems) {
                Debug.Assert(item.Name != "Program4.py");
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

            var progDeletepyc = project.ProjectItems.Item("ProgramDelete.py").FileNames[0] + "c";
            File.WriteAllText(progDeletepyc, "ProgramDelete.pyc");

            project.ProjectItems.Item("ProgramDelete.py").Delete();

            Assert.IsFalse(File.Exists(progDeletepyc), "Should have been deleted: " + progDeletepyc);

            // rename the folder
            project.ProjectItems.Item("SubFolder").Name = "SubFolderNew";
            Assert.AreEqual(project.ProjectItems.Item("SubFolderNew").Name, "SubFolderNew");
            project.Save();
            var projectFileContents = File.ReadAllText(project.FullName);
            Assert.AreNotEqual(-1, projectFileContents.IndexOf("\"SubFolderNew"), "Failed to find relative path for SubFolder");
        }

        public void ChangeDefaultInterpreterProjectClosed(PythonVisualStudioApp app) {
            var service = app.OptionsService;
            var original = service.DefaultInterpreter;
            var interpreters = app.InterpreterService;
            using (var dis = new DefaultInterpreterSetter(interpreters.Interpreters.FirstOrDefault(i => i != original), app.ServiceProvider)) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");
                app.Dte.Solution.Close();

                Assert.AreNotEqual(dis.OriginalInterpreter, service.DefaultInterpreter);
            }

            Assert.AreEqual(original, service.DefaultInterpreter);
        }

        public void AddTemplateItem(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var project = app.OpenProject(sln);

            project.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

            bool foundItem = false;
            foreach (ProjectItem item in project.ProjectItems) {
                if (item.Name == "TemplateItem.py") {
                    foundItem = true;
                }
            }
            Assert.IsTrue(foundItem);
            Assert.IsFalse(project.Saved);
        }

        public void AutomationProperties(VisualStudioApp app) {
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

        public void TestAutomationProject(VisualStudioApp app) {
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
            Assert.IsNotNull(project.Collection, "project.Collection");

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

        public void ProjectItemAutomation(VisualStudioApp app) {
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

        public void RelativePaths(VisualStudioApp app) {
            // link to outside file should show up as top-level item
            var project = app.OpenProject(@"TestData\RelativePaths.sln");

            var item = project.ProjectItems.Item("Program.py");
            Assert.IsNotNull(item);
        }

        public void ProjectConfiguration(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\HelloWorld.sln");

            project.ConfigurationManager.AddConfigurationRow("NewConfig", "Debug", true);
            project.ConfigurationManager.AddConfigurationRow("NewConfig2", "UnknownConfig", true);

            AssertError<ArgumentException>(() => project.ConfigurationManager.DeleteConfigurationRow(null));
            project.ConfigurationManager.DeleteConfigurationRow("NewConfig");
            project.ConfigurationManager.DeleteConfigurationRow("NewConfig2");

            var debug = project.ConfigurationManager.Item("Debug", "Any CPU");
            Assert.IsFalse(debug.IsBuildable);

            Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.PlatformNames)[0]);
            Assert.AreEqual("Any CPU", ((object[])project.ConfigurationManager.SupportedPlatforms)[0]);

            Assert.IsNull(project.ConfigurationManager.ActiveConfiguration.Object);

            //var workingDir = project.ConfigurationManager.ActiveConfiguration.Properties.Item("WorkingDirectory");
            //Assert.AreEqual(".", workingDir);

            // not supported
            AssertError<COMException>(() => project.ConfigurationManager.AddPlatform("NewPlatform", "Any CPU", false));
            AssertError<COMException>(() => project.ConfigurationManager.DeletePlatform("NewPlatform"));
        }

        public void DependentNodes(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\XamlProject.sln");
            var project = app.OpenProject(sln);

            Assert.IsNotNull(project.ProjectItems.Item("Program.py").ProjectItems.Item("Program.xaml"));
            project.ProjectItems.Item("Program.py").Name = "NewProgram.py";

            Assert.IsNotNull(project.ProjectItems.Item("NewProgram.py").ProjectItems.Item("NewProgram.xaml"));
        }

        public void PythonSearchPaths(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddSearchPaths.sln");

            var searchPaths = project.GetPythonProject()._searchPaths;
            var testDir = TestData.GetTempPath();
            var testFile = Path.Combine(testDir, "test.py");

            using (var evt = new AutoResetEvent(false)) {
                searchPaths.Changed += (s, e) => evt.SetIfNotDisposed();

                searchPaths.Add(testDir, false);
                Assert.IsTrue(evt.WaitOne(TimeSpan.FromSeconds(10)), "Failed to see search path added");

                File.WriteAllText(testFile, "# Content for file\r\n");
                Assert.IsTrue(evt.WaitOne(TimeSpan.FromSeconds(10)), "Failed to see file created");

                File.AppendAllText(testFile, "# More content for file\r\n");
                Assert.IsTrue(evt.WaitOne(TimeSpan.FromSeconds(10)), "Failed to see file modified");

                FileUtils.Delete(testFile);
                Assert.IsTrue(evt.WaitOne(TimeSpan.FromSeconds(10)), "Failed to see file deleted");

                searchPaths.Remove(testDir);
                Assert.IsTrue(evt.WaitOne(TimeSpan.FromSeconds(10)), "Failed to see search path removed");
            }
        }

        public void AddProjectReference(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\AddProjectReference.sln");
            var project = app.OpenProject(sln);
            var solutionExplorer = app.OpenSolutionExplorer();

            var moduleProj = app.GetProject("PythonModule");
            app.ServiceProvider.GetUIThread().Invoke(() => {
                project.GetPythonProject().VSProject.References.AddProject(moduleProj);
            });

            solutionExplorer.ExpandAll();

            solutionExplorer.WaitForChildOfProject(project, Strings.ReferencesNodeName, @"PythonModule");
            solutionExplorer.WaitForChildOfProject(project, Strings.SearchPaths, @"..\PythonModule");

            var moduleWithHomeProj = app.GetProject("PythonModuleWithCustomHome");
            app.ServiceProvider.GetUIThread().Invoke(() => {
                project.GetPythonProject().VSProject.References.AddProject(moduleWithHomeProj);
            });
            solutionExplorer.WaitForChildOfProject(project, Strings.ReferencesNodeName, @"PythonModuleWithCustomHome");
            solutionExplorer.WaitForChildOfProject(project, Strings.SearchPaths, @"..\PythonModuleWithCustomHome\CustomHome");

            var nativeProj = app.GetProject("NativeModule");
            app.ServiceProvider.GetUIThread().Invoke(() => {
                project.GetPythonProject().VSProject.References.AddProject(nativeProj);
            });
            solutionExplorer.WaitForChildOfProject(project, Strings.ReferencesNodeName, @"NativeModule");
            solutionExplorer.WaitForChildOfProject(project, Strings.SearchPaths, @"..\..\Debug");

            var csharpProj = app.GetProject("ClassLibrary");
            app.ServiceProvider.GetUIThread().Invoke(() => {
                project.GetPythonProject().VSProject.References.AddProject(csharpProj);
            });
            solutionExplorer.WaitForChildOfProject(project, Strings.ReferencesNodeName, @"ClassLibrary");
            solutionExplorer.WaitForChildOfProject(project, Strings.SearchPaths, @"..\ClassLibrary\bin\Debug");
        }

        public void DotNetReferences(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\XamlProject.sln");

            var references = project.ProjectItems.Item("References");
            foreach (var pf in new[] { references.ProjectItems.Item("PresentationFramework"), references.ProjectItems.Item(3) }) {
                Assert.AreEqual("PresentationFramework", pf.Name);
                Assert.AreEqual(typeof(OAReferenceItem), pf.GetType());
                AssertError<InvalidOperationException>(() => pf.Delete());
                AssertError<InvalidOperationException>(() => pf.Open(""));
            }
        }

        private static void CompileFile(string file, string outname) {
            Assert.IsTrue(Path.IsPathRooted(file), $"{file} is not a full path");
            string loc = typeof(string).Assembly.Location;
            using (var proc = ProcessOutput.Run(
                Path.Combine(Path.GetDirectoryName(loc), "csc.exe"),
                new[] { "/nologo", "/target:library", "/out:" + outname, PathUtils.GetFileOrDirectoryName(file) },
                PathUtils.GetParent(file),
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

        public void DeprecatedPydReferenceNode(PythonVisualStudioApp app) {
            var proj = app.OpenProject(@"TestData\DeprecatedReferences.sln", expectedProjects: 2, projectName: "PydReference");
            Trace.TraceInformation("References:");
            foreach (var node in proj.GetPythonProject().GetReferenceContainer().EnumReferences()) {
                Trace.TraceInformation("  {0}: {1}", node.GetType().Name, node.Caption);
            }
            Assert.AreEqual("..\\spam.pyd", proj.GetPythonProject().GetReferenceContainer().EnumReferences().OfType<DeprecatedReferenceNode>().FirstOrDefault()?.Caption);
        }

        public void DeprecatedWebPIReferenceNode(PythonVisualStudioApp app) {
            var proj = app.OpenProject(@"TestData\DeprecatedReferences.sln", expectedProjects: 2, projectName: "WebPIReference");
            Trace.TraceInformation("References:");
            foreach (var node in proj.GetPythonProject().GetReferenceContainer().EnumReferences()) {
                Trace.TraceInformation("  {0}: {1}", node.GetType().Name, node.Caption);
            }
            Assert.AreEqual("Django 1.4", proj.GetPythonProject().GetReferenceContainer().EnumReferences().OfType<DeprecatedReferenceNode>().FirstOrDefault()?.Caption);
        }

        /// <summary>
        /// Opens a project w/ a reference to a .NET assembly (not a project).  Makes sure we get completion against the assembly, changes the assembly, rebuilds, makes
        /// sure the completion info changes.
        /// </summary>
        public void AddFolderExists(VisualStudioApp app) {
            var projectPath = app.CopyProjectForTest(@"TestData\AddFolderExists.sln");
            var projectDir = PathUtils.GetParent(projectPath);

            try {
                // Ensure X does not exist, otherwise we won't be able to create
                // it and pass the test.
                Directory.Delete(PathUtils.GetAbsoluteDirectoryPath(projectDir, "AddFolderExists\\X"), true);
            } catch { }

            Directory.CreateDirectory(PathUtils.GetAbsoluteDirectoryPath(projectDir, "AddFolderExists\\Y"));

            var project = app.OpenProject(projectPath);
            var solutionExplorer = app.OpenSolutionExplorer();

            solutionExplorer.ExpandAll();

            var solutionNode = solutionExplorer.FindItem("Solution 'AddFolderExists' ");

            var projectNode = solutionNode.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "AddFolderExists"));

            ProjectNewFolder(app, solutionNode, projectNode);            
            
            Keyboard.Type("."); // bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

            app.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot:", "be '.' or '..'");            

            var newFolderNode = projectNode.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "NewFolder1"));
            if (newFolderNode == null) {
                throw new InvalidOperationException("Failed to find the newly created folder.");
            }

            Keyboard.Type(".."); // another bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

            app.CheckMessageBox(MessageBoxButton.Ok, "Directory names cannot:", "be '.' or '..'");
            

            newFolderNode = projectNode.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "NewFolder1"));
            if (newFolderNode == null) {
                throw new InvalidOperationException("Failed to find the newly created folder.");
            }

            Keyboard.Type("Y"); // another bad filename
            Keyboard.Type(System.Windows.Input.Key.Enter);

            app.CheckMessageBox(MessageBoxButton.Ok, "The folder Y already exists.");            

            newFolderNode = projectNode.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.NameProperty, "NewFolder1"));
            if (newFolderNode == null) {
                throw new InvalidOperationException("Failed to find the newly created folder.");
            }

            Keyboard.Type("X"); // directory exists, but is ok.
            Keyboard.Type(System.Windows.Input.Key.Enter);

            // item should be successfully added now.
            WaitForItem(project, "X");
        }

        public void AddFolderCopyAndPasteFile(VisualStudioApp app) {
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\AddFolderCopyAndPasteFile.sln"));
            var solutionExplorer = app.OpenSolutionExplorer();

            solutionExplorer.ExpandAll();

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

        public void CopyAndPasteFolder(VisualStudioApp app) {
            var projectPath = app.CopyProjectForTest(@"TestData\CopyAndPasteFolder.sln");
            var projectDir = PathUtils.GetParent(projectPath);
            var project = app.OpenProject(projectPath);

            var solutionExplorer = app.OpenSolutionExplorer();
            var solutionNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' ");

            var projectNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' ", "CopyAndPasteFolder");

            var folderNode = solutionExplorer.FindItem("Solution 'CopyAndPasteFolder' ", "CopyAndPasteFolder", "X");

            // paste to project node, make sure the files are there
            StringCollection paths = new StringCollection() {
                TestData.GetPath("TestData", "CopiedFiles")
            };

            ClipboardSetFileDropList(paths);

            Mouse.MoveTo(getClickablePointFromBoundingBox(projectNode));
            Mouse.Click();
            Keyboard.ControlV();

            Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' ", "CopyAndPasteFolder", "CopiedFiles"));
            Assert.IsTrue(File.Exists(Path.Combine(projectDir, "CopyAndPasteFolder", "CopiedFiles", "SomeFile.py")));
            Assert.IsTrue(File.Exists(Path.Combine(projectDir, "CopyAndPasteFolder", "CopiedFiles", "Fob", "SomeOtherFile.py")));

            Mouse.MoveTo(getClickablePointFromBoundingBox(folderNode));
            Mouse.Click();

            // paste to folder node, make sure the files are there
            ClipboardSetFileDropList(paths);
            Keyboard.ControlV();

            Thread.Sleep(2000);

            Assert.IsNotNull(solutionExplorer.WaitForItem("Solution 'CopyAndPasteFolder' ", "CopyAndPasteFolder", "X", "CopiedFiles"));
            Assert.IsTrue(File.Exists(Path.Combine(projectDir, "CopyAndPasteFolder", "X", "CopiedFiles", "SomeFile.py")));
            Assert.IsTrue(File.Exists(Path.Combine(projectDir, "CopyAndPasteFolder", "X", "CopiedFiles", "Fob", "SomeOtherFile.py")));
        }

        public void AddFromFileInSubDirectory(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\AddExistingFolder.sln");
            string fullPath = TestData.GetPath(@"TestData\AddExistingFolder.sln");

            Assert.AreEqual(5, project.ProjectItems.Count);

            var item = project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\AddExistingFolder\TestFolder\TestFile.txt"));

            Assert.IsNotNull(item);
            Assert.AreEqual("TestFile.txt", item.Properties.Item("FileName").Value);
            Assert.AreEqual(Path.Combine(Path.GetDirectoryName(fullPath), "AddExistingFolder", "TestFolder", "TestFile.txt"), item.Properties.Item("FullPath").Value);

            Assert.AreEqual(6, project.ProjectItems.Count);
            // Two more items, because we've added the file and its folder

            var folder = project.ProjectItems.Item("TestFolder");
            Assert.IsNotNull(folder);
            Assert.IsNotNull(folder.ProjectItems.Item("TestFile.txt"));
        }

        public void AddFromFileOutsideOfProject(PythonVisualStudioApp app) {
            var options = app.PythonToolsService.GeneralOptions;
            var prevSetting = options.UpdateSearchPathsWhenAddingLinkedFiles;
            app.OnDispose(() => options.UpdateSearchPathsWhenAddingLinkedFiles = prevSetting);
            options.UpdateSearchPathsWhenAddingLinkedFiles = false;

            var project = app.OpenProject(@"TestData\HelloWorld.sln");
            // "Python Environments", "References", "Search Paths", "Program.py"
            Assert.AreEqual(4, project.ProjectItems.Count);

            try {
                FileUtils.Delete(TestData.GetPath(@"TestData\HelloWorld\LocalsTest.py"));
            } catch {
            }
            var item = project.ProjectItems.AddFromFile(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"));

            Assert.IsNotNull(item);
            Assert.AreEqual(5, project.ProjectItems.Count);

            Assert.AreEqual("LocalsTest.py", item.Properties.Item("FileName").Value);

            Assert.AreEqual(true, item.Properties.Item("IsLinkFile").Value);
            Assert.AreEqual(TestData.GetPath(@"TestData\DebuggerProject\LocalsTest.py"), item.Properties.Item("FullPath").Value);
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
        public void CopyFolderWithMultipleItems(VisualStudioApp app) {
            // http://mpfproj10.codeplex.com/workitem/11618
            var project = app.OpenProject(app.CopyProjectForTest(@"TestData\FolderMultipleItems.sln"));
            var solutionExplorer = app.SolutionExplorerTreeView;
            var solutionNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' ");

            var projectNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' ", "FolderMultipleItems");

            var folderNode = solutionExplorer.FindItem("Solution 'FolderMultipleItems' ", "FolderMultipleItems", "A");

            Mouse.MoveTo(getClickablePointFromBoundingBox(folderNode));
            Mouse.Click();
            Keyboard.ControlC();

            Keyboard.ControlV();
            WaitForItem(project, "A - Copy");

            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'FolderMultipleItems' ", "FolderMultipleItems", "A - Copy", "a.py"));
            Assert.IsNotNull(solutionExplorer.FindItem("Solution 'FolderMultipleItems' ", "FolderMultipleItems", "A - Copy", "b.py"));
        }

        /// <summary>
        /// Verify we can start the interactive window when focus in within solution explorer in one of our projects.
        /// </summary>
        public void OpenInteractiveFromSolutionExplorer(PythonVisualStudioApp app) {
            var python = PythonPaths.LatestVersion;
            python.AssertInstalled();

            using (var dis = app.SelectDefaultInterpreter(python)) {
                var project = app.OpenProject(@"TestData\HelloWorld.sln");

                var solutionExplorer = app.OpenSolutionExplorer();

                solutionExplorer.ExpandAll();

                var programNode = solutionExplorer.WaitForChildOfProject(project, "Program.py");
                programNode.Select();

                Thread.Sleep(500);
                app.ServiceProvider.GetUIThread().Invoke(() => {
                    app.ExecuteCommand("Python.Interactive");
                } );
                Keyboard.Type("print('hi')\r");
                using (var interactive = app.WaitForInteractiveWindow("HelloWorld Interactive")) {
                    Assert.IsNotNull(interactive, "Unable to find HelloWorld Interactive");
                    interactive.WaitForTextEnd("hi", ">");
                }
            }
        }

        public void LoadProjectWithDuplicateItems(VisualStudioApp app) {
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

        //[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void EnvironmentVariablesWithDebugging(VisualStudioApp app, PythonProjectGenerator p) {
            var filename = Path.Combine(TestData.GetTempPath(), Path.GetRandomFileName());
            Console.WriteLine("Temp file is: {0}", filename);
            var code = String.Format(@"
        from os import environ
        f = open('{0}', 'w')
        f.write(environ['fob'] + environ['oar'] + environ['baz'])
        f.close()
        while True: pass
        ", filename.Replace("\\", "\\\\"));

            var project = p.Project("EnvironmentVariables",
                ProjectGenerator.Compile("main", code),
                ProjectGenerator.Property(PythonConstants.EnvironmentSetting, "fob=1\noar=2;3\r\nbaz=4"),
                ProjectGenerator.Property(CommonConstants.StartupFile, "main.py")
            );

            using (var solution = project.Generate().ToVs(app)) {
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

        //[TestMethod, Priority(UITestPriority.P0)]
        //[HostType("VSTestHost"), TestCategory("Installed")]
        public void EnvironmentVariablesWithoutDebugging(VisualStudioApp app, PythonProjectGenerator p) {
            var filename = Path.Combine(TestData.GetTempPath(), Path.GetRandomFileName());
            Console.WriteLine("Temp file is: {0}", filename);
            var code = String.Format(@"
        from os import environ
        f = open('{0}', 'w')
        f.write(environ['fob'] + environ['oar'] + environ['baz'])
        f.close()
        while True: pass
        ", filename.Replace("\\", "\\\\"));

            var project = p.Project("EnvironmentVariables",
                ProjectGenerator.Compile("main", code),
                ProjectGenerator.Property(PythonConstants.EnvironmentSetting, "fob=1\noar=2;3\r\nbaz=4"),
                ProjectGenerator.Property(CommonConstants.StartupFile, "main.py")
            );

            using (var solution = project.Generate().ToVs(app)) {
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

        public void PreviewFile(PythonVisualStudioApp app) {
            var solution = app.OpenProject(@"TestData\HelloWorld.sln");

            foreach (var win in app.OpenDocumentWindows) {
                win.Close(vsSaveChanges.vsSaveChangesNo);
            }

            Assert.AreEqual(0, app.OpenDocumentWindows.Count());

            app.OpenSolutionExplorer();
            var projNode = app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' ", "HelloWorld");
            Mouse.MoveTo(getClickablePointFromBoundingBox(projNode));
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(2));
            Assert.AreEqual(0, app.OpenDocumentWindows.Count());

            var fileNode = app.SolutionExplorerTreeView.FindItem("Solution 'HelloWorld' ", "HelloWorld", "Program.py");
            Mouse.MoveTo(getClickablePointFromBoundingBox(fileNode));
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(2));
            try {
                app.WaitForDocument("Program.py");
            } catch (InvalidOperationException) {
                Assert.Fail("Document was not opened");
            }
        }

        public void PreviewMissingFile(PythonVisualStudioApp app) {
            var solution = app.OpenProject(@"TestData\MissingFiles.sln");

            foreach (var win in app.OpenDocumentWindows) {
                win.Close(vsSaveChanges.vsSaveChangesNo);
            }

            Assert.AreEqual(0, app.OpenDocumentWindows.Count());

            app.OpenSolutionExplorer();

            var projNode = app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' ", "HelloWorld");

            Mouse.MoveTo(getClickablePointFromBoundingBox(projNode));
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, app.OpenDocumentWindows.Count());

            var fileNode = app.SolutionExplorerTreeView.FindItem("Solution 'MissingFiles' ", "HelloWorld", "Program2.py");
            Mouse.MoveTo(getClickablePointFromBoundingBox(fileNode));
            Mouse.Click();

            app.WaitForNoDialog(TimeSpan.FromSeconds(5));
            Assert.AreEqual(0, app.OpenDocumentWindows.Count());
        }

        public void SaveWithDataLoss(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\SaveDataLoss.sln");
            var project = app.OpenProject(sln);

            // Open a 0-byte file so that encoding isn't detected as Unicode
            var item = project.ProjectItems.Item("Program.py");
            var window = item.Open();
            window.Activate();

            // Wait for document to be ready
            var filePath = item.Document.FullName;
            var doc = app.GetDocument(filePath);

            // Add some text that doesn't cause any data loss and save
            app.ServiceProvider.GetUIThread().Invoke(() => {
                System.Windows.Clipboard.SetText("# hello\n");
            });
            app.ExecuteCommand("Edit.Paste");
            app.ExecuteCommand("File.SaveAll");
            app.WaitForNoDialog(TimeSpan.FromSeconds(3));

            // Add some text that causes data loss and save
            app.ServiceProvider.GetUIThread().Invoke(() => {
                System.Windows.Clipboard.SetText("# 丁丂七丄丅丆万丈三龺龻\n");
            });
            app.ExecuteCommand("Edit.Paste");
            using (var dlg = new AutomationDialog(app, AutomationElement.FromHandle(app.OpenDialogWithDteExecuteCommand("File.SaveAll")))) {
                AssertUtil.Contains(dlg.Text, "Some Unicode characters in this file could not be saved");
                dlg.ClickButtonAndClose("Yes");
            }

            // Check that re-save as Unicode occurred and there was no data loss
            var expected = "# hello\n# 丁丂七丄丅丆万丈三龺龻\n";
            var text = "";
            for (var retry = 0; retry < 5; retry++) {
                try {
                    text = File.ReadAllText(filePath, Encoding.UTF8);
                    if (text == expected) {
                        break;
                    }
                } catch (IOException) {
                }
                Thread.Sleep(500);
            }
            Assert.AreEqual(expected, text);
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
            // Retry logic to handle delays in the UI
            for (int i = 0; i < 10; i++) {
                try {
                    // Ensure the project node is available and has a clickable point
                    if (projectNode == null || projectNode.Current.BoundingRectangle.IsEmpty) {
                        throw new InvalidOperationException("Project node is not available or does not have a valid bounding rectangle.");
                    }
                    projectNode.SetFocus();
                    Debug.WriteLine($"ProjectNode: BoundingRectangle={projectNode?.Current.BoundingRectangle}, IsOffscreen={projectNode?.Current.IsOffscreen}");

                    //Mouse.MoveTo(projectNode.GetClickablePoint());
                    var boundingRect = projectNode.Current.BoundingRectangle;
                    if (!boundingRect.IsEmpty) {
                        var fallbackPoint = new System.Windows.Point(
                            boundingRect.X + boundingRect.Width / 2,
                            boundingRect.Y + boundingRect.Height / 2
                        );
                        Mouse.MoveTo(fallbackPoint);
                        Mouse.Click();
                    }
                    
                    // Attempt to execute the "New Folder" command
                    app.Dte.ExecuteCommand("Project.NewFolder");
                    return; // Exit the loop if successful
                } catch (System.Windows.Automation.NoClickablePointException) {
                    Console.WriteLine("Project node does not have a clickable point. Retrying...");
                } catch (InvalidOperationException ex) {
                    Console.WriteLine($"Failed to interact with the project node: {ex.Message}");
                } catch (Exception ex) {
                    Console.WriteLine($"Unexpected error: {ex.Message}");
                }

                // Wait before retrying
                Thread.Sleep(1000);
            }

            throw new InvalidOperationException("Failed to create a new folder after multiple attempts.");
        }

        private static void ProjectNewFolderWithName(VisualStudioApp app, System.Windows.Automation.AutomationElement solutionNode, System.Windows.Automation.AutomationElement projectNode, string name) {
            Mouse.MoveTo(getClickablePointFromBoundingBox(projectNode));
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

        private static Point getClickablePointFromBoundingBox(AutomationElement node ) {
            var boundingRect = node.Current.BoundingRectangle;
            if (!boundingRect.IsEmpty) {
                var fallbackPoint = new System.Windows.Point(
                    boundingRect.X + boundingRect.Width / 2,
                    boundingRect.Y + boundingRect.Height / 2
                );
                return fallbackPoint;
            }
            throw new InvalidOperationException("Node does not have a valid bounding rectangle.");
        }
    }
}
