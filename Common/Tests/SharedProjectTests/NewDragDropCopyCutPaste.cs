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

using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Input;
using Microsoft.TC.TestHostAdapters;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.SharedProject;
using TestUtilities.UI;
using Keyboard = TestUtilities.UI.Keyboard;
using Mouse = TestUtilities.UI.Mouse;

namespace Microsoft.VisualStudioTools.SharedProjectTests {
    [TestClass]
    public class NewDragDropCopyCutPaste : SharedProjectTest {
        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveExcludedFolderKeyboard() {
            MoveExcludedFolder(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveExcludedFolderMouse() {
            MoveExcludedFolder(MoveByMouse);
        }

        private void MoveExcludedFolder(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("MoveExcludedFolder", 
                    projectType, 
                    PropertyGroup(
                        Property("ProjectView", "ShowAllFiles")
                    ), 
                    ItemGroup(
                        Folder("Fob", isExcluded: true),
                        Folder("Fob\\Oar", isExcluded: true),
                        Folder("Baz", isExcluded: true)
                    )
                );

                using (var solution = testDef.Generate().ToVs()) {
                    mover(
                        solution.FindItem("MoveExcludedFolder", "Baz"),
                        solution.FindItem("MoveExcludedFolder", "Fob")
                    );

                    solution.AssertFolderDoesntExist("MoveExcludedFolder", "Fob");
                    solution.AssertFolderExists("MoveExcludedFolder", "Baz", "Fob");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveExcludedItemToFolderKeyboard() {
            MoveExcludedItemToFolder(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveExcludedItemToFolderMouse() {
            MoveExcludedItemToFolder(MoveByMouse);
        }

        private void MoveExcludedItemToFolder(MoveDelegate mover) {
            
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("MoveExcludedItemToFolder", 
                    projectType, 
                    PropertyGroup(
                        Property("ProjectView", "ShowAllFiles")
                    ), 
                    ItemGroup(
                        Folder("Folder"),
                        Compile("codefile", isExcluded: true)
                    )
                );

                using (var solution = testDef.Generate().ToVs()) {
                    mover(
                        solution.FindItem("MoveExcludedItemToFolder", "Folder"),
                        solution.FindItem("MoveExcludedItemToFolder", "codefile" + projectType.CodeExtension)
                    );

                    solution.AssertFileDoesntExist("MoveExcludedItemToFolder", "codefile" + projectType.CodeExtension);
                    solution.AssertFileExists("MoveExcludedItemToFolder", "Folder", "codefile" + projectType.CodeExtension);
                    Assert.IsTrue(solution.Project.GetIsFolderExpanded("Folder"));
                    
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameSkipMoveKeyboard() {
            MoveDuplicateFileNameSkipMove(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameSkipMoveMouse() {
            MoveDuplicateFileNameSkipMove(MoveByMouse);
        }

        /// <summary>
        /// Move item within the project from one location to where it already exists, skipping the move.
        /// </summary>
        private void MoveDuplicateFileNameSkipMove(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("MoveDuplicateFileName",
                    projectType,
                    ItemGroup(
                        Folder("Folder"),
                        Content("textfile.txt", "root"),
                        Content("Folder\\textfile.txt", "Folder")
                    )
                );

                using (var solution = testDef.Generate().ToVs()) {
                    mover(
                        solution.FindItem("MoveDuplicateFileName", "Folder"),
                        solution.FindItem("MoveDuplicateFileName", "textfile.txt")
                    );

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.No();
                    }

                    solution.App.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("root", "MoveDuplicateFileName", "textfile.txt");
                    solution.AssertFileExistsWithContent("Folder", "MoveDuplicateFileName", "Folder", "textfile.txt");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNamesSkipOneKeyboard() {
            MoveDuplicateFileNamesSkipOne(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNamesSkipOneMouse() {
            MoveDuplicateFileNamesSkipOne(MoveByMouse);
        }

        /// <summary>
        /// Cut 2 items, paste where they exist, skip pasting the 1st one but paste the 2nd.
        /// 
        /// The 1st item shouldn't be removed from the parent hierarchy, the 2nd should, and only the 2nd item should be overwritten.
        /// </summary>
        private void MoveDuplicateFileNamesSkipOne(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var testDef = new ProjectDefinition("MoveDuplicateFileName",
                    projectType,
                    ItemGroup(
                        Folder("Folder"),
                        Content("textfile1.txt", "root1"),
                        Content("textfile2.txt", "root2"),
                        Content("Folder\\textfile1.txt", "Folder1"),
                        Content("Folder\\textfile2.txt", "Folder2")
                    )
                );

                using (var solution = testDef.Generate().ToVs()) {
                    mover(
                        solution.FindItem("MoveDuplicateFileName", "Folder"),
                        solution.FindItem("MoveDuplicateFileName", "textfile1.txt"),
                        solution.FindItem("MoveDuplicateFileName", "textfile2.txt")
                    );

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.Yes();
                    }

                    solution.App.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("root1", "MoveDuplicateFileName", "textfile1.txt");
                    solution.AssertFileDoesntExist("MoveDuplicateFileName", "textfile2.txt");
                    solution.AssertFileExistsWithContent("Folder1", "MoveDuplicateFileName", "Folder", "textfile1.txt");
                    solution.AssertFileExistsWithContent("root2", "MoveDuplicateFileName", "Folder", "textfile2.txt");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNamesCrossProjectSkipOneKeyboard() {
            MoveDuplicateFileNamesCrossProjectSkipOne(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNamesCrossProjectSkipOneMouse() {
            MoveDuplicateFileNamesCrossProjectSkipOne(MoveByMouse);
        }

        /// <summary>
        /// Cut 2 items, paste where they exist, skip pasting the 1st one but paste the 2nd.
        /// 
        /// The 1st item shouldn't be removed from the parent hierarchy, the 2nd should, and only the 2nd item should be overwritten.
        /// </summary>
        private void MoveDuplicateFileNamesCrossProjectSkipOne(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName",
                        projectType,
                        ItemGroup(
                            Content("textfile1.txt", "textfile1 - lang"),
                            Content("textfile2.txt", "textfile2 - lang")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileName2",
                        projectType,
                        ItemGroup(
                            Folder("Folder"),
                            Content("textfile1.txt", "textfile1 - 2"),
                            Content("textfile2.txt", "textfile2 - 2")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs()) {
                    var item1 = solution.FindItem("MoveDuplicateFileName", "textfile1.txt");
                    var item2 = solution.FindItem("MoveDuplicateFileName", "textfile2.txt");
                    mover(
                        solution.FindItem("MoveDuplicateFileName2"),
                        item1,
                        item2
                    );

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.No();
                    }

                    System.Threading.Thread.Sleep(1000);

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.Yes();
                    }

                    solution.App.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("textfile1 - lang", "MoveDuplicateFileName", "textfile1.txt");
                    solution.AssertFileExistsWithContent("textfile2 - lang", "MoveDuplicateFileName", "textfile2.txt");
                    solution.AssertFileExistsWithContent("textfile1 - 2", "MoveDuplicateFileName2", "textfile1.txt");
                    solution.AssertFileExistsWithContent("textfile2 - lang", "MoveDuplicateFileName2", "textfile2.txt");
                }
            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameCrossProjectSkipMoveKeyboard() {
            MoveDuplicateFileNameCrossProjectSkipMove(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameCrossProjectSkipMoveMouse() {
            MoveDuplicateFileNameCrossProjectSkipMove(MoveByMouse);
        }

        /// <summary>
        /// Move item to where an item by that name exists across 2 projects of the same type.
        /// 
        /// https://pytools.codeplex.com/workitem/1967
        /// </summary>
        private void MoveDuplicateFileNameCrossProjectSkipMove(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName1",
                        projectType,
                        ItemGroup(
                            Content("textfile.txt", "MoveDuplicateFileName1")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileName2",
                        projectType,
                        ItemGroup(
                            Folder("Folder"),
                            Content("textfile.txt", "MoveDuplicateFileName2")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs()) {
                    mover(
                        solution.FindItem("MoveDuplicateFileName2"),
                        solution.FindItem("MoveDuplicateFileName1", "textfile.txt")
                    );

                    using (var dialog = OverwriteFileDialog.Wait(solution.App)) {
                        dialog.No();
                    }

                    solution.App.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("MoveDuplicateFileName1", "MoveDuplicateFileName1", "textfile.txt");
                    solution.AssertFileExistsWithContent("MoveDuplicateFileName2", "MoveDuplicateFileName2", "textfile.txt");
                }

            }
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveKeyboard() {
            MoveDuplicateFileNameCrossProjectCSharpSkipMove(MoveByKeyboard);
        }

        [TestMethod, Priority(0), TestCategory("Core")]
        [HostType("TC Dynamic"), DynamicHostType(typeof(VsIdeHostAdapter))]
        public void MoveDuplicateFileNameCrossProjectCSharpSkipMoveMouse() {
            MoveDuplicateFileNameCrossProjectCSharpSkipMove(MoveByMouse);
        }

        /// <summary>
        /// Move item to where item exists across project types.
        /// </summary>
        private void MoveDuplicateFileNameCrossProjectCSharpSkipMove(MoveDelegate mover) {
            foreach (var projectType in ProjectTypes) {
                var projectDefs = new[] {
                    new ProjectDefinition("MoveDuplicateFileName1",
                        projectType,
                        ItemGroup(
                            Content("textfile.txt", "MoveDuplicateFileName1")
                        )
                    ),
                    new ProjectDefinition("MoveDuplicateFileNameCS",
                        ProjectType.CSharp,
                        ItemGroup(
                            Folder("Folder"),
                            Content("textfile.txt", "MoveDuplicateFileNameCS")
                        )
                    )
                };

                using (var solution = SolutionFile.Generate("MoveDuplicateFileName", projectDefs).ToVs()) {
                    mover(
                        solution.FindItem("MoveDuplicateFileNameCS"),
                        solution.FindItem("MoveDuplicateFileName1", "textfile.txt")
                    );

                    // say no to replacing in the C# project system
                    solution.App.WaitForDialog();
                    Keyboard.Type(Key.N);

                    solution.App.WaitForDialogDismissed();

                    solution.AssertFileExistsWithContent("MoveDuplicateFileName1", "MoveDuplicateFileName1", "textfile.txt");
                    solution.AssertFileExistsWithContent("MoveDuplicateFileNameCS", "MoveDuplicateFileNameCS", "textfile.txt");
                }

            }
        }

        /// <summary>
        /// Selects the provided items with the mouse preparing for a drag and drop
        /// </summary>
        /// <param name="source"></param>
        private static void SelectItemsForDragAndDrop(AutomationElement[] source) {
            AutomationWrapper.Select(source.First());
            for (int i = 1; i < source.Length; i++) {
                AutomationWrapper.AddToSelection(source[i]);
            }

            Mouse.MoveTo(source.Last().GetClickablePoint());
            Mouse.Down(MouseButton.Left);
        }

        /// <summary>
        /// Moves one or more items in solution explorer to the destination using the mouse.
        /// </summary>
        private static void MoveByMouse(AutomationElement destination, params AutomationElement[] source) {
            SelectItemsForDragAndDrop(source);

            try {
                try {
                    Keyboard.Press(Key.LeftShift);
                    Mouse.MoveTo(destination.GetClickablePoint());
                } finally {
                    Mouse.Up(MouseButton.Left);
                }
            } finally {
                Keyboard.Release(Key.LeftShift);
            }
        }

        /// <summary>
        /// Moves one or more items in solution explorer using the keyboard to cut and paste.
        /// </summary>
        /// <param name="destination"></param>
        /// <param name="source"></param>
        private static void MoveByKeyboard(AutomationElement destination, params AutomationElement[] source) {
            AutomationWrapper.Select(source.First());
            for (int i = 1; i < source.Length; i++) {
                AutomationWrapper.AddToSelection(source[i]);
            }
            
            Keyboard.ControlX();

            AutomationWrapper.Select(destination);
            Keyboard.ControlV();
        }

        private delegate void MoveDelegate(AutomationElement destination, params AutomationElement[] source);
    }
}
