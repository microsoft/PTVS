// Visual Studio Shared Project
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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using MessageBoxButton = TestUtilities.MessageBoxButton;
using MSBuild = Microsoft.Build.Evaluation;

namespace ProjectUITests {
    public class LinkedFileTests {
        private static ProjectDefinition LinkedFiles(ProjectType projectType) {
            return new ProjectDefinition(
                "LinkedFiles",
                projectType,
                ProjectGenerator.ItemGroup(
                    ProjectGenerator.Folder("MoveToFolder"),
                    ProjectGenerator.Folder("FolderWithAFile"),
                    ProjectGenerator.Folder("Fob"),
                    ProjectGenerator.Folder("..\\LinkedFilesDir", isExcluded: true),
                    ProjectGenerator.Folder("AlreadyLinkedFolder"),

                    ProjectGenerator.Compile("Program"),
                    ProjectGenerator.Compile("..\\ImplicitLinkedFile"),
                    ProjectGenerator.Compile("..\\ExplicitLinkedFile")
                        .Link("ExplicitDir\\ExplicitLinkedFile"),
                    ProjectGenerator.Compile("..\\ExplicitLinkedFileWrongFilename")
                        .Link("ExplicitDir\\Blah"),
                    ProjectGenerator.Compile("..\\MovedLinkedFile"),
                    ProjectGenerator.Compile("..\\MovedLinkedFileOpen"),
                    ProjectGenerator.Compile("..\\MovedLinkedFileOpenEdit"),
                    ProjectGenerator.Compile("..\\FileNotInProject"),
                    ProjectGenerator.Compile("..\\DeletedLinkedFile"),
                    ProjectGenerator.Compile("LinkedInModule")
                        .Link("Fob\\LinkedInModule"),
                    ProjectGenerator.Compile("SaveAsCreateLink"),
                    ProjectGenerator.Compile("..\\SaveAsCreateFile"),
                    ProjectGenerator.Compile("..\\SaveAsCreateFileNewDirectory"),
                    ProjectGenerator.Compile("FolderWithAFile\\ExistsOnDiskAndInProject"),
                    ProjectGenerator.Compile("FolderWithAFile\\ExistsInProjectButNotOnDisk", isMissing: true),
                    ProjectGenerator.Compile("FolderWithAFile\\ExistsOnDiskButNotInProject"),
                    ProjectGenerator.Compile("..\\LinkedFilesDir\\SomeLinkedFile")
                        .Link("Oar\\SomeLinkedFile"),
                    ProjectGenerator.Compile("..\\RenamedLinkFile")
                        .Link("Fob\\NewNameForLinkFile"),
                    ProjectGenerator.Compile("..\\BadLinkPath")
                        .Link("..\\BadLinkPathFolder\\BadLinkPath"),
                    ProjectGenerator.Compile("..\\RootedLinkIgnored")
                        .Link("C:\\RootedLinkIgnored"),
                    ProjectGenerator.Compile("C:\\RootedIncludeIgnored", isMissing: true)
                        .Link("RootedIncludeIgnored"),
                    ProjectGenerator.Compile("Fob\\AddExistingInProjectDirButNotInProject"),
                    ProjectGenerator.Compile("..\\ExistingItem", isExcluded: true),
                    ProjectGenerator.Compile("..\\ExistsInProjectButNotOnDisk", isExcluded: true),
                    ProjectGenerator.Compile("..\\ExistsOnDiskAndInProject", isExcluded: true),
                    ProjectGenerator.Compile("..\\ExistsOnDiskButNotInProject", isExcluded: true)
                )
            );
        }

        private static SolutionFile MultiProjectLinkedFiles(ProjectType projectType) {
            return SolutionFile.Generate(
                "MultiProjectLinkedFiles",
                new ProjectDefinition(
                    "LinkedFiles1",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("..\\FileNotInProject1"),
                        ProjectGenerator.Compile("..\\FileNotInProject2")
                    )
                ),
                new ProjectDefinition(
                    "LinkedFiles2",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("..\\FileNotInProject2", isMissing: true)
                    )
                )
            );
        }

        public void RenameLinkedNode(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    // implicitly linked node
                    var projectNode = solution.FindItem("LinkedFiles", "ImplicitLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    try {
                        solution.ExecuteCommand("File.Rename");
                        Assert.Fail("Should have failed to rename");
                    } catch (Exception e) {
                        Debug.WriteLine(e.ToString());
                    }


                    // explicitly linked node
                    var explicitLinkedFile = solution.FindItem("LinkedFiles", "ExplicitDir", "ExplicitLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(explicitLinkedFile, "explicitLinkedFile");
                    AutomationWrapper.Select(explicitLinkedFile);

                    try {
                        solution.ExecuteCommand("File.Rename");
                        Assert.Fail("Should have failed to rename");
                    } catch (Exception e) {
                        Debug.WriteLine(e.ToString());
                    }

                    var autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("ImplicitLinkedFile" + projectType.CodeExtension);
                    try {
                        autoItem.Properties.Item("FileName").Value = "Fob";
                        Assert.Fail("Should have failed to rename");
                    } catch (InvalidOperationException) {
                    }

                    autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("ExplicitDir").ProjectItems.Item("ExplicitLinkedFile" + projectType.CodeExtension);
                    try {
                        autoItem.Properties.Item("FileName").Value = "Fob";
                        Assert.Fail("Should have failed to rename");
                    } catch (InvalidOperationException) {
                    }
                }
            }
        }

        public void MoveLinkedNode(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.ExecuteCommand("Edit.Paste");

                    // item should have moved
                    var movedLinkedFile = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFile, "movedLinkedFile");

                    // file should be at the same location
                    Assert.IsTrue(File.Exists(Path.Combine(solution.SolutionDirectory, "MovedLinkedFile" + projectType.CodeExtension)));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.SolutionDirectory, "MoveToFolder\\MovedLinkedFile" + projectType.CodeExtension)));

                    // now move it back
                    AutomationWrapper.Select(movedLinkedFile);
                    solution.ExecuteCommand("Edit.Cut");

                    var originalFolder = solution.FindItem("LinkedFiles");
                    AutomationWrapper.Select(originalFolder);
                    solution.ExecuteCommand("Edit.Paste");

                    var movedLinkedFilePaste = solution.WaitForItem("LinkedFiles", "MovedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFilePaste, "movedLinkedFilePaste");

                    // and make sure we didn't mess up the path in the project file
                    MSBuild.Project buildProject = new MSBuild.Project(solution.GetProject("LinkedFiles").FullName);
                    bool found = false;
                    foreach (var item in buildProject.GetItems("Compile")) {
                        if (item.UnevaluatedInclude == "..\\MovedLinkedFile" + projectType.CodeExtension) {
                            found = true;
                            break;
                        }
                    }

                    Assert.IsTrue(found);
                }
            }
        }

        public void MultiProjectMove(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = MultiProjectLinkedFiles(projectType).ToVs(app)) {

                    var fileNode = solution.FindItem("LinkedFiles1", "FileNotInProject1" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNode, "projectNode");
                    AutomationWrapper.Select(fileNode);

                    solution.ExecuteCommand("Edit.Copy");

                    var folderNode = solution.FindItem("LinkedFiles2");
                    AutomationWrapper.Select(folderNode);

                    solution.ExecuteCommand("Edit.Paste");

                    // item should have moved
                    var copiedFile = solution.WaitForItem("LinkedFiles2", "FileNotInProject1" + projectType.CodeExtension);
                    Assert.IsNotNull(copiedFile, "movedLinkedFile");

                    Assert.AreEqual(
                        true,
                        solution.GetProject("LinkedFiles2").ProjectItems.Item("FileNotInProject1" + projectType.CodeExtension).Properties.Item("IsLinkFile").Value
                    );
                }
            }
        }

        public void MultiProjectMoveExists2(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = MultiProjectLinkedFiles(projectType).ToVs(app)) {

                    var fileNode = solution.FindItem("LinkedFiles1", "FileNotInProject2" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNode, "projectNode");
                    AutomationWrapper.Select(fileNode);

                    solution.ExecuteCommand("Edit.Copy");

                    var folderNode = solution.FindItem("LinkedFiles2");
                    AutomationWrapper.Select(folderNode);

                    ThreadPool.QueueUserWorkItem(x => solution.ExecuteCommand("Edit.Paste"));

                    string path = Path.Combine(solution.SolutionDirectory, "FileNotInProject2" + projectType.CodeExtension);
                    solution.CheckMessageBox(String.Format("There is already a link to '{0}'. You cannot have more than one link to the same file in a project.", path));

                    solution.WaitForDialogDismissed();
                }
            }
        }

        public void MoveLinkedNodeOpen(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var openWindow = solution.GetProject("LinkedFiles").ProjectItems.Item("MovedLinkedFileOpen" + projectType.CodeExtension).Open();
                    Assert.IsNotNull(openWindow, "openWindow");

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFileOpen" + projectType.CodeExtension);

                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.ExecuteCommand("Edit.Paste");

                    var movedLinkedFileOpen = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFileOpen" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFileOpen, "movedLinkedFileOpen");

                    Assert.IsTrue(File.Exists(Path.Combine(solution.SolutionDirectory, Path.Combine(solution.SolutionDirectory, "MovedLinkedFileOpen" + projectType.CodeExtension))));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.SolutionDirectory, "MoveToFolder\\MovedLinkedFileOpen" + projectType.CodeExtension)));

                    // window sholudn't have changed.
                    Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpen" + projectType.CodeExtension), openWindow);
                }
            }
        }

        public void MoveLinkedNodeOpenEdited(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var openWindow = solution.GetProject("LinkedFiles").ProjectItems.Item("MovedLinkedFileOpenEdit" + projectType.CodeExtension).Open();
                    Assert.IsNotNull(openWindow, "openWindow");

                    var selection = ((TextSelection)openWindow.Selection);
                    selection.SelectAll();
                    selection.Delete();

                    var projectNode = solution.FindItem("LinkedFiles", "MovedLinkedFileOpenEdit" + projectType.CodeExtension);

                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "MoveToFolder");
                    AutomationWrapper.Select(folderNode);

                    solution.ExecuteCommand("Edit.Paste");

                    var movedLinkedFileOpenEdit = solution.WaitForItem("LinkedFiles", "MoveToFolder", "MovedLinkedFileOpenEdit" + projectType.CodeExtension);
                    Assert.IsNotNull(movedLinkedFileOpenEdit, "movedLinkedFileOpenEdit");

                    Assert.IsTrue(File.Exists(Path.Combine(solution.SolutionDirectory, "MovedLinkedFileOpenEdit" + projectType.CodeExtension)));
                    Assert.IsFalse(File.Exists(Path.Combine(solution.SolutionDirectory, "MoveToFolder\\MovedLinkedFileOpenEdit" + projectType.CodeExtension)));

                    // window sholudn't have changed.
                    Assert.AreEqual(app.Dte.Windows.Item("MovedLinkedFileOpenEdit" + projectType.CodeExtension), openWindow);

                    Assert.AreEqual(openWindow.Document.Saved, false);
                    openWindow.Document.Save();

                    Assert.AreEqual(new FileInfo(Path.Combine(solution.SolutionDirectory, "MovedLinkedFileOpenEdit" + projectType.CodeExtension)).Length, (long)0);
                }
            }
        }

        public void MoveLinkedNodeFileExistsButNotInProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var fileNode = solution.FindItem("LinkedFiles", "FileNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNode, "projectNode");
                    AutomationWrapper.Select(fileNode);

                    solution.ExecuteCommand("Edit.Cut");

                    var folderNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    AutomationWrapper.Select(folderNode);

                    solution.ExecuteCommand("Edit.Paste");

                    // item should have moved
                    var fileNotInProject = solution.WaitForItem("LinkedFiles", "FolderWithAFile", "FileNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(fileNotInProject, "fileNotInProject");

                    // but it should be the linked file on disk outside of our project, not the file that exists on disk at the same location.
                    var autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("FolderWithAFile").ProjectItems.Item("FileNotInProject" + projectType.CodeExtension);
                    Assert.AreEqual(Path.Combine(solution.SolutionDirectory, "FileNotInProject" + projectType.CodeExtension), autoItem.Properties.Item("FullPath").Value);
                }
            }
        }

        public void DeleteLinkedNode(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "DeletedLinkedFile" + projectType.CodeExtension);
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    solution.ExecuteCommand("Edit.Delete");

                    projectNode = solution.FindItem("LinkedFiles", "DeletedLinkedFile" + projectType.CodeExtension);
                    Assert.AreEqual(null, projectNode);
                    Assert.IsTrue(File.Exists(Path.Combine(solution.SolutionDirectory, "DeletedLinkedFile" + projectType.CodeExtension)));
                }
            }
        }

        public void LinkedFileInProjectIgnored(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "Fob", "LinkedInModule" + projectType.CodeExtension);

                    Assert.IsNull(projectNode);
                }
            }
        }

        public void SaveAsCreateLink(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("SaveAsCreateLink" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);

                    var itemWindow = autoItem.Open();

                    autoItem.SaveAs("..\\SaveAsCreateLink" + projectType.CodeExtension);


                    autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("SaveAsCreateLink" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);
                }
            }
        }

        public void SaveAsCreateFile(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("SaveAsCreateFile" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);

                    var itemWindow = autoItem.Open();

                    autoItem.SaveAs(Path.Combine(solution.SolutionDirectory, "LinkedFiles\\SaveAsCreateFile" + projectType.CodeExtension));

                    autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("SaveAsCreateFile" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);
                }
            }
        }

        public void SaveAsCreateFileNewDirectory(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {

                    var autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("SaveAsCreateFileNewDirectory" + projectType.CodeExtension);
                    var isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, true);

                    var itemWindow = autoItem.Open();

                    Directory.CreateDirectory(Path.Combine(solution.SolutionDirectory, "LinkedFiles\\CreatedDirectory"));
                    autoItem.SaveAs(Path.Combine(solution.SolutionDirectory, "LinkedFiles\\CreatedDirectory\\SaveAsCreateFileNewDirectory" + projectType.CodeExtension));


                    autoItem = solution.GetProject("LinkedFiles").ProjectItems.Item("CreatedDirectory").ProjectItems.Item("SaveAsCreateFileNewDirectory" + projectType.CodeExtension);
                    isLinkFile = autoItem.Properties.Item("IsLinkFile").Value;
                    Assert.AreEqual(isLinkFile, false);
                }
            }
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        public void AddExistingItem(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "ExistingItem" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    var existingItem = solution.WaitForItem("LinkedFiles", "FolderWithAFile", "ExistingItem" + projectType.CodeExtension);
                    Assert.IsNotNull(existingItem, "existingItem");
                }
            }
        }

        /// <summary>
        /// Adding a link to a folder which is already linked in somewhere else.
        /// </summary>
        public void AddExistingItemAndItemIsAlreadyLinked(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "AlreadyLinkedFolder");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "FileNotInProject" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    solution.WaitForDialog();
                    solution.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "A project cannot have more than one link to the same file.", "FileNotInProject" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Adding a duplicate link to the same item.
        /// 
        /// Also because the linked file dir is "LinkedFilesDir" which is a substring of "LinkedFiles" (our project name)
        /// this verifies we deal with the project name string comparison correctly (including a \ at the end of the
        /// path).
        /// </summary>
        public void AddExistingItemAndLinkAlreadyExists(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "Oar");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "LinkedFilesDir\\SomeLinkedFile" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    solution.WaitForDialog();
                    solution.CheckMessageBox(MessageBoxButton.Ok, "There is already a link to", "SomeLinkedFile" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (when the file only exists on disk)
        /// </summary>
        public void AddExistingItemAndFileByNameExistsOnDiskButNotInProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);


                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "ExistsOnDiskButNotInProject" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    solution.WaitForDialog();
                    solution.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (both in the project and on disk)
        /// </summary>
        public void AddExistingItemAndFileByNameExistsOnDiskAndInProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);


                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "ExistsOnDiskAndInProject" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    solution.WaitForDialog();
                    solution.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (in the project, but not on disk)
        /// </summary>
        public void AddExistingItemAndFileByNameExistsInProjectButNotOnDisk(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "FolderWithAFile");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "ExistsInProjectButNotOnDisk" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    solution.WaitForDialog();
                    solution.CheckMessageBox(MessageBoxButton.Ok, "There is already a file of the same name in this folder.");
                }
            }
        }

        /// <summary>
        /// Adding new linked item when the file lives in the project dir but not in the directory we selected
        /// Add Existing Item from.  We should add the file to the directory where it lives.
        /// </summary>
        public void AddExistingItemAsLinkButFileExistsInProjectDirectory(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "Fob");
                    Assert.IsNotNull(projectNode, "projectNode");
                    AutomationWrapper.Select(projectNode);

                    using (var addExistingDlg = solution.AddExistingItem()) {
                        addExistingDlg.FileName = Path.Combine(solution.SolutionDirectory, "LinkedFiles\\Fob\\AddExistingInProjectDirButNotInProject" + projectType.CodeExtension);
                        addExistingDlg.AddLink();
                    }

                    var addExistingInProjectDirButNotInProject = solution.WaitForItem("LinkedFiles", "Fob", "AddExistingInProjectDirButNotInProject" + projectType.CodeExtension);
                    Assert.IsNotNull(addExistingInProjectDirButNotInProject, "addExistingInProjectDirButNotInProject");
                }
            }
        }

        /// <summary>
        /// Reaming the file name in the Link attribute is ignored.
        /// </summary>
        public void RenamedLinkedFile(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "Fob", "NewNameForLinkFile" + projectType.CodeExtension);
                    Assert.IsNull(projectNode);

                    var renamedLinkFile = solution.FindItem("LinkedFiles", "Fob", "RenamedLinkFile" + projectType.CodeExtension);
                    Assert.IsNull(renamedLinkFile, "renamedLinkFile");
                }
            }
        }

        /// <summary>
        /// A link path outside of our project dir will result in the link being ignored.
        /// </summary>
        public void BadLinkPath(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "..");
                    Assert.IsNull(projectNode);

                    projectNode = solution.FindItem("LinkedFiles", "BadLinkPathFolder");
                    Assert.IsNull(projectNode);
                }
            }
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        public void RootedLinkIgnored(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var projectNode = solution.FindItem("LinkedFiles", "RootedLinkIgnored" + projectType.CodeExtension);
                    Assert.IsNull(projectNode);
                }
            }
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        public void RootedIncludeIgnored(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = LinkedFiles(projectType).Generate().ToVs(app)) {
                    var rootedIncludeIgnored = solution.FindItem("LinkedFiles", "RootedIncludeIgnored" + projectType.CodeExtension);
                    Assert.IsNotNull(rootedIncludeIgnored, "rootedIncludeIgnored");
                }
            }
        }

        /// <summary>
        /// Test linked files with a project home set (done by save as in this test)
        /// https://nodejstools.codeplex.com/workitem/1511
        /// </summary>
        public void TestLinkedWithProjectHome(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                using (var solution = MultiProjectLinkedFiles(projectType).ToVs(app)) {
                    var project = (solution as VisualStudioInstance).Project;

                    // save the project to an odd location.  This will result in project home being set.
                    var newProjName = "TempFile";
                    try {
                        project.SaveAs(TestData.GetTempPath() + newProjName + projectType.ProjectExtension);
                    } catch (UnauthorizedAccessException) {
                        Assert.Inconclusive("Couldn't save the file");
                    }

                    // create a temporary file and add a link to it in the project
                    solution.FindItem(newProjName).Select();
                    string tempFile;
                    using (FileUtils.TemporaryTextFile(out tempFile, "Test file")) {
                        using (var addExistingDlg = AddExistingItemDialog.FromDte((solution as VisualStudioInstance).App)) {
                            addExistingDlg.FileName = tempFile;
                            addExistingDlg.AddLink();
                        }

                        // Save the project to commit that link to the project file
                        project.Save();

                        // verify that the project file contains the correct text for Link
                        // (file path is relative to home folder)
                        var projectHomeFolder = project.Properties.Item("ProjectHome").Value as string;
                        var fileText = File.ReadAllText(project.FullName);
                        var relativeTempFile = PathUtils.GetRelativeFilePath(
                            projectHomeFolder,
                            tempFile
                        );

                        var pattern = string.Format(
                            @"<Content Include=""{0}"">\s*<Link>{1}</Link>\s*</Content>",
                            Regex.Escape(relativeTempFile),
                            Regex.Escape(Path.GetFileName(tempFile)));

                        AssertUtil.AreEqual(new Regex(pattern), fileText);
                    }
                }
            }
        }
    }
}
