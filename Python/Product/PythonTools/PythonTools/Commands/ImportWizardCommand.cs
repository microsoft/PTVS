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
using System.Windows;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using SR = Microsoft.PythonTools.Project.SR;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to import a project from existing code.
    /// </summary>
    class ImportWizardCommand : Command {
        private async void CreateProjectAndHandleErrors(
            IVsStatusbar statusBar,
            Microsoft.PythonTools.Project.ImportWizard.ImportWizard dlg
        ) {
            try {
                var path = await dlg.ImportSettings.CreateRequestedProjectAsync();
                if (File.Exists(path)) {
                    object outRef = null, pathRef = ProcessOutput.QuoteSingleArgument(path);
                    PythonToolsPackage.Instance.DTE.Commands.Raise(
                        VSConstants.GUID_VSStandardCommandSet97.ToString("B"),
                        (int)VSConstants.VSStd97CmdID.OpenProject,
                        ref pathRef,
                        ref outRef
                    );
                    statusBar.SetText("");
                    return;
                }
            } catch (UnauthorizedAccessException) {
                MessageBox.Show(SR.GetString(SR.ErrorImportWizardUnauthorizedAccess), SR.ProductName);
            } catch (Exception ex) {
                ActivityLog.LogError(SR.ProductName, ex.ToString());
                MessageBox.Show(SR.GetString(SR.ErrorImportWizardException, ex.GetType().Name), SR.ProductName);
            }
            statusBar.SetText(SR.GetString(SR.StatusImportWizardError));
        }

        public override void DoCommand(object sender, EventArgs args) {
            var statusBar = (IVsStatusbar)CommonPackage.GetGlobalService(typeof(SVsStatusbar));
            statusBar.SetText(SR.GetString(SR.StatusImportWizardStarting));

            var dlg = new Microsoft.PythonTools.Project.ImportWizard.ImportWizard();

            var oleArgs = args as Microsoft.VisualStudio.Shell.OleMenuCmdEventArgs;
            if (oleArgs != null) {
                string projectArgs = oleArgs.InValue as string;
                if (projectArgs != null) {
                    var argItems = projectArgs.Split('|');
                    if (argItems.Length == 2) {
                        dlg.ImportSettings.ProjectPath = CommonUtils.GetAvailableFilename(
                            argItems[1],
                            argItems[0],
                            ".pyproj"
                        );
                        dlg.ImportSettings.SourcePath = argItems[1];
                    }
                }
            } 
            
            if (dlg.ShowModal() ?? false) {
                CreateProjectAndHandleErrors(statusBar, dlg);
            } else {
                statusBar.SetText("");
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidImportWizard; }
        }
    }
}
