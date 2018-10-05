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
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    internal sealed class PackageInstallInfoBar : IVsInfoBarUIEvents, IDisposable {
        private readonly PythonProjectNode _projectNode;
        private uint _adviseCookie;
        private IVsInfoBarUIElement _infoBar;
        private IPythonToolsLogger _logger;

        public PackageInstallInfoBar(PythonProjectNode projectNode) {
            _projectNode = projectNode ?? throw new ArgumentNullException(nameof(projectNode));
        }

        public async System.Threading.Tasks.Task CheckAsync() {
            if (!_projectNode.Site.GetPythonToolsService().GeneralOptions.PromptForPackageInstallation) {
                return;
            }

            var suppressProp = _projectNode.GetProjectProperty(PythonConstants.SuppressPackageInstallationPrompt);
            if (suppressProp.IsTrue()) {
                return;
            }

            var txtPath = _projectNode.GetRequirementsTxtPath();
            if (!File.Exists(txtPath)) {
                return;
            }

            if (!_projectNode.IsActiveInterpreterGlobalDefault) {
                var active = _projectNode.ActiveInterpreter;
                if (active.IsRunnable()) {
                    var pm = _projectNode.InterpreterOptions.GetPackageManagers(active).FirstOrDefault(p => p.UniqueKey == "pip");
                    if (pm != null && await PackagesMissingAsync(pm, txtPath)) {
                        ShowInstallPackages(txtPath, pm);
                    }
                }
            }
        }

        private async Task<bool> PackagesMissingAsync(IPackageManager pm, string txtPath) {
            try {
                var installed = await pm.GetInstalledPackagesAsync(CancellationTokens.After15s);
                var original = File.ReadAllLines(txtPath);
                foreach (var _line in original) {
                    var line = _line;
                    foreach (var m in PythonProjectNode.FindRequirementRegex.Matches(line).Cast<Match>()) {
                        var name = m.Groups["name"].Value;
                        if (installed.FirstOrDefault(pkg => string.CompareOrdinal(pkg.Name, name) == 0) == null) {
                            return true;
                        }
                    }
                }
            } catch (IOException) {
            } catch (OperationCanceledException) {
            }

            return false;
        }

        private void ShowInstallPackages(string txtPath, IPackageManager pm) {
            if (_infoBar != null) {
                return;
            }

            var shell = (IVsShell)ServiceProvider.GlobalProvider.GetService(typeof(SVsShell));
            shell.GetProperty((int)__VSSPROPID7.VSSPROPID_MainWindowInfoBarHost, out object infoBarHostObj);
            var infoBarHost = (IVsInfoBarHost)infoBarHostObj;
            var infoBarFactory = (IVsInfoBarUIFactory)ServiceProvider.GlobalProvider.GetService(typeof(SVsInfoBarUIFactory));
            if (_logger == null) {
                _logger = (IPythonToolsLogger)ServiceProvider.GlobalProvider.GetService(typeof(IPythonToolsLogger));
            }

            Action installPackages = () => {
                _logger?.LogEvent(
                    PythonLogEvent.PackageInstallInfoBar,
                    new PackageInstallInfoBarInfo() {
                        Action = PackageInstallInfoBarActions.Install,
                    }
                );
                PythonProjectNode.InstallRequirementsAsync(_projectNode.Site, pm, txtPath)
                    .HandleAllExceptions(_projectNode.Site, typeof(PackageInstallInfoBar))
                    .DoNotWait();
                _infoBar.Close();
            };

            Action projectIgnore = () => {
                _logger?.LogEvent(
                    PythonLogEvent.PackageInstallInfoBar,
                    new PackageInstallInfoBarInfo() {
                        Action = PackageInstallInfoBarActions.Ignore,
                    }
                );
                _projectNode.SetProjectProperty(PythonConstants.SuppressEnvironmentCreationPrompt, true.ToString());
                _infoBar.Close();
            };

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtInstallPackagesInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(txtPath),
                    _projectNode.Caption,
                    pm.Factory.Configuration.Description
            )));
            actions.Add(new InfoBarButton(Strings.RequirementsTxtInfoBarInstallPackagesAction, installPackages));
            actions.Add(new InfoBarButton(Strings.RequirementsTxtInfoBarProjectIgnoreAction, projectIgnore));

            var infoBarModel = new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true);
            _infoBar = infoBarFactory.CreateInfoBar(infoBarModel);
            infoBarHost.AddInfoBar(_infoBar);

            _infoBar.Advise(this, out uint cookie);
            _adviseCookie = cookie;

            _logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Prompt,
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
