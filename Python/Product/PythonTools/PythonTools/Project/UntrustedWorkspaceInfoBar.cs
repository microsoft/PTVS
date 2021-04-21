//// Python Tools for Visual Studio
//// Copyright(c) Microsoft Corporation
//// All rights reserved.
////
//// Licensed under the Apache License, Version 2.0 (the License); you may not use
//// this file except in compliance with the License. You may obtain a copy of the
//// License at http://www.apache.org/licenses/LICENSE-2.0
////
//// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
//// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
//// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
//// MERCHANTABILITY OR NON-INFRINGEMENT.
////
//// See the Apache Version 2.0 License for specific language governing
//// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using Task = System.Threading.Tasks.Task;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.PythonTools.Logging;

namespace Microsoft.PythonTools.Project {
    internal class UntrustedWorkspaceInfoBar : PythonInfoBar {
        private const string _trustedWorkspacesCategory = "TrustedWorkspaces";

        private readonly IPythonWorkspaceContext _workspace;
        private readonly PythonToolsService _pts;

        public UntrustedWorkspaceInfoBar(IServiceProvider site, IPythonWorkspaceContext workspace)
            : base(site) {

            _workspace = workspace;
            _pts = site.GetPythonToolsService();
        }

        public async override Task CheckAsync() {
            if (IsCreated) {
                return;
            }
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            var isTrusted = _pts.LoadBool(_workspace.Location, _trustedWorkspacesCategory);
            if (isTrusted != null) {
                _workspace.IsTrusted = isTrusted.Value;
                return;
            }

            var infoBarMessage = new List<IVsInfoBarTextSpan> {
                 new InfoBarTextSpan(Strings.UntrustedWorkspaceInfoBarText)
            };
            var actionItems = new List<InfoBarActionItem> {
                new InfoBarHyperlink(Strings.AlwaysTrustWorkspaceInfoBarAction, (Action)(() => Trust(UntrustedWorkspaceInfoBarAction.AlwaysTrust, trust: true, persist: true))),
                new InfoBarHyperlink(Strings.TrustOnceWorkspaceInfoBarAction, (Action)(() => Trust(UntrustedWorkspaceInfoBarAction.TrustOnce, trust: true, persist: false))),
                new InfoBarHyperlink(Strings.DontTrustWorkspaceInfoBarAction, (Action)(() => Trust(UntrustedWorkspaceInfoBarAction.DontTrust, trust: false, persist: false))),
            };

            Logger?.LogEvent(
                PythonLogEvent.UntrustedWorkspaceInfoBar,
                new PythonVersionNotSupportedInfoBarInfo {
                    Action = UntrustedWorkspaceInfoBarAction.Prompt,
                }
            );

            Create(new InfoBarModel(infoBarMessage, actionItems, KnownMonikers.StatusSecurityWarning));
        }

        private void Trust(string action, bool trust, bool persist) {
            Logger?.LogEvent(
                PythonLogEvent.UntrustedWorkspaceInfoBar,
                new PythonVersionNotSupportedInfoBarInfo { Action = action }
            );

            _workspace.IsTrusted = trust;
            if (persist) {
                _pts.SaveBool(_workspace.Location, _trustedWorkspacesCategory, true);
            }
            Close();
        }

        internal static void ClearTrustedWorkspaces(PythonToolsService pts) {
            pts.DeleteCategory(_trustedWorkspacesCategory);
        }
    }
}
