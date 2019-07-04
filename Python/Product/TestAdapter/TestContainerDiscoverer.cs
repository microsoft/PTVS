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
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter.Model;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;
using Microsoft.PythonTools.Interpreter;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projectMap;
        private bool _firstLoad, _isDisposed, _forceRefresh, _setupComplete;
        private SolutionEventsListener _solutionListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;
        private readonly HashSet<string> _pytestFrameworkConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pytest.ini" , "setup.cfg" , "tox.ini" };
      

        [ImportingConstructor]
        private TestContainerDiscoverer(
            [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, 
            [Import(typeof(IOperationState))]IOperationState operationState,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider
        ) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _workspaceContextProvider = workspaceContextProvider ?? throw new ArgumentNullException(nameof(workspaceContextProvider));
            _projectMap = new ConcurrentDictionary<string, ProjectInfo>();
            _firstLoad = true;
            _forceRefresh = false;
            _setupComplete = false;

            _solutionListener = new SolutionEventsListener(_serviceProvider);
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;
            _solutionListener.SolutionOpened += OnSolutionLoaded;
            _solutionListener.SolutionClosed += OnSolutionClosed;
            _solutionListener.StartListeningForChanges();

            _testFilesAddRemoveListener = new TestFileAddRemoveListener(_serviceProvider, new Guid());
            _testFilesAddRemoveListener.TestFileChanged += OnProjectItemChanged;
            _testFilesAddRemoveListener.StartListeningForTestFileChanges();
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                if (_solutionListener != null) {
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

        public bool IsWorkspace {
            get {
                return false;
            }
        }

         private bool HasLoadedWorkspace() => _workspaceContextProvider.Workspace != null;

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (!HasLoadedWorkspace()
                    && (_firstLoad || _forceRefresh)) {
                    _projectMap.Clear();

                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad || _forceRefresh) {
                            SetupSolution();
                            _firstLoad = false;
                            _forceRefresh = false;
                        }
                    });
                }

                return _projectMap.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        public TestContainer GetTestContainer(string projectHome, string path) {
            if (_projectMap.TryGetValue(projectHome, out ProjectInfo projectInfo)) {
                if (projectInfo.TryGetContainer(path, out TestContainer container)) {
                    return container;
                }
            }
            return null;
        }

        private void OnSolutionClosed(object sender, EventArgs e) {
            _firstLoad = true;
            _setupComplete = false;
            _projectMap.Clear();

            if (_testFilesUpdateWatcher != null) {
                _testFilesUpdateWatcher.FileChangedEvent -= OnProjectItemChanged;
                _testFilesUpdateWatcher.Dispose();
                _testFilesUpdateWatcher = null;
            }
        }

        private void OnSolutionLoaded(object sender, EventArgs e) {
        }

        private void SetupSolution() {
            // add file watchers before loading projects
            var oldTestFilesUpdateWatcher = _testFilesUpdateWatcher;
            _testFilesUpdateWatcher = new TestFilesUpdateWatcher();
            _testFilesUpdateWatcher.FileChangedEvent += OnProjectItemChanged;
            oldTestFilesUpdateWatcher?.Dispose();

            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));

            // add all source files
            foreach (var project in VsProjectExtensions.EnumerateLoadedProjects(solution)) {
                LoadProject(project);
            }

            _setupComplete = true;
        }

        public ProjectInfo GetProjectInfo(string projectHome) {
            if (projectHome != null 
                &&_projectMap.TryGetValue(projectHome, out ProjectInfo projectInfo)) {
                return projectInfo;
            }
            return null;
        }

        public event EventHandler TestContainersUpdated;

        private void NotifyContainerChanged() {
            // guard against triggering multiple updates during initial load until setup is complete
            if (!_firstLoad){ 
              TestContainersUpdated?.Invoke(this, EventArgs.Empty);
            }
        }
       
        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            if (!_setupComplete)
                return;

            LoadProject(e.Project);
        }

        private void LoadProject(IVsProject  vsProject) {
            var pyProj = PythonProject.FromObject(vsProject);
            if (pyProj == null)
                return;

            bool isEnabled = false;
            try {
                isEnabled = pyProj.GetProperty(PythonConstants.PyTestEnabledSetting).IsTrue();
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.WriteLine("Exception : " + ex.Message);
            }

            if (isEnabled) {
                var projInfo = new ProjectInfo(this, pyProj);
                _projectMap[projInfo.ProjectHome] = projInfo;
                var files = FilteredTestOrSettingsFiles(vsProject);
                UpdateSolutionTestContainersAndFileWatchers(files, projInfo, isAdd: true);
                NotifyContainerChanged();
            }
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project == null)
                return;

            string projectHome = e.Project.GetProjectHome();
            if (projectHome != null
                && _projectMap.TryRemove(projectHome, out ProjectInfo projToRemove)) {

                var files = FilteredTestOrSettingsFiles(e.Project);
                UpdateSolutionTestContainersAndFileWatchers(files, projToRemove, isAdd: false);
                projToRemove.Dispose();

                NotifyContainerChanged();
            }
        }

        private IEnumerable<string> FilteredTestOrSettingsFiles(IVsProject project) {
            return project.GetProjectItems()
                .Where(s => IsTestFileOrSetting(s));
        }

        private void UpdateSolutionTestContainersAndFileWatchers(IEnumerable<string> sources, ProjectInfo projInfo, bool isAdd) {
            foreach (var path in sources) {
                if (isAdd) {
                    projInfo.AddTestContainer(path);
                    _testFilesUpdateWatcher.AddWatch(path);
                } else {
                    projInfo.RemoveTestContainer(path);
                    _testFilesUpdateWatcher.RemoveWatch(path);
                }
            }
        }

        private bool IsTestFile(string path) {
            return Path.GetExtension(path).Equals(PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsTestFileOrSetting(string path) {
            return IsSettingsFile(path) || Path.GetExtension(path).Equals(PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e) {
            if (String.IsNullOrEmpty(e.File)) 
                return;

            if (IsSettingsFile(e.File)) {
                _forceRefresh = true;

                switch (e.ChangedReason) {
                    case TestFileChangedReason.None:
                        break;
                    case TestFileChangedReason.Renamed: // TestFileAddRemoveListener rename triggers Added and Removed
                        break;
                    case TestFileChangedReason.Added:
                        _testFilesUpdateWatcher.AddWatch(e.File); 
                        break;
                    case TestFileChangedReason.Changed:
                        _testFilesUpdateWatcher.AddWatch(e.File);
                        break;
                    case TestFileChangedReason.Removed:
                        _testFilesUpdateWatcher.RemoveWatch(e.File);
                        break;
                }

                NotifyContainerChanged();
                return;
            }

            if (!IsTestFile(e.File))
                return;

            IVsProject vsProject = e.Project;
            if (vsProject == null) {
                var rdt = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
                vsProject = VsProjectExtensions.PathToProject(e.File, rdt);
            }

            if (vsProject == null)
                return;

            string projectHome = vsProject.GetProjectHome();
            if (projectHome != null &&
                _projectMap.TryGetValue(projectHome, out ProjectInfo projectInfo)) {

                var sources = new List<string>() { e.File };

                switch (e.ChangedReason) {
                    case TestFileChangedReason.Added:
                        UpdateSolutionTestContainersAndFileWatchers(sources, projectInfo, isAdd: true);
                        break;
                    case TestFileChangedReason.Changed:
                        //Need to increment version number so Test Explorer notices a change
                        UpdateSolutionTestContainersAndFileWatchers(sources, projectInfo, isAdd: true);
                        break;
                    case TestFileChangedReason.Removed:
                        UpdateSolutionTestContainersAndFileWatchers(sources, projectInfo, isAdd: false);
                        break;
                    default:
                        //In changed case file watcher observed a file changed event
                        //In this case we just have to fire TestContainerChnaged event
                        //TestFileAddRemoveListener rename event triggers Added and Removed so Rename isn't needed
                        break;
                }
                NotifyContainerChanged();
            }
        }

        private bool IsSettingsFile(string file) {
            if (String.IsNullOrEmpty(file))
                return false;

            return _pytestFrameworkConfigFiles.Contains(Path.GetFileName(file));
        }
    }
}