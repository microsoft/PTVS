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

using MSBuild = Microsoft.Build.Evaluation;

namespace Microsoft.PythonTools.Project {

    [Export(typeof(IProjectContextProvider))]
    [Export(typeof(VsProjectContextProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class VsProjectContextProvider : IProjectContextProvider {
        private readonly Dictionary<PythonProjectNode, MSBuild.Project> _projects = new Dictionary<PythonProjectNode, MSBuild.Project>();
        private readonly Dictionary<string, object> _createdFactories = new Dictionary<string, object>();

        [ImportingConstructor]
        public VsProjectContextProvider() {
        }

        public void UpdateProject(PythonProjectNode node, MSBuild.Project project) {
            lock (_projects) {
                if (project == null) {
                    _projects.Remove(node);
                } else if (!_projects.ContainsKey(node) || _projects[node] != project) {
                    _projects[node] = project;
                }
            }

            // Always raise the event, this also occurs when we're adding projects
            // to the MSBuild.Project.
            ProjectsChanaged?.Invoke(this, EventArgs.Empty);
        }

        public void InterpreterLoaded(object context, InterpreterConfiguration configuration) {
            lock (_createdFactories) {
                _createdFactories[configuration.Id] = context;
            }
        }

        public void InterpreterUnloaded(object context, InterpreterConfiguration configuration) {
            lock (_createdFactories) {
                _createdFactories.Remove(configuration.Id);
            }
        }

        public bool IsProjectSpecific(InterpreterConfiguration configuration) {
            lock (_createdFactories) {
                return _createdFactories.ContainsKey(configuration.Id);
            }
        }

        public bool IsProjectSpecific(string id) {
            lock (_createdFactories) {
                return _createdFactories.ContainsKey(id);
            }
        }

        public event EventHandler ProjectsChanaged;
        public event EventHandler<ProjectChangedEventArgs> ProjectChanged;

        public void OnProjectChanged(object project) {
            ProjectChanged?.Invoke(this, new ProjectChangedEventArgs(project));
        }

        public IEnumerable<object> Projects {
            get {
                lock (_projects) {
                    return _projects.Values.ToArray();
                }
            }
        }
    }
}
