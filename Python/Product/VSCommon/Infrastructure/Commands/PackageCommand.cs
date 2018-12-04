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
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace Microsoft.PythonTools.Infrastructure.Commands {
    internal abstract class PackageCommand : OleMenuCommand {
        protected PackageCommand(Guid group, int id) :
            base(OnCommand, null, OnBeforeQueryStatus, new CommandID(group, id)) {
        }

        protected virtual void SetStatus() { }

        protected virtual void Handle() { }

        protected virtual void Handle(object inArg, out object outArg) {
            outArg = null;
            Handle();
        }

        private static void OnBeforeQueryStatus(object sender, EventArgs e) {
            PackageCommand command = sender as PackageCommand;
            command?.SetStatus();
        }

        public static void OnCommand(object sender, EventArgs args) {
            var command = sender as PackageCommand;
            if (command != null) {
                object inArg, outArg;

                var oleArgs = args as OleMenuCmdEventArgs;
                inArg = oleArgs?.InValue;

                command.Handle(inArg, out outArg);

                if (oleArgs != null && oleArgs.OutValue != IntPtr.Zero) {
                    Marshal.GetNativeVariantForObject(outArg, oleArgs.OutValue);
                }
            }
        }
    }
}
