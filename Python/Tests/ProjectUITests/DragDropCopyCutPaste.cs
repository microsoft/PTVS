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
using System.IO;
using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;

namespace ProjectUITests {
    public class DragDropCopyCutPaste {
        public void MultiPasteKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MultiPaste(app, pg, CopyByKeyboard);
        }

        public void MultiPasteMouse(VisualStudioApp app, ProjectGenerator pg) {
            MultiPaste(app, pg, CopyByMouse);
        }

        /// <summary>
        /// Cut item, paste into folder, paste into top-level, 2nd paste should prompt for overwrite
        /// </summary>
        private void MultiPaste(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("HelloWorld",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("server"),
                        ProjectGenerator.Compile("server2"),
                        ProjectGenerator.Folder("SubFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var server = solution.WaitForItem("HelloWorld", "server" + projectType.CodeExtension);
                    var server2 = solution.WaitForItem("HelloWorld", "server2" + projectType.CodeExtension);

                    mover(
                        solution,
                        solution.WaitForItem("HelloWorld", "SubFolder"),
                        solution.WaitForItem("HelloWorld", "server" + projectType.CodeExtension),
                        solution.WaitForItem("HelloWorld", "server2" + projectType.CodeExtension)
                    );

                    // paste once, multiple items should be pasted
                    Assert.IsNotNull(solution.WaitForItem("HelloWorld", "SubFolder", "server" + projectType.CodeExtension));
                    Assert.IsNotNull(solution.WaitForItem("HelloWorld", "SubFolder", "server2" + projectType.CodeExtension));

                    solution.SelectSolutionNode();

                    mover(
                        solution,
                        solution.WaitForItem("HelloWorld", "SubFolder"),
                        solution.WaitForItem("HelloWorld", "server" + projectType.CodeExtension),
                        solution.WaitForItem("HelloWorld", "server2" + projectType.CodeExtension)
                    );

                    // paste again, we should get the replace prompts...
                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.Cancel();
                    }

                    // https://pytools.codeplex.com/workitem/1154
                    // and we shouldn't get a second dialog after cancelling...
                    solution.WaitForDialogDismissed();
                }
            }
        }

        /// <summary>
        /// Cut item, paste into folder, paste into top-level, 2nd paste shouldn’t do anything
        /// </summary>
        public void CutPastePasteItem(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("CutPastePasteItem"),
                        ProjectGenerator.Folder("PasteFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    var folder = solution.WaitForItem("DragDropCopyCutPaste", "PasteFolder");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutPastePasteItem" + projectType.CodeExtension);
                    AutomationWrapper.Select(file);

                    solution.ControlX();

                    AutomationWrapper.Select(folder);
                    solution.ControlV();
                    solution.AssertFileExists("DragDropCopyCutPaste", "PasteFolder", "CutPastePasteItem" + projectType.CodeExtension);

                    AutomationWrapper.Select(project);
                    solution.ControlV();

                    System.Threading.Thread.Sleep(1000);

                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CutPastePasteItem" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Cut item, rename it, paste into top-level, check error message
        /// </summary>
        public void CutRenamePaste(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CutRenamePaste"),
                        ProjectGenerator.Compile("CutRenamePaste\\CutRenamePaste")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutRenamePaste", "CutRenamePaste" + projectType.CodeExtension);

                    AutomationWrapper.Select(file);
                    solution.ControlX();

                    AutomationWrapper.Select(file);
                    solution.Type(Key.F2);
                    solution.Type("CutRenamePasteNewName");
                    solution.Type(Key.Enter);

                    System.Threading.Thread.Sleep(1000);
                    AutomationWrapper.Select(project);
                    solution.ControlV();

                    solution.CheckMessageBox("The source URL 'CutRenamePaste" + projectType.CodeExtension + "' could not be found.");
                }
            }
        }

        /// <summary>
        /// Cut item, rename it, paste into top-level, check error message
        /// </summary>
        public void CutDeletePaste(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CutDeletePaste"),
                        ProjectGenerator.Compile("CutDeletePaste\\CutDeletePaste")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutDeletePaste", "CutDeletePaste" + projectType.CodeExtension);

                    AutomationWrapper.Select(file);
                    solution.ControlX();

                    File.Delete(Path.Combine(solution.SolutionDirectory, @"DragDropCopyCutPaste\CutDeletePaste\CutDeletePaste" + projectType.CodeExtension));

                    AutomationWrapper.Select(project);
                    solution.ControlV();

                    solution.CheckMessageBox("The item 'CutDeletePaste" + projectType.CodeExtension + "' does not exist in the project directory. It may have been moved, renamed or deleted.");

                    Assert.IsNotNull(solution.FindItem("DragDropCopyCutPaste", "CutDeletePaste", "CutDeletePaste" + projectType.CodeExtension));
                }
            }
        }

        public void CopyFileToFolderTooLongKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CutOrCopyFileToFolderTooLong(app, pg, CopyByKeyboard);
        }

        public void CopyFileToFolderTooLongMouse(VisualStudioApp app, ProjectGenerator pg) {
            CutOrCopyFileToFolderTooLong(app, pg, CopyByMouse);
        }

        public void CutFileToFolderTooLongKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CutOrCopyFileToFolderTooLong(app, pg, MoveByKeyboard);
        }

        public void CutFileToFolderTooLongMouse(VisualStudioApp app, ProjectGenerator pg) {
            CutOrCopyFileToFolderTooLong(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Adds a new folder which fits exactly w/ no space left in the path name
        /// </summary>
        private void CutOrCopyFileToFolderTooLong(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("LFN",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("server")
                    )
                );

                using (var solution = SolutionFile.Generate("LongFileNames", 40, testDef).ToVs(app)) {
                    // find server, send copy & paste, verify copy of file is there
                    var projectNode = solution.WaitForItem("LFN");
                    AutomationWrapper.Select(projectNode);

                    solution.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
                    solution.PressAndRelease(Key.D);
                    solution.PressAndRelease(Key.Right);
                    solution.PressAndRelease(Key.D);
                    solution.Type("012345678912345678");
                    solution.PressAndRelease(Key.Enter);

                    var folderNode = solution.WaitForItem("LFN", "012345678912345678");
                    Assert.IsNotNull(folderNode);

                    var serverNode = solution.WaitForItem("LFN", "server" + projectType.CodeExtension);
                    AutomationWrapper.Select(serverNode);
                    solution.ControlC();
                    solution.ControlV();

                    var serverCopy = solution.WaitForItem("LFN", "server - Copy" + projectType.CodeExtension);
                    Assert.IsNotNull(serverCopy);

                    mover(solution, folderNode, serverCopy);

                    // Depending on VS version/update, the message may be:
                    //  "The filename is too long."
                    //  "The filename or extension is too long."
                    solution.CheckMessageBox(" filename ", " is too long.");
                }
            }
        }

        /// <summary>
        /// Cut folder, rename it, paste into top-level, check error message
        /// </summary>
        public void CutRenamePasteFolder(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CutRenamePaste"),
                        ProjectGenerator.Folder("CutRenamePaste\\CutRenamePasteFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutRenamePaste", "CutRenamePasteFolder");
                    AutomationWrapper.Select(file);
                    solution.ControlX();

                    solution.Type(Key.F2);
                    solution.Type("CutRenamePasteFolderNewName");
                    solution.Type(Key.Enter);
                    System.Threading.Thread.Sleep(1000);

                    AutomationWrapper.Select(project);
                    solution.ControlV();

                    solution.CheckMessageBox("The source URL 'CutRenamePasteFolder' could not be found.");
                }
            }
        }

        /// <summary>
        /// Copy a file node, drag and drop a different file, paste the node, should succeed
        /// </summary>
        public void CopiedBeforeDragPastedAfterDrop(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("CopiedBeforeDragPastedAfterDrop"),
                        ProjectGenerator.Compile("DragAndDroppedDuringCopy"),
                        ProjectGenerator.Folder("DragDuringCopyDestination"),
                        ProjectGenerator.Folder("PasteFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var project = solution.WaitForItem("DragDropCopyCutPaste");
                    Assert.AreNotEqual(null, project);
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CopiedBeforeDragPastedAfterDrop" + projectType.CodeExtension);
                    Assert.AreNotEqual(null, file);
                    var draggedFile = solution.WaitForItem("DragDropCopyCutPaste", "DragAndDroppedDuringCopy" + projectType.CodeExtension);
                    Assert.AreNotEqual(null, draggedFile);
                    var dragFolder = solution.WaitForItem("DragDropCopyCutPaste", "DragDuringCopyDestination");
                    Assert.AreNotEqual(null, dragFolder);

                    AutomationWrapper.Select(file);
                    solution.ControlC();

                    MoveByMouse(
                        solution,
                        dragFolder,
                        draggedFile
                    );

                    var folder = solution.WaitForItem("DragDropCopyCutPaste", "PasteFolder");
                    AutomationWrapper.Select(folder);
                    solution.ControlV();

                    solution.AssertFileExists("DragDropCopyCutPaste", "PasteFolder", "CopiedBeforeDragPastedAfterDrop" + projectType.CodeExtension);
                    solution.AssertFileExists("DragDropCopyCutPaste", "CopiedBeforeDragPastedAfterDrop" + projectType.CodeExtension);
                }
            }
        }

        public void DragToAnotherProjectKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            DragToAnotherProject(app, pg, CopyByKeyboard);
        }

        public void DragToAnotherProjectMouse(VisualStudioApp app, ProjectGenerator pg) {
            DragToAnotherProject(app, pg, DragAndDrop);
        }

        /// <summary>
        /// Copy from CSharp into our project
        /// </summary>
        private void DragToAnotherProject(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("!Source"),
                            ProjectGenerator.Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("DraggedToOtherProject")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("ConsoleApplication1"),
                        solution.WaitForItem("DragDropCopyCutPaste", "!Source", "DraggedToOtherProject" + projectType.CodeExtension)
                    );

                    solution.AssertFileExists("ConsoleApplication1", "DraggedToOtherProject" + projectType.CodeExtension);
                    solution.AssertFileExists("DragDropCopyCutPaste", "!Source", "DraggedToOtherProject" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Cut folder, paste onto itself, should report an error that the destination is the same as the source
        ///     Cannot move 'X'. The destination folder is the same as the source folder.
        /// </summary>
        public void CutFolderPasteOnSelf(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CutFolderPasteOnSelf")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "CutFolderPasteOnSelf"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CutFolderPasteOnSelf")
                    );

                    solution.CheckMessageBox("Cannot move 'CutFolderPasteOnSelf'. The destination folder is the same as the source folder.");

                    solution.AssertFolderExists("DragDropCopyCutPaste", "CutFolderPasteOnSelf");
                    solution.AssertFolderDoesntExist("DragDropCopyCutPaste", "CutFolderPasteOnSelf - Copy");
                }
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        /// </summary>
        public void DragFolderOntoSelf(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("DragFolderOntoSelf"),
                        ProjectGenerator.Compile("DragFolderOntoSelf\\File")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var draggedFolder = solution.WaitForItem("DragDropCopyCutPaste", "DragFolderOntoSelf");
                    AutomationWrapper.Select(draggedFolder);

                    draggedFolder.DragOntoThis(draggedFolder);

                    solution.AssertFolderExists("DragDropCopyCutPaste", "DragFolderOntoSelf");
                    solution.AssertFileExists("DragDropCopyCutPaste", "DragFolderOntoSelf", "File" + projectType.CodeExtension);
                    solution.AssertFolderDoesntExist("DragDropCopyCutPaste", "DragFolderOntoSelf - Copy");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "DragFolderOntoSelf", "File - Copy" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        /// </summary>
        public void DragFolderOntoChild(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("ParentFolder"),
                        ProjectGenerator.Folder("ParentFolder\\ChildFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByMouse(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "ParentFolder", "ChildFolder"),
                        solution.WaitForItem("DragDropCopyCutPaste", "ParentFolder")
                    );

                    solution.CheckMessageBox("Cannot move 'ParentFolder'. The destination folder is a subfolder of the source folder.");
                    solution.WaitForDialogDismissed();

                    var draggedFolder = solution.FindItem("DragDropCopyCutPaste", "ParentFolder");
                    Assert.IsNotNull(draggedFolder);
                    var childFolder = solution.FindItem("DragDropCopyCutPaste", "ParentFolder", "ChildFolder");
                    Assert.IsNotNull(childFolder);
                    var parentInChildFolder = solution.FindItem("DragDropCopyCutPaste", "ParentFolder", "ChildFolder", "ParentFolder");
                    Assert.IsNull(parentInChildFolder);
                }
            }
        }

        /// <summary>
        /// Move a file to a location where A file with the same name now already exists.  We should get an overwrite
        /// dialog, and after answering yes to overwrite the file should be moved.
        /// </summary>
        public void CutFileReplace(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("MoveDupFilename"),
                        ProjectGenerator.Folder("MoveDupFilename\\Fob"),
                        ProjectGenerator.Compile("MoveDupFilename\\Fob\\server"),
                        ProjectGenerator.Compile("MoveDupFilename\\server")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "MoveDupFilename"),
                        solution.WaitForItem("DragDropCopyCutPaste", "MoveDupFilename", "Fob", "server" + projectType.CodeExtension)
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.Yes();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "MoveDupFilename", "server" + projectType.CodeExtension);
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "MoveDupFilename", "Fob", "server" + projectType.CodeExtension);
                }
            }
        }

        public void CutFolderAndFile(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CutFolderAndFile"),
                        ProjectGenerator.Folder("CutFolderAndFile\\CutFolder"),
                        ProjectGenerator.Compile("CutFolderAndFile\\CutFolder\\CutFolderAndFile")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var folder = solution.WaitForItem("DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder");
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder", "CutFolderAndFile" + projectType.CodeExtension);
                    var dest = solution.WaitForItem("DragDropCopyCutPaste");

                    AutomationWrapper.Select(folder);
                    AutomationWrapper.AddToSelection(file);

                    solution.ControlX();
                    AutomationWrapper.Select(dest);
                    solution.ControlV();

                    solution.AssertFileExists("DragDropCopyCutPaste", "CutFolder", "CutFolderAndFile" + projectType.CodeExtension);
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CutFolderAndFile", "CutFolder");
                }
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        ///     Cannot move 'CutFilePasteSameLocation'. The destination folder is the same as the source folder.
        /// </summary>
        public void CutFilePasteSameLocation(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("CutFilePasteSameLocation")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CutFilePasteSameLocation" + projectType.CodeExtension)
                    );

                    solution.CheckMessageBox("Cannot move 'CutFilePasteSameLocation" + projectType.CodeExtension + "'. The destination folder is the same as the source folder.");

                    solution.AssertFileExists("DragDropCopyCutPaste", "CutFilePasteSameLocation" + projectType.CodeExtension);
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CutFilePasteSameLocation - Copy" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Drag and drop a folder onto itself, nothing should happen
        ///     Cannot move 'DragFolderAndFileToSameFolder'. The destination folder is the same as the source folder.
        /// </summary>
        public void DragFolderAndFileOntoSelf(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("DragFolderAndFileOntoSelf"),
                        ProjectGenerator.Compile("DragFolderAndFileOntoSelf\\File")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var folder = solution.WaitForItem("DragDropCopyCutPaste", "DragFolderAndFileOntoSelf");
                    DragAndDrop(
                        solution,
                        folder,
                        folder,
                        solution.WaitForItem("DragDropCopyCutPaste", "DragFolderAndFileOntoSelf", "File" + projectType.CodeExtension)
                    );

                    solution.CheckMessageBox("Cannot move 'DragFolderAndFileOntoSelf'. The destination folder is the same as the source folder.");
                }
            }
        }

        /// <summary>
        /// Add folder from another project, folder contains items on disk which are not in the project, only items in the project should be added.
        /// </summary>
        public void CopyFolderFromAnotherHierarchy(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("!Source"),
                            ProjectGenerator.Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("CopiedFolderWithItemsNotInProject"),
                            ProjectGenerator.Compile("CopiedFolderWithItemsNotInProject\\Class"),
                            ProjectGenerator.Content("CopiedFolderWithItemsNotInProject\\Text.txt", "", isExcluded:true)
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    CopyByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste"),
                        solution.WaitForItem("ConsoleApplication1", "CopiedFolderWithItemsNotInProject")
                    );

                    solution.WaitForItem("DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Class.cs");

                    solution.AssertFolderExists("DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject");
                    solution.AssertFileExists("DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Class.cs");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CopiedFolderWithItemsNotInProject", "Text.txt");
                }
            }
        }

        public void CopyDeletePaste(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CopyDeletePaste"),
                        ProjectGenerator.Compile("CopyDeletePaste\\CopyDeletePaste")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    var file = solution.WaitForItem("DragDropCopyCutPaste", "CopyDeletePaste", "CopyDeletePaste" + projectType.CodeExtension);
                    var project = solution.WaitForItem("DragDropCopyCutPaste");

                    AutomationWrapper.Select(file);
                    solution.ControlC();

                    AutomationWrapper.Select(file);
                    solution.Type(Key.Delete);
                    solution.WaitForDialog();

                    solution.Type("\r");

                    solution.WaitForDialogDismissed();

                    solution.WaitForItemRemoved("DragDropCopyCutPaste", "CopyDeletePaste", "CopyDeletePaste" + projectType.CodeExtension);

                    AutomationWrapper.Select(project);
                    solution.ControlV();

                    solution.CheckMessageBox("The source URL 'CopyDeletePaste" + projectType.CodeExtension + "' could not be found.");
                }
            }
        }

        public void CrossHierarchyFileDragAndDropKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CrossHierarchyFileDragAndDrop(app, pg, CopyByKeyboard);
        }

        public void CrossHierarchyFileDragAndDropMouse(VisualStudioApp app, ProjectGenerator pg) {
            CrossHierarchyFileDragAndDrop(app, pg, DragAndDrop);
        }

        /// <summary>
        /// Copy from C# into our project
        /// </summary>
        private void CrossHierarchyFileDragAndDrop(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("DropFolder")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Compile("CrossHierarchyFileDragAndDrop")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "DropFolder"),
                        solution.WaitForItem("ConsoleApplication1", "CrossHierarchyFileDragAndDrop.cs")
                    );

                    solution.AssertFileExists("DragDropCopyCutPaste", "DropFolder", "CrossHierarchyFileDragAndDrop.cs");
                }
            }
        }

        public void MoveDuplicateFolderNameKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFolderName(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFolderNameMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFolderName(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Drag file from another hierarchy into folder in our hierarchy, item should be added
        ///     Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.
        /// </summary>
        private void MoveDuplicateFolderName(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("DuplicateFolderName"),
                        ProjectGenerator.Folder("DuplicateFolderNameTarget"),
                        ProjectGenerator.Folder("DuplicateFolderNameTarget\\DuplicateFolderName")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "DuplicateFolderNameTarget"),
                        solution.WaitForItem("DragDropCopyCutPaste", "DuplicateFolderName")
                    );

                    solution.CheckMessageBox("Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.");
                }
            }
        }

        public void CopyDuplicateFolderNameKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CopyDuplicateFolderName(app, pg, CopyByKeyboard);
        }

        public void CopyDuplicateFolderNameMouse(VisualStudioApp app, ProjectGenerator pg) {
            CopyDuplicateFolderName(app, pg, CopyByMouse);
        }

        /// <summary>
        /// Copy folder to a destination where the folder already exists.  Say don't copy, nothing should be copied.
        /// </summary>
        private void CopyDuplicateFolderName(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CopyDuplicateFolderName"),
                        ProjectGenerator.Compile("CopyDuplicateFolderName\\server"),
                        ProjectGenerator.Folder("CopyDuplicateFolderNameTarget"),
                        ProjectGenerator.Folder("CopyDuplicateFolderNameTarget\\CopyDuplicateFolderName")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CopyDuplicateFolderName")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "The folder 'CopyDuplicateFolderName' already exists.");
                        dialog.No();
                    }

                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget", "CopyDuplicateFolderName", "server" + projectType.CodeExtension);
                }
            }
        }

        public void MoveCrossHierarchyKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveCrossHierarchy(app, pg, MoveByKeyboard);
        }

        public void MoveCrossHierarchyMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveCrossHierarchy(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cut item from one project, paste into another project, item should be removed from original project
        /// </summary>
        private void MoveCrossHierarchy(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("!Source"),
                            ProjectGenerator.Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Compile("CrossHierarchyCut")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste"),
                        solution.WaitForItem("ConsoleApplication1", "CrossHierarchyCut.cs")
                    );

                    solution.AssertFileExists("DragDropCopyCutPaste", "CrossHierarchyCut.cs");
                    solution.AssertFileDoesntExist("ConsoleApplication1", "CrossHierarchyCut.cs");
                }
            }
        }

        public void MoveReverseCrossHierarchyKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveReverseCrossHierarchy(app, pg, MoveByKeyboard);
        }

        public void MoveReverseCrossHierarchyMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveReverseCrossHierarchy(app, pg, MoveByMouse);
        }

        public void MoveDuplicateFileNameOverwriteKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameOverwrite(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameOverwriteMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameOverwrite(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 2 files with the same name, answers yes to overwrite them, and
        /// makes sure only one file is left.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameOverwrite(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.Yes();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "A", "quox.txt");

                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        public void MoveDuplicateFileNameOverwriteAllItemsKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameOverwriteAllItems(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameOverwriteAllItemsMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameOverwriteAllItems(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, answers yes to overwrite them and
        /// checks do this for all items, and makes sure only one file is left.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameOverwriteAllItems(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Folder("C"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A"),
                        ProjectGenerator.Content("C\\quox.txt", "C")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "C", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.AllItems = true;
                        dialog.Yes();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "A", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "C", "quox.txt");

                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        public void MoveDuplicateFileNameDontOverwriteKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwrite(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameDontOverwriteMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwrite(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 2 files with the same name, pastes them to a folder, and makes
        /// sure we get prompted to overwrite.  Answers no to overwriting, both
        /// files should still be in the project.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwrite(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.No();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    // one of the fils should still exist...
                    try {
                        solution.AssertFileExists("DragDropCopyCutPaste", "quox.txt");
                    } catch (AssertFailedException) {
                        solution.AssertFileExists("DragDropCopyCutPaste", "A", "quox.txt");
                    }

                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        public void MoveDuplicateFileNameDontOverwrite2Keyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwrite2(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameDontOverwrite2Mouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwrite2(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, and makes
        /// sure that we get multiple prompts to overwrite.  Answers no to all of them, and
        /// all the files should still exist somewhere.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwrite2(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Folder("C"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A"),
                        ProjectGenerator.Content("C\\quox.txt", "C")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "C", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.No();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    int totalCount = solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("A").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("C").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Cast<EnvDTE.ProjectItem>().Where(IsFile).Count();

                    Assert.AreEqual(3, totalCount);
                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        public void MoveDuplicateFileNameDontOverwriteAllItemsKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwriteAllItems(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameDontOverwriteAllItemsMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameDontOverwriteAllItems(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, checks
        /// do this for all items, and makes sure all the files still exist somewhere.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwriteAllItems(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Folder("C"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A"),
                        ProjectGenerator.Content("C\\quox.txt", "C")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "C", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.AllItems = true;
                        dialog.No();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    int totalCount = solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("A").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("C").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Cast<EnvDTE.ProjectItem>().Where(IsFile).Count();

                    Assert.AreEqual(3, totalCount);
                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        public void MoveDuplicateFileNameCancelKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCancel(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameCancelMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCancel(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, and makes sure
        /// we get a prompt to overwrite.  Cancels on the 1st prompt and ensures all
        /// of the files are still there.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameCancel(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("B"),
                        ProjectGenerator.Folder("C"),
                        ProjectGenerator.Content("quox.txt", "top-level"),
                        ProjectGenerator.Content("A\\quox.txt", "A"),
                        ProjectGenerator.Content("C\\quox.txt", "C")
                    )
                );

                using (var solution = project.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "B"),
                        solution.WaitForItem("DragDropCopyCutPaste", "A", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "C", "quox.txt"),
                        solution.WaitForItem("DragDropCopyCutPaste", "quox.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'quox.txt' already exists.");
                        dialog.Cancel();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    int totalCount = solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("A").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("C").ProjectItems.Count +
                        solution.GetProject("DragDropCopyCutPaste").ProjectItems.Cast<EnvDTE.ProjectItem>().Where(IsFile).Count();

                    Assert.AreEqual(3, totalCount);
                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        private static bool IsFile(EnvDTE.ProjectItem projectItem) {
            Guid guid;
            if (Guid.TryParse(projectItem.Kind, out guid)) {
                return guid == VSConstants.GUID_ItemType_PhysicalFile;
            }
            return false;
        }

        /// <summary>
        /// Cut an item from our project, paste into another project, item should be removed from our project
        /// </summary>
        private void MoveReverseCrossHierarchy(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Compile("CrossHierarchyCut")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("ConsoleApplication1"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CrossHierarchyCut" + projectType.CodeExtension)
                    );

                    solution.AssertFileExists("ConsoleApplication1", "CrossHierarchyCut" + projectType.CodeExtension);
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CrossHierarchyCut" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Drag item from our project to other project, copy
        /// Drag item from other project to our project, still copy back
        /// </summary>
        public void MoveDoubleCrossHierarchy(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("!Source"),
                            ProjectGenerator.Compile("!Source\\DoubleCrossHierarchy")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Compile("DoubleCrossHierarchy")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    DragAndDrop(
                        solution,
                        solution.WaitForItem("ConsoleApplication1"),
                        solution.WaitForItem("DragDropCopyCutPaste", "!Source", "DoubleCrossHierarchy" + projectType.CodeExtension)
                    );

                    solution.AssertFileExists("ConsoleApplication1", "DoubleCrossHierarchy" + projectType.CodeExtension);
                    solution.AssertFileExists("DragDropCopyCutPaste", "!Source", "DoubleCrossHierarchy" + projectType.CodeExtension);

                    DragAndDrop(
                        solution,
                        solution.FindItem("DragDropCopyCutPaste"),
                        solution.FindItem("ConsoleApplication1", "DoubleCrossHierarchy.cs")
                    );

                    solution.AssertFileExists("DragDropCopyCutPaste", "DoubleCrossHierarchy.cs");
                    solution.AssertFileExists("ConsoleApplication1", "DoubleCrossHierarchy.cs");
                }
            }
        }

        /// <summary>
        /// Drag item from another project, drag same item again, prompt to overwrite, say yes, only one item should be in the hierarchy
        /// </summary>
        public void DragTwiceAndOverwrite(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("DraggedToOtherProject"),
                            ProjectGenerator.Compile("DragTwiceAndOverwrite")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    for (int i = 0; i < 2; i++) {
                        DragAndDrop(
                            solution,
                            solution.WaitForItem("DragDropCopyCutPaste"),
                            solution.WaitForItem("ConsoleApplication1", "DragTwiceAndOverwrite.cs")
                        );
                    }

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file named 'DragTwiceAndOverwrite.cs' already exists.");
                        dialog.Yes();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "DragTwiceAndOverwrite.cs");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "DragTwiceAndOverwrite - Copy.cs");
                }
            }
        }

        /// <summary>
        /// Drag item from another project, drag same item again, prompt to overwrite, say yes, only one item should be in the hierarchy
        /// </summary>
        public void CopyFolderMissingItem(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("CopyFolderMissingItem"),
                        ProjectGenerator.Compile("CopyFolderMissingItem\\missing", isMissing: true),
                        ProjectGenerator.Folder("PasteFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    CopyByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "PasteFolder"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CopyFolderMissingItem")
                    );

                    // make sure no dialogs pop up
                    solution.CheckMessageBox("The item 'missing" + projectType.CodeExtension + "' does not exist in the project directory. It may have been moved, renamed or deleted.");

                    solution.AssertFolderExists("DragDropCopyCutPaste", "CopyFolderMissingItem");
                    solution.AssertFolderDoesntExist("DragDropCopyCutPaste", "PasteFolder", "CopyFolderMissingItem");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "PasteFolder", "missing" + projectType.CodeExtension);
                }
            }
        }

        /// <summary>
        /// Copy missing file
        /// 
        /// https://pytools.codeplex.com/workitem/1141
        /// </summary>
        public void CopyPasteMissingFile(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Compile("MissingFile", isMissing: true),
                        ProjectGenerator.Folder("PasteFolder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    CopyByKeyboard(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "PasteFolder"),
                        solution.WaitForItem("DragDropCopyCutPaste", "MissingFile" + projectType.CodeExtension)
                    );

                    solution.CheckMessageBox("The item 'MissingFile" + projectType.CodeExtension + "' does not exist in the project directory. It may have been moved, renamed or deleted.");
                }
            }
        }

        /// <summary>
        /// Drag folder to a location where a file with the same name already exists.
        /// 
        /// https://nodejstools.codeplex.com/workitem/241
        /// </summary>
        public void MoveFolderExistingFile(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("PasteFolder"),
                        ProjectGenerator.Content("PasteFolder\\FolderCollision", ""),
                        ProjectGenerator.Folder("FolderCollision")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByKeyboard(
                        solution,
                        solution.FindItem("DragDropCopyCutPaste", "PasteFolder"),
                        solution.FindItem("DragDropCopyCutPaste", "FolderCollision")
                    );

                    solution.CheckMessageBox("Unable to add 'FolderCollision'. A file with that name already exists.");
                }
            }
        }

        /// <summary>
        /// Cannot move folder with contents in solution explorer
        /// 
        /// http://pytools.codeplex.com/workitem/2609
        /// </summary>
        public void MoveFolderWithContents(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("FolderWithContentsProj",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("A\\B"),
                        ProjectGenerator.Content("A\\B\\File.txt", ""),
                        ProjectGenerator.Folder("C")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    MoveByKeyboard(
                        solution,
                        solution.FindItem("FolderWithContentsProj", "C"),
                        solution.FindItem("FolderWithContentsProj", "A", "B")
                    );

                    solution.AssertFolderExists("FolderWithContentsProj", "A");
                    solution.AssertFolderDoesntExist("FolderWithContentsProj", "A", "B");
                    solution.AssertFileDoesntExist("FolderWithContentsProj", "A", "B", "File.txt");
                    solution.AssertFolderExists("FolderWithContentsProj", "C");
                    solution.AssertFolderExists("FolderWithContentsProj", "C", "B");
                    solution.AssertFileExists("FolderWithContentsProj", "C", "B", "File.txt");
                }
            }
        }

        public void CopyFolderWithContents(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("FolderWithContentsProj",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("A"),
                        ProjectGenerator.Folder("A\\B"),
                        ProjectGenerator.Content("A\\B\\File.txt", ""),
                        ProjectGenerator.Folder("C")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    CopyByKeyboard(
                        solution,
                        solution.FindItem("FolderWithContentsProj", "C"),
                        solution.FindItem("FolderWithContentsProj", "A", "B")
                    );

                    solution.AssertFolderExists("FolderWithContentsProj", "A");
                    solution.AssertFolderExists("FolderWithContentsProj", "A", "B");
                    solution.AssertFileExists("FolderWithContentsProj", "A", "B", "File.txt");
                    solution.AssertFolderExists("FolderWithContentsProj", "C");
                    solution.AssertFolderExists("FolderWithContentsProj", "C", "B");
                    solution.AssertFileExists("FolderWithContentsProj", "C", "B", "File.txt");
                }
            }
        }

        public void MoveProjectToSolutionFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveProjectToSolutionFolder(app, pg, MoveByKeyboard);
        }

        public void MoveProjectToSolutionFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveProjectToSolutionFolder(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cut an item from our project, paste into another project, item should be removed from our project
        /// </summary>
        private void MoveProjectToSolutionFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new ISolutionElement[] {
                    new ProjectDefinition("DragDropCopyCutPaste", projectType),
                    ProjectGenerator.SolutionFolder("SolFolder")
                };

                using (var solution = SolutionFile.Generate("DragDropCopyCutPaste", projects).ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("SolFolder"),
                        solution.WaitForItem("DragDropCopyCutPaste")
                    );

                    Assert.IsNotNull(solution.WaitForItem("SolFolder", "DragDropCopyCutPaste"));
                }
            }
        }

        /// <summary>
        /// Copy read-only file within project - ensure RO attribute is removed.
        /// </summary>
        public void CopyReadOnlyFileByKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CopyReadOnlyFile(app, pg, CopyByKeyboard);
        }

        /// <summary>
        /// Copy read-only file within project - ensure RO attribute is removed.
        /// </summary>
        public void CopyReadOnlyFileByMouse(VisualStudioApp app, ProjectGenerator pg) {
            CopyReadOnlyFile(app, pg, CopyByMouse);
        }

        private void CopyReadOnlyFile(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "CopyReadOnlyFile",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Compile("Class")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("CopyReadOnlyFile", projects).ToVs(app)) {
                    var classFile = Path.Combine(solution.SolutionDirectory, "CopyReadOnlyFile", "Class" + projectType.CodeExtension);
                    Assert.IsTrue(File.Exists(classFile));
                    File.SetAttributes(classFile, FileAttributes.ReadOnly | FileAttributes.Archive);
                    Assert.IsTrue(File.GetAttributes(classFile).HasFlag(FileAttributes.ReadOnly));
                    Assert.IsTrue(File.GetAttributes(classFile).HasFlag(FileAttributes.Archive));

                    var classCopyFile = Path.Combine(solution.SolutionDirectory, "CopyReadOnlyFile", "Class - Copy" + projectType.CodeExtension);
                    Assert.IsFalse(File.Exists(classCopyFile));

                    mover(
                        solution,
                        solution.WaitForItem("CopyReadOnlyFile"),
                        solution.WaitForItem("CopyReadOnlyFile", "Class" + projectType.CodeExtension)
                    );

                    solution.WaitForItem("CopyReadOnlyFile", "Class - Copy" + projectType.CodeExtension);

                    Assert.IsTrue(File.Exists(classCopyFile));
                    Assert.IsFalse(File.GetAttributes(classCopyFile).HasFlag(FileAttributes.ReadOnly), "Read-only attribute was not cleared");
                    Assert.IsTrue(File.GetAttributes(classCopyFile).HasFlag(FileAttributes.Archive), "Other attributes were cleared");
                }
            }
        }

        public void CopyFileFromFolderToLinkedFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            CopyFileFromFolderToLinkedFolder(app, pg, CopyByKeyboard);
        }

        public void CopyFileFromFolderToLinkedFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            CopyFileFromFolderToLinkedFolder(app, pg, CopyByMouse);
        }

        /// <summary>
        /// Copy item from folder to a symbolic link of that folder.  Expect a copy to be made.
        /// NOTE: Because of symbolic link creation, this test must be run as administrator.
        /// </summary>
        private void CopyFileFromFolderToLinkedFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate copier) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveLinkedFolder",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("Folder"),
                            ProjectGenerator.Content("Folder\\FileInFolder.txt", "File inside of linked folder..."),
                            ProjectGenerator.SymbolicLink("FolderLink", "Folder")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveLinkedFolder", projectDefs).ToVs(app)) {
                    copier(
                        solution,
                        solution.FindItem("MoveLinkedFolder", "FolderLink"),
                        solution.FindItem("MoveLinkedFolder", "Folder", "FileInFolder.txt"));

                    // Verify that after the dialog our files are still present.
                    solution.AssertFileExists("MoveLinkedFolder", "FolderLink", "FileInFolder.txt");
                    solution.AssertFileExists("MoveLinkedFolder", "Folder", "FileInFolder.txt");

                    // Verify the copies were made.
                    solution.AssertFileExists("MoveLinkedFolder", "FolderLink", "FileInFolder - Copy.txt");
                    solution.AssertFileExists("MoveLinkedFolder", "Folder", "FileInFolder - Copy.txt");
                }
            }
        }

        // https://github.com/Microsoft/PTVS/issues/206
        // Copy and paste cross project into a folder should include the item in the folder
        public void CopyFileToFolderCrossProject(VisualStudioApp app, ProjectGenerator pg) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("CopyToFolderCrossProjectDest",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("Folder")
                        )
                    ),
                    new ProjectDefinition("CopyToFolderCrossProjectSource",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Content("File.txt", "File copied to folder")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("CopyFileToFolderCrossProject", projectDefs).ToVs(app)) {
                    CopyByKeyboard(
                        solution,
                        solution.FindItem("CopyToFolderCrossProjectDest", "Folder"),
                        solution.FindItem("CopyToFolderCrossProjectSource", "File.txt"));

                    // Verify the files were copied
                    solution.AssertFileExists("CopyToFolderCrossProjectDest", "Folder", "File.txt");
                }
            }
        }

        internal delegate void MoveDelegate(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source);

        /// <summary>
        /// Moves one or more items in solution explorer to the destination using the mouse.
        /// </summary>
        internal static void MoveByMouse(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            destination.DragOntoThis(Key.LeftShift, source);
            vs.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        /// <summary>
        /// Moves or copies (taking the default behavior) one or more items in solution explorer to 
        /// the destination using the mouse.
        /// </summary>
        private static void DragAndDrop(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            destination.DragOntoThis(source);
            vs.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        /// <summary>
        /// Moves one or more items in solution explorer to the destination using the mouse.
        /// </summary>
        internal static void CopyByMouse(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            destination.DragOntoThis(Key.LeftCtrl, source);
        }

        /// <summary>
        /// Moves one or more items in solution explorer using the keyboard to cut and paste.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        internal static void MoveByKeyboard(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            AutomationWrapper.Select(source.First());
            for (int i = 1; i < source.Length; i++) {
                AutomationWrapper.AddToSelection(source[i]);
            }

            vs.ControlX();

            AutomationWrapper.Select(destination);
            vs.ControlV();
            vs.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        /// <summary>
        /// Moves one or more items in solution explorer using the keyboard to cut and paste.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        internal static void CopyByKeyboard(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            AutomationWrapper.Select(source.First());
            for (int i = 1; i < source.Length; i++) {
                AutomationWrapper.AddToSelection(source[i]);
            }

            vs.ControlC();

            AutomationWrapper.Select(destination);
            vs.ControlV();
        }
    }
}
