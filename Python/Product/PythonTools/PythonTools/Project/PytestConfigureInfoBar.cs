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
using System.ComponentModel.Composition;
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
                infoBarMessage = Strings.PyTestConfigurationFileFound.FormatUI(InfoBarData.Caption);
                acceptActionItem = new InfoBarHyperlink(Strings.PyTestEnableInfoBarAction, (Action)EnablePytestAction);

            } else if (validConfigFile &&
                InfoBarData.TestFramework != TestFrameworkType.Pytest
                && !(await IsPyTestInstalled())
            ) {
                //Case #2. "Install and enable Pytest"
                infoBarMessage = Strings.PyTestConfigurationFileFound.FormatUI(InfoBarData.Caption);
                acceptActionItem = new InfoBarHyperlink(
                    Strings.PyTestInstallAndEnableInfoBarAction,
                    (Action)(async () => await InstallAndEnablePytestActionAsync())
                );

            } else if (InfoBarData.TestFramework == TestFrameworkType.Pytest && !(await IsPyTestInstalled())) {
                //Case #3 and #4. "Install Pytest"
                infoBarMessage = Strings.PyTestNotInstalled.FormatUI(InfoBarData.Caption);
                acceptActionItem = new InfoBarHyperlink(Strings.PyTestInstallInfoBarAction,
                    (Action)(async () => await InstallPytestActionAsync())
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

        private async Task InstallPytestActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.Install);
            if (!await InstallPyTestAsync()) {
                //Install failed. Force output window to display itself.
                //TODO Raymon
            }
            Close();
        }

        private void EnablePytestAction() {
            LogEvent(ConfigurePytestInfoBarActions.Enable);
            SetProperty(PythonConstants.TestFrameworkSetting, "Pytest");
            Close();
        }

        private async Task InstallAndEnablePytestActionAsync() {
            LogEvent(ConfigurePytestInfoBarActions.EnableAndInstall);
            if (!await InstallPyTestAsync()) {
                //Install failed. Take some action TODO RAymon
            }

            SetProperty(PythonConstants.TestFrameworkSetting, "Pytest");
            Close();
        }

        private void IgnoreAction() {
            LogEvent(ConfigurePytestInfoBarActions.Ignore);
            SetProperty(PythonConstants.SuppressPytestConfigPrompt, true.ToString());
            Close();
        }

        private async Task<Boolean> InstallPyTestAsync() {
            //Is there a better way to do this? Combining two link queries? TODO
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
            //TODO Simplify syntax Raymon
            var packageManagers = InfoBarData.InterpreterOptionsService.GetPackageManagers(InfoBarData.InterpreterFactory);

            var getPackageManagersTask = new List<Task<IList<PackageSpec>>>();
            foreach (var packageManager in packageManagers) {
                getPackageManagersTask.Add(packageManager.GetInstalledPackagesAsync(CancellationToken.None));
            }
            var packages = await Task.WhenAll(getPackageManagersTask);

            bool pytestPackageFound = false;
            foreach (var packageSpecs in packages) {
                var result = packageSpecs.FirstOrDefault(
                    x => String.Equals(x.Name, nameof(TestFrameworkType.Pytest), StringComparison.OrdinalIgnoreCase)
                );

                if (result != null) {
                    pytestPackageFound = true;
                    break;
                }
            }

            return pytestPackageFound;
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

        protected abstract void SetProperty(string propertyName, string propertyValue);

        protected class PyTestInfoBarData {
            public IInterpreterOptionsService InterpreterOptionsService { get; set; }
            public IPythonInterpreterFactory InterpreterFactory { get; set; }

            public bool IsGloballySuppressed = false; //TODO Raymon
            public bool InfoBarSuppressed = false;

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
                IsGloballySuppressed = false, //TODO Raymon
                InfoBarSuppressed = Project.GetProjectProperty(PythonConstants.SuppressPytestConfigPrompt).IsTrue(),
                Caption = Project.Caption,
                Context = InfoBarContexts.Project,
                PyTestConfigFilePath = Project.GetPyTestConfigFilePath(),
                TestFramework = PyTestInfoBarData.GetTestFramework(
                    Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false)
                )
            };

            infoBarData.InterpreterOptionsService = Site.GetPythonToolsService().InterpreterOptionsService;
            

            await CheckAsync(infoBarData);
        }

        protected override void SetProperty(string propertyName, string propertyValue) {
            Project.SetProjectProperty(propertyName, propertyValue);
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
                IsGloballySuppressed = false, //TODO Raymon
                InfoBarSuppressed = WorkspaceContext.GetBoolProperty(PythonConstants.SuppressPytestConfigPrompt) ?? false,
                Caption = WorkspaceContext.WorkspaceName,
                Context = InfoBarContexts.Workspace,
                PyTestConfigFilePath = GetPyTestConfigFilePath(),
                TestFramework = PyTestInfoBarData.GetTestFramework(WorkspaceContext.GetStringProperty(PythonConstants.TestFrameworkSetting)),
            };



            await CheckAsync(infoBarData);
        }

        private string GetPyTestConfigFilePath() {
            return PythonConstants.PyTestFrameworkConfigFiles
                .FirstOrDefault(fileName => File.Exists(PathUtils.GetAbsoluteFilePath(WorkspaceContext.Location, fileName)));
        }

        protected override async void SetProperty(string propertyName, string propertyValue) {
            await WorkspaceContext.SetPropertyAsync(propertyName, propertyValue);
        }



    }









}
