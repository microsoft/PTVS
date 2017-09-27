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
    public class DebugProject {
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
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.DebugPythonProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectSubFolderStartupFileSysPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.DebugPythonProjectSubFolderStartupFileSysPath));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectWithAndWithoutClearingPythonPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.DebugPythonProjectWithAndWithoutClearingPythonPath));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreter() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.DebugPythonCustomInterpreter));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreterMissing() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.DebugPythonCustomInterpreterMissing));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void PendingBreakPointLocation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.PendingBreakPointLocation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BoundBreakpoint() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.BoundBreakpoint));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.Step));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ShowCallStackOnCodeMap() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.ShowCallStackOnCodeMap));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step3() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.Step3));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Step5() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.Step5));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void SetNextLine() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.SetNextLine));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void TerminateProcess() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.TerminateProcess));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void EnumModules() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.EnumModules));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void MainThread() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.MainThread));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ExpressionEvaluation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.ExpressionEvaluation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleException() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.SimpleException));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleException2() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.SimpleException2));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void SimpleExceptionUnhandled() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.SimpleExceptionUnhandled));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ExceptionInImportLibNotReported() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.ExceptionInImportLibNotReported));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void Breakpoints() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.Breakpoints));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BreakpointsDisable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.BreakpointsDisable));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void BreakpointsDisableReenable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.BreakpointsDisableReenable));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LaunchWithErrorsDontRun() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.LaunchWithErrorsDontRun));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithDebuggingNoProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithoutDebuggingNoProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithDebuggingNotInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithoutDebuggingNotInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithDebuggingInProject));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void StartWithDebuggingSubfolderInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithDebuggingSubfolderInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithoutDebuggingInProject));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithDebuggingNoScript));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProject.StartWithoutDebuggingNoScript));
        }
    }
}
