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
using Microsoft.PythonTools.Common.Parsing;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Project {
    internal class PythonNotSupportedInfoBar : PythonInfoBar {
        private const string DismissedVersionsCategory = "DismissedPythonVersionWarnings";
        private readonly Func<IPythonInterpreterFactory> _getActiveInterpreterFunc;
        private readonly HashSet<Version> _versionsTriggered = new HashSet<Version>();
        private IPythonInterpreterFactory _interpreterTriggeredInfoBar;
        private readonly string _context;

        public PythonNotSupportedInfoBar(IServiceProvider site, string context, Func<IPythonInterpreterFactory> getActiveInterpreterFunc) : base(site) {
            _getActiveInterpreterFunc = getActiveInterpreterFunc ?? throw new ArgumentNullException(nameof(getActiveInterpreterFunc));
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public override Task CheckAsync() {
            return IsDisposed ? Task.CompletedTask : Site.GetUIThread().InvokeAsync(Check);
        }

        private void Check() {
            if (IsDisposed) {
                return;
            }

            var activeInterpreter = _getActiveInterpreterFunc();
            var version = activeInterpreter?.Configuration?.Version;
            var pythonService = Site.GetPythonToolsService();
            var shouldWarn = activeInterpreter.IsRunnable() &&
                version != null &&
                (version.Major < 3 || version.ToLanguageVersion() == PythonLanguageVersion.None) &&
                pythonService.GeneralOptions.PromptForPythonVersionNotSupported;
            var versionKey = version == null ? null : new Version(version.Major, version.Minor);
            var isDismissed = versionKey != null &&
                pythonService.LoadBool(versionKey.ToString(), DismissedVersionsCategory) == true;

            if (_interpreterTriggeredInfoBar != null &&
                (!shouldWarn ||
                 isDismissed ||
                 activeInterpreter.Configuration.Id != _interpreterTriggeredInfoBar.Configuration.Id ||
                 !versionKey.Equals(GetTriggeredVersion()))) {
                if (IsCreated && !IsVisible) {
                    _versionsTriggered.Remove(GetTriggeredVersion());
                }
                Close();
                _interpreterTriggeredInfoBar = null;
            }

            if (IsCreated ||
                !shouldWarn ||
                isDismissed ||
                !_versionsTriggered.Add(versionKey)
            ) {
                return;
            }

            _interpreterTriggeredInfoBar = activeInterpreter;
            var message = version.Major == 2
                ? Strings.WarningPython2NotSupported
                : Strings.PythonVersionNotSupportedInfoBarText.FormatUI(activeInterpreter.Configuration.Description);
            var infoBarTextSpanMessage = new InfoBarTextSpan(message);
            var infoBarMessage = new List<IVsInfoBarTextSpan> { infoBarTextSpanMessage };
            var actionItems = new List<InfoBarActionItem> {
                new InfoBarHyperlink(
                    Strings.PythonVersionNotSupportedDontShowMessageAgain,
                    (Action)(() => DoNotShowAgainAction(activeInterpreter)))
            };

            LogEvent(PythonVersionNotSupportedInfoBarAction.Prompt);
            Create(new InfoBarModel(infoBarMessage, actionItems, KnownMonikers.StatusInformation, isCloseButtonVisible: true));
        }

        private void DoNotShowAgainAction(IPythonInterpreterFactory interpreter) {
            if (IsDisposed || !IsVisible || _interpreterTriggeredInfoBar != interpreter) {
                return;
            }

            LogEvent(PythonVersionNotSupportedInfoBarAction.Ignore);
            Site.GetPythonToolsService().SaveBool(GetTriggeredVersion().ToString(), DismissedVersionsCategory, true);
            Close();
        }

        private Version GetTriggeredVersion() {
            var version = _interpreterTriggeredInfoBar.Configuration.Version;
            return new Version(version.Major, version.Minor);
        }

        private void LogEvent(string action) {
            Logger?.LogEvent(
                PythonLogEvent.PythonNotSupportedInfoBar,
                new PythonVersionNotSupportedInfoBarInfo {
                    Action = action,
                    Context = _context,
                    PythonVersion = _interpreterTriggeredInfoBar.Configuration.Version.ToString()
                }
            );
        }
    }
}
