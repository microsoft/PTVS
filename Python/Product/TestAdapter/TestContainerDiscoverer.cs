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
using System.IO;
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
        private SolutionEventsListener _solutionListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _projectInfo = new Dictionary<string, ProjectInfo>();
            _firstLoad = true;

            _solutionListener = new SolutionEventsListener(serviceProvider);
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;
         
            _testFilesUpdateWatcher = new TestFilesUpdateWatcher();
            _testFilesUpdateWatcher.FileChangedEvent += OnProjectItemChanged;

            _testFilesAddRemoveListener = new TestFileAddRemoveListener(serviceProvider, new Guid());
            _testFilesAddRemoveListener.TestFileChanged += OnProjectItemChanged;
            _testFilesAddRemoveListener.StartListeningForTestFileChanges();
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                if(_solutionListener != null) {
                    _solutionListener.ProjectLoaded -= OnProjectLoaded;
                    _solutionListener.ProjectUnloading -= OnProjectUnloaded;
                    _solutionListener.ProjectClosing -= OnProjectUnloaded;
                    _solutionListener.Dispose();
                }

                if (_testFilesUpdateWatcher != null) {
                    _testFilesUpdateWatcher.FileChangedEvent -= OnProjectItemChanged;
                    _testFilesUpdateWatcher.Dispose();
                    _testFilesUpdateWatcher = null;
                }

                if (_testFilesAddRemoveListener != null) {
                    _testFilesAddRemoveListener.TestFileChanged -= OnProjectItemChanged;
                    _testFilesAddRemoveListener.Dispose();
                    _testFilesAddRemoveListener = null;
                }
            }
        }

        public Uri ExecutorUri {
            get {
                return PythonConstants.ExecutorUri;
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

        public ProjectInfo GetProjectInfo(string projectHome) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(projectHome, out projectInfo)) {
                return projectInfo;
            }
            return null;
        }

        private void NotifyContainerChanged() {
            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler TestContainersUpdated;

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            OnProjectLoadedAsync(e.Project).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async Task OnProjectLoadedAsync(IVsProject project) {
            var pyProj = PythonProject.FromObject(project);
            if (pyProj != null 
                && pyProj.GetProperty(PythonConstants.PyTestEnabledSetting).IsTrue()) {

                var sources = project.GetProjectItems();
                var projInfo = new ProjectInfo(this, pyProj);
                UpdateTestContainers(sources, projInfo, isAdd:true);
                _projectInfo[pyProj.ProjectHome] = projInfo;
            }

            NotifyContainerChanged();
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

            NotifyContainerChanged();
        }

        private void UpdateTestContainers(IEnumerable<string> sources, ProjectInfo projInfo, bool isAdd) {
            foreach (var path in sources) {

                if (!Path.GetExtension(path).Equals(PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                if (isAdd) {
                    projInfo.AddTestContainer(path);
                    _testFilesUpdateWatcher.AddWatch(path);
                }
                else {
                    projInfo.RemoveTestContainer(path);
                    _testFilesUpdateWatcher.RemoveWatch(path);
                }
            }

            NotifyContainerChanged();
        }

        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e) {
            if(String.IsNullOrEmpty(e.File)) {
                return;
            }
  
            IVsProject vsProject = e.Project;
            if(vsProject == null) {
                var rdt = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
                vsProject = VsProjectExtensions.PathToProject(e.File, rdt);
            }

            var pyProj = PythonProject.FromObject(vsProject);
            if (pyProj != null &&
                _projectInfo.TryGetValue(pyProj.ProjectHome, out ProjectInfo projectInfo)) {

                var sources = new List<string>() { e.File };

                switch (e.ChangedReason) {
                    case TestFileChangedReason.None:
                        break;
                    case TestFileChangedReason.Renamed: // rename triggers Added and Removed
                        break;
                    case TestFileChangedReason.Added:
                        UpdateTestContainers(sources, projectInfo, isAdd: true);
                        break;
                    case TestFileChangedReason.Changed:
                        UpdateTestContainers(sources, projectInfo, isAdd: true);
                        break;
                    case TestFileChangedReason.Removed:
                        UpdateTestContainers(sources, projectInfo, isAdd: false);
                        break;
                }
            }
        }
    }
}