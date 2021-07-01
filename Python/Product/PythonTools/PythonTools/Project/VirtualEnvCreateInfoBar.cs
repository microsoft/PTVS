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

using Microsoft.PythonTools.Environments;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project
{
    internal abstract class VirtualEnvCreateInfoBar : PythonInfoBar
    {
        public VirtualEnvCreateInfoBar(IServiceProvider site)
            : base(site)
        {
        }

        protected string RequirementsTxtPath { get; set; }

        protected string Caption { get; set; }

        protected string Context { get; set; }

        protected bool IsGloballySuppressed =>
          !Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate;

        protected abstract void ShowAddEnvironmentDialog();

        protected abstract void Suppress();

        protected void ShowInfoBar()
        {
            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtCreateVirtualEnvInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(RequirementsTxtPath),
                    Caption
            )));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarCreateVirtualEnvAction, (Action)CreateEnvironment));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarProjectIgnoreAction, (Action)Ignore));

            Logger?.LogEvent(
                PythonLogEvent.VirtualEnvCreateInfoBar,
                new VirtualEnvCreateInfoBarInfo()
                {
                    Action = VirtualEnvCreateInfoBarActions.Prompt,
                    Context = Context,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void Ignore()
        {
            Logger?.LogEvent(
                PythonLogEvent.VirtualEnvCreateInfoBar,
                new VirtualEnvCreateInfoBarInfo()
                {
                    Action = VirtualEnvCreateInfoBarActions.Ignore,
                    Context = Context,
                }
            );
            Suppress();
            Close();
        }

        private void CreateEnvironment()
        {
            Logger?.LogEvent(
                PythonLogEvent.VirtualEnvCreateInfoBar,
                new VirtualEnvCreateInfoBarInfo()
                {
                    Action = VirtualEnvCreateInfoBarActions.Create,
                    Context = Context,
                }
            );
            ShowAddEnvironmentDialog();
            Close();
        }
    }

    internal sealed class VirtualEnvCreateProjectInfoBar : VirtualEnvCreateInfoBar
    {
        public VirtualEnvCreateProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode)
            : base(site)
        {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        private PythonProjectNode Project { get; }

        public override async Task CheckAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsCreated || IsGloballySuppressed)
            {
                return;
            }

            RequirementsTxtPath = Project.GetRequirementsTxtPath();
            Caption = Project.Caption;
            Context = InfoBarContexts.Project;

            if (Project.GetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt).IsTrue())
            {
                return;
            }

            if (!File.Exists(RequirementsTxtPath))
            {
                return;
            }

            if (!Project.IsActiveInterpreterGlobalDefault)
            {
                return;
            }

            ShowInfoBar();
        }

        protected override void ShowAddEnvironmentDialog()
        {
            AddEnvironmentDialog.ShowAddVirtualEnvironmentDialogAsync(
                Site,
                Project,
                null,
                null,
                null,
                RequirementsTxtPath
            ).HandleAllExceptions(Site, typeof(VirtualEnvCreateInfoBar)).DoNotWait();
        }

        protected override void Suppress()
        {
            Project.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
        }
    }

    internal sealed class VirtualEnvCreateWorkspaceInfoBar : VirtualEnvCreateInfoBar
    {
        public VirtualEnvCreateWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site)
        {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        private IPythonWorkspaceContext Workspace { get; }

        public override async Task CheckAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsCreated || IsGloballySuppressed)
            {
                return;
            }

            RequirementsTxtPath = Workspace.GetRequirementsTxtPath();
            Caption = Workspace.WorkspaceName;
            Context = InfoBarContexts.Workspace;

            if (Workspace.GetBoolProperty(PythonConstants.SuppressEnvironmentCreationPrompt) == true)
            {
                return;
            }

            if (!File.Exists(RequirementsTxtPath))
            {
                return;
            }

            if (!Workspace.IsCurrentFactoryDefault)
            {
                return;
            }

            ShowInfoBar();
        }

        protected override void ShowAddEnvironmentDialog()
        {
            AddEnvironmentDialog.ShowAddVirtualEnvironmentDialogAsync(
                Site,
                null,
                Workspace,
                null,
                null,
                RequirementsTxtPath
            ).HandleAllExceptions(Site, typeof(VirtualEnvCreateInfoBar)).DoNotWait();
        }

        protected override void Suppress()
        {
            Workspace.SetPropertyAsync(PythonConstants.SuppressEnvironmentCreationPrompt, true)
                .HandleAllExceptions(Site, typeof(VirtualEnvCreateWorkspaceInfoBar))
                .DoNotWait();
        }
    }
}
