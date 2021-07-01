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

namespace Microsoft.PythonTools.Commands
{
    using DebuggerOptions = Microsoft.PythonTools.Debugger.Concord.DebuggerOptions;

    internal class UsePythonStepping : DkmDebuggerCommand
    {
        public UsePythonStepping(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        public override int CommandId
        {
            get { return (int)PkgCmdIDList.cmdidUsePythonStepping; }
        }

        protected override bool IsPythonDeveloperCommand
        {
            get { return true; }
        }

        public override EventHandler BeforeQueryStatus
        {
            get
            {
                return (sender, args) =>
                {
                    base.BeforeQueryStatus(sender, args);
                    var cmd = (OleMenuCommand)sender;
                    cmd.Checked = DebuggerOptions.UsePythonStepping;
                };
            }
        }

        public override void DoCommand(object sender, EventArgs args)
        {
            DebuggerOptions.UsePythonStepping = !DebuggerOptions.UsePythonStepping;
        }
    }
}
