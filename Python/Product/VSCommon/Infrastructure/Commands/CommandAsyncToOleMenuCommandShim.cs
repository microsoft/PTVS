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

namespace Microsoft.PythonTools.Infrastructure.Commands {
    internal class CommandAsyncToOleMenuCommandShim : PackageCommand {
        private readonly IAsyncCommand _command;

        public CommandAsyncToOleMenuCommandShim(Guid group, int id, IAsyncCommand command)
            : base(group, id) {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        protected override void SetStatus() {
            var status = _command.Status;
            Supported = status.HasFlag(CommandStatus.Supported);
            Enabled = status.HasFlag(CommandStatus.Enabled);
            Visible = !status.HasFlag(CommandStatus.Invisible);
            Checked = status.HasFlag(CommandStatus.Latched);
        }

        protected override void Handle(object inArg, out object outArg) {
            outArg = null;
            _command.InvokeAsync().DoNotWait();
        }
    }
}
