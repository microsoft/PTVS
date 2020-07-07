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

namespace DjangoUITestsRunner {
    [TestClass]
    public class DjangoProjectUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.DjangoUITests",
            // Remote class name
            $"DjangoUITests.{GetType().Name}"
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
        public void NewDjangoProject() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.NewDjangoProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void NewDjangoProjectSafeProjectName() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.NewDjangoProjectSafeProjectName));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void DjangoCollectStaticFilesCommand() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.DjangoCollectStaticFilesCommand));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DjangoShellCommand() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.DjangoShellCommand));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DjangoCommandsNonDjangoApp() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.DjangoCommandsNonDjangoApp));
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void StartNewApp() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.StartNewApp));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartNewAppDuplicateName() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.StartNewAppDuplicateName));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartNewAppSameAsProjectName() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.StartNewAppSameAsProjectName));
        }

        [Ignore] // https://devdiv.visualstudio.com/DevDiv/_workitems?id=433488
        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DebugProjectProperties() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.DebugProjectProperties));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void DjangoProjectWithSubdirectory() {
            _vs.RunTest(nameof(DjangoUITests.DjangoProjectUITests.DjangoProjectWithSubdirectory));
        }
    }
}
