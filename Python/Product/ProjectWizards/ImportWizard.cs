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


// This assembly is no longer being used for new wizards and is maintained for
// backwards compatibility until we can merge ImportWizard into the main DLL.
//
// All new wizards should be added to Microsoft.PythonTools.ProjectWizards.
extern alias OLEInterop;

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Microsoft.PythonTools.Common;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class ImportWizard : IWizard {
        public void BeforeOpeningFile(EnvDTE.ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(EnvDTE.Project project) { }
        public void ProjectItemFinishedGenerating(EnvDTE.ProjectItem projectItem) { }
        public void RunFinished() { }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            try {
                Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
            } catch {
                // If it fails (doesn't exist/contains files/read-only), let the directory stay.
            }

            var oleProvider = automationObject as OLEInterop::Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
            if (oleProvider == null) {
                MessageBox.Show("Unable to start wizard: no automation object available.", "Visual Studio");
                throw new WizardBackoutException();
            }

            using (var serviceProvider = new ServiceProvider(oleProvider)) {
                int hr = EnsurePackageLoaded(serviceProvider);
                if (ErrorHandler.Failed(hr)) {
                    MessageBox.Show(string.Format("Unable to start wizard: failed to load Python support Package (0x{0:X08})", hr), "Visual Studio");
                    throw new WizardBackoutException();
                }
                var uiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));

                // Exclusive = new solution
                // Non-exclusive = add to existing solution
                replacementsDictionary.TryGetValue("$exclusiveproject$", out string exclusiveText);
                if (!bool.TryParse(exclusiveText, out bool exclusive)) {
                    exclusive = false;
                }

                string projName = replacementsDictionary["$projectname$"];
                string solnName;
                replacementsDictionary.TryGetValue("$specifiedsolutionname$", out solnName);
                string directory;
                if (String.IsNullOrWhiteSpace(solnName) || !exclusive) {
                    // Create directory is unchecked, destinationdirectory is the
                    // directory name the user entered plus the project name, we want
                    // to remove the project name.
                    directory = Path.GetDirectoryName(replacementsDictionary["$destinationdirectory$"]);
                } else {
                    // Create directory is checked, the destinationdirectory is the
                    // directory the user entered plus the project name plus the
                    // solution name - we want to remove both extra folders
                    directory = Path.GetDirectoryName(Path.GetDirectoryName(replacementsDictionary["$destinationdirectory$"]));
                }

                object inObj = projName + "|" + directory + "|" + (!exclusive).ToString();
                var guid = CommonGuidList.guidPythonToolsCmdSet;
                hr = uiShell.PostExecCommand(ref guid, PkgCmdIDList.cmdidImportWizard, 0, ref inObj);
                if (ErrorHandler.Failed(hr)) {
                    MessageBox.Show(string.Format("Unable to start wizard: Unexpected error 0x{0:X08}", hr), "Visual Studio");
                }
            }
            throw new WizardCancelledException();
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }

        private static int EnsurePackageLoaded(IServiceProvider serviceProvider) {
            var shell = (IVsShell)serviceProvider.GetService(typeof(SVsShell));

            var pkgGuid = CommonGuidList.guidPythonToolsPackage;
            if (ErrorHandler.Failed(shell.IsPackageLoaded(ref pkgGuid, out var pkg)) || pkg == null) {
                return shell.LoadPackage(ref pkgGuid, out pkg);
            }
            return VSConstants.S_OK;
        }
    }
}
