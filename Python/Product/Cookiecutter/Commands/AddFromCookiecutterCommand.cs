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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Commands {
    /// <summary>
    /// Provides the command for opening the cookiecutter window.
    /// </summary>
    class AddFromCookiecutterCommand : Command {
        private EnvDTE80.DTE2 _dte;

        public AddFromCookiecutterCommand() {
            _dte = CookiecutterPackage.Instance.DTE;
        }

        public override void DoCommand(object sender, EventArgs args) {
            string targetFolder = GetTargetFolder();
            CookiecutterPackage.Instance.NewCookiecutterSession(targetFolder);
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;
                    oleMenuCmd.Enabled = !string.IsNullOrEmpty(GetTargetFolder());
                };
            }
        }

        public string Description {
            get {
                // Not used
                return string.Empty;
            }
        }

        public override int CommandId {
            get { return (int)PackageIds.cmdidAddFromCookiecutter; }
        }

        private string GetTargetFolder() {
            try {
                var paths = GetSelectedItemPaths().ToArray();
                if (paths.Length == 1) {
                    var p = paths[0];
                    return Directory.Exists(p) ? p : null;
                }
            } catch (Exception e) when (!e.IsCriticalException()) {
            }

            return null;
        }

        private IEnumerable<string> GetSelectedItemPaths() {
            var items = (Array)_dte.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (EnvDTE.UIHierarchyItem selItem in items) {
                var item = selItem.Object as EnvDTE.ProjectItem;
                if (item != null && item.Properties != null) {
                    yield return item.Properties.Item("FullPath").Value.ToString();
                }

                var proj = selItem.Object as EnvDTE.Project;
                if (proj != null) {
                    var projFolder = GetProjectFolder(proj);
                    if (!string.IsNullOrEmpty(projFolder)) {
                        yield return projFolder;
                    }
                }
            }
        }

        private static string GetProjectFolder(EnvDTE.Project proj) {
            try {
                // Python and C# projects
                if (proj.Properties != null) {
                    return proj.Properties.Item("FullPath").Value.ToString();
                }
            } catch (ArgumentException) {
                // C++ project
                try {
                    if (proj.Object != null) {
                        return proj.Object.ProjectDirectory;
                    }
                } catch (Exception) {
                }
            }

            return null;
        }
    }
}
