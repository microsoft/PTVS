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
// MERCHANTABLITY OR NON-INFRINGEMENT.
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

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void DefaultInterpreterSelected() {
            _vs.RunTest(nameof(PUIT.DefaultInterpreterSelected));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void StartupProjectSelected() {
            _vs.RunTest(nameof(PUIT.StartupProjectSelected));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void NewProfilingSession() {
            _vs.RunTest(nameof(PUIT.NewProfilingSession));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void DeleteMultipleSessions() {
            _vs.RunTest(nameof(PUIT.DeleteMultipleSessions));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void NewProfilingSessionOpenSolution() {
            _vs.RunTest(nameof(PUIT.NewProfilingSessionOpenSolution));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchPythonProfilingWizard() {
            _vs.RunTest(nameof(PUIT.LaunchPythonProfilingWizard));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProject() {
            _vs.RunTest(nameof(PUIT.LaunchProject));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProjectWithSpaceInFilename() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithSpaceInFilename));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProjectWithSearchPath() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithSearchPath));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProjectWithPythonPathSet() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithPythonPathSet));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProjectWithPythonPathClear() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithPythonPathClear));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchProjectWithEnvironment() {
            _vs.RunTest(nameof(PUIT.LaunchProjectWithEnvironment));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestSaveDirtySession() {
            _vs.RunTest(nameof(PUIT.TestSaveDirtySession));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestDeleteReport() {
            _vs.RunTest(nameof(PUIT.TestDeleteReport));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestCompareReports() {
            _vs.RunTest(nameof(PUIT.TestCompareReports));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestRemoveReport() {
            _vs.RunTest(nameof(PUIT.TestRemoveReport));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestOpenReport() {
            _vs.RunTest(nameof(PUIT.TestOpenReport));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestOpenReportCtxMenu() {
            _vs.RunTest(nameof(PUIT.TestOpenReportCtxMenu));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTargetPropertiesForProject() {
            _vs.RunTest(nameof(PUIT.TestTargetPropertiesForProject));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTargetPropertiesForInterpreter() {
            _vs.RunTest(nameof(PUIT.TestTargetPropertiesForInterpreter));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestTargetPropertiesForExecutable() {
            _vs.RunTest(nameof(PUIT.TestTargetPropertiesForExecutable));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void TestStopProfiling() {
            _vs.RunTest(nameof(PUIT.TestStopProfiling));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void MultipleTargets() {
            _vs.RunTest(nameof(PUIT.MultipleTargets));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void MultipleTargetsWithProjectHome() {
            _vs.RunTest(nameof(PUIT.MultipleTargetsWithProjectHome));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void MultipleReports() {
            _vs.RunTest(nameof(PUIT.MultipleReports));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchExecutable() {
            _vs.RunTest(nameof(PUIT.LaunchExecutable));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ClassProfile() {
            _vs.RunTest(nameof(PUIT.ClassProfile));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void OldClassProfile() {
            _vs.RunTest(nameof(PUIT.OldClassProfile));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void DerivedProfile() {
            _vs.RunTest(nameof(PUIT.DerivedProfile));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void Pystone() {
            _vs.RunTest(nameof(PUIT.Pystone));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython26() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython26));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython27() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython27));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython27x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython27x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython31() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython31));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython32() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython32));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython32x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython32x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython33() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython33));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython33x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython33x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython34() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython34));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython34x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython34x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython35() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython35));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython35x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython35x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython36() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython36));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython36x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython36x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython37() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython37));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void BuiltinsProfilePython37x64() {
            _vs.RunTest(nameof(PUIT.BuiltinsProfilePython37x64));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchExecutableUsingInterpreterGuid() {
            _vs.RunTest(nameof(PUIT.LaunchExecutableUsingInterpreterGuid));
        }
    }
}
