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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal sealed class CondaEnvCreateInfoBar : PythonProjectInfoBar {
        public CondaEnvCreateInfoBar(PythonProjectNode projectNode)
            : base(projectNode) {
        }

        public override async Task CheckAsync() {
            if (IsCreated) {
                return;
            }

            if (!Project.Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate) {
                return;
            }

            var suppressProp = Project.GetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt);
            if (suppressProp.IsTrue()) {
                return;
            }

            // Skip if active is already conda
            var active = Project.ActiveInterpreter;
            if (IsCondaEnvOrAnaconda(active) && active.IsRunnable()) {
                return;
            }

            var yamlPath = Project.GetEnvironmentYmlPath();

            var all = Project.InterpreterFactories.ToArray();
            var allConda = all.Where(IsCondaEnvOrAnaconda).ToArray();
            var foundConda = allConda.Where(f => f.IsRunnable()).ToArray();
            var condaNotFoundNames = Project.InvalidInterpreterIds
                .Where(id => CondaEnvironmentFactoryProvider.IsCondaEnv(id))
                .Select(id => CondaEnvironmentFactoryProvider.NameFromId(id))
                .Where(name => name != null)
                .ToArray();

            string existingName;
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

            Action create = () => {
                Logger?.LogEvent(
                    PythonLogEvent.CondaEnvCreateInfoBar,
                    new CondaEnvCreateInfoBarInfo() {
                        Action = CondaEnvCreateInfoBarActions.Create,
                    }
                );
                Project.ShowAddCondaEnvironment(existingName, yamlPath);
                Close();
            };

            Action projectIgnore = () => {
                Logger?.LogEvent(
                    PythonLogEvent.CondaEnvCreateInfoBar,
                    new CondaEnvCreateInfoBarInfo() {
                        Action = CondaEnvCreateInfoBarActions.Ignore,
                    }
                );
                Project.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
                Close();
            };

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            var msg = existingName != null
                ? Strings.CondaInfoBarRecreateMessage.FormatUI(Project.Caption, existingName)
                : Strings.CondaInfoBarCreateNewMessage.FormatUI(Project.Caption);

            messages.Add(new InfoBarTextSpan(msg));
            actions.Add(new InfoBarButton(Strings.CondaInfoBarCreateAction, create));
            actions.Add(new InfoBarButton(Strings.CondaInfoBarProjectIgnoreAction, projectIgnore));

            Logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Prompt,
                    Reason = existingName != null ? CondaEnvCreateInfoBarReasons.MissingEnv : CondaEnvCreateInfoBarReasons.NoEnv,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private bool IsCondaEnvOrAnaconda(IPythonInterpreterFactory fact) {
            if (CondaEnvironmentFactoryProvider.IsCondaEnv(fact)) {
                return true;
            }

            // If it's not a conda env, but has conda package manager, then do not prompt
            // (this may be the root anaconda installation)
            var pm = Project.InterpreterOptions.GetPackageManagers(fact)?.FirstOrDefault(p => p.UniqueKey == "conda");
            return pm != null;
        }
    }
}
