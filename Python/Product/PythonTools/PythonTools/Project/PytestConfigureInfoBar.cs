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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OutputWindowRedirector = Microsoft.VisualStudioTools.Infrastructure.OutputWindowRedirector;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    /*
     * Bugs: 
     *      If an info bar is shown and then a user selects pytest from project settings->Tests->pytest, then the info bar will remain
     *      If the user adds a config file after the project is loaded, the info bar will not be displayed
     */
    internal abstract class PyTestInfoBar : PythonInfoBar {
        private PyTestInfoBarData InfoBarData { get; set; }

        protected PyTestInfoBar(IServiceProvider site) : base(site) {

        }

        protected async Task CheckAsync(PyTestInfoBarData infoBarData) {
            InfoBarData = infoBarData;
            if (IsCreated || InfoBarData.IsGloballySuppressed || InfoBarData.InfoBarSuppressed) {
                return;
            }

            /*
            There are 4 possible cases. 
            #1: Config file is     present, pytest is     installed, pytest is not enabled -> "Enable Pytest"
            #2: Config file is     present, pytest is not installed, pytest is not enabled -> "Install and enable Pytest"
            #3: Config file is     present, pytest is not installed, pytest is     enabled -> "Install Pytest"
            #4: Config file is not present, pytest is not installed, pytest is     enabled -> "Install Pytest"
             */
            bool validConfigFile = File.Exists(infoBarData.PyTestConfigFilePath);

            string infoBarMessage;
            InfoBarHyperlink acceptActionItem;

            if (validConfigFile &&
                InfoBarData.TestFramework != TestFrameworkType.Pytest
                && await IsPyTestInstalled()
            ) {
                //Case #1. "Enable Pytest"
                infoBarMessage = Strings.PyTestInstalledConfigurationFileFound.FormatUI(InfoBarData.Caption, InfoBarData.Context.ToLower());
                acceptActionItem = new InfoBarHyperlink(Strings.PyTestEnableInfoBarAction, (Action)EnablePytestAction);

            } else if (validConfigFile &&
                InfoBarData.TestFramework != TestFrameworkType.Pytest
                && !(await IsPyTestInstalled())
            ) {
                //Case #2. "Install and enable Pytest"
                infoBarMessage = Strings.PyTestNotInstalledConfigurationFileFound.FormatUI(InfoBarData.Caption, InfoBarData.Context.ToLower());
                acceptActionItem = new InfoBarHyperlink(
                    Strings.PyTestInstallAndEnableInfoBarAction,
                    (Action)InstallAndEnablePytestAction
                );

            } else if (InfoBarData.TestFramework == TestFrameworkType.Pytest && !(await IsPyTestInstalled())) {
                //Case #3 and #4. "Install Pytest"
                infoBarMessage = Strings.PyTestNotInstalled.FormatUI(InfoBarData.Caption, InfoBarData.Context.ToLower());
                acceptActionItem = new InfoBarHyperlink(Strings.PyTestInstallInfoBarAction,
                    (Action)InstallPytestAction
                );

            } else {
                return;
            }

            ShowInfoBar(infoBarMessage, acceptActionItem);
        }

        private void ShowInfoBar(string infoBarMessage, InfoBarHyperlink acceptActionItem) {
            LogEvent(ConfigurePytestInfoBarActions.Prompt);
            var messages = new List<IVsInfoBarTextSpan>();
            var acceptAction = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(infoBarMessage));
            acceptAction.Add(acceptActionItem);
            acceptAction.Add(new InfoBarHyperlink(Strings.PyTestIgnoreInfoBarAction, (Action)IgnoreAction));

            Create(new InfoBarModel(messages, acceptAction, KnownMonikers.StatusInformation));
        }

        private void InstallPytestAction() {
            InstallPytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task InstallPytestActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.Install);
            var result = await InstallPyTestAsync();
            if (!result) {
                var generalOutputWindow = OutputWindowRedirector.GetGeneral(Site);
                generalOutputWindow.ShowAndActivate();
            }

            Close();
        }

        private void EnablePytestAction() {
            EnablePytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task EnablePytestActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.Enable);
            await SetPropertyAsync(PythonConstants.TestFrameworkSetting, "Pytest");
            Close();
        }

        private void InstallAndEnablePytestAction() {
            InstallAndEnablePytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task InstallAndEnablePytestActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.EnableAndInstall);
            if (!await InstallPyTestAsync()) {
                var generalOutputWindow = OutputWindowRedirector.GetGeneral(Site);
                generalOutputWindow.ShowAndActivate();
            }

            await SetPropertyAsync(PythonConstants.TestFrameworkSetting, "Pytest");
            Close();
        }

        private void IgnoreAction() {
            IgnoreActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task IgnoreActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.Ignore);
            await SetPropertyAsync(PythonConstants.SuppressPytestConfigPrompt, true.ToString());
            Close();
        }

        private async Task<bool> InstallPyTestAsync() {
            var packageManagers = InfoBarData.InterpreterOptionsService.GetPackageManagers(InfoBarData.InterpreterFactory);

            foreach (var packageManager in packageManagers) {
                bool pytestInstalled = await packageManager.InstallAsync(
                    new PackageSpec("pytest"),
                    new VsPackageManagerUI(Site),
                    CancellationToken.None
                );

                if (pytestInstalled) {
                    return true;
                }
            }

            return false;
        }

        private async Task<bool> IsPyTestInstalled() {
            var packageManagers = InfoBarData.InterpreterOptionsService.GetPackageManagers(InfoBarData.InterpreterFactory);
            var getPackageManagersTask = packageManagers.Select(
                packageManager => packageManager.GetInstalledPackagesAsync(CancellationToken.None)
            ).ToList();

            var packagesList = await Task.WhenAll(getPackageManagersTask);
            return packagesList.Any(packages => packages.Any(package =>
                String.Equals(package.Name, nameof(TestFrameworkType.Pytest), StringComparison.OrdinalIgnoreCase))
            );
        }

        private void LogEvent(string action) {
            Logger?.LogEvent(
                PythonLogEvent.PyTestInfoBar,
                new ConfigurePytestInfoBarInfo() {
                    Action = action,
                    Context = InfoBarData.Context
                }
            );
        }

        protected abstract Task SetPropertyAsync(string propertyName, string propertyValue);

        protected class PyTestInfoBarData {
            public IInterpreterOptionsService InterpreterOptionsService { get; set; }
            public IPythonInterpreterFactory InterpreterFactory { get; set; }

            public bool IsGloballySuppressed { get; set; }
            public bool InfoBarSuppressed { get; set; }

            public string Caption { get; set; }
            public string Context { get; set; }
            public string PyTestConfigFilePath { get; set; }
            public TestFrameworkType TestFramework { get; set; }

            public static TestFrameworkType GetTestFramework(string propertyValue) {
                if (Enum.TryParse(
                    propertyValue,
                    ignoreCase: true,
                    out TestFrameworkType parsedFramework)
                ) {
                    return parsedFramework;
                }

                return TestFrameworkType.None;
            }
        }
    }

    internal sealed class PyTestProjectInfoBar : PyTestInfoBar {
        private PythonProjectNode Project { get; }

        public PyTestProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode) : base(site) {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public override async Task CheckAsync() {
            var infoBarData = new PyTestInfoBarData {
                InterpreterOptionsService = Site.GetPythonToolsService().InterpreterOptionsService,
                InterpreterFactory = Project.ActiveInterpreter,
                IsGloballySuppressed = !Site.GetPythonToolsService().GeneralOptions.PromptForPyTestInstallOrEnable,
                InfoBarSuppressed = Project.GetProjectProperty(PythonConstants.SuppressPytestConfigPrompt).IsTrue(),
                Caption = Project.Caption,
                Context = InfoBarContexts.Project,
                PyTestConfigFilePath = Project.GetPyTestConfigFilePath(),
                TestFramework = PyTestInfoBarData.GetTestFramework(
                    Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false)
                )
            };

            await CheckAsync(infoBarData);
        }

        protected override Task SetPropertyAsync(string propertyName, string propertyValue) {
            Project.SetProjectProperty(propertyName, propertyValue);//but this function isn't async. 
            return Task.CompletedTask;
        }
    }

    internal sealed class PyTestWorkspaceInfoBar : PyTestInfoBar {
        private IPythonWorkspaceContext WorkspaceContext { get; }

        public PyTestWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext pythonWorkspaceContext) : base(site) {
            WorkspaceContext = pythonWorkspaceContext ?? throw new ArgumentNullException(nameof(pythonWorkspaceContext));
        }

        public override async Task CheckAsync() {
            var infoBarData = new PyTestInfoBarData {
                InterpreterOptionsService = Site.GetPythonToolsService().InterpreterOptionsService,
                InterpreterFactory = WorkspaceContext.CurrentFactory,
                IsGloballySuppressed = !Site.GetPythonToolsService().GeneralOptions.PromptForPyTestInstallOrEnable,
                InfoBarSuppressed = WorkspaceContext.GetBoolProperty(PythonConstants.SuppressPytestConfigPrompt) ?? false,
                Caption = WorkspaceContext.WorkspaceName,
                Context = InfoBarContexts.Workspace,
                PyTestConfigFilePath = GetPyTestConfigFilePath(),
                TestFramework = PyTestInfoBarData.GetTestFramework(WorkspaceContext.GetStringProperty(PythonConstants.TestFrameworkSetting)),
            };

            await CheckAsync(infoBarData);
        }

        private string GetPyTestConfigFilePath() {
            var fileName = PythonConstants.PyTestFrameworkConfigFiles
                .FirstOrDefault(x => File.Exists(PathUtils.GetAbsoluteFilePath(WorkspaceContext.Location, x)));

            return string.IsNullOrEmpty(fileName) ? "" : Path.Combine(WorkspaceContext.Location, fileName);
        }

        protected override Task SetPropertyAsync(string propertyName, string propertyValue) {
            return WorkspaceContext.SetPropertyAsync(propertyName, propertyValue);
        }

    }
}
