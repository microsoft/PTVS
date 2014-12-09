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
using Microsoft.PythonTools.DkmDebugger;
using Microsoft.VisualStudio.Shell;


namespace Microsoft.PythonTools.Commands {
    using DebuggerOptions = Microsoft.PythonTools.DkmDebugger.DebuggerOptions;

    internal class ShowPythonViewCommand : DkmDebuggerCommand {
        public ShowPythonViewCommand(IServiceProvider serviceProvider)
            : base(serviceProvider) {
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidShowPythonView; }
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    base.BeforeQueryStatus(sender, args);
                    var cmd = (OleMenuCommand)sender;
                    cmd.Checked = DebuggerOptions.ShowPythonViewNodes;
                };
            }
        }

        public override void DoCommand(object sender, EventArgs args) {
            DebuggerOptions.ShowPythonViewNodes = !DebuggerOptions.ShowPythonViewNodes;

            // A hackish way to force debugger to refresh its views, so that our EE is requeried and can use the new option value.
            var debugger = _serviceProvider.GetDTE().Debugger;
            debugger.HexDisplayMode = debugger.HexDisplayMode;
        }
    }
}
