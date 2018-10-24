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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Commands {
    class ViewEnvironmentStatusCommand : Command {
        private readonly PythonToolsPackage _package;

        public ViewEnvironmentStatusCommand(PythonToolsPackage package) {
            _package = package;
        }

        public override void DoCommand(object sender, EventArgs args) {
            _package.ViewEnvironmentStatus();
        }

        public override EventHandler BeforeQueryStatus {
            get {
                return (sender, args) => {
                    ((OleMenuCommand)sender).Visible = false;
                    ((OleMenuCommand)sender).Supported = true;
                    ((OleMenuCommand)sender).Enabled = true;
                };
            }
        }

        public override int CommandId {
            get { return (int)PkgCmdIDList.cmdidViewEnvironmentStatus; }
        }
    }
}
