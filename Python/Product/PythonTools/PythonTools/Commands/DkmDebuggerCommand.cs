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
using System.Linq;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    internal abstract class DkmDebuggerCommand : Command {
        private const string PythonDeveloperRegistryValue = "PythonDeveloper";
        internal readonly IServiceProvider _serviceProvider;

        public DkmDebuggerCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        protected virtual bool IsPythonDeveloperCommand {
            get { return false; }
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    var cmd = (OleMenuCommand)sender;
                    cmd.Visible = false;

                    if (IsPythonDeveloperCommand) {
                        var settings = (IVsSettingsManager)_serviceProvider.GetService(typeof(SVsSettingsManager));
                        IVsSettingsStore store;
                        if (ErrorHandler.Succeeded(settings.GetReadOnlySettingsStore((uint)__VsEnclosingScopes.EnclosingScopes_UserSettings, out store))) {
                            int value;                            
                            if (ErrorHandler.Failed(store.GetIntOrDefault(PythonCoreConstants.BaseRegistryKey, PythonDeveloperRegistryValue, 0, out value))) {
                                return;
                            }

                            if (value == 0) {
                                return;
                            }
                        }
                    }

                    DkmEngineSettings engineSettings = null;
                    try {
                        engineSettings = DkmEngineSettings.FindSettings(DkmEngineId.NativeEng);
                    } catch (ObjectDisposedException) {
                    }

                    if (engineSettings == null) {
                        // Native debugger is not loaded at all, so this is either pure Python debugging or something else entirely.
                        return;
                    }

                    // Check if any processes being debugged as native also have the Python engine loaded, and enable command if so.
                    cmd.Visible = engineSettings.GetProcesses().Any(process => process.DebugLaunchSettings.EngineFilter.Contains(AD7Engine.DebugEngineGuid));
                };
            }
        }
    }
}
