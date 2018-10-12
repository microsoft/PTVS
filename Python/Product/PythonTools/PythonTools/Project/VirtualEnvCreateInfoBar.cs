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
using System.IO;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    internal sealed class VirtualEnvCreateInfoBar : IVsInfoBarUIEvents, IDisposable {
        private readonly PythonProjectNode _projectNode;
        private uint _adviseCookie;
        private IVsInfoBarUIElement _infoBar;
        private IPythonToolsLogger _logger;

        public VirtualEnvCreateInfoBar(PythonProjectNode projectNode) {
            _projectNode = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public async System.Threading.Tasks.Task CheckAsync() {
            if (!_projectNode.Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate) {
                return;
            }

            var suppressProp = _projectNode.GetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt);
            if (suppressProp.IsTrue()) {
                return;
            }

            var txtPath = _projectNode.GetRequirementsTxtPath();
            if (!File.Exists(txtPath)) {
                return;
            }

            if (_projectNode.IsActiveInterpreterGlobalDefault) {
                ShowCreateVirtualEnvironment(txtPath);
            }
        }

        private void ShowCreateVirtualEnvironment(string txtPath) {
            if (_infoBar != null) {
                return;
            }

            var shell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(SVsShell));
            if (ErrorHandler.Failed(shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out object infoBarHostObj)) || infoBarHostObj == null) {
                return;
            }

            var infoBarHost = (IVsInfoBarHost)infoBarHostObj;
            var infoBarFactory = (IVsInfoBarUIFactory)ServiceProvider.GlobalProvider.GetService(typeof(SVsInfoBarUIFactory));
            if (_logger == null) {
                _logger = (IPythonToolsLogger)ServiceProvider.GlobalProvider.GetService(typeof(IPythonToolsLogger));
            }

            Action createVirtualEnv = () => {
                _logger?.LogEvent(
                    PythonLogEvent.VirtualEnvCreateInfoBar,
                    new VirtualEnvCreateInfoBarInfo() {
                        Action = VirtualEnvCreateInfoBarActions.Create,
                    }
                );
                AddEnvironmentDialog.ShowAddVirtualEnvironmentDialogAsync(
                    _projectNode.Site,
                    _projectNode,
                    null,
                    null,
                    txtPath
                ).HandleAllExceptions(_projectNode.Site, typeof(VirtualEnvCreateInfoBar)).DoNotWait();
                _infoBar.Close();
            };

            Action projectIgnore = () => {
                _logger?.LogEvent(
                    PythonLogEvent.VirtualEnvCreateInfoBar,
                    new VirtualEnvCreateInfoBarInfo() {
                        Action = VirtualEnvCreateInfoBarActions.Ignore,
                    }
                );
                _projectNode.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
                _infoBar.Close();
            };

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtCreateVirtualEnvInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(txtPath),
                    _projectNode.Caption
            )));
            actions.Add(new InfoBarButton(Strings.RequirementsTxtInfoBarCreateVirtualEnvAction, createVirtualEnv));
            actions.Add(new InfoBarButton(Strings.RequirementsTxtInfoBarProjectIgnoreAction, projectIgnore));

            var infoBarModel = new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true);
            _infoBar = infoBarFactory.CreateInfoBar(infoBarModel);
            infoBarHost.AddInfoBar(_infoBar);

            _infoBar.Advise(this, out uint cookie);
            _adviseCookie = cookie;

            _logger?.LogEvent(
                PythonLogEvent.VirtualEnvCreateInfoBar,
                new VirtualEnvCreateInfoBarInfo() {
                    Action = VirtualEnvCreateInfoBarActions.Prompt,
                }
            );
        }

        public void OnActionItemClicked(IVsInfoBarUIElement infoBarUIElement, IVsInfoBarActionItem actionItem) {
            ((Action)actionItem.ActionContext)();
        }

        public void OnClosed(IVsInfoBarUIElement infoBarUIElement) {
            infoBarUIElement.Unadvise(_adviseCookie);
            _infoBar = null;
        }

        public void Dispose() {
            _infoBar?.Close();
        }
    }
}
