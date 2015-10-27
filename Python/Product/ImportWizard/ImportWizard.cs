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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.


// This assembly is no longer being used for new wizards and is maintained for
// backwards compatibility until we can merge ImportWizard into the main DLL.
//
// All new wizards should be added to Microsoft.PythonTools.ProjectWizards.

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.ImportWizard {
    public sealed class Wizard : IWizard {
        public void BeforeOpeningFile(EnvDTE.ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(EnvDTE.Project project) { }
        public void ProjectItemFinishedGenerating(EnvDTE.ProjectItem projectItem) { }
        public void RunFinished() { }

        private static async void DoNotWait(Task task) {
            await task;
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            try {
                Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
            } catch {
                // If it fails (doesn't exist/contains files/read-only), let the directory stay.
            }

            var dte = automationObject as DTE;
            if (dte == null) {
                var provider = automationObject as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
                if (provider != null) {
                    dte = new ServiceProvider(provider).GetService(typeof(DTE)) as DTE;
                }
            }
            if (dte == null) {
                MessageBox.Show("Unable to start wizard: no automation object available.", "Python Tools for Visual Studio");
            } else {
                DoNotWait(Task.Run(() => {
                    string projName = replacementsDictionary["$projectname$"];
                    string solnName;
                    replacementsDictionary.TryGetValue("$specifiedsolutionname$", out solnName);
                    string directory;
                    if (String.IsNullOrWhiteSpace(solnName)) {
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

                    object inObj = projName + "|" + directory, outObj = null; 
                    dte.Commands.Raise(GuidList.guidPythonToolsCmdSet.ToString("B"), (int)PkgCmdIDList.cmdidImportWizard, ref inObj, ref outObj);
                }));
            }
            throw new WizardCancelledException();
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }
    }
}
