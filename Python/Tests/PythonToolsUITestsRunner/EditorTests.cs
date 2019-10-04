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
    public class EditorTests {
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

        //[TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        //[TestCategory("Installed")]
        //public void CompletionsCaseSensitive() {
        //    _vs.RunTest(nameof(PythonToolsUITests.EditorTests.CompletionsCaseSensitive));
        //}

        //[TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        //[TestCategory("Installed")]
        //public void TypingTest() {
        //    _vs.RunTest(nameof(PythonToolsUITests.EditorTests.TypingTest));
        //}

        //[TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        //[TestCategory("Installed")]
        //public void CompletionTests() {
        //    _vs.RunTest(nameof(PythonToolsUITests.EditorTests.CompletionTests));
        //}

        [Ignore] // https://github.com/microsoft/PTVS/issues/5892
        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void OpenInvalidUnicodeFile() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.OpenInvalidUnicodeFile));
        }

        [Ignore] // https://github.com/microsoft/PTVS/issues/5891
        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void IndentationInconsistencyWarning() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyWarning));
        }

        [Ignore] // https://github.com/microsoft/PTVS/issues/5891
        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void IndentationInconsistencyError() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyError));
        }

        [Ignore] // https://github.com/microsoft/PTVS/issues/5891
        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void IndentationInconsistencyIgnore() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.IndentationInconsistencyIgnore));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST), TestCategory("Squiggle")]
        [TestCategory("Installed")]
        public void ImportPresent() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportPresent));
        }

        [TestMethod, Priority(0), TestCategory("Squiggle")]
        [TestCategory("Installed")]
        public void ImportSelf() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportSelf));
        }

        [TestMethod, Priority(0), TestCategory("Squiggle")]
        [TestCategory("Installed")]
        public void ImportMissing() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportMissing));
        }

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST), TestCategory("Squiggle")]
        [TestCategory("Installed")]
        public void ImportMissingThenAddFile() {
            _vs.RunTest(nameof(PythonToolsUITests.EditorTests.ImportMissingThenAddFile));
        }
    }
}