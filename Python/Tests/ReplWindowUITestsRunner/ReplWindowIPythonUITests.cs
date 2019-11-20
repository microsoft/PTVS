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

namespace ReplWindowUITestsRunner {
    [TestClass, Ignore]
    public abstract class ReplWindowIPythonUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.ReplWindowUITests",
            // Remote class name
            $"ReplWindowUITests.{nameof(ReplWindowUITests)}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        protected abstract string Interpreter { get; }

        #region IPython tests

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonMode() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonMode), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonCtrlBreakAborts() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonCtrlBreakAborts), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonSimpleCompletion() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonSimpleCompletion), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonSimpleSignatureHelp() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonSimpleSignatureHelp), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonInlineGraph() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonInlineGraph), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void IPythonStartInInteractive() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.IPythonStartInInteractive), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void ExecuteInIPythonReplSysArgv() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInIPythonReplSysArgv), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void ExecuteInIPythonReplSysArgvScriptArgs() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ExecuteInIPythonReplSysArgvScriptArgs), Interpreter);
        }

        #endregion
    }

    [TestClass]
    public class ReplWindowIPythonUITests27 : ReplWindowIPythonUITests {
        protected override string Interpreter => "Anaconda27|Anaconda27_x64|Python27|Python27_x64";
    }

    [TestClass]
    public class ReplWindowIPythonUITests36 : ReplWindowIPythonUITests {
        protected override string Interpreter => "Anaconda36|Anaconda36_x64|Python36|Python36_x64";
    }
}
