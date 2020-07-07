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
    public class TestExplorerTests {
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
        public void RunAllUnittestProject() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.RunAllUnittestProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RunAllUnittestWorkspace() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.RunAllUnittestWorkspace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RunAllPytestProject() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.RunAllPytestProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void RunAllPytestWorkspace() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.RunAllPytestWorkspace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPytestProject() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.DebugPytestProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPytestWorkspace() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.DebugPytestWorkspace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugUnittestProject() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.DebugUnittestProject));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugUnittestWorkspace() {
            _vs.RunTest(nameof(PythonToolsUITests.TestExplorerTests.DebugUnittestWorkspace));
        }
    }
}
