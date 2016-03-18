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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
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
            bool changed = false;
            lock (_projects) {
                if (project == null) {
                    changed = _projects.Remove(node);
                } else if (!_projects.ContainsKey(node) || _projects[node] != project) {
                    _projects[node] = project;
                    changed = true;
                }
            }

            if (changed) {
                ProjectsChanaged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void InterpreterLoaded(object context, InterpreterConfiguration configuration) {
            lock(_createdFactories) {
                _createdFactories[configuration.Id] = context;
            }
        }

        public void InterpreterUnloaded(object context, InterpreterConfiguration configuration) {
            lock(_createdFactories) {
                _createdFactories.Remove(configuration.Id);
            }
        }

        public bool IsProjectSpecific(InterpreterConfiguration configuration) {
            lock(_createdFactories) {
                return _createdFactories.ContainsKey(configuration.Id);
            }
        }

        public bool IsProjectSpecific(string id) {
            lock (_createdFactories) {
                return _createdFactories.ContainsKey(id);
            }
        }

        public event EventHandler ProjectsChanaged;

        public IEnumerable<object> Projects {
            get {
                lock(_projects) {
                    return _projects.Values.ToArray();
                }
            }
        }
    }

#if FALSE
    [Guid(GuidList.guidLoadedProjectInterpreterFactoryProviderString)]
    [Export(typeof(IPythonInterpreterFactoryProvider))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    sealed class LoadedProjectInterpreterFactoryProvider : IPythonInterpreterFactoryProvider, IDisposable {
        private SolutionEventsListener _listener;
        private readonly Dictionary<IPythonInterpreterFactoryProvider, IVsProject> _providers;

        public LoadedProjectInterpreterFactoryProvider() {
            _providers = new Dictionary<IPythonInterpreterFactoryProvider, IVsProject>();
        }

        public void Dispose() {
            if (_listener != null) {
                _listener.Dispose();
            }
        }

        public void SetSolution(IVsSolution solution) {
            if (_listener != null) {
                throw new InvalidOperationException("Cannot set solution multiple times");
            }

            if (solution != null) {
                _listener = new SolutionEventsListener(solution);
                _listener.ProjectLoaded += Solution_ProjectLoaded;
                _listener.ProjectClosing += Solution_ProjectUnloading;
                _listener.ProjectUnloading += Solution_ProjectUnloading;
                _listener.StartListeningForChanges();

                lock (_providers) {
                    foreach (var project in solution.EnumerateLoadedProjects()
                        .Select(p => p.GetPythonProject())
                        .Where(p => p != null)) {
                        _providers[project.Interpreters] = project;
                    }
                }
            }
        }

        private void Solution_ProjectLoaded(object sender, ProjectEventArgs e) {
            var project = e.Project.GetPythonProject();
            if (project != null) {
                ProjectLoaded(project.Interpreters, project);
            }
        }

        private void Solution_ProjectUnloading(object sender, ProjectEventArgs e) {
            var project = e.Project.GetPythonProject();
            if (project != null) {
                ProjectUnloaded(project.Interpreters);
            }
        }

        internal void ProjectLoaded(IPythonInterpreterFactoryProvider provider, IVsProject project) {
            if (provider == null) {
                return;
            }

            lock (_providers) {
                if (!_providers.ContainsKey(provider)) {
                    _providers[provider] = project;
                    provider.InterpreterFactoriesChanged += Interpreters_InterpreterFactoriesChanged;
                }
            }

            OnInterpreterFactoriesChanged();
        }

        internal void ProjectUnloaded(IPythonInterpreterFactoryProvider provider) {
            if (provider == null) {
                return;
            }

            lock (_providers) {
                if (_providers.Remove(provider)) {
                    provider.InterpreterFactoriesChanged -= Interpreters_InterpreterFactoriesChanged;
                }
            }

            OnInterpreterFactoriesChanged();
        }

        void Interpreters_InterpreterFactoriesChanged(object sender, EventArgs e) {
            OnInterpreterFactoriesChanged();
        }

        public IVsProject GetProject(IPythonInterpreterFactory factory) {
            lock (_providers) {
                foreach (var kv in _providers) {
                    var pif = kv.Key as MSBuildProjectInterpreterFactoryProvider;
                    if (pif != null) {
                        if (pif.Contains(factory)) {
                            return kv.Value;
                        }
                    } else if (kv.Key.GetInterpreterFactories().Contains(factory)) {
                        return kv.Value;
                    }
                }
            }
            return null;
        }

        public IEnumerable<IPythonInterpreterFactory> GetInterpreterFactories() {
            IPythonInterpreterFactoryProvider[] providers;
            lock (_providers) {
                providers = _providers.Keys.ToArray();
            }

            foreach (var pif in providers) {
                var msb = pif as MSBuildProjectInterpreterFactoryProvider;
                if (msb != null) {
                    foreach (var f in msb.GetProjectSpecificInterpreterFactories()) {
                        yield return f;
                    }
                } else {
                    foreach (var f in pif.GetInterpreterFactories()) {
                        yield return f;
                    }
                }
            }
        }

        public IEnumerable<InterpreterConfiguration> GetInterpreterConfigurations() {
            return GetInterpreterFactories().Select(x => x.Configuration);
        }

        public IPythonInterpreterFactory GetInterpreterFactory(string id) {
            return GetInterpreterFactories()
                .Where(x => x.Configuration.Id == id)
                .FirstOrDefault();
        }

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public event EventHandler InterpreterFactoriesChanged;

    }
#endif
}
