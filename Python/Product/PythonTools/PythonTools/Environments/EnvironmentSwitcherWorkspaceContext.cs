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

namespace Microsoft.PythonTools.Environments
{
    sealed class EnvironmentSwitcherWorkspaceContext : IEnvironmentSwitcherContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IInterpreterRegistryService _registryService;
        private readonly IPythonWorkspaceContext _pythonWorkspace;

        public EnvironmentSwitcherWorkspaceContext(IServiceProvider serviceProvider, IPythonWorkspaceContext pythonWorkspace)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _pythonWorkspace = pythonWorkspace ?? throw new ArgumentNullException(nameof(pythonWorkspace));
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _pythonWorkspace.ActiveInterpreterChanged += OnActiveInterpreterChanged;
        }

        public IEnumerable<IPythonInterpreterFactory> AllFactories => _registryService.Interpreters;

        public IPythonInterpreterFactory CurrentFactory => Workspace.CurrentFactory;

        public IPythonWorkspaceContext Workspace => _pythonWorkspace;

        public event EventHandler EnvironmentsChanged;

        public async Task ChangeFactoryAsync(IPythonInterpreterFactory factory)
        {
            await _pythonWorkspace.SetInterpreterFactoryAsync(factory);
        }

        private void OnActiveInterpreterChanged(object sender, EventArgs e)
        {
            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _pythonWorkspace.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
        }
    }
}
