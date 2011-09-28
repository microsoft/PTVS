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
using Microsoft.VisualStudio.Shell;
using Microsoft.PythonTools.Refactoring;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to send selected text from a buffer to the remote REPL window.
    /// </summary>
    class RemoveImportsCommand : Command {
        public override void DoCommand(object sender, EventArgs args) {
            new ImportRemover(CommonPackage.GetActiveTextView(), true).RemoveImports();
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = true;
                    ((OleMenuCommand)sender).Supported = true;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidRemoveImports; }
        }
    }
}
