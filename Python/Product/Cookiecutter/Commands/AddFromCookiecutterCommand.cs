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
using System.IO;
using System.Linq;
using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Commands {
    /// <summary>
    /// Provides the command for opening the cookiecutter window.
    /// </summary>
    class AddFromCookiecutterCommand : Command {
        public AddFromCookiecutterCommand() {
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
                var paths = CookiecutterPackage.GetSelectedItemPaths().ToArray();
                if (paths.Length == 1) {
                    var p = paths[0];
                    return Directory.Exists(p) ? p : null;
                }
            } catch (Exception e) when (!e.IsCriticalException()) {
            }

            return null;
        }
    }
}
