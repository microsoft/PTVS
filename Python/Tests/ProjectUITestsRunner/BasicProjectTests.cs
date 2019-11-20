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
    public class BasicProjectTests {
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
        public void ProjectAddItem() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.ProjectAddItem));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void CleanSolution() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.CleanSolution));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void BuildSolution() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.BuildSolution));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void OpenCommandHere() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.OpenCommandHere));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void PasteFileWhileOpenInEditor() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.PasteFileWhileOpenInEditor));
        }

        /// <summary>
        /// Checks various combinations of item visibility from within the users project
        /// and from imported projects and how it's controlled by the Visible metadata.
        /// </summary>
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void ItemVisibility() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.ItemVisibility));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ProjectAddExistingExcludedFolder() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.ProjectAddExistingExcludedFolder));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RenameFile() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.RenameFile));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RenameFileExistsInHierarchy() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.RenameFileExistsInHierarchy));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RenameFileExistsInHierarchy_FileOpen_Cancel() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.RenameFileExistsInHierarchy_FileOpen_Cancel));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void RenameFileExistsInHierarchy_FileOpen_Save() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.RenameFileExistsInHierarchy_FileOpen_Save));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void RenameFileExistsInHierarchy_FileOpen_DontSave() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.RenameFileExistsInHierarchy_FileOpen_DontSave));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void IsDocumentInProject() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.IsDocumentInProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DeleteFolderWithReadOnlyFile() {
            _vs.RunTest(nameof(ProjectUITests.BasicProjectTests.DeleteFolderWithReadOnlyFile));
        }
    }
}
