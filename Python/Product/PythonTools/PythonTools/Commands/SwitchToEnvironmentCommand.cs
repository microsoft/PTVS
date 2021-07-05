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
    class SwitchToEnvironmentCommand : IAsyncCommandRange {
        private readonly IServiceProvider _serviceProvider;
        private readonly EnvironmentSwitcherManager _envSwitchMgr;
        private IPythonInterpreterFactory[] _allFactories;
        private IPythonInterpreterFactory _currentFactory;

        public SwitchToEnvironmentCommand(IServiceProvider serviceProvider) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _envSwitchMgr = serviceProvider.GetPythonToolsService().EnvironmentSwitcherManager;
        }

        public int MaxCount => 64; // 0x4090 - 0x4050

        public CommandStatus GetStatus(int index) {
            _allFactories = _envSwitchMgr.AllFactories.ToArray();
            _currentFactory = _envSwitchMgr.CurrentFactory;

            if (index >= _allFactories.Length) {
                return CommandStatus.SupportedAndInvisible;
            }

            var activeIndex = Array.IndexOf(_allFactories, _currentFactory);

            return index == activeIndex
                ? CommandStatus.SupportedAndEnabled | CommandStatus.Latched
                : CommandStatus.SupportedAndEnabled;
        }

        public string GetText(int index) => _allFactories[index].Configuration.Description;

        public Task InvokeAsync(int index) => _envSwitchMgr.SwitchToFactoryAsync(_allFactories[index]);
    }
}
