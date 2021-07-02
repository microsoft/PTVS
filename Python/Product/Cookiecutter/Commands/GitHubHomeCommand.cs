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

using Microsoft.CookiecutterTools.Infrastructure;

namespace Microsoft.CookiecutterTools.Commands
{
    /// <summary>
    /// Provides the command for opening the cookiecutter window.
    /// </summary>
    class GitHubCommand : Command
    {
        private readonly CookiecutterToolWindow _window;
        private readonly int _commandId;

        public GitHubCommand(CookiecutterToolWindow window, int commandId)
        {
            _window = window;
            _commandId = commandId;
        }

        public override void DoCommand(object sender, EventArgs args)
        {
            _window.NavigateToGitHub(_commandId);
        }

        public override EventHandler BeforeQueryStatus
        {
            get
            {
                return (sender, args) =>
                {
                    var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;
                    oleMenuCmd.Enabled = (_window.CanNavigateToGitHub());
                };
            }
        }

        public override int CommandId
        {
            get { return _commandId; }
        }
    }
}
