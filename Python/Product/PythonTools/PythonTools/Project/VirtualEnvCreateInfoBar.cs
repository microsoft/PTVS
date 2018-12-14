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
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal sealed class VirtualEnvCreateInfoBar : PythonProjectInfoBar {
        public VirtualEnvCreateInfoBar(PythonProjectNode projectNode)
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

            if (!Project.IsActiveInterpreterGlobalDefault) {
                return;
            }

            var txtPath = Project.GetRequirementsTxtPath();
            if (!File.Exists(txtPath)) {
                return;
            }

            Action createVirtualEnv = () => {
                Logger?.LogEvent(
                    PythonLogEvent.VirtualEnvCreateInfoBar,
                    new VirtualEnvCreateInfoBarInfo() {
                        Action = VirtualEnvCreateInfoBarActions.Create,
                    }
                );
                AddEnvironmentDialog.ShowAddVirtualEnvironmentDialogAsync(
                    Project.Site,
                    Project,
                    null,
                    null,
                    null,
                    txtPath
                ).HandleAllExceptions(Project.Site, typeof(VirtualEnvCreateInfoBar)).DoNotWait();
                Close();
            };

            Action projectIgnore = () => {
                Logger?.LogEvent(
                    PythonLogEvent.VirtualEnvCreateInfoBar,
                    new VirtualEnvCreateInfoBarInfo() {
                        Action = VirtualEnvCreateInfoBarActions.Ignore,
                    }
                );
                Project.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
                Close();
            };

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtCreateVirtualEnvInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(txtPath),
                    Project.Caption
            )));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarCreateVirtualEnvAction, createVirtualEnv));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarProjectIgnoreAction, projectIgnore));

            Logger?.LogEvent(
                PythonLogEvent.VirtualEnvCreateInfoBar,
                new VirtualEnvCreateInfoBarInfo() {
                    Action = VirtualEnvCreateInfoBarActions.Prompt,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }
    }
}
