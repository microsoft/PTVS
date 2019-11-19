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
    public class RemoveImportTests {
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

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void FromImport1() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImport1));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImport2() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImport2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImportParens1() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportParens1));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImportParens2() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportParens2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImportParensTrailingComma1() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportParensTrailingComma1));
        }

        [TestMethod, Priority(UITestPriority.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void FromImportParensTrailingComma2() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportParensTrailingComma2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import1() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import1));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import2() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import3() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import3));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import4() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import4));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import5() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import5));
        }

        [TestMethod, Priority(UITestPriority.P2)]
        [TestCategory("Installed")]
        public void Import6() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import6));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ImportComment() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.ImportComment));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImportComment() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportComment));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ImportDup() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.ImportDup));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImportDup() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImportDup));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void Import() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.Import));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FromImport() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FromImport));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void FutureImport() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.FutureImport));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void LocalScopeDontRemoveGlobal() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.LocalScopeDontRemoveGlobal));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void LocalScopeOnly() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.LocalScopeOnly));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ImportTrailingWhitespace() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.ImportTrailingWhitespace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ClosureReference() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.ClosureReference));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void NameMangledUnmangled() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.NameMangledUnmangled));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void NameMangledMangled() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.NameMangledMangled));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void EmptyFuncDef1() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.EmptyFuncDef1));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void EmptyFuncDef2() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.EmptyFuncDef2));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void EmptyFuncDefWhitespace() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.EmptyFuncDefWhitespace));
        }

        [TestMethod, Priority(UITestPriority.P0)]
        [TestCategory("Installed")]
        public void ImportStar() {
            _vs.RunTest(nameof(PythonToolsUITests.RemoveImportTests.ImportStar));
        }
    }
}
