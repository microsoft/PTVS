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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to debug script from a document tab.
    /// </summary>
    class DebugAsScriptCommand : Command {
        public override void DoCommand(object sender, EventArgs args) {
            var file = CommonPackage.GetActiveTextView().GetFilePath();

            // Launch with project context if there is one and it contains the active document
            // Fallback to using default python project
            var pythonProjectNode = CommonPackage.GetStartupProject() as PythonProjectNode;
            if ((pythonProjectNode != null) && (pythonProjectNode.FindChild(file) == null)) {
                pythonProjectNode = null;
            }
            IPythonProject pythonProject = pythonProjectNode as IPythonProject ?? new DefaultPythonProject(file);

            var launcher = PythonToolsPackage.GetLauncher(pythonProject);
            launcher.LaunchFile(file, true);
        }

        public override int? EditFilterQueryStatus(ref VisualStudio.OLE.Interop.OLECMD cmd, IntPtr pCmdText) {
            var activeView = CommonPackage.GetActiveTextView();
            if (activeView != null && activeView.TextBuffer.ContentType.IsOfType(PythonCoreConstants.ContentType)) {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
            } else {
                cmd.cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE);
            }

            return VSConstants.S_OK;
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = false;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidDebugAsScript; }
        }
    }
}
