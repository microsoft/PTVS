using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.PythonTools.Interpreter;
using Task = System.Threading.Tasks.Task;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Project {
    /*
     * Bugs: 
     *      If an info bar is shown and then a user selects pytest from project settings->Tests->pytest, then the info bar will remain
     *      If the user adds a config file after the project is loaded, the info bar will not be displayed
     */
    internal abstract class ConfigurePytestInfoBar : PythonInfoBar {
        private readonly IInterpreterOptionsService _interpreterOptionsService;
        private IPythonInterpreterFactory _interpreterFactory;
        private bool IsGloballySuppressed = false; //TODO
        private string InfoBarContext { get; set; }

        protected ConfigurePytestInfoBar(IServiceProvider site) : base(site) {
            _interpreterOptionsService = site.GetComponentModel().GetService<IInterpreterOptionsService>();
        }

        protected async Task CheckAsync(
            IPythonInterpreterFactory interpreterFactory,
            TestFrameworkType currentTestingFramework,
            bool suppressPytestConfigInfoBar,
            string pyTestConfigFilePath,
            string caption,
            string context
        ) {
            if (IsCreated ||
                IsGloballySuppressed ||
                currentTestingFramework == TestFrameworkType.Pytest ||
                string.IsNullOrEmpty(pyTestConfigFilePath) ||
                suppressPytestConfigInfoBar ||
                interpreterFactory == null
            ) {
                return;
            }
            _interpreterFactory = interpreterFactory;
            InfoBarContext = context;


            //Simplify syntax TODO
            var packageManagers = _interpreterOptionsService.GetPackageManagers(_interpreterFactory);
            var getPackagesTask = new List<Task<IList<PackageSpec>>>();
            foreach (var packageManager in packageManagers) {
                getPackagesTask.Add(packageManager.GetInstalledPackagesAsync(CancellationToken.None));
            }

            Boolean pytestPackageFound = false;
            var packages = await Task.WhenAll(getPackagesTask);
            foreach (var packageSpecs in packages) {
                var result = packageSpecs.FirstOrDefault(
                    x => String.Equals(x.Name, nameof(TestFrameworkType.Pytest), StringComparison.OrdinalIgnoreCase
                ));

                if (result != null) {
                    pytestPackageFound = true;
                    break;
                }
            }

            if (pytestPackageFound) {
                ShowInfoBar(
                    new InfoBarHyperlink(Strings.ConfigurePytestInfoBarEnableAction, (Action)EnablePytest),
                    pyTestConfigFilePath,
                    caption
                );
            } else {
                ShowInfoBar(
                    new InfoBarHyperlink(Strings.ConfigurePytestInfoBarInstallAndEnableAction, (Action)InstallAndEnablePytest),
                    pyTestConfigFilePath,
                    caption
                );
            }
        }

        private void ShowInfoBar(InfoBarActionItem configureActionItem, string pytestConfigFilePath, string caption) {
            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.ConfigurePytestTxtInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(pytestConfigFilePath),
                    caption
            )));

            actions.Add(configureActionItem);
            actions.Add(new InfoBarHyperlink(Strings.ConfigurePytestTextInfoBarIgnoreAction, (Action)Ignore));

            LogEvent(ConfigurePytestInfoBarActions.Prompt);
            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation));
        }

        private async void InstallAndEnablePytest() {
            LogEvent(ConfigurePytestInfoBarActions.EnableAndInstall);

            //IS there a better way to do this? Combining two link queries? TODO
            Boolean pytestInstalled = false;
            var packageManagers = _interpreterOptionsService.GetPackageManagers(_interpreterFactory);
            foreach (var packageManager in packageManagers) {
                pytestInstalled = await packageManager.InstallAsync(
                    new PackageSpec("pytest"),
                    new VsPackageManagerUI(Site),
                    CancellationToken.None
                    );

                if (pytestInstalled) {
                    SetProperty(PythonConstants.TestFrameworkSetting, "Pytest");
                    break;
                }
            }

            if (!pytestInstalled) {
                //Pytest installation failed.Show error message? TODO
            }

            Close();
        }

        private void EnablePytest() {
            LogEvent(ConfigurePytestInfoBarActions.Enable);
            SetProperty(PythonConstants.TestFrameworkSetting, "Pytest");
            Close();
        }

        private void Ignore() {
            LogEvent(ConfigurePytestInfoBarActions.Ignore);
            SetProperty(PythonConstants.SuppressPytestConfigPrompt, true.ToString());
            Close();
        }

        private void LogEvent(string action) {
            Logger?.LogEvent(
                PythonLogEvent.ConfigurePytestInfoBar,
                new ConfigurePytestInfoBarInfo() {
                    Action = action,
                    Context = InfoBarContext
                }
            );
        }

        protected abstract void SetProperty(string propertyName, string propertyValue);

        protected TestFrameworkType GetTestFramework(string propertyValue) {
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

    internal sealed class ConfigurePytestProjectInfoBar : ConfigurePytestInfoBar {
        private PythonProjectNode Project { get; }

        public ConfigurePytestProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode) : base(site) {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public override async Task CheckAsync() {
            await CheckAsync(
                Project.ActiveInterpreter,
                GetTestFramework(Project.GetProjectProperty(PythonConstants.TestFrameworkSetting, false)),
                Project.GetProjectProperty(PythonConstants.SuppressPytestConfigPrompt).IsTrue(),
                Project.GetPyTestConfigFilePath(),
                Project.Caption,
                InfoBarContexts.Project
            );
        }

        protected override void SetProperty(string propertyName, string propertyValue) {
            Project.SetProjectProperty(propertyName, propertyValue);
        }
    }

    internal sealed class ConfigurePytestWorkspaceInfoBar : ConfigurePytestInfoBar {
        private IPythonWorkspaceContext WorkspaceContext { get; }

        public ConfigurePytestWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext pythonWorkspaceContext) : base(site) {
            WorkspaceContext = pythonWorkspaceContext ?? throw new ArgumentNullException(nameof(pythonWorkspaceContext));
        }

        public override async Task CheckAsync() {
            await CheckAsync(
                WorkspaceContext.CurrentFactory,
                GetTestFramework(WorkspaceContext.GetStringProperty(PythonConstants.TestFrameworkSetting)),
                WorkspaceContext.GetBoolProperty(PythonConstants.SuppressPytestConfigPrompt) ?? false,
                GetPyTestConfigFilePath(),
                WorkspaceContext.WorkspaceName,
                InfoBarContexts.Workspace
            );
        }

        private string GetPyTestConfigFilePath() {
            return PythonConstants.PyTestFrameworkConfigFiles
                .FirstOrDefault(fileName => File.Exists(PathUtils.GetAbsoluteFilePath(WorkspaceContext.Location, fileName)));
        }

        protected override void SetProperty(string propertyName, string propertyValue) {
            //TODO Do I have to await here? Can I just fire and forget? 
            WorkspaceContext.SetPropertyAsync(propertyName, propertyValue);
        }

    }
}
