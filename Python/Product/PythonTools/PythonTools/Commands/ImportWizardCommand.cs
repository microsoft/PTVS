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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    /// <summary>
    /// Provides the command to import a project from existing code.
    /// </summary>
    class ImportWizardCommand : Command {
        private readonly IServiceProvider _serviceProvider;

        public ImportWizardCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider;
        }

        private async void CreateProjectAndHandleErrors(
            IVsStatusbar statusBar,
            Microsoft.PythonTools.Project.ImportWizard.ImportWizard dlg,
            bool addToExistingSolution
        ) {
            try {
                var path = await dlg.ImportSettings.CreateRequestedProjectAsync();
                if (File.Exists(path)) {
                    object outRef = null, pathRef = ProcessOutput.QuoteSingleArgument(path);
                    _serviceProvider.GetDTE().Commands.Raise(
                        VSConstants.GUID_VSStandardCommandSet97.ToString("B"),
                        addToExistingSolution
                            ? (int)VSConstants.VSStd97CmdID.AddExistingProject
                            : (int)VSConstants.VSStd97CmdID.OpenProject,
                        ref pathRef,
                        ref outRef
                    );
                    statusBar.SetText("");
                    return;
                }
            } catch (UnauthorizedAccessException) {
                MessageBox.Show(Strings.ErrorImportWizardUnauthorizedAccess, Strings.ProductTitle);
            } catch (Exception ex) {
                ActivityLog.LogError(Strings.ProductTitle, ex.ToString());
                MessageBox.Show(Strings.ErrorImportWizardException.FormatUI(ex.GetType().Name), Strings.ProductTitle);
            }
            statusBar.SetText(Strings.StatusImportWizardError);
        }

        public override void DoCommand(object sender, EventArgs args) {
            var statusBar = (IVsStatusbar)_serviceProvider.GetService(typeof(SVsStatusbar));
            statusBar.SetText(Strings.StatusImportWizardStarting);

            string initialProjectPath = null, initialSourcePath = null;
            bool addToExistingSolution = false;

            var oleArgs = args as OleMenuCmdEventArgs;
            if (oleArgs != null) {
                string projectArgs = oleArgs.InValue as string;
                if (projectArgs != null) {
                    var argItems = projectArgs.Split('|');
                    if (argItems.Length == 3) {
                        bool.TryParse(argItems[2], out addToExistingSolution);
                    }
                    if (argItems.Length >= 2) {
                        initialProjectPath = PathUtils.GetAvailableFilename(
                            argItems[1],
                            argItems[0],
                            ".pyproj"
                        );
                        initialSourcePath = argItems[1];
                    }
                }
            }

            var dlg = new Project.ImportWizard.ImportWizard(
                _serviceProvider,
                initialSourcePath,
                initialProjectPath
            );

            if (dlg.ShowModal() ?? false) {
                CreateProjectAndHandleErrors(statusBar, dlg, addToExistingSolution);
            } else {
                statusBar.SetText("");
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidImportWizard; }
        }
    }
}
