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
using System.IO;
using System.Linq;
using System.Windows.Input;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.UI;
using Mouse = TestUtilities.UI.Mouse;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    public class ProjectHomeTests {
        public void LoadRelativeProjects(VisualStudioApp app) {
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

        public void AddDeleteItem(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\ProjectHomeSingleProject.sln");
            var slnDir = PathUtils.GetParent(sln);
            var project = app.OpenProject(sln);

            Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

            project.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

            var newItem = project.ProjectItems.Item("TemplateItem.py");
            Assert.IsNotNull(newItem);
            Assert.AreEqual(false, project.Saved);
            project.Save();
            Assert.AreEqual(true, project.Saved);
            Assert.IsTrue(File.Exists(Path.Combine(slnDir, "ProjectHomeProjects", "TemplateItem.py")));

            newItem.Delete();
            Assert.AreEqual(false, project.Saved);
            project.Save();
            Assert.AreEqual(true, project.Saved);
            Assert.IsFalse(File.Exists(Path.Combine(slnDir, "ProjectHomeProjects", "TemplateItem.py")));
        }

        public void AddDeleteItem2(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\ProjectHomeSingleProject.sln");
            var slnDir = PathUtils.GetParent(sln);
            var project = app.OpenProject(sln);

            var folder = project.ProjectItems.Item("Subfolder");

            Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

            folder.ProjectItems.AddFromTemplate(((Solution2)app.Dte.Solution).GetProjectItemTemplate("PyClass.zip", "pyproj"), "TemplateItem.py");

            var newItem = folder.ProjectItems.Item("TemplateItem.py");
            Assert.IsNotNull(newItem);
            Assert.AreEqual(false, project.Saved);
            project.Save();
            Assert.AreEqual(true, project.Saved);
            Assert.IsTrue(File.Exists(Path.Combine(slnDir, "ProjectHomeProjects", "Subfolder", "TemplateItem.py")));

            newItem.Delete();
            Assert.AreEqual(false, project.Saved);
            project.Save();
            Assert.AreEqual(true, project.Saved);
            Assert.IsFalse(File.Exists(Path.Combine(slnDir, "ProjectHomeProjects", "Subfolder", "TemplateItem.py")));
        }

        public void AddDeleteFolder(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

            Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

            project.ProjectItems.AddFolder("NewFolder");

            var newFolder = project.ProjectItems.Item("NewFolder");
            Assert.IsNotNull(newFolder);
            Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
            newFolder.Delete();
        }

        public void AddDeleteSubfolder(VisualStudioApp app) {
            var project = app.OpenProject(@"TestData\ProjectHomeSingleProject.sln");

            var folder = project.ProjectItems.Item("Subfolder");

            Assert.AreEqual("ProjectSingle.pyproj", Path.GetFileName(project.FileName));

            folder.ProjectItems.AddFolder("NewFolder");

            var newFolder = folder.ProjectItems.Item("NewFolder");
            Assert.IsNotNull(newFolder);
            Assert.AreEqual(TestData.GetPath(@"TestData\ProjectHomeProjects\Subfolder\NewFolder\"), newFolder.Properties.Item("FullPath").Value);
            newFolder.Delete();
        }

        public void SaveProjectAndCheckProjectHome(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\HelloWorld.sln");
            var slnDir = PathUtils.GetParent(sln);

            EnvDTE.Project project;
            try {
                project = app.OpenProject(sln);

                project.SaveAs(Path.Combine(slnDir, "ProjectHomeProjects", "TempFile.pyproj"));

                Assert.AreEqual(
                    PathUtils.TrimEndSeparator(Path.Combine(slnDir, "HelloWorld")),
                    PathUtils.TrimEndSeparator(((OAProject)project).ProjectNode.ProjectHome)
                );

                app.Dte.Solution.SaveAs("HelloWorldRelocated.sln");
            } finally {
                app.Dte.Solution.Close();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            project = app.OpenProject(Path.Combine(slnDir, "HelloWorldRelocated.sln"));

            Assert.AreEqual("TempFile.pyproj", project.FileName);

            Assert.AreEqual(
                PathUtils.TrimEndSeparator(Path.Combine(slnDir, "HelloWorld")),
                PathUtils.TrimEndSeparator(((OAProject)project).ProjectNode.ProjectHome)
            );
        }

        public void DragDropRelocatedTest(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\DragDropRelocatedTest.sln");
            var slnDir = PathUtils.GetParent(sln);
            FileUtils.CopyDirectory(TestData.GetPath("TestData", "DragDropRelocatedTest"), Path.Combine(slnDir, "DragDropRelocatedTest"));
            app.OpenProject(sln);

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'DragDropRelocatedTest' (1 of 1 project)", "DragDropTest", "TestFolder", "SubItem.py");
            var point = folder.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Down(MouseButton.Left);

            var projectItem = window.FindItem("Solution 'DragDropRelocatedTest' (1 of 1 project)", "DragDropTest");
            point = projectItem.GetClickablePoint();
            Mouse.MoveTo(point);
            Mouse.Up(MouseButton.Left);

            using (var dlg = AutomationDialog.WaitForDialog(app)) {
                dlg.OK();
            }
            Assert.IsNotNull(window.WaitForItem("Solution 'DragDropRelocatedTest' (1 of 1 project)", "DragDropTest", "SubItem.py"));

            app.Dte.Solution.Close(true);
            // Ensure file was moved and the path was updated correctly.
            var project = app.OpenProject(sln);
            foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
            }
        }

        public void CutPasteRelocatedTest(VisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\CutPasteRelocatedTest.sln");
            var slnDir = PathUtils.GetParent(sln);
            FileUtils.CopyDirectory(TestData.GetPath("TestData", "CutPasteRelocatedTest"), Path.Combine(slnDir, "CutPasteRelocatedTest"));
            app.OpenProject(sln);

            app.OpenSolutionExplorer();
            var window = app.SolutionExplorerTreeView;

            var folder = window.FindItem("Solution 'CutPasteRelocatedTest' (1 of 1 project)", "CutPasteTest", "TestFolder", "SubItem.py");
            AutomationWrapper.Select(folder);
            app.ExecuteCommand("Edit.Cut");

            var projectItem = window.FindItem("Solution 'CutPasteRelocatedTest' (1 of 1 project)", "CutPasteTest");
            AutomationWrapper.Select(projectItem);
            app.ExecuteCommand("Edit.Paste");

            Assert.IsNotNull(window.WaitForItem("Solution 'CutPasteRelocatedTest' (1 of 1 project)", "CutPasteTest", "SubItem.py"));

            app.Dte.Solution.Close(true);
            // Ensure file was moved and the path was updated correctly.
            var project = app.OpenProject(sln);
            foreach (var item in project.ProjectItems.OfType<OAFileItem>()) {
                Assert.IsTrue(File.Exists((string)item.Properties.Item("FullPath").Value), (string)item.Properties.Item("FullPath").Value);
            }
        }
    }
}
