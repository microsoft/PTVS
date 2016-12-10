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
using Microsoft.CookiecutterTools.Infrastructure;
using Microsoft.CookiecutterTools.Model;

namespace Microsoft.CookiecutterTools.Commands {
    /// <summary>
    /// Provides the command for opening the cookiecutter window.
    /// </summary>
    class AddFromCookiecutterCommand : Command {
        private readonly CookiecutterPackage _package;
        private readonly ProjectSystemClient _projectSystem;

        public AddFromCookiecutterCommand(CookiecutterPackage package) {
            _package = package;
            _projectSystem = new ProjectSystemClient(package.DTE);
        }

        public override void DoCommand(object sender, EventArgs args) {
            var location = _projectSystem.GetSelectedFolderProjectLocation();
            _package.NewCookiecutterSession(location);
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;
                    oleMenuCmd.Enabled = _projectSystem.GetSelectedFolderProjectLocation() != null;
                    oleMenuCmd.Visible = oleMenuCmd.Enabled;
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
    }
}
