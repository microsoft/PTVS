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

using Microsoft.PythonTools.Environments;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Commands {
    class CurrentEnvironmentListCommand : OleMenuCommand {
        private readonly IServiceProvider _serviceProvider;
        private readonly EnvironmentSwitcherManager _envSwitchMgr;

        public CurrentEnvironmentListCommand(IServiceProvider serviceProvider)
            : base(null, new CommandID(GuidList.guidPythonToolsCmdSet, (int)PkgCmdIDList.comboIdCurrentEnvironmentList)) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
        }

        public override void Invoke(object inArg, IntPtr outArg, OLECMDEXECOPT options) {
            var envs = _envSwitchMgr.AllFactories
                .Select(f => f.Configuration.Description)
                .OrderBy(desc => desc)
                .Append(Strings.AddEnvironmentComboListEntry)
                .ToArray();
            Marshal.GetNativeVariantForObject(envs, outArg);
        }
    }
}
