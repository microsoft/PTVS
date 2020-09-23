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

namespace Microsoft.PythonTools.Environments {
    sealed class EnvironmentSwitcherWorkspaceContext : IEnvironmentSwitcherContext {
        private readonly IInterpreterRegistryService _registryService;

        public EnvironmentSwitcherWorkspaceContext(IServiceProvider serviceProvider, IPythonWorkspaceContext pythonWorkspace) {
            Workspace = pythonWorkspace ?? throw new ArgumentNullException(nameof(pythonWorkspace));
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            Workspace.ActiveInterpreterChanged += OnActiveInterpreterChanged;
        }

        public IEnumerable<IPythonInterpreterFactory> AllFactories => _registryService.Interpreters;

        public IPythonInterpreterFactory CurrentFactory => Workspace.CurrentFactory;

        public IPythonWorkspaceContext Workspace { get; }

        public event EventHandler EnvironmentsChanged;

        public async Task ChangeFactoryAsync(IPythonInterpreterFactory factory) {
            await Workspace.SetInterpreterFactoryAsync(factory);
        }

        private void OnActiveInterpreterChanged(object sender, EventArgs e) {
            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
            Workspace.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
        }
    }
}
