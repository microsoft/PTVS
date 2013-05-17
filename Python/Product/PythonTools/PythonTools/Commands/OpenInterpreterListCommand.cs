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
using System.Windows;
using Microsoft.PythonTools.InterpreterList;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command for opening the interpreter list.
    /// </summary>
    class OpenInterpreterListCommand : Command {

        public override void DoCommand(object sender, EventArgs args) {
            var window = PythonToolsPackage.Instance.FindWindowPane(typeof(InterpreterListToolWindow), 0, true) as ToolWindowPane;
            if (window != null) {
                var frame = window.Frame as IVsWindowFrame;
                if (frame != null) {
                    ErrorHandler.ThrowOnFailure(frame.Show());
                }
                var content = window.Content as UIElement;
                if (content != null) {
                    content.Focus();
                }
            }
        }

        public string Description {
            get {
                return "Python Interpreters";
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidInterpreterList; }
        }
    }
}
