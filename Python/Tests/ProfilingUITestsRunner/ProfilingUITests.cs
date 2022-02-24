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
using PUIT = ProfilingUITests.ProfilingUITests;

namespace ProfilingUITestsRunner {
    [TestClass]
    public class ProfilingUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.ProfilingUITests",
            // Remote class name
            $"ProfilingUITests.{GetType().Name}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DefaultInterpreterSelected() {
            _vs.RunTest(nameof(PUIT.DefaultInterpreterSelected));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void StartupProjectSelected() {
            _vs.RunTest(nameof(PUIT.StartupProjectSelected));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void NewProfilingSession() {
            _vs.RunTest(nameof(PUIT.NewProfilingSession));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DeleteMultipleSessions() {
            _vs.RunTest(nameof(PUIT.DeleteMultipleSessions));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void NewProfilingSessionOpenSolution() {
            _vs.RunTest(nameof(PUIT.NewProfilingSessionOpenSolution));
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void LaunchPythonProfilingWizard() {
            _vs.RunTest(nameof(PUIT.LaunchPythonProfilingWizard));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectPython27() {
            _vs.RunTest(nameof(PUIT.LaunchProjectPython27));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectPython35() {
            _vs.RunTest(nameof(PUIT.LaunchProjectPython35));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectPython36() {
            _vs.RunTest(nameof(PUIT.LaunchProjectPython36));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectPython37() {
            _vs.RunTest(nameof(PUIT.LaunchProjectPython37));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectPython38() {
            _vs.RunTest(nameof(PUIT.LaunchProjectPython38));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithSpaceInFilename() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithSpaceInFilename));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithSolutionFolder() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithSolutionFolder));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithSearchPath() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithSearchPath));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithPythonPathSet() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithPythonPathSet));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithPythonPathClear() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithPythonPathClear));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void LaunchProjectWithEnvironment() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithEnvironment));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SaveDirtySession() {
            _vs.RunTest(nameof(PUIT.SaveDirtySession));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DeleteReport() {
            _vs.RunTest(nameof(PUIT.DeleteReport));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void CompareReports() {
            _vs.RunTest(nameof(PUIT.CompareReports));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void RemoveReport() {
            _vs.RunTest(nameof(PUIT.RemoveReport));
        }

        // P2 because the report viewer may crash VS depending on prior state.
        // We will restart VS before running this test to ensure it is clean.
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void OpenReport() {
            _vs.RunTest(nameof(PUIT.OpenReport));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void OpenReportCtxMenu() {
            _vs.RunTest(nameof(PUIT.OpenReportCtxMenu));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void TargetPropertiesForProject() {
            _vs.RunTest(nameof(PUIT.TargetPropertiesForProject));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void TargetPropertiesForInterpreter() {
            _vs.RunTest(nameof(PUIT.TargetPropertiesForInterpreter));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void TargetPropertiesForExecutable() {
            _vs.RunTest(nameof(PUIT.TargetPropertiesForExecutable));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void StopProfiling() {
            _vs.RunTest(nameof(PUIT.StopProfiling));
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void MultipleTargets() {
            _vs.RunTest(nameof(PUIT.MultipleTargets));
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void MultipleTargetsWithProjectHome() {
            _vs.RunTest(nameof(PUIT.MultipleTargetsWithProjectHome));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void MultipleReports() {
            _vs.RunTest(nameof(PUIT.MultipleReports));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void LaunchExecutable() {
            _vs.RunTest(nameof(PUIT.LaunchExecutable));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ClassProfile() {
            _vs.RunTest(nameof(PUIT.ClassProfile));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void OldClassProfile() {
            _vs.RunTest(nameof(PUIT.OldClassProfile));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DerivedProfile() {
            _vs.RunTest(nameof(PUIT.DerivedProfile));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void Pystone() {
            _vs.RunTest(nameof(PUIT.Pystone));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython27() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython27));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython27x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython27x64));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython35() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython35));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython35x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython35x64));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython36() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython36));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython36x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython36x64));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython37() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython37));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython37x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython37x64));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython38() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython38));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython38x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython38x64));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython310() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython310));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython310x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython310x64));  
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void LaunchExecutableUsingInterpreterGuid() {
            _vs.RunTest(nameof(PUIT.LaunchExecutableUsingInterpreterGuid));
        }
    }
}
