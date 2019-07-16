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
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projectMap;
        private bool _firstLoad, _isDisposed, _isRefresh, _setupComplete;
        private SolutionEventsListener _solutionListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;
        private readonly HashSet<string> _pytestFrameworkConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pytest.ini", "setup.cfg", "tox.ini" };


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
            _isRefresh = false;
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

                _projectMap.Clear();

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

        private bool IsSolutionMode() => _workspaceContextProvider.Workspace == null;

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (IsSolutionMode()
                    && (_firstLoad || _isRefresh)) {
                    _projectMap.Clear();

                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad || _isRefresh) {
                            SetupSolution();
                            _firstLoad = false;
                            _isRefresh = false;
                        }
                    });
                }

                return _projectMap.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        private void OnSolutionClosed(object sender, EventArgs e) {
            ResetSolution();
        }
       
        /// <summary>
        /// This can be triggered before our class is created so it isn't being used
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnSolutionLoaded(object sender, EventArgs e) {
        }

        private void SetupSolution() {
            // create file watcher before loading projects
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

        private void ResetSolution() {
            _firstLoad = true;
            _isRefresh = false;
            _setupComplete = false;
            _projectMap.Clear();

            if (_testFilesUpdateWatcher != null) {
                _testFilesUpdateWatcher.FileChangedEvent -= OnProjectItemChanged;
                _testFilesUpdateWatcher.Dispose();
                _testFilesUpdateWatcher = null;
            }
        }


        public ProjectInfo GetProjectInfo(string projectHome) {
            if (projectHome != null
                && _projectMap.TryGetValue(projectHome, out ProjectInfo projectInfo)) {
                return projectInfo;
            }
            return null;
        }

        public event EventHandler TestContainersUpdated;

        private void NotifyContainerChanged() {
            // guard against triggering multiple updates during initial load until setup is complete
            if (!_firstLoad && !_isRefresh) {
                TestContainersUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            // We delay project test discovery until test explorer requests tests.
            // But if a project is loaded after SetupSolution() runs, we still need to update.
            if (!_setupComplete)
                return;

            LoadProject(e.Project);
        }

        private void LoadProject(IVsProject vsProject) {
            // check for python projects
            var pyProj = PythonProject.FromObject(vsProject);
            if (pyProj == null)
                return;

            // always register for project property changes
            pyProj.ProjectPropertyChanged -= OnTestPropertiesChanged;
            pyProj.ProjectPropertyChanged += OnTestPropertiesChanged;

            TestFrameworkType testFrameworkType = GetTestFramework(pyProj);

            if (testFrameworkType != TestFrameworkType.None) {
                IVsHierarchy hierarchy = (IVsHierarchy)vsProject;
                var projectName = hierarchy == null ? hierarchy.GetNameProperty() : string.Empty;
                var projInfo = new ProjectInfo(pyProj, projectName);
                _projectMap[projInfo.ProjectHome] = projInfo;
                var files = FilteredTestOrSettingsFiles(vsProject);
                UpdateSolutionTestContainersAndFileWatchers(files, projInfo, isAdd: true);
                NotifyContainerChanged();
            }
        }

        private static TestFrameworkType GetTestFramework(PythonProject pyProj) {
            var testFrameworkType = TestFrameworkType.None;
            try {
                string testFrameworkStr = pyProj.GetProperty(PythonConstants.TestFrameworkSetting);
                if (Enum.TryParse<TestFrameworkType>(testFrameworkStr, ignoreCase: true, out TestFrameworkType parsedFramworked)) {
                    testFrameworkType = parsedFramworked;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.WriteLine("Exception : " + ex.Message);
            }

            return testFrameworkType;
        }

        private void OnTestPropertiesChanged(object sender, PythonProjectPropertyChangedArgs e) {
            // Need to special case the changing of testframeworks
            // We want to clear test containers and watchers except still listen for project setting changes ie. ProjectPropertyChanged
            if (e.PropertyName == PythonConstants.TestFrameworkSetting) {
                var pythonProj = (PythonProject)sender;
                if (pythonProj != null) {
                    RemoveTestContainersAndNotify(pythonProj.ProjectHome);
                }
            }
            else {
                NotifyContainerChanged();
            }
            _isRefresh = true;
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project == null)
                return;

            RemoveTestContainersAndNotify(e.Project.GetProjectHome());

            // Unregister Properties handler
            var pyProj = PythonProject.FromObject(e.Project);
            if (pyProj != null) {
                pyProj.ProjectPropertyChanged -= OnTestPropertiesChanged;
            }
        }

        private void RemoveTestContainersAndNotify(string projectHome) {
            if (projectHome != null
                && _projectMap.TryRemove(projectHome, out ProjectInfo projToRemove)) {

                var testFilesToRemove = projToRemove.GetAllContainers().Select(t => t.Source);
                UpdateSolutionTestContainersAndFileWatchers(testFilesToRemove, projToRemove, isAdd: false);
                projToRemove.Dispose();

                NotifyContainerChanged();
            }
        }

        private IEnumerable<string> FilteredTestOrSettingsFiles(IVsProject project) {
            return project.GetProjectItems()
                .Where(s => IsTestFileOrSetting(s));
        }

        public void UpdateSolutionTestContainersAndFileWatchers(IEnumerable<string> sources, ProjectInfo projInfo, bool isAdd) {
            foreach (var path in sources) {
                if (isAdd) {
                    projInfo.AddTestContainer(this, path);
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
                _isRefresh = true;
                return;
            }

            if (!IsTestFile(e.File))
                return;

            // bschnurr todo: this is only looking at opened files
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