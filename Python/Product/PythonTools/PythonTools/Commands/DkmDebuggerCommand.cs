/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Linq;
using Microsoft.PythonTools.Debugger.DebugEngine;
using Microsoft.PythonTools.DkmDebugger;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace Microsoft.PythonTools.Commands {
    internal abstract class DkmDebuggerCommand : Command {
        private const string PythonDeveloperRegistryValue = "PythonDeveloper";

        protected virtual bool IsPythonDeveloperCommand {
            get { return false; }
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    var cmd = (OleMenuCommand)sender;
                    cmd.Visible = false;

                    if (IsPythonDeveloperCommand) {
                        var key = PythonToolsPackage.UserRegistryRoot.OpenSubKey(PythonCoreConstants.BaseRegistryKey);
                        if (key != null) {
                            var value = key.GetValue(PythonDeveloperRegistryValue, 0) as int?;
                            if (value == null || value == 0) {
                                return;
                            }
                        }
                    }

                    var engineSettings = DkmEngineSettings.FindSettings(DkmEngineId.NativeEng);
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
