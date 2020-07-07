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
    public abstract class ReplWindowBasicUITests {
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

        #region Basic tests

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void RegressionImportSysBackspace() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.RegressionImportSysBackspace), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void RegressionImportMultipleModules() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.RegressionImportMultipleModules), Interpreter);
        }

        //[Ignore] // https://github.com/Microsoft/PTVS/issues/2762
        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void SyntaxHighlightingRaiseException() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.SyntaxHighlightingRaiseException), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void PrintWithParens() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.PrintWithParens), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void UndeletableIndent() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.UndeletableIndent), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void InlineImage() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.InlineImage), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void ImportCompletions() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.ImportCompletions), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void Comments() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.Comments), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void NoSnippets() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.NoSnippets), Interpreter);
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void TestPydocRedirected() {
            _vs.RunTest(nameof(ReplWindowUITests.ReplWindowUITests.TestPydocRedirected), Interpreter);
        }

        #endregion
    }

    [TestClass]
    public class ReplWindowBasicUITests27 : ReplWindowBasicUITests {
        protected override string Interpreter => "Python27|Python27_x64";
    }

    [TestClass]
    public class ReplWindowBasicUITestsIPy27 : ReplWindowBasicUITests {
        protected override string Interpreter => "IronPython27|IronPython27_x64";
    }

    [TestClass]
    public class ReplWindowBasicUITests36 : ReplWindowBasicUITests {
        protected override string Interpreter => "Python36|Python36_x64";
    }
}
