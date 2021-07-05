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

using Microsoft.PythonTools.Project;

namespace Microsoft.PythonTools.Environments {
    sealed class EnvironmentSwitcherProjectContext : IEnvironmentSwitcherContext {
        private readonly PythonProjectNode _project;

        public EnvironmentSwitcherProjectContext(PythonProjectNode project) {
            _project = project ?? throw new ArgumentNullException(nameof(project));
            _project.ActiveInterpreterChanged += OnSettingsChanged;
            _project.InterpreterFactoriesChanged += OnSettingsChanged;
        }

        public IEnumerable<IPythonInterpreterFactory> AllFactories => Project.InterpreterFactories;

        public IPythonInterpreterFactory CurrentFactory => Project.GetPythonInterpreterFactory();

        public PythonProjectNode Project => _project;

        public event EventHandler EnvironmentsChanged;

        public Task ChangeFactoryAsync(IPythonInterpreterFactory factory) {
            Project.SetInterpreterFactory(factory);
            return Task.CompletedTask;
        }

        private void OnSettingsChanged(object sender, EventArgs e) {
            Debug.Assert(sender == Project);
            EnvironmentsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() {
            Project.ActiveInterpreterChanged -= OnSettingsChanged;
            Project.InterpreterFactoriesChanged -= OnSettingsChanged;
        }
    }
}
