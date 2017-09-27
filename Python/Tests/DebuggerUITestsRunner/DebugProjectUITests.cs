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

namespace DebuggerUITestsRunner {
    [TestClass]
    public class DebugProjectUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.DebuggerUITests",
            // Remote class name
            $"DebuggerUITests.{GetType().Name}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectSubFolderStartupFileSysPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProjectSubFolderStartupFileSysPath));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectWithAndWithoutClearingPythonPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProjectWithAndWithoutClearingPythonPath));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreter() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonCustomInterpreter));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreterMissing() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonCustomInterpreterMissing));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void PendingBreakPointLocation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.PendingBreakPointLocation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BoundBreakpoint() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BoundBreakpoint));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ShowCallStackOnCodeMap() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.ShowCallStackOnCodeMap));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step3() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step3));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step5() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step5));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void SetNextLine() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SetNextLine));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void TerminateProcess() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.TerminateProcess));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void EnumModules() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.EnumModules));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void MainThread() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.MainThread));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ExpressionEvaluation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.ExpressionEvaluation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleException() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleException));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleException2() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleException2));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleExceptionUnhandled() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleExceptionUnhandled));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ExceptionInImportLibNotReported() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.ExceptionInImportLibNotReported));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Breakpoints() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Breakpoints));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BreakpointsDisable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BreakpointsDisable));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BreakpointsDisableReenable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BreakpointsDisableReenable));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchWithErrorsDontRun() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.LaunchWithErrorsDontRun));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNoProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNoProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNotInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNotInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingInProject));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void StartWithDebuggingSubfolderInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingSubfolderInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNoScript));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNoScript));
        }
    }
}
