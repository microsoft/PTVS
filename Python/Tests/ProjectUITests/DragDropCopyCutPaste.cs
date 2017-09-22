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
// MERCHANTABLITY OR NON-INFRINGEMENT.
// 
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Mouse = TestUtilities.UI.Mouse;

namespace ProjectUITests {
    //[TestClass]
    public class DragDropCopyCutPaste : SharedProjectTest {
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MultiPasteKeyboard(VisualStudioApp app) {
            MultiPaste(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MultiPasteMouse(VisualStudioApp app) {
            MultiPaste(app, CopyByMouse);
        }

        /// <summary>
        /// Cut item, paste into folder, paste into top-level, 2nd paste should prompt for overwrite
        /// </summary>
        private void MultiPaste(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("HelloWorld",
                    projectType,
                    ItemGroup(
                        Compile("server"),
                        Compile("server2"),
                        Folder("SubFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutPastePasteItem(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Compile("CutPastePasteItem"),
                        Folder("PasteFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutRenamePaste(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutRenamePaste"),
                        Compile("CutRenamePaste\\CutRenamePaste")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutDeletePaste(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutDeletePaste"),
                        Compile("CutDeletePaste\\CutDeletePaste")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyFileToFolderTooLongKeyboard(VisualStudioApp app) {
            CopyFileToFolderTooLong(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyFileToFolderTooLongMouse(VisualStudioApp app) {
            CopyFileToFolderTooLong(app, CopyByMouse);
        }

        /// <summary>
        /// Adds a new folder which fits exactly w/ no space left in the path name
        /// </summary>
        private void CopyFileToFolderTooLong(VisualStudioApp app, MoveDelegate copier) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("LFN",
                    projectType,
                    ItemGroup(
                        Compile("server")
                    )
                );

                using (var solution = SolutionFile.Generate("LongFileNames", 29, testDef).ToVs(app)) {
                    // find server, send copy & paste, verify copy of file is there
                    var projectNode = solution.WaitForItem("LFN");
                    AutomationWrapper.Select(projectNode);

                    solution.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
                    solution.PressAndRelease(Key.D);
                    solution.PressAndRelease(Key.Right);
                    solution.PressAndRelease(Key.D);
                    solution.Type("01234567891");
                    solution.PressAndRelease(Key.Enter);

                    var folderNode = solution.WaitForItem("LFN", "01234567891");
                    Assert.IsNotNull(folderNode);

                    var serverNode = solution.WaitForItem("LFN", "server" + projectType.CodeExtension);
                    AutomationWrapper.Select(serverNode);
                    solution.ControlC();
                    solution.ControlV();

                    var serverCopy = solution.WaitForItem("LFN", "server - Copy" + projectType.CodeExtension);
                    Assert.IsNotNull(serverCopy);

                    copier(solution, folderNode, serverCopy);

                    // Depending on VS version/update, the message may be:
                    //  "The filename is too long."
                    //  "The filename or extension is too long."
                    solution.CheckMessageBox(" filename ", " is too long.");
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFileToFolderTooLongKeyboard(VisualStudioApp app) {
            CutFileToFolderTooLong(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFileToFolderTooLongMouse(VisualStudioApp app) {
            CutFileToFolderTooLong(app, MoveByMouse);
        }

        /// <summary>
        /// Adds a new folder which fits exactly w/ no space left in the path name
        /// </summary>
        private void CutFileToFolderTooLong(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("LFN",
                    projectType,
                    ItemGroup(
                        Compile("server")
                    )
                );

                using (var solution = SolutionFile.Generate("LongFileNames", 29, testDef).ToVs(app)) {
                    // find server, send copy & paste, verify copy of file is there
                    var projectNode = solution.WaitForItem("LFN");
                    AutomationWrapper.Select(projectNode);

                    solution.PressAndRelease(Key.F10, Key.LeftCtrl, Key.LeftShift);
                    solution.PressAndRelease(Key.D);
                    solution.PressAndRelease(Key.Right);
                    solution.PressAndRelease(Key.D);
                    solution.Type("01234567891");
                    solution.PressAndRelease(Key.Enter);

                    var folderNode = solution.WaitForItem("LFN", "01234567891");
                    Assert.IsNotNull(folderNode);

                    var serverNode = solution.FindItem("LFN", "server" + projectType.CodeExtension);
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutRenamePasteFolder(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutRenamePaste"),
                        Folder("CutRenamePaste\\CutRenamePasteFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopiedBeforeDragPastedAfterDrop(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Compile("CopiedBeforeDragPastedAfterDrop"),
                        Compile("DragAndDroppedDuringCopy"),
                        Folder("DragDuringCopyDestination"),
                        Folder("PasteFolder")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragToAnotherProjectKeyboard(VisualStudioApp app) {
            DragToAnotherProject(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragToAnotherProjectMouse(VisualStudioApp app) {
            DragToAnotherProject(app, DragAndDrop);
        }

        /// <summary>
        /// Copy from CSharp into our project
        /// </summary>
        private void DragToAnotherProject(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Folder("!Source"),
                            Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Folder("DraggedToOtherProject")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFolderPasteOnSelf(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutFolderPasteOnSelf")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragFolderOntoSelf(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("DragFolderOntoSelf"),
                        Compile("DragFolderOntoSelf\\File")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragFolderOntoChild(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("ParentFolder"),
                        Folder("ParentFolder\\ChildFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFileReplace(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("MoveDupFilename"),
                        Folder("MoveDupFilename\\Fob"),
                        Compile("MoveDupFilename\\Fob\\server"),
                        Compile("MoveDupFilename\\server")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFolderAndFile(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CutFolderAndFile"),
                        Folder("CutFolderAndFile\\CutFolder"),
                        Compile("CutFolderAndFile\\CutFolder\\CutFolderAndFile")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CutFilePasteSameLocation(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Compile("CutFilePasteSameLocation")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragFolderAndFileOntoSelf(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("DragFolderAndFileOntoSelf"),
                        Compile("DragFolderAndFileOntoSelf\\File")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyFolderFromAnotherHierarchy(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Folder("!Source"),
                            Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Folder("CopiedFolderWithItemsNotInProject"),
                            Compile("CopiedFolderWithItemsNotInProject\\Class"),
                            Content("CopiedFolderWithItemsNotInProject\\Text.txt", "", isExcluded:true)
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyDeletePaste(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CopyDeletePaste"),
                        Compile("CopyDeletePaste\\CopyDeletePaste")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CrossHierarchyFileDragAndDropKeyboard(VisualStudioApp app) {
            CrossHierarchyFileDragAndDrop(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CrossHierarchyFileDragAndDropMouse(VisualStudioApp app) {
            CrossHierarchyFileDragAndDrop(app, DragAndDrop);
        }

        /// <summary>
        /// Copy from C# into our project
        /// </summary>
        private void CrossHierarchyFileDragAndDrop(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Folder("DropFolder")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Compile("CrossHierarchyFileDragAndDrop")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFolderNameKeyboard(VisualStudioApp app) {
            MoveDuplicateFolderName(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFolderNameMouse(VisualStudioApp app) {
            MoveDuplicateFolderName(app, MoveByMouse);
        }

        /// <summary>
        /// Drag file from another hierarchy into folder in our hierarchy, item should be added
        ///     Cannot move the folder 'DuplicateFolderName'. A folder with that name already exists in the destination directory.
        /// </summary>
        private void MoveDuplicateFolderName(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("DuplicateFolderName"),
                        Folder("DuplicateFolderNameTarget"),
                        Folder("DuplicateFolderNameTarget\\DuplicateFolderName")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyDuplicateFolderNameKeyboard(VisualStudioApp app) {
            CopyDuplicateFolderName(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyDuplicateFolderNameMouse(VisualStudioApp app) {
            CopyDuplicateFolderName(app, CopyByMouse);
        }

        /// <summary>
        /// Copy folder to a destination where the folder already exists.  Say don't copy, nothing should be copied.
        /// </summary>
        private void CopyDuplicateFolderName(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CopyDuplicateFolderName"),
                        Compile("CopyDuplicateFolderName\\server"),
                        Folder("CopyDuplicateFolderNameTarget"),
                        Folder("CopyDuplicateFolderNameTarget\\CopyDuplicateFolderName")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.WaitForItem("DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget"),
                        solution.WaitForItem("DragDropCopyCutPaste", "CopyDuplicateFolderName")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "This folder already contains a folder called 'CopyDuplicateFolderName'");
                        dialog.No();
                    }

                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "CopyDuplicateFolderNameTarget", "CopyDuplicateFolderName", "server" + projectType.CodeExtension);
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveCrossHierarchyKeyboard(VisualStudioApp app) {
            MoveCrossHierarchy(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveCrossHierarchyMouse(VisualStudioApp app) {
            MoveCrossHierarchy(app, MoveByMouse);
        }

        /// <summary>
        /// Cut item from one project, paste into another project, item should be removed from original project
        /// </summary>
        private void MoveCrossHierarchy(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Folder("!Source"),
                            Compile("!Source\\DraggedToOtherProject")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Compile("CrossHierarchyCut")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveReverseCrossHierarchyKeyboard(VisualStudioApp app) {
            MoveReverseCrossHierarchy(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveReverseCrossHierarchyMouse(VisualStudioApp app) {
            MoveReverseCrossHierarchy(app, MoveByMouse);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameOverwriteKeyboard(VisualStudioApp app) {
            MoveDuplicateFileNameOverwrite(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameOverwriteMouse(VisualStudioApp app) {
            MoveDuplicateFileNameOverwrite(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 2 files with the same name, answers yes to overwrite them, and
        /// makes sure only one file is left.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameOverwrite(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
                        dialog.Yes();
                    }

                    solution.AssertFileExists("DragDropCopyCutPaste", "B", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "quox.txt");
                    solution.AssertFileDoesntExist("DragDropCopyCutPaste", "A", "quox.txt");

                    Assert.AreEqual(1, solution.GetProject("DragDropCopyCutPaste").ProjectItems.Item("B").ProjectItems.Count);
                }
            }
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameOverwriteAllItemsKeyboard(VisualStudioApp app) {
            MoveDuplicateFileNameOverwriteAllItems(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameOverwriteAllItemsMouse(VisualStudioApp app) {
            MoveDuplicateFileNameOverwriteAllItems(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, answers yes to overwrite them and
        /// checks do this for all items, and makes sure only one file is left.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameOverwriteAllItems(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Folder("C"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A"),
                        Content("C\\quox.txt", "C")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwriteKeyboard(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwrite(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwriteMouse(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwrite(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 2 files with the same name, pastes them to a folder, and makes
        /// sure we get prompted to overwrite.  Answers no to overwriting, both
        /// files should still be in the project.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwrite(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwrite2Keyboard(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwrite2(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwrite2Mouse(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwrite2(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, and makes
        /// sure that we get multiple prompts to overwrite.  Answers no to all of them, and
        /// all the files should still exist somewhere.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwrite2(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Folder("C"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A"),
                        Content("C\\quox.txt", "C")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwriteAllItemsKeyboard(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwriteAllItems(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameDontOverwriteAllItemsMouse(VisualStudioApp app) {
            MoveDuplicateFileNameDontOverwriteAllItems(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, checks
        /// do this for all items, and makes sure all the files still exist somewhere.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameDontOverwriteAllItems(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Folder("C"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A"),
                        Content("C\\quox.txt", "C")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameCancelKeyboard(VisualStudioApp app) {
            MoveDuplicateFileNameCancel(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDuplicateFileNameCancelMouse(VisualStudioApp app) {
            MoveDuplicateFileNameCancel(app, MoveByMouse);
        }

        /// <summary>
        /// Cuts 3 files with the same name, pastes them to a folder, and makes sure
        /// we get a prompt to overwrite.  Cancels on the 1st prompt and ensures all
        /// of the files are still there.
        /// </summary>
        /// <param name="mover"></param>
        private void MoveDuplicateFileNameCancel(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var project = new ProjectDefinition(
                    "DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("B"),
                        Folder("C"),
                        Content("quox.txt", "top-level"),
                        Content("A\\quox.txt", "A"),
                        Content("C\\quox.txt", "C")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'quox.txt' already exists.");
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
        private void MoveReverseCrossHierarchy(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Compile("CrossHierarchyCut")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveDoubleCrossHierarchy(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType,
                        ItemGroup(
                            Folder("!Source"),
                            Compile("!Source\\DoubleCrossHierarchy")
                        )
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Compile("DoubleCrossHierarchy")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void DragTwiceAndOverwrite(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "DragDropCopyCutPaste",
                        projectType
                    ),
                    new ProjectDefinition(
                        "ConsoleApplication1",
                        ProjectType.CSharp,
                        ItemGroup(
                            Folder("DraggedToOtherProject"),
                            Compile("DragTwiceAndOverwrite")
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
                        AssertUtil.Contains(dialog.Text, "A file with the same name 'DragTwiceAndOverwrite.cs' already exists.");
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyFolderMissingItem(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("CopyFolderMissingItem"),
                        Compile("CopyFolderMissingItem\\missing", isMissing: true),
                        Folder("PasteFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyPasteMissingFile(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Compile("MissingFile", isMissing: true),
                        Folder("PasteFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveFolderExistingFile(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("DragDropCopyCutPaste",
                    projectType,
                    ItemGroup(
                        Folder("PasteFolder"),
                        Content("PasteFolder\\FolderCollision", ""),
                        Folder("FolderCollision")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveFolderWithContents(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("FolderWithContentsProj",
                    projectType,
                    ItemGroup(
                        Folder("A"),
                        Folder("A\\B"),
                        Content("A\\B\\File.txt", ""),
                        Folder("C")
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

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveProjectToSolutionFolderKeyboard(VisualStudioApp app) {
            MoveProjectToSolutionFolder(app, MoveByKeyboard);
        }

        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void MoveProjectToSolutionFolderMouse(VisualStudioApp app) {
            MoveProjectToSolutionFolder(app, MoveByMouse);
        }

        /// <summary>
        /// Cut an item from our project, paste into another project, item should be removed from our project
        /// </summary>
        private void MoveProjectToSolutionFolder(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new ISolutionElement[] {
                    new ProjectDefinition("DragDropCopyCutPaste", projectType),
                    SolutionFolder("SolFolder")
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
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyReadOnlyFileByKeyboard(VisualStudioApp app) {
            CopyReadOnlyFile(app, CopyByKeyboard);
        }

        /// <summary>
        /// Copy read-only file within project - ensure RO attribute is removed.
        /// </summary>
        //[TestMethod, Priority(1)]
        //[TestCategory("Installed")]
        public void CopyReadOnlyFileByMouse(VisualStudioApp app) {
            CopyReadOnlyFile(app, CopyByMouse);
        }

        private void CopyReadOnlyFile(VisualStudioApp app, MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projects = new[] {
                    new ProjectDefinition(
                        "CopyReadOnlyFile",
                        projectType,
                        ItemGroup(
                            Compile("Class")
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

        //[TestMethod, Priority(2)]
        //[TestCategory("Installed")]
        public void CopyFileFromFolderToLinkedFolderKeyboard(VisualStudioApp app) {
            CopyFileFromFolderToLinkedFolder(app, CopyByKeyboard);
        }

        //[TestMethod, Priority(2)]
        //[TestCategory("Installed")]
        public void CopyFileFromFolderToLinkedFolderMouse(VisualStudioApp app) {
            CopyFileFromFolderToLinkedFolder(app, CopyByMouse);
        }

        /// <summary>
        /// Copy item from folder to a symbolic link of that folder.  Expect a copy to be made.
        /// NOTE: Because of symbolic link creation, this test must be run as administrator.
        /// </summary>
        private void CopyFileFromFolderToLinkedFolder(VisualStudioApp app, MoveDelegate copier) {
            foreach (var projectType in ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveLinkedFolder",
                        projectType,
                        ItemGroup(
                            Folder("Folder"),
                            Content("Folder\\FileInFolder.txt", "File inside of linked folder..."),
                            SymbolicLink("FolderLink", "Folder")
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
        //[TestMethod, Priority(2)]
        //[TestCategory("Installed")]
        public void CopyFileToFolderCrossProject(VisualStudioApp app) {
            foreach (var projectType in ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("CopyToFolderCrossProjectDest",
                        projectType,
                        ItemGroup(
                            Folder("Folder")
                        )
                    ),
                    new ProjectDefinition("CopyToFolderCrossProjectSource",
                        projectType,
                        ItemGroup(
                            Content("File.txt", "File copied to folder")
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
        }

        /// <summary>
        /// Moves or copies (taking the default behavior) one or more items in solution explorer to 
        /// the destination using the mouse.
        /// </summary>
        private static void DragAndDrop(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            destination.DragOntoThis(source);
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
