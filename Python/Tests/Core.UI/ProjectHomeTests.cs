/* ****************************************************************************
 *
 * Copyright (c) Steve Dower (Zooba).
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
using System.Text;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudioTools.Project.Automation;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Mouse = TestUtilities.UI.Mouse;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    [TestClass]
    public class ProjectHomeTests {
        [ClassInitialize]
        public static void DoDeployment(TestContext context) {
            AssertListener.Initialize();
            PythonTestData.Deploy();
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void LoadRelativeProjects() {
            using (var app = new VisualStudioApp()) {
            string fullPath = TestData.GetPath(@"TestData\ProjectHomeProjects.sln");
                app.OpenProject(@"TestData\ProjectHomeProjects.sln", expectedProjects: 9);

                foreach (var project in app.Dte.Solution.Projects.OfType<Project>()) {
                    var name = Path.GetFileName(project.FileName);
                    if (name.StartsWith("ProjectA")) {
                        // Should have ProgramA.py, Subfolder\ProgramB.py and Subfolder\Subsubfolder\ProgramC.py
                        var programA = project.ProjectItems.Item("ProgramA.py");
                        Assert.IsNotNull(programA);

                        var subfolder = project.ProjectItems.Item("Subfolder");
                        var programB = subfolder.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = subfolder.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else if (name.StartsWith("ProjectB")) {
                        // Should have ProgramB.py and Subsubfolder\ProgramC.py
                        var programB = project.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = project.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else if (name.StartsWith("ProjectSln")) {
                        // Should have ProjectHomeProjects\ProgramA.py, 
                        // ProjectHomeProjects\Subfolder\ProgramB.py and
                        // ProjectHomeProjects\Subfolder\Subsubfolder\ProgramC.py
                        var projectHome = project.ProjectItems.Item("ProjectHomeProjects");
                        var programA = projectHome.ProjectItems.Item("ProgramA.py");
                        Assert.IsNotNull(programA);

                        var subfolder = projectHome.ProjectItems.Item("Subfolder");
                        var programB = subfolder.ProjectItems.Item("ProgramB.py");
                        Assert.IsNotNull(programB);

                        var subsubfolder = subfolder.ProjectItems.Item("Subsubfolder");
                        var programC = subsubfolder.ProjectItems.Item("ProgramC.py");
                        Assert.IsNotNull(programC);
                    } else {
                        Assert.Fail("Wrong project file name", name);
                    }
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddDeleteItem() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                project.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                var newItem = project.ProjectItems.Item("TemplateItem.py");
                Assert.IsNotNull(newItem);
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\TemplateItem.py")));

                newItem.Delete();
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\TemplateItem.py")));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddDeleteItem2() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

                var folder = project.ProjectItems.Item("Subfolder");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                folder.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

                var newItem = folder.ProjectItems.Item("TemplateItem.py");
                Assert.IsNotNull(newItem);
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsTrue(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\TemplateItem.py")));

                newItem.Delete();
                Assert.AreEqual(false, project.Saved);
                project.Save();
                Assert.AreEqual(true, project.Saved);
                Assert.IsFalse(File.Exists(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\TemplateItem.py")));
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddDeleteFolder() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                project.ProjectItems.AddFolder("NewFolder");

                var newFolder = project.ProjectItems.Item("NewFolder");
                Assert.IsNotNull(newFolder);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
                newFolder.Delete();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void AddDeleteSubfolder() {
            using (var app = new VisualStudioApp()) {
                var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

                var folder = project.ProjectItems.Item("Subfolder");

                Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

                folder.ProjectItems.AddFolder("NewFolder");

                var newFolder = folder.ProjectItems.Item("NewFolder");
                Assert.IsNotNull(newFolder);
                Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
                newFolder.Delete();
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void SaveProjectAs() {
            using (var app = new VisualStudioApp()) {
                EnvDTE.Project project;
                try {
                    project = app.OpenProject(@"TestData\HelloWorld.sln");

                    project.SaveAs(TestData.GetPath(@"TestData\ProjectHomeProjects\TempFile.pyproj"));

                    Assert.AreEqual(TestData.GetPath(@"TestData\HelloWorld\"),
                        ((OAProject)project).ProjectNode.ProjectHome);

                    app.Dte.Solution.SaveAs("HelloWorldRelocated.sln");
                } finally {
                    app.Dte.Solution.Close();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

                project = app.OpenProject(@"TestData\HelloWorldRelocated.sln");

                Assert.AreEqual("TempFile.pyproj", project.FileName);

                Assert.AreEqual(TestData.GetPath(@"TestData\HelloWorld\"),
                    ((OAProject)project).ProjectNode.ProjectHome);
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void DragDropTest() {
            using (var app = new VisualStudioApp()) {
                app.OpenProject(@"TestData\DragDropRelocatedTest.sln");

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.FindItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest", "TestFolder", "SubItem.py");
                var point = folder.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Down(MouseButton.Left);

                var projectItem = window.FindItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest");
                point = projectItem.GetClickablePoint();
                Mouse.MoveTo(point);
                Mouse.Up(MouseButton.Left);

                Assert.AreNotEqual(null, window.WaitForItem("Solution 'DragDropRelocatedTest' (1 project)", "DragDropTest", "SubItem.py"));

                app.Dte.Solution.Close(true);
                // Ensure file was moved and the path was updated correctly.
                var project = app.OpenProject(@"TestData\DragDropRelocatedTest.sln");
                foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                    Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("VSTestHost")]
        public void CutPasteTest() {
            using (var app = new VisualStudioApp()) {
                app.OpenProject(@"TestData\CutPasteRelocatedTest.sln");

                app.OpenSolutionExplorer();
                var window = app.SolutionExplorerTreeView;

                var folder = window.FindItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest", "TestFolder", "SubItem.py");
                AutomationWrapper.Select(folder);
                app.ExecuteCommand("Edit.Cut");

                var projectItem = window.FindItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest");
                AutomationWrapper.Select(projectItem);
                app.ExecuteCommand("Edit.Paste");

                Assert.IsNotNull(window.WaitForItem("Solution 'CutPasteRelocatedTest' (1 project)", "CutPasteTest", "SubItem.py"));

                app.Dte.Solution.Close(true);
                // Ensure file was moved and the path was updated correctly.
                var project = app.OpenProject(@"TestData\CutPasteRelocatedTest.sln");
                foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                    Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
                }
            }
        }
    }
}
