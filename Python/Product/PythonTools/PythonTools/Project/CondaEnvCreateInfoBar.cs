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
    internal sealed class CondaEnvCreateInfoBar : PythonProjectInfoBar {
        public CondaEnvCreateInfoBar(IServiceProvider site, PythonProjectNode projectNode)
            : base(site, projectNode) {
        }

        public CondaEnvCreateInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site, workspace) {
        }

        public override async Task CheckAsync() {
            if (IsCreated) {
                return;
            }

            if (!Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate) {
                return;
            }

            if (IsSuppressed(PythonConstants.SuppressEnvironmentCreationPrompt)) {
                return;
            }

            // Skip if active is already conda
            var active = Project?.ActiveInterpreter ?? Workspace?.CurrentFactory;
            if (IsCondaEnvOrAnaconda(active) && active.IsRunnable()) {
                return;
            }

            var yamlPath = Project?.GetEnvironmentYmlPath() ?? Workspace?.GetEnvironmentYmlPath();
            string existingName = null;

            if (Project != null) {
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
                    existingName = condaNotFoundNames.First();
                } else if (!foundConda.Any() && !string.IsNullOrEmpty(yamlPath)) {
                    // Propose to create a new one, since there's a yaml file and no conda references
                    existingName = null;
                } else {
                    // Nothing to do
                    return;
                }
            } else if (Workspace != null) {
                if (Workspace.IsCurrentFactoryDefault == false) {
                    return;
                }

                if (!File.Exists(yamlPath)) {
                    return;
                }
            }

            ShowInfoBar(yamlPath, existingName);
        }

        private void ShowInfoBar(string yamlPath, string existingName) {
            var context = Project != null ? InfoBarContexts.Project : InfoBarContexts.Workspace;
            var projectOrWorkspaceName = Project?.Caption ?? Workspace?.WorkspaceName ?? string.Empty;

            Action create = () => CreateEnvironment(context, yamlPath, existingName);
            Action projectIgnore = () => Ignore(context);

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            var msg = existingName != null
                ? Strings.CondaInfoBarRecreateMessage.FormatUI(projectOrWorkspaceName, existingName)
                : Strings.CondaInfoBarCreateNewMessage.FormatUI(projectOrWorkspaceName);

            messages.Add(new InfoBarTextSpan(msg));
            actions.Add(new InfoBarHyperlink(Strings.CondaInfoBarCreateAction, create));
            actions.Add(new InfoBarHyperlink(Strings.CondaInfoBarProjectIgnoreAction, projectIgnore));

            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Prompt,
                    Reason = existingName != null ? CondaEnvCreateInfoBarReasons.MissingEnv : CondaEnvCreateInfoBarReasons.NoEnv,
                    Context = context,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void Ignore(string context) {
            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Ignore,
                    Context = context,
                }
            );
            SuppressAsync(PythonConstants.SuppressEnvironmentCreationPrompt)
                .HandleAllExceptions(Site, typeof(CondaEnvCreateInfoBar))
                .DoNotWait();
            Close();
        }

        private void CreateEnvironment(string context, string yamlPath, string existingName) {
            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Create,
                    Context = context,
                }
            );
            if (Project != null) {
                Project.ShowAddCondaEnvironment(existingName, yamlPath);
            } else if (Workspace != null) {
                AddEnvironmentDialog.ShowAddCondaEnvironmentDialogAsync(
                    Site,
                    null,
                    Workspace,
                    existingName,
                    yamlPath,
                    null
                ).HandleAllExceptions(Site, typeof(CondaEnvCreateInfoBar)).DoNotWait();
            }
            Close();
        }

        private bool IsCondaEnvOrAnaconda(IPythonInterpreterFactory fact) {
            if (CondaEnvironmentFactoryProvider.IsCondaEnv(fact)) {
                return true;
            }

            // If it's not a conda env, but has conda package manager, then do not prompt
            // (this may be the root anaconda installation)
            var options = Site.GetPythonToolsService().InterpreterOptionsService;
            var pm = options.GetPackageManagers(fact)?.FirstOrDefault(p => p.UniqueKey == "conda");
            return pm != null;
        }
    }
}
