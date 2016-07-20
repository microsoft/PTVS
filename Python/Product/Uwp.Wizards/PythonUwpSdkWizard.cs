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

using EnvDTE;
using Microsoft.PythonTools.Uwp.Interpreter;
using Microsoft.VisualStudio.TemplateWizard;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Microsoft.PythonTools.Uwp.Wizards {
    public sealed class PythonUwpSdkWizard : IWizard {
        public void ProjectFinishedGenerating(EnvDTE.Project project) { }
        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
        public void RunFinished() { }

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams
        ) {
            // Pick the largest installed version
            var version = InstalledPythonUwpInterpreter.GetInterpreters().Max(x => x.Key);

            if (version == null) {
                // Show an error dialog if CPython UWP SDK is not installed
                var msg = "Could not find CPython UWP SDK.  \nPlease download from <http://go.microsoft.com/fwlink/?LinkID=613495&amp;clcid=0x409> and restart Visual Studio.";
                MessageBox.Show(msg, "Python Tools for Visual Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new WizardCancelledException(msg);
            }

            replacementsDictionary.Add("$pythonuwpsdkversion$", version.ToString());
        }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}

