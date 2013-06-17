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

using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ImportWizard {
    public sealed class Wizard : IWizard {
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
                System.Threading.Tasks.Task.Factory.StartNew(() => {
                    object inObj = null, outObj = null;
                    dte.Commands.Raise(GuidList.guidPythonToolsCmdSet.ToString("B"), (int)PkgCmdIDList.cmdidImportWizard, ref inObj, ref outObj);
                });
            }
            throw new WizardCancelledException();
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }
    }
}
