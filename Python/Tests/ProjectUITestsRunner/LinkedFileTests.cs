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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace ProjectUITestsRunner {
    [TestClass]
    public class LinkedFileTests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.ProjectUITests",
            // Remote class name
            $"ProjectUITests.{GetType().Name}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RenameLinkedNode() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.RenameLinkedNode));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MoveLinkedNode() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MoveLinkedNode));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MultiProjectMove() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MultiProjectMove));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MultiProjectMoveExists2() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MultiProjectMoveExists2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MoveLinkedNodeOpen() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MoveLinkedNodeOpen));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MoveLinkedNodeOpenEdited() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MoveLinkedNodeOpenEdited));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void MoveLinkedNodeFileExistsButNotInProject() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.MoveLinkedNodeFileExistsButNotInProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DeleteLinkedNode() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.DeleteLinkedNode));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void LinkedFileInProjectIgnored() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.LinkedFileInProjectIgnored));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SaveAsCreateLink() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.SaveAsCreateLink));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SaveAsCreateFile() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.SaveAsCreateFile));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SaveAsCreateFileNewDirectory() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.SaveAsCreateFileNewDirectory));
        }

        /// <summary>
        /// Adding a duplicate link to the same item
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItem() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItem));
        }

        /// <summary>
        /// Adding a link to a folder which is already linked in somewhere else.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAndItemIsAlreadyLinked() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAndItemIsAlreadyLinked));
        }

        /// <summary>
        /// Adding a duplicate link to the same item.
        /// 
        /// Also because the linked file dir is "LinkedFilesDir" which is a substring of "LinkedFiles" (our project name)
        /// this verifies we deal with the project name string comparison correctly (including a \ at the end of the
        /// path).
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAndLinkAlreadyExists() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAndLinkAlreadyExists));
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (when the file only exists on disk)
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAndFileByNameExistsOnDiskButNotInProject() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAndFileByNameExistsOnDiskButNotInProject));
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (both in the project and on disk)
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAndFileByNameExistsOnDiskAndInProject() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAndFileByNameExistsOnDiskAndInProject));
        }

        /// <summary>
        /// Adding new linked item when file of same name exists (in the project, but not on disk)
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAndFileByNameExistsInProjectButNotOnDisk() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAndFileByNameExistsInProjectButNotOnDisk));
        }

        /// <summary>
        /// Adding new linked item when the file lives in the project dir but not in the directory we selected
        /// Add Existing Item from.  We should add the file to the directory where it lives.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void AddExistingItemAsLinkButFileExistsInProjectDirectory() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.AddExistingItemAsLinkButFileExistsInProjectDirectory));
        }

        /// <summary>
        /// Reaming the file name in the Link attribute is ignored.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RenamedLinkedFile() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.RenamedLinkedFile));
        }

        /// <summary>
        /// A link path outside of our project dir will result in the link being ignored.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void BadLinkPath() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.BadLinkPath));
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RootedLinkIgnored() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.RootedLinkIgnored));
        }

        /// <summary>
        /// A rooted link path is ignored.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RootedIncludeIgnored() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.RootedIncludeIgnored));
        }

        /// <summary>
        /// Test linked files with a project home set (done by save as in this test)
        /// https://nodejstools.codeplex.com/workitem/1511
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void TestLinkedWithProjectHome() {
            _vs.RunTest(nameof(ProjectUITests.LinkedFileTests.TestLinkedWithProjectHome));
        }
    }
}
