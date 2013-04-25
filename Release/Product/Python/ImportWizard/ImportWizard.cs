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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ImportWizard {
    public sealed class ImportSettings {
        public string SourceFilesPath { get; set; }
        public string StartupFile { get; set; }
        public string Filter { get; set; }
        public string[] SearchPaths { get; set; }
        public string InterpreterId { get; set; }
        public string InterpreterVersion { get; set; }
        public bool DjangoSupport { get; set; }
    }

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

            var provider = new ServiceProvider((Microsoft.VisualStudio.OLE.Interop.IServiceProvider)automationObject);
            var dte = provider.GetService(typeof(DTE)) as DTE;
            if (dte != null) {
                System.Threading.Tasks.Task.Factory.StartNew(() => dte.ExecuteCommand("Tools.ImportPythonProject"));
            }
            throw new WizardCancelledException();
        }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}
