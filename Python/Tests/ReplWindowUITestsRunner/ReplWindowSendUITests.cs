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

namespace ReplWindowUITestsRunner
{
    [TestClass]
    public class ReplWindowSendUITests
    {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.ReplWindowUITests",
            // Remote class name
            $"ReplWindowUITests.{GetType().Name}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveLineByLine()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveLineByLine));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveCellByCell()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveCellByCell));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveDelayed()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveDelayed));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveSelection()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveSelection));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveSelectionNoWait()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveSelectionNoWait));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveOutputSelected()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveOutputSelected));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveWorkspaceInterpreter()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveWorkspaceInterpreter));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveWorkspacePackage()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveWorkspacePackage));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SendToInteractiveWorkspaceSearchPathPackage()
        {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowSendUITests.SendToInteractiveWorkspaceSearchPathPackage));
        }
    }
}
