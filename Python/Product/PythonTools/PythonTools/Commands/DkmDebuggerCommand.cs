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

                    var debugger = PythonToolsPackage.GetGlobalService(typeof(SVsShellDebugger)) as IVsDebugger2;
                    if (debugger == null) {
                        return;
                    }

                    IVsEnumGUID enumEngines;
                    debugger.EnumDebugEngines(out enumEngines);

                    bool isDebuggingPython = false, isDebuggingNative = false;
                    var engineGuid = new Guid[1];
                    uint fetched;
                    while (enumEngines.Next(1, engineGuid, out fetched) >= 0 && fetched != 0) {
                        if (engineGuid[0] == AD7Engine.DebugEngineGuid) {
                            isDebuggingPython = true;
                        } else if (engineGuid[0] == DkmEngineId.NativeEng) {
                            isDebuggingNative = true;
                        }
                    }

                    cmd.Visible = isDebuggingPython && isDebuggingNative;
                };
            }
        }
    }
}
