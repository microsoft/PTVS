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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    internal sealed class CondaEnvCreateInfoBar : IVsInfoBarUIEvents, IDisposable {
        private readonly PythonProjectNode _projectNode;
        private uint _adviseCookie;
        private IVsInfoBarUIElement _infoBar;
        private IPythonToolsLogger _logger;

        public CondaEnvCreateInfoBar(PythonProjectNode projectNode) {
            _projectNode = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public void Check() {
            if (!_projectNode.Site.GetPythonToolsService().GeneralOptions.PromptForEnvCreate) {
                return;
            }

            var suppressProp = _projectNode.GetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt);
            if (suppressProp.IsTrue()) {
                return;
            }

            // Skip if active is already conda
            var active = _projectNode.ActiveInterpreter;
            if (IsCondaEnvOrAnaconda(active) && active.IsRunnable()) {
                return;
            }

            var yamlPath = _projectNode.GetEnvironmentYmlPath();

            var all = _projectNode.InterpreterFactories.ToArray();
            var allConda = all.Where(IsCondaEnvOrAnaconda).ToArray();
            var foundConda = allConda.Where(f => f.IsRunnable()).ToArray();
            var condaNotFoundNames = _projectNode.InvalidInterpreterIds
                .Where(id => CondaEnvironmentFactoryProvider.IsCondaEnv(id))
                .Select(id => CondaEnvironmentFactoryProvider.NameFromId(id))
                .Where(name => name != null)
                .ToArray();

            if (condaNotFoundNames.Any() && !foundConda.Any()) {
                // Propose to recreate one of the conda references, since they are all missing
                Show(condaNotFoundNames.First(), yamlPath);
            } else if (!foundConda.Any() && !string.IsNullOrEmpty(yamlPath)) {
                // Propose to create a new one, since there's a yaml file and no conda references
                Show(null, yamlPath);
            }
        }

        private bool IsCondaEnvOrAnaconda(IPythonInterpreterFactory fact) {
            if (CondaEnvironmentFactoryProvider.IsCondaEnv(fact)) {
                return true;
            }

            // If it's not a conda env, but has conda package manager, then do not prompt
            // (this may be the root anaconda installation)
            var pm = _projectNode.InterpreterOptions.GetPackageManagers(fact)?.FirstOrDefault(p => p.UniqueKey == "conda");
            return pm != null;
        }

        private void Show(string existingName, string yamlPath) {
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

            Action create = () => {
                _logger?.LogEvent(
                    PythonLogEvent.CondaEnvCreateInfoBar,
                    new CondaEnvCreateInfoBarInfo() {
                        Action = CondaEnvCreateInfoBarActions.Create,
                    }
                );
                _projectNode.ShowAddCondaEnvironment(existingName, yamlPath);
                _infoBar.Close();
            };

            Action projectIgnore = () => {
                _logger?.LogEvent(
                    PythonLogEvent.CondaEnvCreateInfoBar,
                    new CondaEnvCreateInfoBarInfo() {
                        Action = CondaEnvCreateInfoBarActions.Ignore,
                    }
                );
                _projectNode.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
                _infoBar.Close();
            };

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            var msg = existingName != null
                ? Strings.CondaInfoBarRecreateMessage.FormatUI(_projectNode.Caption, existingName)
                : Strings.CondaInfoBarCreateNewMessage.FormatUI(_projectNode.Caption);

            messages.Add(new InfoBarTextSpan(msg));
            actions.Add(new InfoBarButton(Strings.CondaInfoBarCreateAction, create));
            actions.Add(new InfoBarButton(Strings.CondaInfoBarProjectIgnoreAction, projectIgnore));

            var infoBarModel = new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true);
            _infoBar = infoBarFactory.CreateInfoBar(infoBarModel);
            infoBarHost.AddInfoBar(_infoBar);

            _infoBar.Advise(this, out uint cookie);
            _adviseCookie = cookie;

            _logger?.LogEvent(
                PythonLogEvent.CondaEnvCreateInfoBar,
                new CondaEnvCreateInfoBarInfo() {
                    Action = CondaEnvCreateInfoBarActions.Prompt,
                    Reason = existingName != null ? CondaEnvCreateInfoBarReasons.MissingEnv : CondaEnvCreateInfoBarReasons.NoEnv,
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
