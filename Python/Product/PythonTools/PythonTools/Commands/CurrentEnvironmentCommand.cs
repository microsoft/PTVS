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
extern alias OLEInterop;

using System;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Common;
using Microsoft.PythonTools.Environments;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using IServiceProvider = System.IServiceProvider;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Commands {
    class CurrentEnvironmentCommand : OleMenuCommand {
        private readonly IServiceProvider _serviceProvider;
        private readonly EnvironmentSwitcherManager _envSwitchMgr;
        private readonly IPythonToolsLogger _logger;

        public CurrentEnvironmentCommand(IServiceProvider serviceProvider)
            : base(null, null, QueryStatus, new CommandID(CommonGuidList.guidPythonToolsCmdSet, (int)PkgCmdIDList.comboIdCurrentEnvironment)) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
            _logger = serviceProvider.GetService(typeof(IPythonToolsLogger)) as IPythonToolsLogger;
        }

        public override void Invoke(object inArg, IntPtr outArg, OLEInterop::Microsoft.VisualStudio.OLE.Interop.OLECMDEXECOPT options) {
            // getting the current value
            if (outArg != IntPtr.Zero) {
                var text = _envSwitchMgr.CurrentFactory?.Configuration.Description ?? string.Empty;
                Marshal.GetNativeVariantForObject(text, outArg);
            }

            // setting the current value
            if (inArg != null) {
                var text = inArg as string;
                var factory = _envSwitchMgr.AllFactories.SingleOrDefault(f => f.Configuration.Description == text);
                if (factory != null) {
                    _logger?.LogEvent(PythonLogEvent.SelectEnvFromToolbar, new SelectEnvFromToolbarInfo() {
                        InterpreterId = factory.Configuration.Id,
                        Architecture = factory.Configuration.Architecture.ToString(),
                        Version = factory.Configuration.Version.ToString(),
                        IsIronPython = factory.Configuration.IsIronPython(),
                    });

                    SwitchToFactoryAsync(factory).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
                } else {
                    // The special "Add Environment..." entry, or any entry that no longer exists brings up the add dialog
                    _logger?.LogEvent(PythonLogEvent.AddEnvFromToolbar, null);
                    AddEnvironmentCommand
                        .AddEnvironmentAsync(_envSwitchMgr, _serviceProvider, AddEnvironmentDialog.PageKind.VirtualEnvironment)
                        .HandleAllExceptions(_serviceProvider, GetType())
                        .DoNotWait();
                }
            }
        }

        private static void QueryStatus(object sender, EventArgs e) {
            var omc = sender as CurrentEnvironmentCommand;
            if (omc != null) {
                omc.Enabled = omc._envSwitchMgr.IsInPythonMode;
            }
        }

        private async Task SwitchToFactoryAsync(IPythonInterpreterFactory factory) {
            await _envSwitchMgr.SwitchToFactoryAsync(factory);
            var uiShell = _serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
            uiShell?.UpdateCommandUI(0);
        }
    }
}
