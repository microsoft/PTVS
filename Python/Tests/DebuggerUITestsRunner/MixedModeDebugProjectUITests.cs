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
using TestUtilities;

namespace DebuggerUITestsRunner {
    [TestClass, Ignore]
    public abstract class MixedModeDebugProjectUITests {
        #region UI test boilerplate
        public VsTestInvoker _vs => new VsTestInvoker(
            VsTestContext.Instance,
            // Remote container (DLL) name
            "Microsoft.PythonTools.Tests.DebuggerUITests",
            // Remote class name
            $"DebuggerUITests.{nameof(MixedModeDebugProjectUITests)}"
        );

        public TestContext TestContext { get; set; }

        protected abstract string Interpreter { get; }

        [TestInitialize]
        public void TestInitialize() => VsTestContext.Instance.TestInitialize(TestContext.DeploymentDirectory);
        [TestCleanup]
        public void TestCleanup() => VsTestContext.Instance.TestCleanup();
        [ClassCleanup]
        public static void ClassCleanup() => VsTestContext.Instance.Dispose();
        #endregion

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DebugPurePythonProject() {
            _vs.RunTest(nameof(DebuggerUITests.MixedModeDebugProjectUITests.DebugPurePythonProject), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DebugMixedModePythonProject() {
            _vs.RunTest(nameof(DebuggerUITests.MixedModeDebugProjectUITests.DebugMixedModePythonProject), Interpreter);
        }
    }

    [TestClass]
    public class MixedModeDebugProjectUITests27_32 : MixedModeDebugProjectUITests {
        protected override string Interpreter => "Python27";
    }

    [TestClass]
    public class MixedModeDebugProjectUITests27_64 : MixedModeDebugProjectUITests {
        protected override string Interpreter => "Python27_x64";
    }

    [TestClass]
    public class MixedModeDebugProjectUITests3x_32 : MixedModeDebugProjectUITests {
        protected override string Interpreter => "Python314";
    }

    [TestClass]
    public class MixedModeDebugProjectUITests3x_64 : MixedModeDebugProjectUITests {
        protected override string Interpreter => "Python314_x64";
    }
}
