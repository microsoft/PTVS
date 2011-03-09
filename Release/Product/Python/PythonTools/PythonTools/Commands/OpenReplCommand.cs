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
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Repl;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for starting the Python REPL window.
    /// </summary>
    class OpenReplCommand : Command {
        private static Guid _replGuid = new Guid("{ABF3BBD6-DA27-468D-B169-B29243A757C5}");
        private readonly int _cmdId;
        private readonly IPythonInterpreterFactory _factory;

        public OpenReplCommand(int cmdId, IPythonInterpreterFactory factory) {
            _cmdId = cmdId;
            _factory = factory;
        }

        public override void DoCommand(object sender, EventArgs args) {
            var window = (ToolWindowPane)ExecuteInReplCommand.EnsureReplWindow(_factory);

            IVsWindowFrame windowFrame = (IVsWindowFrame)window.Frame;
            ErrorHandler.ThrowOnFailure(windowFrame.Show());
            ((IReplWindow)window).Focus();
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return QueryStatusMethod;
            }
        }

        private void QueryStatusMethod(object sender, EventArgs args) {
            var oleMenu = sender as OleMenuCommand;

            if (_factory == null) {
                oleMenu.Visible = false;
                oleMenu.Enabled = false;
                oleMenu.Supported = false;
            } else {
                oleMenu.Visible = true;
                oleMenu.Enabled = true;
                oleMenu.Supported = true;
                oleMenu.Text = _factory.GetInterpreterDisplay() + " Interactive";
            }
        }
        
        public override int CommandId {
            get { return (int)_cmdId; }
        }
    }
}
