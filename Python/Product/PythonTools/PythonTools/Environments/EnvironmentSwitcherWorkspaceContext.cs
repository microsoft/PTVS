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
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Settings;

namespace Microsoft.PythonTools.Environments {
    sealed class EnvironmentSwitcherWorkspaceContext : IEnvironmentSwitcherContext {
        private readonly IServiceProvider _serviceProvider;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly IWorkspace _workspace;
        private readonly IWorkspaceSettingsManager _workspaceSettingsMgr;

        public EnvironmentSwitcherWorkspaceContext(IServiceProvider serviceProvider, IWorkspace workspace) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _optionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            _workspaceSettingsMgr = _workspace.GetSettingsManager();
            _workspaceSettingsMgr.OnWorkspaceSettingsChanged += OnSettingsChanged;
        }

        public IEnumerable<IPythonInterpreterFactory> AllFactories => _registryService.Interpreters;

        public IPythonInterpreterFactory CurrentFactory => _workspace.GetInterpreterFactory(_registryService, _optionsService);

        public IWorkspace Workspace => _workspace;

        public event EventHandler EnvironmentsChanged;

        public async Task ChangeFactoryAsync(IPythonInterpreterFactory factory) {
            await _workspace.SetInterpreterFactoryAsync(factory);
        }

        private Task OnSettingsChanged(object sender, EventArgs e) {
            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Dispose() {
            _workspaceSettingsMgr.OnWorkspaceSettingsChanged -= OnSettingsChanged;
        }
    }
}
