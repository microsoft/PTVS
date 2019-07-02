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
using Task = System.Threading.Tasks.Task;
using Microsoft.PythonTools.Interpreter;
using System.Threading;
using System.Collections.Concurrent;
using System.Diagnostics.Eventing.Reader;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projectMap;
        private bool _firstLoad, _isDisposed, _forceRefresh;
        private SolutionEventsListener _solutionListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private TestFilesUpdateWatcher _workspaceUpdateWatcher;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;
        private readonly HashSet<string> _pytestConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pytest.ini" , "setup.cfg" , "tox.ini", "PythonSettings.json" };
        private readonly Timer _deferredChangeNotification;
      

        [ImportingConstructor]
        private TestContainerDiscoverer(
            [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, 
            [Import(typeof(IOperationState))]IOperationState operationState,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider
        ) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _projectMap = new ConcurrentDictionary<string, ProjectInfo>();
            _firstLoad = true;
            _forceRefresh = false;
            _deferredChangeNotification = new Timer(OnDeferredNotifyChanged);
            _workspaceContextProvider = workspaceContextProvider ?? throw new ArgumentNullException(nameof(workspaceContextProvider));
            _workspaceContextProvider.WorkspaceClosed += OnWorkspaceClosed;
            _workspaceContextProvider.WorkspaceInitialized += OnWorkspaceLoaded;
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;

                _deferredChangeNotification.Dispose();

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

                if (_workspaceUpdateWatcher != null) {
                    _workspaceUpdateWatcher.FileChangedEvent -= OnWorkspaceFileChanged;
                    _workspaceUpdateWatcher.Dispose();
                    _workspaceUpdateWatcher = null;
                }

                if (_testFilesAddRemoveListener != null) {
                    _testFilesAddRemoveListener.TestFileChanged -= OnProjectItemChanged;
                    _testFilesAddRemoveListener.Dispose();
                    _testFilesAddRemoveListener = null;
                }

                if (_workspaceContextProvider != null) {
                    _workspaceContextProvider.WorkspaceClosed -= OnWorkspaceClosed;
                    _workspaceContextProvider.WorkspaceInitialized -= OnWorkspaceLoaded;
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
                return (_workspaceContextProvider != null) 
                    && (_workspaceContextProvider.Workspace != null);
            }
        }

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (_firstLoad || _forceRefresh) {
                    _projectMap.Clear();

                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad || _forceRefresh) {
                            var workspace = _workspaceContextProvider.Workspace;
                            if (workspace != null) {
                                SetupWorkspace(workspace);
                            } else {
                                SetupCurrentSolution();
                            }

                            _firstLoad = false;
                            _forceRefresh = false;
                        }
                    });
                }

                return _projectMap.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        public TestContainer GetTestContainer(string projectHome, string path) {
            ProjectInfo projectInfo;
            if (_projectMap.TryGetValue(projectHome, out projectInfo)) {
                TestContainer container;
                if (projectInfo.TryGetContainer(path, out container)) {
                    return container;
                }
            }

            return null;
        }

        private void OnSolutionClosed(object sender, EventArgs e) {
            _firstLoad = true;
            if (_testFilesUpdateWatcher != null) {
                _testFilesUpdateWatcher.FileChangedEvent -= OnProjectItemChanged;
                _testFilesUpdateWatcher.Dispose();
                _testFilesUpdateWatcher = null;
            }

            if (_solutionListener != null) {
                _solutionListener.ProjectLoaded -= OnProjectLoaded;
                _solutionListener.ProjectUnloading -= OnProjectUnloaded;
                _solutionListener.ProjectClosing -= OnProjectUnloaded;
                _solutionListener.Dispose();
            }
        }

        private void OnSolutionLoaded(object sender, EventArgs e) {
        }

        private void OnWorkspaceClosed(object sender, PythonWorkspaceContextEventArgs e) {
            _firstLoad = true;
            if (_workspaceUpdateWatcher != null) {
                _workspaceUpdateWatcher.FileChangedEvent -= OnWorkspaceFileChanged;
                _workspaceUpdateWatcher.Dispose();
                _workspaceUpdateWatcher = null;
            }
            var projInfo = (_workspaceContextProvider.Workspace != null) ? GetProjectInfo(_workspaceContextProvider.Workspace.Location) : null;
            if (projInfo == null)
                return;

            if (_workspaceContextProvider.Workspace != null
                && _projectMap.TryRemove(_workspaceContextProvider.Workspace.Location, out ProjectInfo projToRemove)) {

                projToRemove.Dispose();
            }
        }

        private void OnWorkspaceLoaded(object sender, PythonWorkspaceContextEventArgs e) {
        }

        private void SetupCurrentSolution() {
            // Get current solution
            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));

            //var tasks = VsProjectExtensions.EnumerateLoadedProjects(solution)
            //    .Select(proj => OnProjectLoadedAsync(proj)).ToList();
            //await Task.WhenAll(tasks);

            foreach (var project in VsProjectExtensions.EnumerateLoadedProjects(solution)) {
                OnProjectLoaded(null, new ProjectEventArgs(project));
            }

            var oldSolutionListener = _solutionListener;
            _solutionListener = new SolutionEventsListener(_serviceProvider);
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;
            _solutionListener.SolutionOpened += OnSolutionLoaded;
            _solutionListener.SolutionClosed += OnSolutionClosed;
            _solutionListener.StartListeningForChanges();
            oldSolutionListener?.Dispose();

            var oldFileListener = _testFilesAddRemoveListener;
            _testFilesAddRemoveListener = new TestFileAddRemoveListener(_serviceProvider, new Guid());
            _testFilesAddRemoveListener.TestFileChanged += OnProjectItemChanged;
            _testFilesAddRemoveListener.StartListeningForTestFileChanges();
            oldFileListener?.Dispose();
        }

        private void SetupWorkspace(IPythonWorkspaceContext workspace) {
            var projInfo = new ProjectInfo(this, workspace);
            _projectMap[projInfo.ProjectHome] = projInfo;

            var oldWatcher = _workspaceUpdateWatcher;
            _workspaceUpdateWatcher = new TestFilesUpdateWatcher();
            _workspaceUpdateWatcher.FileChangedEvent += OnWorkspaceFileChanged;
            _workspaceUpdateWatcher.AddDirectoryWatch(workspace.Location);
            oldWatcher?.Dispose();

            var files = Directory.EnumerateFiles(workspace.Location, "*.*", SearchOption.AllDirectories);
            foreach (var file in files) {
                projInfo.AddTestContainer(file);
            }
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
            if (!_firstLoad && !_forceRefresh) { 
              TestContainersUpdated?.Invoke(this, EventArgs.Empty);
            }
            //try {
            //    _deferredChangeNotification.Change(100, Timeout.Infinite);
            //} catch (ObjectDisposedException) {
            //}
        }
       
        private void OnDeferredNotifyChanged(object state) {
            //TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            var pyProj = PythonProject.FromObject(e.Project);
            if (pyProj != null
                && pyProj.GetProperty(PythonConstants.PyTestEnabledSetting).IsTrue()) {

                var projInfo = new ProjectInfo(this, pyProj);
                _projectMap[projInfo.ProjectHome] = projInfo;

                if (_testFilesUpdateWatcher == null) {
                    _testFilesUpdateWatcher = new TestFilesUpdateWatcher();
                    _testFilesUpdateWatcher.FileChangedEvent += OnProjectItemChanged;
                }

                var files = FilteredTestOrSettingsFiles(e.Project);
                UpdateSolutionTestContainersAndFileWatchers(files, projInfo, isAdd: true);
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

            return _pytestConfigFiles.Contains(Path.GetFileName(file));
        }

        private void OnWorkspaceFileChanged(object sender, TestFileChangedEventArgs e) {
            if (String.IsNullOrEmpty(e.File))
                return;

            if (IsSettingsFile(e.File)) {
                _forceRefresh = true;
                NotifyContainerChanged();
                return;
            }

            if (!IsTestFile(e.File))
                return;

            IPythonWorkspaceContext workspace = _workspaceContextProvider.Workspace;
            if (workspace == null)
                return;

            var projInfo = GetProjectInfo(workspace.Location);
            if (projInfo == null)
                return;

            switch (e.ChangedReason) {
                case TestFileChangedReason.Added:
                    projInfo.AddTestContainer(e.File);
                    break;
                case TestFileChangedReason.Changed:
                    projInfo.AddTestContainer(e.File);
                    break;
                case TestFileChangedReason.Removed:
                    projInfo.RemoveTestContainer(e.File);
                    break;
                case TestFileChangedReason.Renamed:
                    projInfo.RemoveTestContainer(e.OldFile);
                    projInfo.AddTestContainer(e.File);
                    break;
                default:
                    //In changed case file watcher observed a file changed event
                    //In this case we just have to fire TestContainerChnaged event
                    break;
            }
            NotifyContainerChanged();
        }
    }
}