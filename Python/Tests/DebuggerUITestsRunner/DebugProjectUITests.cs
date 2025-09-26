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

namespace DebuggerUITestsRunner {
    public abstract class DebugProjectUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.DebuggerUITests",
            // Remote class name
            $"DebuggerUITests.{nameof(DebugProjectUITests)}"
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        protected abstract bool UseVsCodeDebugger { get; }

        protected abstract string Interpreter { get; }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DebugPythonProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectSubFolderStartupFileSysPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProjectSubFolderStartupFileSysPath), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectWithClearingPythonPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProjectWithClearingPythonPath), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPythonProjectWithoutClearingPythonPath() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonProjectWithoutClearingPythonPath), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreter() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonCustomInterpreter), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void DebugPythonCustomInterpreterMissing() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.DebugPythonCustomInterpreterMissing), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void PendingBreakPointLocation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.PendingBreakPointLocation), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void BoundBreakpoint() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BoundBreakpoint), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Step() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Step3() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step3), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Step5() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Step5), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StepMultiProc() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StepMultiProc), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SetNextLine() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SetNextLine), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void TerminateProcess() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.TerminateProcess), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void EnumModules() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.EnumModules), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void MainThread() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.MainThread), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ExpressionEvaluation() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.ExpressionEvaluation), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SimpleException() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleException), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SimpleException2() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleException2), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void SimpleExceptionUnhandled() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.SimpleExceptionUnhandled), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ExceptionInImportLibNotReported() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.ExceptionInImportLibNotReported), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Breakpoints() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.Breakpoints), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BreakpointsDisable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BreakpointsDisable), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void BreakpointsDisableReenable() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.BreakpointsDisableReenable), UseVsCodeDebugger, Interpreter);
        }

        [Ignore] // see PythonProjectNode's ShouldWarnOnLaunch disabled
        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void LaunchWithErrorsDontRun() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.LaunchWithErrorsDontRun), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNoProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNoProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNotInProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNotInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNotInProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingInProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void StartWithDebuggingSubfolderInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingSubfolderInProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingInProject() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingInProject), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartWithDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithDebuggingNoScript), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void StartWithoutDebuggingNoScript() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.StartWithoutDebuggingNoScript), UseVsCodeDebugger, Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void WebProjectLauncherNoStartupFile() {
            _vs.RunTest(nameof(DebuggerUITests.DebugProjectUITests.WebProjectLauncherNoStartupFile), UseVsCodeDebugger, Interpreter);
        }
    }

    [TestClass]
    public class DebugProjectUITestsLegacyPtvsd : DebugProjectUITests {
        protected override bool UseVsCodeDebugger => false;
        protected override string Interpreter => ""; // Use the existing global default
    }

    public abstract class DebugProjectUITestsDebugPy : DebugProjectUITests {
        protected override bool UseVsCodeDebugger => true;
    }

    [TestClass]
    public class DebugProjectUITestsDebugPy27 : DebugProjectUITestsDebugPy {
        protected override string Interpreter => "Python27|Python27_x64";
    }

    [TestClass]
    public class DebugProjectUITestsDebugPy37 : DebugProjectUITestsDebugPy {
        protected override string Interpreter => "Python314|Python314_x64";
    }
}
