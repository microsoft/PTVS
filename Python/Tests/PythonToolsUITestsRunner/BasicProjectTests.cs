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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestRunnerInterop;

namespace PythonToolsUITestsRunner {
    [TestClass]
    public class BasicProjectTests {
        private static readonly VsTestContext _vs = new VsTestContext(
            "Microsoft.PythonTools.Tests.PythonToolsUITests",
            "PythonToolsUITests.BasicProjectTests",
            null
        );

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void TestInitialize() {
            _vs.TestInitialize(TestContext.DeploymentDirectory);
        }

        [TestCleanup]
        public void TestCleanup() {
            _vs.TestCleanup();
        }

        [ClassCleanup]
        public static void ClassCleanup() {
            _vs.Dispose();
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void TemplateDirectories() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.TemplateDirectories));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void UserProjectFile() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.UserProjectFile));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void SetDefaultInterpreter() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.SetDefaultInterpreter));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LoadPythonProject() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.LoadPythonProject));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void LoadPythonProjectWithNoConfigurations() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.LoadPythonProjectWithNoConfigurations));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void SaveProjectAs() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.SaveProjectAs));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void RenameProjectTest() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.RenameProjectTest));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectAddItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddItem));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectAddFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddFolder));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ProjectAddFolderThroughUI() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectAddFolderThroughUI));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void AddExistingFolder() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddExistingFolder));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void AddExistingFolderWhileDebugging() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddExistingFolderWhileDebugging));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectBuild() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectBuild));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void ProjectRenameAndDeleteItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectRenameAndDeleteItem));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ChangeDefaultInterpreterProjectClosed() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ChangeDefaultInterpreterProjectClosed));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void AddTemplateItem() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AddTemplateItem));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void AutomationProperties() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.AutomationProperties));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectItemAutomation() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectItemAutomation));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void RelativePaths() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.RelativePaths));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void ProjectConfiguration() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.ProjectConfiguration));
        }

        [TestMethod, Priority(1)]
        [TestCategory("Installed")]
        public void DependentNodes() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DependentNodes));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DotNetReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetReferences));
        }

        [TestMethod, Priority(0)]
        [TestCategory("Installed")]
        public void DotNetSearchPathReferences() {
            _vs.RunTest(nameof(PythonToolsUITests.BasicProjectTests.DotNetSearchPathReferences));
        }

    }
}
