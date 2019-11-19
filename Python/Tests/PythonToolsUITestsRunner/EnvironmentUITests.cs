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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace PythonToolsUITestsRunner {
    [TestClass]
    public class EnvironmentUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.PythonToolsUITests",
            // Remote class name
            $"PythonToolsUITests.{GetType().Name}"
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
        public void InstallUninstallPackage() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.InstallUninstallPackage));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void CreateInstallRequirementsTxt() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.CreateInstallRequirementsTxt));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void InstallGenerateRequirementsTxt() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.InstallGenerateRequirementsTxt));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void LoadVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.LoadVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ActivateVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ActivateVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void RemoveVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.RemoveVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DeleteVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.DeleteVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DefaultBaseInterpreterSelection() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.DefaultBaseInterpreterSelection));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ProjectCreateVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectCreateVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ProjectCreateCondaEnvFromPackages() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectCreateCondaEnvFromPackages));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ProjectCreateCondaEnvFromEnvFile() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectCreateCondaEnvFromEnvFile));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ProjectAddExistingVEnvLocal() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectAddExistingVEnvLocal));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ProjectAddCustomEnvLocal() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectAddCustomEnvLocal));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ProjectAddExistingEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.ProjectAddExistingEnv));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void WorkspaceCreateVEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceCreateVEnv));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void WorkspaceCreateCondaEnvFromEnvFile() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceCreateCondaEnvFromEnvFile));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void WorkspaceCreateCondaEnvFromPackages() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceCreateCondaEnvFromPackages));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void WorkspaceCreateCondaEnvFromNoPackages() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceCreateCondaEnvFromNoPackages));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void WorkspaceAddCustomEnvLocal() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceAddCustomEnvLocal));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void WorkspaceAddExistingEnv() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.WorkspaceAddExistingEnv));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void LaunchUnknownEnvironment() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.LaunchUnknownEnvironment));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void EnvironmentReplWorkingDirectory() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.EnvironmentReplWorkingDirectory));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void VirtualEnvironmentReplWorkingDirectory() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.VirtualEnvironmentReplWorkingDirectory));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void SwitcherSingleProject() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.SwitcherSingleProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SwitcherWorkspace() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.SwitcherWorkspace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SwitcherNoProject() {
            _vs.RunTest(nameof(PythonToolsUITests.EnvironmentUITests.SwitcherNoProject));
        }
    }
}
