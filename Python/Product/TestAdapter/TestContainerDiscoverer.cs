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
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter.Model;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;

using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        
        private readonly Dictionary<string, ProjectInfo> _projectInfo;
        private bool _firstLoad, _isDisposed;
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri _ExecutorUri = new Uri(ExecutorUriString);

        private readonly SolutionEventsListener _solutionListener;
        //private readonly TestFilesUpdateWatcher _testFilesUpdateWatcher;

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _projectInfo = new Dictionary<string, ProjectInfo>();

            _solutionListener = new SolutionEventsListener(serviceProvider);
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;
         

            //_testFilesUpdateWatcher = new TestFilesUpdateWatcher();

            _firstLoad = true;
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                _solutionListener.Dispose();
            //    _testFilesUpdateWatcher.Dispose();
            }
        }

        public Uri ExecutorUri {
            get {
                return _ExecutorUri;
            }
        }

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (_firstLoad) {
                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad) {
                            _firstLoad = false;
                            // Get current solution
                            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
                            foreach (var project in VsProjectExtensions.EnumerateLoadedProjects(solution)) {
                                OnProjectLoaded(null, new ProjectEventArgs(project));
                            }
                            _solutionListener.StartListeningForChanges();
                        }
                    });
                }

                return _projectInfo.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        public TestContainer GetTestContainer(string projectHome, string path) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(projectHome, out projectInfo)) {
                TestContainer container;
                if (projectInfo.TryGetContainer(path, out container)) {
                    return container;
                }
            }

            return null;
        }

        public LaunchConfiguration GetLaunchConfigurationOrThrow(string projectHome) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(projectHome, out projectInfo)) {
                return projectInfo.GetLaunchConfigurationOrThrow();
            }
            return null;
        }

        public void NotifyChanged() {
            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler TestContainersUpdated;

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            OnProjectLoadedAsync(e.Project).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async Task OnProjectLoadedAsync(IVsProject project) {
            var pyProj = PythonProject.FromObject(project);
            if (pyProj != null) {

                var sources = project.GetProjectItems();
                var projInfo = new ProjectInfo(this, pyProj, sources);
                projInfo.UpdateTestCases();
                _projectInfo[pyProj.ProjectHome] = projInfo;
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                var pyProj = PythonProject.FromObject(e.Project);
                ProjectInfo events;
                if (pyProj != null &&
                    _projectInfo.TryGetValue(pyProj.ProjectHome, out events) &&
                    _projectInfo.Remove(pyProj.ProjectHome)) {
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}
