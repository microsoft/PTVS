/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Project {
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

        private void OnInterpreterFactoriesChanged() {
            var evt = InterpreterFactoriesChanged;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }

        public event EventHandler InterpreterFactoriesChanged;
    }
}
