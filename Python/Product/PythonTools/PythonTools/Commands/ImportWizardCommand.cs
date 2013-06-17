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
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to import a project from existing code.
    /// </summary>
    class ImportWizardCommand : Command {
        public override void DoCommand(object sender, EventArgs args) {
            var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
            statusBar.SetText("Importing project...");

            var dlg = new Microsoft.PythonTools.Project.ImportWizard.ImportWizard();
            if (dlg.ShowModal() ?? false) {
                var settings = dlg.ImportSettings;

                settings.CreateRequestedProjectAsync()
                    .ContinueWith(t => {
                        var path = t.Result;
                        if (!string.IsNullOrEmpty(path) && File.Exists(path)) {
                            object outRef = null, pathRef = path;
                            PythonToolsPackage.Instance.DTE.Commands.Raise(VSConstants.GUID_VSStandardCommandSet97.ToString("B"), (int)VSConstants.VSStd97CmdID.OpenProject, ref pathRef, ref outRef);
                        }
                        statusBar.SetText("");
                    }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            } else {
                statusBar.SetText("");
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidImportWizard; }
        }
    }
}
