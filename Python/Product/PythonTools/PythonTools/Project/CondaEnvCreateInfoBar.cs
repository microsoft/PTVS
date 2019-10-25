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
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal abstract class CondaEnvCreateInfoBar : PythonInfoBar {
        public CondaEnvCreateInfoBar(IServiceProvider site)
            : base(site) {
        }

        protected string EnvironmentYmlPath { get; set; }

        protected string Caption { get; set; }

        protected string Context { get; set; }

        protected string MissingEnvName { get; set; }

        protected bool IsGloballySuppressed =>
            !Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate;

        protected abstract void ShowAddEnvironmentDialog();

        protected abstract void Suppress();

        protected bool IsCondaEnvOrAnaconda(IPythonInterpreterFactory fact) {
            if (CondaEnvironmentFactoryProvider.IsCondaEnv(fact)) {
                return true;
            }

            // If it's not a conda env, but has conda package manager, then do not prompt
            // (this may be the root anaconda installation)
            var options = Site.GetPythonToolsService().InterpreterOptionsService;
            var pm = options.GetPackageManagers(fact)?.FirstOrDefault(p => p.UniqueKey == "conda");
            return pm != null;
        }

        protected void ShowInfoBar() {
            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            var msg = MissingEnvName != null
                ? Strings.CondaInfoBarRecreateMessage.FormatUI(Caption, MissingEnvName)
                : Strings.CondaInfoBarCreateNewMessage.FormatUI(Caption);

            messages.Add(new InfoBarTextSpan(msg));
            actions.Add(new InfoBarHyperlink(Strings.CondaInfoBarCreateAction, (Action)CreateEnvironment));
            actions.Add(new InfoBarHyperlink(Strings.CondaInfoBarProjectIgnoreAction, (Action)Ignore));

            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Prompt,
                    Reason = MissingEnvName != null ? CondaEnvCreateInfoBarReasons.MissingEnv : CondaEnvCreateInfoBarReasons.NoEnv,
                    Context = Context,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void Ignore() {
            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Ignore,
                    Context = Context,
                }
            );
            Suppress();
            Close();
        }

        private void CreateEnvironment() {
            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Create,
                    Context = Context,
                }
            );
            ShowAddEnvironmentDialog();
            Close();
        }
    }

    sealed class CondaEnvCreateProjectInfoBar : CondaEnvCreateInfoBar {
        public CondaEnvCreateProjectInfoBar(IServiceProvider site, PythonProjectNode projectNode)
            :base (site) {
            Project = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        private PythonProjectNode Project { get; }

        public override async Task CheckAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsCreated || IsGloballySuppressed) {
                return;
            }

            EnvironmentYmlPath = Project.GetEnvironmentYmlPath();
            Caption = Project.Caption;
            Context = InfoBarContexts.Project;
            MissingEnvName = null;

            if (Project.GetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt).IsTrue()) {
                return;
            }

            // Skip if active is already conda
            var active = Project.ActiveInterpreter;
            if (IsCondaEnvOrAnaconda(active) && active.IsRunnable()) {
                return;
            }

            var all = Project.InterpreterFactories.ToArray();
            var allConda = all.Where(IsCondaEnvOrAnaconda).ToArray();
            var foundConda = allConda.Where(f => f.IsRunnable()).ToArray();
            var condaNotFoundNames = Project.InvalidInterpreterIds
                .Where(id => CondaEnvironmentFactoryProvider.IsCondaEnv(id))
                .Select(id => CondaEnvironmentFactoryProvider.NameFromId(id))
                .Where(name => name != null)
                .ToArray();

            if (condaNotFoundNames.Any() && !foundConda.Any()) {
                // Propose to recreate one of the conda references, since they are all missing
                MissingEnvName = condaNotFoundNames.First();
            } else if (!foundConda.Any() && !string.IsNullOrEmpty(EnvironmentYmlPath)) {
                // Propose to create a new one, since there's a yaml file and no conda references
                MissingEnvName = null;
            } else {
                // Nothing to do
                return;
            }

            ShowInfoBar();
        }

        protected override void ShowAddEnvironmentDialog() {
            Project.ShowAddCondaEnvironment(MissingEnvName, EnvironmentYmlPath);
        }

        protected override void Suppress() {
            Project.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
        }
    }

    sealed class CondaEnvCreateWorkspaceInfoBar : CondaEnvCreateInfoBar {
        public CondaEnvCreateWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site) {
            Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        }

        private IPythonWorkspaceContext Workspace { get; }

        public override async Task CheckAsync() {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (IsCreated || IsGloballySuppressed) {
                return;
            }

            EnvironmentYmlPath = Workspace.GetEnvironmentYmlPath();
            Caption = Workspace.WorkspaceName;
            Context = InfoBarContexts.Workspace;
            MissingEnvName = null;

            if (Workspace.GetBoolProperty(PythonConstants.SuppressEnvironmentCreationPrompt) == true) {
                return;
            }

            // Skip if active is already conda
            var active = Workspace.CurrentFactory;
            if (IsCondaEnvOrAnaconda(active) && active.IsRunnable()) {
                return;
            }

            if (Workspace.IsCurrentFactoryDefault == false) {
                return;
            }

            if (!File.Exists(EnvironmentYmlPath)) {
                return;
            }

            ShowInfoBar();
        }

        protected override void ShowAddEnvironmentDialog() {
            AddEnvironmentDialog.ShowAddCondaEnvironmentDialogAsync(
                Site,
                null,
                Workspace,
                MissingEnvName,
                EnvironmentYmlPath,
                null
            ).HandleAllExceptions(Site, typeof(CondaEnvCreateInfoBar)).DoNotWait();
        }

        protected override void Suppress() {
            Workspace.SetPropertyAsync(PythonConstants.SuppressEnvironmentCreationPrompt, true)
                .HandleAllExceptions(Site, typeof(CondaEnvCreateWorkspaceInfoBar))
                .DoNotWait();
        }
    }
}
