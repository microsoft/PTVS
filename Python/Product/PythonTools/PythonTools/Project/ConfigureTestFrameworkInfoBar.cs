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
using Microsoft.VisualStudioTools.Project;
using OutputWindowRedirector = Microsoft.VisualStudioTools.Infrastructure.OutputWindowRedirector;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    /*
     * Bugs: 
     *      If an info bar is shown and then a user changes the configuration such that the info bar should not be shown,
     *          then the info bar will remain
     *      If the user adds a pytest config file or python test file after the project or workspace is loaded,
     *          then the info bar will not be displayed
     *
     *  https://github.com/microsoft/PTVS/issues/5495
     */
    internal abstract class TestFrameworkInfoBar : PythonInfoBar {
        private TestFrameworkInfoBarData _infoBarData;

        protected TestFrameworkInfoBar(IServiceProvider site) : base(site) {

        }

        protected async Task CheckAsync(TestFrameworkInfoBarData frameworkInfoBarData) {
            _infoBarData = frameworkInfoBarData;
            if (IsCreated ||
                _infoBarData.IsGloballySuppressed ||
                _infoBarData.InfoBarSuppressed ||
                _infoBarData.TestFramework == TestFrameworkType.UnitTest
            ) {
                return;
            }
            
            bool isPytestEnabled = _infoBarData.TestFramework == TestFrameworkType.Pytest;
            bool isValidPytestConfigFile = File.Exists(frameworkInfoBarData.PyTestConfigFilePath);

            string infoBarMessage;
            List<InfoBarHyperlink> acceptActionItems = new List<InfoBarHyperlink>();

            if (isPytestEnabled) {
                if (!await IsPyTestInstalledAsync()) {
                    infoBarMessage = Strings.PyTestNotInstalled.FormatUI(_infoBarData.Caption, _infoBarData.ContextLocalized);
                    acceptActionItems.Add(new InfoBarHyperlink(Strings.PyTestInstallInfoBarAction, (Action)InstallPytestAction));
                } else {
                    return;
                }

            } else if (isValidPytestConfigFile) {
                if (await IsPyTestInstalledAsync()) {
                    infoBarMessage = Strings.PyTestInstalledConfigurationFileFound.FormatUI(_infoBarData.Caption, _infoBarData.ContextLocalized);
                    acceptActionItems.Add(new InfoBarHyperlink(Strings.PyTestEnableInfoBarAction, (Action)EnablePytestAction));
                } else {
                    infoBarMessage = Strings.PyTestNotInstalledConfigurationFileFound.FormatUI(_infoBarData.Caption, _infoBarData.ContextLocalized);
                    acceptActionItems.Add(new InfoBarHyperlink(Strings.PyTestInstallAndEnableInfoBarAction, (Action)InstallAndEnablePytestAction));
                }

            } else if (PythonTestFileFound((x) => PythonConstants.DefaultTestFileNameRegex.IsMatch(PathUtils.GetFileOrDirectoryName(x)))) {
                infoBarMessage = Strings.PythonTestFileDetected.FormatUI(_infoBarData.Caption, _infoBarData.ContextLocalized);

                if (await IsPyTestInstalledAsync()) {
                    acceptActionItems.Add(new InfoBarHyperlink(Strings.PyTestEnableInfoBarAction, (Action)EnablePytestAction));
                } else {
                    acceptActionItems.Add(new InfoBarHyperlink(Strings.PyTestInstallAndEnableInfoBarAction, (Action)InstallAndEnablePytestAction));
                }

                acceptActionItems.Add(new InfoBarHyperlink(Strings.UnitTestEnableInfoBarAction, (Action)EnableUnitTestAction));
            } else {
                return;
            }

            ShowInfoBar(infoBarMessage, acceptActionItems);
        }


        private void ShowInfoBar(string infoBarMessage, List<InfoBarHyperlink> acceptActionItems) {
            LogEvent(ConfigureTestFrameworkInfoBarActions.Prompt);

            acceptActionItems.Add(new InfoBarHyperlink(Strings.PythonTestFrameworkIgnoreInfoBarAction, (Action)IgnoreAction));
            Create(new InfoBarModel(
                new List<IVsInfoBarTextSpan> { new InfoBarTextSpan(infoBarMessage) },
                acceptActionItems,
                KnownMonikers.StatusInformation)
            );
        }

        private async Task<bool> InstallPyTestAsync() {
            var packageManagers = _infoBarData.InterpreterOptionsService.GetPackageManagers(_infoBarData.InterpreterFactory);
            string failureMessage = null;

            foreach (var packageManager in packageManagers) {
                try {
                    bool pytestInstalled = await packageManager.InstallAsync(
                        new PackageSpec("pytest"),
                        new VsPackageManagerUI(Site),
                        CancellationToken.None
                    );

                    if (pytestInstalled) {
                        return true;
                    }
                } catch (InvalidOperationException e) {
                    failureMessage = e.Message;
                }
            }

            if (!String.IsNullOrEmpty(failureMessage)) {
                // Something couldn't be installed.
                Create(new InfoBarModel(failureMessage));
            }

            return false;
        }

        private async Task<bool> IsPyTestInstalledAsync() {
            var packageManagers = _infoBarData.InterpreterOptionsService.GetPackageManagers(_infoBarData.InterpreterFactory);
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
                PythonLogEvent.ConfigureTestFrameworkInfoBar,
                new ConfigureTestFrameworkInfoBarInfo() {
                    Action = action,
                    Context = _infoBarData.Context,
                }
            );
        }

        private void InstallPytestAction() {
            InstallPytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task InstallPytestActionAsync() {
            LogEvent(ConfigureTestFrameworkInfoBarActions.InstallPytest);
            Close();

            var result = await InstallPyTestAsync();
            if (!result) {
                var generalOutputWindow = OutputWindowRedirector.GetGeneral(Site);
                generalOutputWindow.ShowAndActivate();
            }

        }

        private void EnablePytestAction() {
            EnablePytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task EnablePytestActionAsync() {
            LogEvent(ConfigureTestFrameworkInfoBarActions.EnablePytest);
            Close();

            await SetPropertyAsync(PythonConstants.TestFrameworkSetting, "Pytest");
        }

        private void InstallAndEnablePytestAction() {
            InstallAndEnablePytestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task InstallAndEnablePytestActionAsync() {
            LogEvent(ConfigureTestFrameworkInfoBarActions.EnableAndInstallPytest);
            Close();

            if (!await InstallPyTestAsync()) {
                var generalOutputWindow = OutputWindowRedirector.GetGeneral(Site);
                generalOutputWindow.ShowAndActivate();
            }

            await SetPropertyAsync(PythonConstants.TestFrameworkSetting, "Pytest");
        }

        private void EnableUnitTestAction() {
            EnableUnitTestActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task EnableUnitTestActionAsync() {
            LogEvent(ConfigureTestFrameworkInfoBarActions.EnableUnitTest);
            Close();

            await SetPropertyAsync(PythonConstants.TestFrameworkSetting, "unittest");
        }

        private void IgnoreAction() {
            IgnoreActionAsync().HandleAllExceptions(Site, GetType()).DoNotWait();
        }

        private async Task IgnoreActionAsync() {
            LogEvent(ConfigureTestFrameworkInfoBarActions.Ignore);
            Close();

            await SetPropertyAsync(PythonConstants.SuppressConfigureTestFrameworkPrompt, "true");
        }

        protected abstract bool PythonTestFileFound(Predicate<string> fileFilter);

        protected abstract Task SetPropertyAsync(string propertyName, string propertyValue);

        protected class TestFrameworkInfoBarData {
            public IInterpreterOptionsService InterpreterOptionsService { get; set; }
            public IPythonInterpreterFactory InterpreterFactory { get; set; }

            public bool IsGloballySuppressed { get; set; }
            public bool InfoBarSuppressed { get; set; }

            public string RootDirectory { get; set; }
            public string PyTestConfigFilePath { get; set; }

            public string Caption { get; set; }
            public string Context { get; set; }
            public string ContextLocalized { get; set; }

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

    internal sealed class TestFrameworkProjectInfoBar : TestFrameworkInfoBar {
        private PythonProjectNode Project { get; }

        public TestFrameworkProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode) : base(site) {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public override async Task CheckAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Add this check to prevent accessing project properties before project is fully opened
            if (!Project.IsProjectOpened) {
                return;
            }

            var infoBarData = new TestFrameworkInfoBarData {
                InterpreterOptionsService = Site.GetPythonToolsService().InterpreterOptionsService,
                InterpreterFactory = Project.ActiveInterpreter,
                IsGloballySuppressed = !Site.GetPythonToolsService().GeneralOptions.PromptForTestFrameWorkInfoBar,
                InfoBarSuppressed = Project.GetProjectProperty(PythonConstants.SuppressConfigureTestFrameworkPrompt).IsTrue(),
                RootDirectory = Project.ProjectHome,
                Caption = Project.Caption,
                Context = InfoBarContexts.Project,
                ContextLocalized = Strings.ProjectText,
                PyTestConfigFilePath = Project.GetPyTestConfigFilePath(),
                TestFramework = TestFrameworkInfoBarData.GetTestFramework(
                    Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false)
                )
            };

            await CheckAsync(infoBarData);
        }

        protected override Task SetPropertyAsync(string propertyName, string propertyValue) {
            Project.SetProjectProperty(propertyName, propertyValue);
            return Task.CompletedTask;
        }

        protected override bool PythonTestFileFound(Predicate<string> fileFilter) {
            return Project.AllVisibleDescendants
                .Where(x => (x is PythonFileNode || x is CommonNonCodeFileNode))
                .Select(f => f.Url)
                .Where(File.Exists)
                .Where(x => fileFilter(x))
                .Any();
        }
    }

    internal sealed class TestFrameworkWorkspaceInfoBar : TestFrameworkInfoBar {
        private IPythonWorkspaceContext WorkspaceContext { get; }

        public TestFrameworkWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext pythonWorkspaceContext) : base(site) {
            WorkspaceContext = pythonWorkspaceContext ?? throw new ArgumentNullException(nameof(pythonWorkspaceContext));
        }

        public override async Task CheckAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var infoBarData = new TestFrameworkInfoBarData {
                InterpreterOptionsService = Site.GetPythonToolsService().InterpreterOptionsService,
                InterpreterFactory = WorkspaceContext.CurrentFactory,
                IsGloballySuppressed = !Site.GetPythonToolsService().GeneralOptions.PromptForTestFrameWorkInfoBar,
                InfoBarSuppressed = WorkspaceContext.GetBoolProperty(PythonConstants.SuppressConfigureTestFrameworkPrompt) ?? false,
                RootDirectory = WorkspaceContext.Location,
                Caption = WorkspaceContext.WorkspaceName,
                Context = InfoBarContexts.Workspace,
                ContextLocalized = Strings.WorkspaceText,
                PyTestConfigFilePath = GetPyTestConfigFilePath(),
                TestFramework = TestFrameworkInfoBarData.GetTestFramework(WorkspaceContext.GetStringProperty(PythonConstants.TestFrameworkSetting)),
            };

            await CheckAsync(infoBarData);
        }

        private string GetPyTestConfigFilePath() {
            var fileName = PythonConstants.PyTestFrameworkConfigFiles
                .FirstOrDefault(x => File.Exists(PathUtils.GetAbsoluteFilePath(WorkspaceContext.Location, x)));

            return string.IsNullOrEmpty(fileName) ? "" : Path.Combine(WorkspaceContext.Location, fileName);
        }

        protected override async Task SetPropertyAsync(string propertyName, string propertyValue) {
            await WorkspaceContext.SetPropertyAsync(propertyName, propertyValue);
        }

        protected override bool PythonTestFileFound(Predicate<string> fileFilter) {
            return WorkspaceContext.EnumerateUserFiles(fileFilter).Any();
        }
    }
}