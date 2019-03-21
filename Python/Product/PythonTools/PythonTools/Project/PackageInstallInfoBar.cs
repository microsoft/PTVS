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
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal sealed class PackageInstallInfoBar : PythonProjectInfoBar {
        public PackageInstallInfoBar(IServiceProvider site, PythonProjectNode projectNode)
            : base(site, projectNode) {
        }

        public PackageInstallInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site, workspace) {
        }

        public override async Task CheckAsync() {
            if (IsCreated) {
                return;
            }

            var options = Site.GetPythonToolsService().InterpreterOptionsService;

            if (!Site.GetPythonToolsService().GeneralOptions.PromptForPackageInstallation) {
                return;
            }

            if (IsSuppressed(PythonConstants.SuppressPackageInstallationPrompt)) {
                return;
            }

            var txtPath = Project?.GetRequirementsTxtPath() ?? Workspace?.GetRequirementsTxtPath();
            if (!File.Exists(txtPath)) {
                return;
            }

            if (Project?.IsActiveInterpreterGlobalDefault ?? Workspace?.IsCurrentFactoryDefault ?? false) {
                return;
            }

            var active = Project?.ActiveInterpreter ?? Workspace?.CurrentFactory;
            if (!active.IsRunnable()) {
                return;
            }

            var pm = options.GetPackageManagers(active).FirstOrDefault(p => p.UniqueKey == "pip");
            if (pm == null) {
                return;
            }

            var missing = await PackagesMissingAsync(pm, txtPath);
            if (!missing) {
                return;
            }

            ShowInfoBar(txtPath, pm);
        }

        private void ShowInfoBar(string txtPath, IPackageManager pm) {
            var context = Project != null ? InfoBarContexts.Project : InfoBarContexts.Workspace;
            var projectOrWorkspaceName = Project?.Caption ?? Workspace?.WorkspaceName ?? string.Empty;

            Action installPackages = () => InstallPackages(context, txtPath, pm);
            Action projectIgnore = () => Ignore(context);

            var messages = new List<IVsInfoBarTextSpan>();
            var actions = new List<InfoBarActionItem>();

            messages.Add(new InfoBarTextSpan(
                Strings.RequirementsTxtInstallPackagesInfoBarMessage.FormatUI(
                    PathUtils.GetFileOrDirectoryName(txtPath),
                    projectOrWorkspaceName,
                    pm.Factory.Configuration.Description
            )));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarInstallPackagesAction, installPackages));
            actions.Add(new InfoBarHyperlink(Strings.RequirementsTxtInfoBarProjectIgnoreAction, projectIgnore));

            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Prompt,
                    Context = context,
                }
            );

            Create(new InfoBarModel(messages, actions, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void Ignore(string context) {
            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Ignore,
                    Context = context,
                }
            );
            SuppressAsync(PythonConstants.SuppressPackageInstallationPrompt)
                .HandleAllExceptions(Site, typeof(PackageInstallInfoBar))
                .DoNotWait();
            Close();
        }

        private void InstallPackages(string context, string txtPath, IPackageManager pm) {
            Logger?.LogEvent(
                PythonLogEvent.PackageInstallInfoBar,
                new PackageInstallInfoBarInfo() {
                    Action = PackageInstallInfoBarActions.Install,
                    Context = context,
                }
            );
            PythonProjectNode.InstallRequirementsAsync(Site, pm, txtPath)
                .HandleAllExceptions(Site, typeof(PackageInstallInfoBar))
                .DoNotWait();
            Close();
        }

        private async Task<bool> PackagesMissingAsync(IPackageManager pm, string txtPath) {
            try {
                var original = File.ReadAllLines(txtPath);
                var installed = await pm.GetInstalledPackagesAsync(CancellationTokens.After15s);
                return PipRequirementsUtils.AnyPackageMissing(original, installed);
            } catch (IOException) {
            } catch (OperationCanceledException) {
            }

            return false;
        }
    }
}
