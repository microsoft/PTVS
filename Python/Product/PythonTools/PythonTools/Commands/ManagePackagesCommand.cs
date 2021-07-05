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
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Commands {
    class ManagePackagesCommand : IAsyncCommand {
        private readonly IServiceProvider _serviceProvider;
        private readonly EnvironmentSwitcherManager _envSwitchMgr;
        private readonly IInterpreterOptionsService _optionsService;

        public ManagePackagesCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
            _optionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
        }

        public CommandStatus Status {
            get {
                return _envSwitchMgr.CurrentFactory != null ? CommandStatus.SupportedAndEnabled : CommandStatus.Supported;
            }
        }

        public async Task InvokeAsync() {
            var factory = _envSwitchMgr.CurrentFactory;
            if (factory == null) {
                return;
            }

            var pm = _optionsService.GetPackageManagers(factory).FirstOrDefault();
            if (pm == null) {
                return;
            }

            await InterpreterList.InterpreterListToolWindow.OpenAtAsync(
                _serviceProvider,
                factory,
                typeof(EnvironmentsList.PipExtensionProvider)
            );
        }
    }
}
