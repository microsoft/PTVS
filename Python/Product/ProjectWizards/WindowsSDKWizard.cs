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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.IO;
using Microsoft.PythonTools.ProjectWizards.Properties;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudioTools;
using Microsoft.Win32;
using Project = EnvDTE.Project;
using ProjectItem = EnvDTE.ProjectItem;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class WindowsSDKWizard : IWizard {
        public void ProjectFinishedGenerating(Project project) {}
        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
        public void RunFinished() { }

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams
        ) {
            string winSDKVersion = string.Empty;
            try {
                string keyValue = string.Empty;
                // Attempt to get the installation folder of the Windows 10 SDK
                RegistryKey key = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows Kits\Instlled Roots");
                if (null != key) {
                    keyValue = (string)key.GetValue("KitsRoot10") + "Include";
                }
                // Get the latest SDK version from the name of the directory in the Include path of the SDK installation.
                if (string.IsNullOrEmpty(keyValue)) {
                    string dirName = Directory.GetDirectories(keyValue).OrderByDescending(x => x).FirstOrDefault();
                    winSDKVersion = Path.GetFileName(dirName);
                }
            } catch(Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }
            }
            
            if(string.IsNullOrEmpty(winSDKVersion)){
                winSDKVersion = "10.0.0.0"; // Default value to put in project file
            }

            replacementsDictionary.Add("$winsdkversion$", winSDKVersion);
            replacementsDictionary.Add("$winsdkminversion$", winSDKVersion);
        }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}
