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
    [Ignore] // https://github.com/microsoft/PTVS/issues/5888
    [TestClass]
    public class AddImportTests {
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

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DocString() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.DocString));
        }

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void UnicodeDocString() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.UnicodeDocString));
        }

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void DocStringFuture() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.DocStringFuture));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFrom() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFrom));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromSubpackage() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromSubpackage));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ImportWithErrors() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportWithErrors));
        }

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportBuiltinFunction() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportBuiltinFunction));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImport() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImport));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImportAsName() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImportAsName));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImportParens() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImportParens));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImportParensAsName() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImportParensAsName));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImportParensAsNameTrailingComma() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImportParensAsNameTrailingComma));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportFunctionFromExistingFromImportParensTrailingComma() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportFunctionFromExistingFromImportParensTrailingComma));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportPackage() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportPackage));
        }

        [TestMethod, Priority(VsTestContext.P0_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void ImportSubPackage() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.ImportSubPackage));
        }

        [TestMethod, Priority(VsTestContext.P2_FAILING_UI_TEST)]
        [TestCategory("Installed")]
        public void Parameters() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.Parameters));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AssignedWithoutTypeInfo() {
            _vs.RunTest(nameof(PythonToolsUITests.AddImportTests.AssignedWithoutTypeInfo));
        }
    }
}
