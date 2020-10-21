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

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class MsPythonConfigWizard : IWizard {
        private const string _configFileName = "mspythonconfig.json";

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(Project project) { }
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }

        public void RunFinished() { }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            if (!(automationObject is DTE2 dte)) {
                return;
            }

            var projectPath = TryGetProjectPath(dte);
            if (string.IsNullOrEmpty(projectPath)) {
                replacementsDictionary.TryGetValue("$solutiondirectory$", out projectPath);
            }

            if (string.IsNullOrEmpty(projectPath)) {
                return;
            }

            var fileName = Path.Combine(projectPath, _configFileName);
            if (File.Exists(fileName)) {
                MessageBox.Show(Strings.ConfigFileAlreadyExists, Strings.ProductTitle, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
            } else {
                File.WriteAllText(fileName,
@"{
    ""$schema"": ""https://pvsc.blob.core.windows.net/mspythonconfig-schema/pyrightconfig-schema.json""
}
");
            }
            OpenFile(dte, fileName);
        }

        public bool ShouldAddProjectItem(string filePath) => false;

        private static string TryGetProjectPath(DTE2 dte) {
            var items = (Array)dte.ToolWindows.SolutionExplorer.SelectedItems;

            foreach (UIHierarchyItem selItem in items) {
                if (selItem.Object is ProjectItem item && item.Properties != null) {
                    if (item.Kind.Equals(Constants.vsProjectItemKindPhysicalFolder, StringComparison.OrdinalIgnoreCase)) {
                        return item.Properties.Item("FullPath").Value.ToString();
                    }

                    if (item.Kind.Equals(Constants.vsProjectItemKindPhysicalFile, StringComparison.OrdinalIgnoreCase)) {
                        return Path.GetDirectoryName(item.Properties.Item("FullPath").Value.ToString());
                    }
                } else if(selItem.Object is Project p && p.Properties != null) {
                    return p.Properties.Item("FullPath").Value.ToString();
                }
            }
            return null;
        }

        private static void OpenFile(DTE2 dte, string fileName) {
            var serviceProvider = new ServiceProvider(dte as VisualStudio.OLE.Interop.IServiceProvider);
            VsShellUtilities.OpenDocument(serviceProvider, fileName);
            Command command = dte.Commands.Item("SolutionExplorer.SyncWithActiveDocument");
            if (command.IsAvailable) {
                dte.Commands.Raise(command.Guid, command.ID, null, null);
            }
            dte.ActiveDocument.Activate();
        }
    }
}