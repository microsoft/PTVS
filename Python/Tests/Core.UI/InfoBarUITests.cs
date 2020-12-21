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

using System;
using System.IO;
using System.Text;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestUtilities;
using TestUtilities.Python;
using TestUtilities.UI.Python;
using Path = System.IO.Path;

namespace PythonToolsUITests {
    public class InfoBarUITests {
        public void VirtualEnvProjectPrompt(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarReqsTxt\InfoBarReqsTxt.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertCreateVirtualEnvInfoBarVisibility(app, true);
            }
        }

        public void VirtualEnvProjectNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarReqsTxt\InfoBarReqsTxt.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenSolution(app, sln);
                AssertCreateVirtualEnvInfoBarVisibility(app, false);
            }
        }

        public void VirtualEnvProjectNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarReqsTxt\InfoBarReqsTxtSuppressed.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertCreateVirtualEnvInfoBarVisibility(app, false);
            }
        }

        public void VirtualEnvWorkspacePrompt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateVirtualEnvInfoBarVisibility(app, true);
            }
        }

        public void VirtualEnvWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateVirtualEnvInfoBarVisibility(app, false);
            }
        }

        public void VirtualEnvWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle",
                settingsFileContents: "{ \"SuppressEnvironmentCreationPrompt\": true }"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateVirtualEnvInfoBarVisibility(app, false);
            }
        }

        public void VirtualEnvWorkspaceNoPromptNoReqsTxt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder();

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateVirtualEnvInfoBarVisibility(app, false);
            }
        }

        public void CondaEnvProjectPrompt(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYml.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertCreateCondaEnvInfoBarVisibility(app, true);
            }
        }

        public void CondaEnvProjectNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYml.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenSolution(app, sln);
                AssertCreateCondaEnvInfoBarVisibility(app, false);
            }
        }

        public void CondaEnvProjectNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYmlSuppressed.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertCreateCondaEnvInfoBarVisibility(app, false);
            }
        }

        public void CondaEnvWorkspacePrompt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateCondaEnvInfoBarVisibility(app, true);
            }
        }

        public void CondaEnvWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateCondaEnvInfoBarVisibility(app, false);
            }
        }

        public void CondaEnvWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1",
                settingsFileContents: "{ \"SuppressEnvironmentCreationPrompt\": true }"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateCondaEnvInfoBarVisibility(app, false);
            }
        }

        public void CondaEnvWorkspaceNoPromptNoEnvYml(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder();

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertCreateCondaEnvInfoBarVisibility(app, false);
            }
        }

        public void InstallPackagesProjectPrompt(PythonVisualStudioApp app) {
            // The project has a reference to 3.7 32-bit 'env' virtual env
            // requirements.txt lists "bottle" and "cookies"
            // Install only "bottle" and we should see prompt
            var basePython = PythonPaths.Python37;
            basePython.AssertInstalled();

            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarMissingPackages\InfoBarMissingPackages.sln");

            basePython.CreateVirtualEnv(
                Path.Combine(Path.GetDirectoryName(sln), "env"),
                new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenSolution(app, sln);
                AssertInstallPackagesInfoBarVisibility(app, true);
            }
        }

        public void InstallPackagesProjectNoPromptNoMissingPackage(PythonVisualStudioApp app) {
            // The project has a reference to 3.7 32-bit 'env' virtual env
            // requirements.txt lists "bottle" and "cookies"
            // Install both, we should not see prompt
            var basePython = PythonPaths.Python37;
            basePython.AssertInstalled();

            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarMissingPackages\InfoBarMissingPackages.sln");

            basePython.CreateVirtualEnv(
                Path.Combine(Path.GetDirectoryName(sln), "env"),
                new[] { "bottle", "cookies" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenSolution(app, sln);
                AssertInstallPackagesInfoBarVisibility(app, false);
            }
        }

        public void InstallPackagesWorkspacePrompt(PythonVisualStudioApp app) {
            var basePython = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            basePython.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: basePython,
                virtualEnvPackages: new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInstallPackagesInfoBarVisibility(app, true);
            }
        }

        public void InstallPackagesWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var basePython = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            basePython.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: basePython,
                virtualEnvPackages: new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: false)) {
                OpenFolder(app, workspaceFolder);
                AssertInstallPackagesInfoBarVisibility(app, false);
            }
        }

        public void InstallPackagesWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var basePython = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            basePython.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\", \"SuppressPackageInstallationPrompt\": true }",
                virtualEnvBase: basePython,
                virtualEnvPackages: new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInstallPackagesInfoBarVisibility(app, false);
            }
        }

        public void InstallPackagesWorkspaceNoPromptNoMissingPackage(PythonVisualStudioApp app) {
            var basePython = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            basePython.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: basePython,
                virtualEnvPackages: new[] { "bottle", "cookies" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInstallPackagesInfoBarVisibility(app, false);
            }
        }

        public void InstallPackagesWorkspaceNoPromptGlobalDefaultEnv(PythonVisualStudioApp app) {
            var globalDefault = PythonPaths.Python37_x64 ?? PythonPaths.Python37;
            globalDefault.AssertInstalled();

            // We never prompt when using global default environment
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "packagethatdoesntexist"
            );

            using (app.SelectDefaultInterpreter(globalDefault))
            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInstallPackagesInfoBarVisibility(app, false);
            }
        }

        private static void OpenFolder(PythonVisualStudioApp app, string workspaceFolder) {
            app.OpenFolder(workspaceFolder);
            app.OpenDocument(Path.Combine(workspaceFolder, "main.py"));
        }

        private static void OpenSolution(PythonVisualStudioApp app, string solutionFilePath) {
            app.OpenProject(solutionFilePath);
            app.OpenDocument(Path.Combine(Path.GetDirectoryName(solutionFilePath), "main.py"));
        }

        private static string CreateWorkspaceFolder(
            string reqsFileContents = null,
            string envFileContents = null,
            string settingsFileContents = null,
            PythonVersion virtualEnvBase = null,
            string[] virtualEnvPackages = null
        ) {
            var workspaceFolder = TestData.GetTempPath();

            File.WriteAllText(
                Path.Combine(workspaceFolder, "main.py"),
                string.Empty,
                Encoding.UTF8
            );

            if (reqsFileContents != null) {
                File.WriteAllText(
                    Path.Combine(workspaceFolder, "requirements.txt"),
                    reqsFileContents,
                    Encoding.UTF8
                );
            }

            if (envFileContents != null) {
                File.WriteAllText(
                    Path.Combine(workspaceFolder, "environment.yml"),
                    envFileContents,
                    Encoding.UTF8
                );
            }

            if (settingsFileContents != null) {
                File.WriteAllText(
                    Path.Combine(workspaceFolder, "PythonSettings.json"),
                    settingsFileContents,
                    Encoding.UTF8
                );
            }

            if (virtualEnvBase != null) {
                virtualEnvBase.CreateVirtualEnv(Path.Combine(workspaceFolder, "env"), virtualEnvPackages);
            }

            return workspaceFolder;
        }

        private static PythonCreateVirtualEnvInfoBar AssertCreateVirtualEnvInfoBarVisibility(PythonVisualStudioApp app, bool expectedVisible) {
            var infoBar = app.FindCreateVirtualEnvInfoBar(TimeSpan.FromSeconds(5));
            if (expectedVisible) {
                Assert.IsNotNull(infoBar, "Expected info bar to be visible");
            } else {
                Assert.IsNull(infoBar, "Expected info bar to NOT be visible");
            }

            return infoBar;
        }

        private static PythonCreateCondaEnvInfoBar AssertCreateCondaEnvInfoBarVisibility(PythonVisualStudioApp app, bool expectedVisible) {
            var infoBar = app.FindCreateCondaEnvInfoBar(TimeSpan.FromSeconds(5));
            if (expectedVisible) {
                Assert.IsNotNull(infoBar, "Expected info bar to be visible");
            } else {
                Assert.IsNull(infoBar, "Expected info bar to NOT be visible");
            }

            return infoBar;
        }

        private static PythonInstallPackagesInfoBar AssertInstallPackagesInfoBarVisibility(PythonVisualStudioApp app, bool expectedVisible) {
            var infoBar = app.FindInstallPackagesInfoBar(TimeSpan.FromSeconds(5));
            if (expectedVisible) {
                Assert.IsNotNull(infoBar, "Expected info bar to be visible");
            } else {
                Assert.IsNull(infoBar, "Expected info bar to NOT be visible");
            }

            return infoBar;
        }
    }
}
