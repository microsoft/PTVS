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
using EnvDTE;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class AzureDebugWebConfigWizard : IWizard {
        public void BeforeOpeningFile(ProjectItem projectItem) { }

        public void ProjectFinishedGenerating(Project project) { }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem) {
            if (!projectItem.Name.Equals("web.debug.config", StringComparison.OrdinalIgnoreCase)) {
                return;
            }

            var projectDir = PathUtils.GetParent(projectItem.get_FileNames(0));

            // Also copy Microsoft.PythonTools.WebRole.dll and ptvsd into the project
            var ptvsdSource = PythonToolsInstallPath.TryGetFile("ptvsd\\__init__.py", GetType().Assembly);
            var ptvsdDest = PathUtils.GetAbsoluteDirectoryPath(projectDir, "ptvsd");
            if (File.Exists(ptvsdSource) && !Directory.Exists(ptvsdDest)) {
                Directory.CreateDirectory(ptvsdDest);
                var sourceDir = PathUtils.GetParent(ptvsdSource);
                foreach (var file in PathUtils.EnumerateFiles(sourceDir, pattern: "*.py" , fullPaths: false)) {
                    var destFile = PathUtils.GetAbsoluteFilePath(ptvsdDest, file);
                    if (!Directory.Exists(PathUtils.GetParent(destFile))) {
                        Directory.CreateDirectory(PathUtils.GetParent(destFile));
                    }
                    
                    File.Copy(PathUtils.GetAbsoluteFilePath(sourceDir, file), destFile, true);
                }

                projectItem.ContainingProject.ProjectItems.AddFromDirectory(PathUtils.TrimEndSeparator(ptvsdDest));
            }


            var webRoleSource = PythonToolsInstallPath.TryGetFile("Microsoft.PythonTools.WebRole.dll", GetType().Assembly);
            if (File.Exists(webRoleSource)) {
                ProjectItem binFolderItem;
                try {
                    binFolderItem = projectItem.ContainingProject.ProjectItems.Item("bin");
                } catch (ArgumentException) {
                    binFolderItem = projectItem.ContainingProject.ProjectItems.AddFolder("bin");
                }

                binFolderItem?.ProjectItems.AddFromFileCopy(webRoleSource);
            }
        }

        public void RunFinished() { }

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams
        ) {
            replacementsDictionary["$secret$"] = Path.GetRandomFileName().Replace(".", "");
        }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}
