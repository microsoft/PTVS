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
    internal class AsyncCommandRangeToOleMenuCommandShim : PackageCommand {
        private readonly IAsyncCommandRange _commandRange;
        private readonly int _maxCount;

        public AsyncCommandRangeToOleMenuCommandShim(Guid group, int id, IAsyncCommandRange commandRange) : base(group, id) {
            _commandRange = commandRange ?? throw new ArgumentNullException(nameof(commandRange));
            _maxCount = commandRange.MaxCount;
        }

        protected override void SetStatus() {
            var index = MatchedCommandId - CommandID.ID;
            if (index >= _maxCount) {
                Visible = false;
                Enabled = false;
                MatchedCommandId = 0;
                return;
            }

            if (MatchedCommandId == 0) {
                index = 0;
            }

            var status = _commandRange.GetStatus(index);

            Supported = status.HasFlag(CommandStatus.Supported);
            Enabled = status.HasFlag(CommandStatus.Enabled);
            Visible = !status.HasFlag(CommandStatus.Invisible);
            Checked = status.HasFlag(CommandStatus.Latched);

            if (Visible) {
                Text = _commandRange.GetText(index);
            }

            MatchedCommandId = 0;
        }

        protected override void Handle(object inArg, out object outArg) {
            outArg = null;
            if (Checked) {
                return;
            }

            var index = MatchedCommandId == 0 ? 0 : MatchedCommandId - CommandID.ID;
            if (index < 0 || index >= _maxCount) {
                MatchedCommandId = 0;
                return;
            }

            _commandRange.InvokeAsync(index).DoNotWait();
        }

        public override bool DynamicItemMatch(int cmdId) {
            var index = cmdId - CommandID.ID;

            if (index >= 0 && index < _maxCount) {
                MatchedCommandId = cmdId;
                return true;
            }

            MatchedCommandId = 0;
            return false;
        }
    }
}
