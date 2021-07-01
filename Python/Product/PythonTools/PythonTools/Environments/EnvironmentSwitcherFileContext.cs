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
    sealed class EnvironmentSwitcherFileContext : IEnvironmentSwitcherContext
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IInterpreterRegistryService _registryService;
        private readonly string _filePath;

        public EnvironmentSwitcherFileContext(IServiceProvider serviceProvider, string filePath)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _optionsService = serviceProvider.GetComponentModel().GetService<IInterpreterOptionsService>();
            _registryService = serviceProvider.GetComponentModel().GetService<IInterpreterRegistryService>();
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }

        public IEnumerable<IPythonInterpreterFactory> AllFactories => _registryService.Interpreters;

        public IPythonInterpreterFactory CurrentFactory => _optionsService.DefaultInterpreter;

#pragma warning disable CS0067
        public event EventHandler EnvironmentsChanged;
#pragma warning restore CS0067

        public Task ChangeFactoryAsync(IPythonInterpreterFactory factory)
        {
            // For now, we are changing the default interpreter
            // We may want to make it specific to the _filePath instead
            // https://github.com/Microsoft/PTVS/issues/4856
            _optionsService.DefaultInterpreter = factory;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
