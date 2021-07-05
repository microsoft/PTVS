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

using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal abstract class PackageInstallInfoBar : PythonInfoBar {
        public PackageInstallInfoBar(IServiceProvider site)
            : base(site) {
        }

        protected string RequirementsTxtPath { get; set; }

        protected string Caption { get; set; }

        protected string Context { get; set; }

        protected IPackageManager PackageManager { get; set; }

        protected bool IsGloballySuppressed =>
          !Site.GetPythonToolsService().GeneralOptions.PromptForPackageInstallation;

        protected abstract void Suppress();

        protected void ShowInfoBar() {
            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtInstallPackagesInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(RequirementsTxtPath),
                    Caption,
                    PackageManager.Factory.Configuration.Description
            )));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarInstallPackagesAction, (Action)InstallPackages));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarProjectIgnoreAction, (Action)Ignore));

            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Prompt,
                    Context = Context,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void Ignore() {
            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Ignore,
                    Context = Context,
                }
            );
            Suppress();
            Close();
        }

        private void InstallPackages() {
            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Install,
                    Context = Context,
                }
            );
            PythonProjectNode.InstallRequirementsAsync(Site, PackageManager, RequirementsTxtPath)
                .HandleAllExceptions(Site, typeof(PackageInstallInfoBar))
                .DoNotWait();
            Close();
        }

        protected static async Task<bool> DetectMissingPackagesAsync(IPackageManager packageManager, string reqTxtPath) {
            try {
                return await PipRequirementsUtils.DetectMissingPackagesAsync(
                    packageManager.Factory.Configuration.InterpreterPath,
                    reqTxtPath
                );
            } catch (IOException) {
            } catch (OperationCanceledException) {
            }

            return false;
        }
    }

    internal sealed class PackageInstallProjectInfoBar : PackageInstallInfoBar {
        public PackageInstallProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode)
            : base(site) {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        private PythonProjectNode Project { get; }

        public override async Task CheckAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsCreated || IsGloballySuppressed) {
                return;
            }

            RequirementsTxtPath = Project.GetRequirementsTxtPath();
            Caption = Project.Caption;
            Context = InfoBarContexts.Project;
            PackageManager = null;

            if (Project.GetProjectProperty(PythonConstants.SuppressPackageInstallationPrompt).IsTrue()) {
                return;
            }

            var txtPath = Project.GetRequirementsTxtPath();
            if (!File.Exists(txtPath)) {
                return;
            }

            if (Project.IsActiveInterpreterGlobalDefault) {
                return;
            }

            var active = Project.ActiveInterpreter;
            if (!active.IsRunnable()) {
                return;
            }

            var options = Site.GetPythonToolsService().InterpreterOptionsService;
            PackageManager = options.GetPackageManagers(active).FirstOrDefault(p => p.UniqueKey == "pip");
            if (PackageManager == null) {
                return;
            }

            var missing = await DetectMissingPackagesAsync(PackageManager, RequirementsTxtPath);
            if (!missing) {
                return;
            }

            ShowInfoBar();
        }

        protected override void Suppress() {
            Project.SetProjectProperty(PythonConstants.SuppressPackageInstallationPrompt, true.ToString());
        }
    }

    internal sealed class PackageInstallWorkspaceInfoBar : PackageInstallInfoBar {
        public PackageInstallWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site) {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        private IPythonWorkspaceContext Workspace { get; }

        public override async Task CheckAsync() {
            if (IsCreated || IsGloballySuppressed) {
                return;
            }

            RequirementsTxtPath = Workspace.GetRequirementsTxtPath();
            Caption = Workspace.WorkspaceName;
            Context = InfoBarContexts.Workspace;
            PackageManager = null;

            if (Workspace.GetBoolProperty(PythonConstants.SuppressPackageInstallationPrompt) == true) {
                return;
            }

            if (!File.Exists(RequirementsTxtPath)) {
                return;
            }

            if (Workspace.IsCurrentFactoryDefault) {
                return;
            }

            var active = Workspace.CurrentFactory;
            if (!active.IsRunnable()) {
                return;
            }

            var options = Site.GetPythonToolsService().InterpreterOptionsService;
            PackageManager = options.GetPackageManagers(active).FirstOrDefault(p => p.UniqueKey == "pip");
            if (PackageManager == null) {
                return;
            }

            var missing = await DetectMissingPackagesAsync(PackageManager, RequirementsTxtPath);
            if (!missing) {
                return;
            }

            ShowInfoBar();
        }

        protected override void Suppress() {
            Workspace.SetPropertyAsync(PythonConstants.SuppressPackageInstallationPrompt, true)
                .HandleAllExceptions(Site, typeof(PackageInstallWorkspaceInfoBar))
                .DoNotWait();
        }
    }
}
