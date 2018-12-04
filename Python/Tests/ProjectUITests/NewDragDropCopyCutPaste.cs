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

using System.Linq;
using System.Windows.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;

namespace ProjectUITests {
    [TestClass]
    public class NewDragDropCopyCutPaste {
        public void MoveToMissingFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveToMissingFolder(app, pg, MoveByKeyboard);
        }

        public void MoveToMissingFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveToMissingFolder(app, pg, MoveByMouse);
        }

        private void MoveToMissingFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveToMissingFolder",
                    projectType,
                    ProjectGenerator.PropertyGroup(
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                    ),
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Fob", isExcluded: false, isMissing: true),
                        ProjectGenerator.Compile("codefile", isExcluded: false)
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveToMissingFolder", "Fob"),
                        solution.FindItem("MoveToMissingFolder", "codefile" + projectType.CodeExtension)
                    );

                    solution.AssertFileDoesntExist("MoveToMissingFolder", "codefile" + projectType.CodeExtension);
                    solution.AssertFileExists("MoveToMissingFolder", "Fob", "codefile" + projectType.CodeExtension);
                }
            }
        }

        public void MoveExcludedFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveExcludedFolder(app, pg, MoveByKeyboard);
        }

        public void MoveExcludedFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveExcludedFolder(app, pg, MoveByMouse);
        }

        private void MoveExcludedFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveExcludedFolder",
                    projectType,
                    ProjectGenerator.PropertyGroup(
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                    ),
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Fob", isExcluded: true),
                        ProjectGenerator.Folder("Fob\\Oar", isExcluded: true),
                        ProjectGenerator.Folder("Baz", isExcluded: true)
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveExcludedFolder", "Baz"),
                        solution.FindItem("MoveExcludedFolder", "Fob")
                    );

                    solution.AssertFolderDoesntExist("MoveExcludedFolder", "Fob");
                    solution.AssertFolderExists("MoveExcludedFolder", "Baz", "Fob");
                }
            }
        }

        public void MoveExcludedItemToFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveExcludedItemToFolder(app, pg, MoveByKeyboard);
        }

        public void MoveExcludedItemToFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveExcludedItemToFolder(app, pg, MoveByMouse);
        }

        private void MoveExcludedItemToFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {

            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveExcludedItemToFolder",
                    projectType,
                    ProjectGenerator.PropertyGroup(
                        ProjectGenerator.Property("ProjectView", "ShowAllFiles")
                    ),
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder"),
                        ProjectGenerator.Compile("codefile", isExcluded: true)
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveExcludedItemToFolder", "Folder"),
                        solution.FindItem("MoveExcludedItemToFolder", "codefile" + projectType.CodeExtension)
                    );

                    solution.AssertFileDoesntExist("MoveExcludedItemToFolder", "codefile" + projectType.CodeExtension);
                    solution.AssertFileExists("MoveExcludedItemToFolder", "Folder", "codefile" + projectType.CodeExtension);
                    Assert.IsTrue(solution.GetProject("MoveExcludedItemToFolder").GetIsFolderExpanded("Folder"));

                }
            }
        }

        public void MoveDuplicateFileNameSkipMoveKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameSkipMove(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameSkipMoveMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameSkipMove(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Move item within the project from one location to where it already exists, skipping the move.
        /// </summary>
        private void MoveDuplicateFileNameSkipMove(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveDuplicateFileName",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder"),
                        ProjectGenerator.Content("textfile.txt", "root"),
                        ProjectGenerator.Content("Folder\\textfile.txt", "Folder")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileName", "Folder"),
                        solution.FindItem("MoveDuplicateFileName", "textfile.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.No();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("root", "MoveDuplicateFileName", "textfile.txt");
                    solution.AssertFileExistsWithContent("Folder", "MoveDuplicateFileName", "Folder", "textfile.txt");
                }
            }
        }

        public void MoveDuplicateFileNamesSkipOneKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesSkipOne(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNamesSkipOneMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesSkipOne(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cut 2 items, paste where they exist, skip pasting the 1st one but paste the 2nd.
        /// 
        /// The 1st item shouldn't be removed from the parent hierarchy, the 2nd should, and only the 2nd item should be overwritten.
        /// </summary>
        private void MoveDuplicateFileNamesSkipOne(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveDuplicateFileName",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Folder"),
                        ProjectGenerator.Content("textfile1.txt", "root1"),
                        ProjectGenerator.Content("textfile2.txt", "root2"),
                        ProjectGenerator.Content("Folder\\textfile1.txt", "Folder1"),
                        ProjectGenerator.Content("Folder\\textfile2.txt", "Folder2")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileName", "Folder"),
                        solution.FindItem("MoveDuplicateFileName", "textfile1.txt"),
                        solution.FindItem("MoveDuplicateFileName", "textfile2.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.Yes();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("root1", "MoveDuplicateFileName", "textfile1.txt");
                    solution.AssertFileDoesntExist("MoveDuplicateFileName", "textfile2.txt");
                    solution.AssertFileExistsWithContent("Folder1", "MoveDuplicateFileName", "Folder", "textfile1.txt");
                    solution.AssertFileExistsWithContent("root2", "MoveDuplicateFileName", "Folder", "textfile2.txt");
                }
            }
        }

        public void MoveDuplicateFileNamesFoldersSkipOneKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesFoldersSkipOne(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNamesFoldersSkipOneMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesFoldersSkipOne(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cut 2 items, paste where they exist, skip pasting the 1st one but paste the 2nd.
        /// 
        /// The 1st item shouldn't be removed from the parent hierarchy, the 2nd should, and only the 2nd item should be overwritten.
        /// </summary>
        private void MoveDuplicateFileNamesFoldersSkipOne(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var testDef = new ProjectDefinition("MoveDuplicateFileName",
                    projectType,
                    ProjectGenerator.ItemGroup(
                        ProjectGenerator.Folder("Source"),
                        ProjectGenerator.Content("Source\\textfile1.txt", "source1"),
                        ProjectGenerator.Content("Source\\textfile2.txt", "source2"),

                        ProjectGenerator.Folder("Target"),
                        ProjectGenerator.Content("Target\\textfile1.txt", "target1"),
                        ProjectGenerator.Content("Target\\textfile2.txt", "target2")
                    )
                );

                using (var solution = testDef.Generate().ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileName", "Target"),
                        solution.FindItem("MoveDuplicateFileName", "Source", "textfile1.txt"),
                        solution.FindItem("MoveDuplicateFileName", "Source", "textfile2.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.Yes();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("source1", "MoveDuplicateFileName", "Source", "textfile1.txt");
                    solution.AssertFileDoesntExist("MoveDuplicateFileName", "textfile2.txt");
                    solution.AssertFileExistsWithContent("target1", "MoveDuplicateFileName", "Target", "textfile1.txt");
                    solution.AssertFileExistsWithContent("source2", "MoveDuplicateFileName", "Target", "textfile2.txt");
                }
            }
        }

        public void MoveDuplicateFileNamesCrossProjectSkipOneKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesCrossProjectSkipOne(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNamesCrossProjectSkipOneMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNamesCrossProjectSkipOne(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Cut 2 items, paste where they exist, skip pasting the 1st one but paste the 2nd.
        /// 
        /// The 1st item shouldn't be removed from the parent hierarchy, the 2nd should, and only the 2nd item should be overwritten.
        /// </summary>
        private void MoveDuplicateFileNamesCrossProjectSkipOne(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Content("textfile1.txt", "textfile1 - lang"),
                            ProjectGenerator.Content("textfile2.txt", "textfile2 - lang")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileName2",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("Folder"),
                            ProjectGenerator.Content("textfile1.txt", "textfile1 - 2"),
                            ProjectGenerator.Content("textfile2.txt", "textfile2 - 2")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs(app)) {
                    var item1 = solution.FindItem("MoveDuplicateFileName", "textfile1.txt");
                    var item2 = solution.FindItem("MoveDuplicateFileName", "textfile2.txt");
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileName2"),
                        item1,
                        item2
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.Yes();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("textfile1 - lang", "MoveDuplicateFileName", "textfile1.txt");
                    solution.AssertFileExistsWithContent("textfile2 - lang", "MoveDuplicateFileName", "textfile2.txt");
                    solution.AssertFileExistsWithContent("textfile1 - 2", "MoveDuplicateFileName2", "textfile1.txt");
                    solution.AssertFileExistsWithContent("textfile2 - lang", "MoveDuplicateFileName2", "textfile2.txt");
                }
            }
        }

        public void MoveDuplicateFileNameCrossProjectSkipMoveKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCrossProjectSkipMove(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameCrossProjectSkipMoveMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCrossProjectSkipMove(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Move item to where an item by that name exists across 2 projects of the same type.
        /// 
        /// https://pytools.codeplex.com/workitem/1967
        /// </summary>
        private void MoveDuplicateFileNameCrossProjectSkipMove(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName1",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Content("textfile.txt", "MoveDuplicateFileName1")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileName2",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("Folder"),
                            ProjectGenerator.Content("textfile.txt", "MoveDuplicateFileName2")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileName2"),
                        solution.FindItem("MoveDuplicateFileName1", "textfile.txt")
                    );

                    using (var dialog = solution.WaitForOverwriteFileDialog()) {
                        dialog.No();
                    }

                    solution.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("MoveDuplicateFileName1", "MoveDuplicateFileName1", "textfile.txt");
                    solution.AssertFileExistsWithContent("MoveDuplicateFileName2", "MoveDuplicateFileName2", "textfile.txt");
                }

            }
        }

        public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCrossProjectCSharpSkipMove(app, pg, MoveByKeyboard);
        }

        public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveDuplicateFileNameCrossProjectCSharpSkipMove(app, pg, MoveByMouse);
        }

        /// <summary>
        /// Move item to where item exists across project types.
        /// </summary>
        private void MoveDuplicateFileNameCrossProjectCSharpSkipMove(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName1",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Content("textfile.txt", "MoveDuplicateFileName1")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileNameCS",
                        ProjectType.CSharp,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Folder("Folder"),
                            ProjectGenerator.Content("textfile.txt", "MoveDuplicateFileNameCS")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveDuplicateFileNameCS"),
                        solution.FindItem("MoveDuplicateFileName1", "textfile.txt")
                    );

                    // say no to replacing in the C# project system
                    using (var dlg = AutomationDialog.WaitForDialog(app)) {
                        dlg.ClickButtonAndClose("No");
                    }

                    solution.AssertFileExistsWithContent("MoveDuplicateFileName1", "MoveDuplicateFileName1", "textfile.txt");
                    solution.AssertFileExistsWithContent("MoveDuplicateFileNameCS", "MoveDuplicateFileNameCS", "textfile.txt");
                }

            }
        }

        public void MoveFileFromFolderToLinkedFolderKeyboard(VisualStudioApp app, ProjectGenerator pg) {
            MoveFileFromFolderToLinkedFolder(app, pg, MoveByKeyboard);
            app.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        public void MoveFileFromFolderToLinkedFolderMouse(VisualStudioApp app, ProjectGenerator pg) {
            MoveFileFromFolderToLinkedFolder(app, pg, MoveByMouse);
            app.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        /// <summary>
        /// Move item to a folder that has a symbolic link.  Verify we cannot move 
        /// ourselves to ourselves and that moves are reflected in both the folder and its symbolic link.
        /// NOTE: Because of symbolic link creation, this test must be run as administrator.
        /// </summary>
        private void MoveFileFromFolderToLinkedFolder(VisualStudioApp app, ProjectGenerator pg, MoveDelegate mover) {
            foreach (var projectType in pg.ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveLinkedFolder",
                        projectType,
                        ProjectGenerator.ItemGroup(
                            ProjectGenerator.Content("textfile.txt", "text file contents"),
                            ProjectGenerator.Folder("Folder"),
                            ProjectGenerator.Content("Folder\\FileInFolder.txt", "File inside of linked folder..."),
                            ProjectGenerator.SymbolicLink("FolderLink", "Folder")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveLinkedFolder", projectDefs).ToVs(app)) {
                    mover(
                        solution,
                        solution.FindItem("MoveLinkedFolder", "FolderLink"),
                        solution.FindItem("MoveLinkedFolder", "Folder", "FileInFolder.txt")
                    );

                    // Say okay to the error that pops up since we can't move to ourselves.
                    solution.WaitForDialog();
                    Keyboard.Type(Key.Enter);

                    solution.WaitForDialogDismissed();

                    // Verify that after the dialog our files are still present.
                    solution.AssertFileExists("MoveLinkedFolder", "FolderLink", "FileInFolder.txt");
                    solution.AssertFileExists("MoveLinkedFolder", "Folder", "FileInFolder.txt");

                    // Now move the text file in the root.  Expect it to move and be in both.
                    mover(
                        solution,
                        solution.FindItem("MoveLinkedFolder", "FolderLink"),
                        solution.FindItem("MoveLinkedFolder", "textfile.txt")
                    );

                    solution.AssertFileExists("MoveLinkedFolder", "FolderLink", "textfile.txt");
                    solution.AssertFileExists("MoveLinkedFolder", "Folder", "textfile.txt");
                }
            }
        }

        /// <summary>
        /// Moves one or more items in solution explorer to the destination using the mouse.
        /// </summary>
        private static void MoveByMouse(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            destination.DragOntoThis(Key.LeftShift, source);
            vs.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        /// <summary>
        /// Moves one or more items in solution explorer using the keyboard to cut and paste.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        private static void MoveByKeyboard(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source) {
            AutomationWrapper.Select(source.First());
            for (int i = 1; i < source.Length; i++) {
                AutomationWrapper.AddToSelection(source[i]);
            }

            vs.ControlX();

            AutomationWrapper.Select(destination);
            vs.ControlV();
            vs.MaybeCheckMessageBox(TestUtilities.MessageBoxButton.Ok, "One or more files will be");
        }

        private delegate void MoveDelegate(IVisualStudioInstance vs, ITreeNode destination, params ITreeNode[] source);
    }
}
