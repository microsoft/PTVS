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
using System.Windows.Automation;
using Microsoft.PythonTools;
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
                AssertInfoBarVisibility(app, true, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvProjectNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarReqsTxt\InfoBarReqsTxt.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvProjectNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarReqsTxt\InfoBarReqsTxtSuppressed.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvWorkspacePrompt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, true, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle",
                settingsFileContents: "{ \"SuppressEnvironmentCreationPrompt\": true }"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void VirtualEnvWorkspaceNoPromptNoReqsTxt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder();

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarCreateVirtualEnvAction);
            }
        }

        public void CondaEnvProjectPrompt(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYml.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, true, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvProjectNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYml.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, false, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvProjectNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarEnvYml\InfoBarEnvYmlSuppressed.sln");

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, false, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvWorkspacePrompt(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, true, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: false)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder(
                envFileContents: "name: test\ndependencies:\n- cookies==2.2.1",
                settingsFileContents: "{ \"SuppressEnvironmentCreationPrompt\": true }"
            );

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.CondaInfoBarCreateAction);
            }
        }

        public void CondaEnvWorkspaceNoPromptNoEnvYml(PythonVisualStudioApp app) {
            var workspaceFolder = CreateWorkspaceFolder();

            using (new PythonOptionsSetter(app.Dte, promptForEnvCreate: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.CondaInfoBarCreateAction);
            }
        }

        public void InstallPackagesProjectPrompt(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarMissingPackages\InfoBarMissingPackages.sln");

            // The project has a reference to 'env' subfolder, which we need to create
            // requirements.txt lists "bottle" and "cookies"
            // Install only "bottle" and we should see prompt
            CreateVirtualEnvironmentWithPackages(
                python,
                Path.Combine(Path.GetDirectoryName(sln), "env"),
                new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, true, Strings.RequirementsTxtInfoBarInstallPackagesAction);
            }
        }

        public void InstallPackagesProjectNoPromptNoMissingPackage(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var sln = app.CopyProjectForTest(@"TestData\InfoBar\InfoBarMissingPackages\InfoBarMissingPackages.sln");

            // The project has a reference to 'env' subfolder, which we need to create
            // requirements.txt lists "bottle" and "cookies"
            // Install both, we should not see prompt
            CreateVirtualEnvironmentWithPackages(
                python,
                Path.Combine(Path.GetDirectoryName(sln), "env"),
                new[] { "bottle", "cookies" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenSolution(app, sln);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarInstallPackagesAction);
            }
        }

        public void InstallPackagesWorkspacePrompt(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: python,
                virtualEnvPackages: new[] { "bottle" }
            );

            OpenFolder(app, workspaceFolder);

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                AssertInfoBarVisibility(app, true, Strings.RequirementsTxtInfoBarInstallPackagesAction);
            }
        }

        public void InstallPackagesWorkspaceNoPromptGlobalSuppress(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: python,
                virtualEnvPackages: new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: false)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarInstallPackagesAction);
            }
        }

        public void InstallPackagesWorkspaceNoPromptLocalSuppress(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\", \"SuppressPackageInstallationPrompt\": true }",
                virtualEnvBase: python,
                virtualEnvPackages: new[] { "bottle" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarInstallPackagesAction);
            }
        }

        public void InstallPackagesWorkspaceNoPromptNoMissingPackage(PythonVisualStudioApp app) {
            var python = PythonPaths.Python37;
            python.AssertInstalled();

            var workspaceFolder = CreateWorkspaceFolder(
                reqsFileContents: "bottle\ncookies",
                settingsFileContents: "{ \"Interpreter\": \"env\\\\scripts\\\\python.exe\" }",
                virtualEnvBase: python,
                virtualEnvPackages: new[] { "bottle", "cookies" }
            );

            using (new PythonOptionsSetter(app.Dte, promptForPackageInstallation: true)) {
                OpenFolder(app, workspaceFolder);
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarInstallPackagesAction);
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
                AssertInfoBarVisibility(app, false, Strings.RequirementsTxtInfoBarInstallPackagesAction);
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
                CreateVirtualEnvironmentWithPackages(
                    virtualEnvBase,
                    Path.Combine(workspaceFolder, "env"),
                    virtualEnvPackages
                );
            }

            return workspaceFolder;
        }

        private static void CreateVirtualEnvironmentWithPackages(PythonVersion baseEnv, string envFolderPath, string[] packages) {
            EnvironmentUITests.CreateVirtualEnvironment(baseEnv, envFolderPath);

            var envPythonExePath = Path.Combine(envFolderPath, "scripts", "python.exe");
            foreach (var package in packages.MaybeEnumerate()) {
                using (var output = ProcessOutput.RunHiddenAndCapture(envPythonExePath, "-m", "pip", "install", package)) {
                    Assert.IsTrue(output.Wait(TimeSpan.FromSeconds(30)));
                    Assert.AreEqual(0, output.ExitCode);
                }
            }
        }

        private static void AssertInfoBarVisibility(PythonVisualStudioApp app, bool expectedVisible, string hyperLinkName) {
            var infoBar = app.FindFirstInfoBar(
                new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, hyperLinkName),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)),
                TimeSpan.FromSeconds(5)
            );

            if (expectedVisible) {
                Assert.IsNotNull(infoBar, $"Expected info bar with hyperlink '{hyperLinkName}' to be visible");
            } else {
                Assert.IsNull(infoBar, $"Expected info bar with hyperlink '{hyperLinkName}' to NOT be visible");
            }
        }
    }
}
