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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscovererProject))]
    class TestContainerDiscovererProject : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projectMap;
        private readonly PackageManagerEventSink _packageManagerEventSink;
        private bool _firstLoad, _isDisposed, _isRefresh, _setupComplete;
        private SolutionEventsListener _solutionListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;
        private readonly Timer _deferredTestChangeNotification;

        [ImportingConstructor]
        private TestContainerDiscovererProject(
            [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
            [Import(typeof(IOperationState))]IOperationState operationState,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider,
            [Import] IInterpreterOptionsService interpreterOptionsService
        ) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _workspaceContextProvider = workspaceContextProvider ?? throw new ArgumentNullException(nameof(workspaceContextProvider));
            _projectMap = new ConcurrentDictionary<string, ProjectInfo>();
            _packageManagerEventSink = new PackageManagerEventSink(interpreterOptionsService);
            _packageManagerEventSink.InstalledPackagesChanged += OnInstalledPackagesChanged;
            _firstLoad = true;
            _isRefresh = false;
            _setupComplete = false;
            _deferredTestChangeNotification = new Timer(OnDeferredTestChanged);

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
                _deferredTestChangeNotification.Dispose();
                _projectMap.Clear();
                _packageManagerEventSink.InstalledPackagesChanged -= OnInstalledPackagesChanged;
                _packageManagerEventSink.Dispose();

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

        public Uri ExecutorUri => PythonConstants.PythonProjectContainerDiscovererUri;

        public bool IsWorkspace => false;

        private bool IsSolutionMode() => _workspaceContextProvider?.Workspace == null;

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (IsSolutionMode() &&
                    (_firstLoad || _isRefresh)) {
                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad || _isRefresh) {
                            _firstLoad = false;
                            _isRefresh = false;

                            _projectMap.Clear();
                            _packageManagerEventSink.UnwatchAll();

                            SetupSolution();
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
            _packageManagerEventSink.UnwatchAll();

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
            try {
                _deferredTestChangeNotification.Change(500, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void OnDeferredTestChanged(object state) {
            // guard against triggering multiple updates during initial load until setup is complete
            if (!_firstLoad) {
                TestContainersUpdated?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            // We delay project test discovery until test explorer requests tests.
            // But if a project is loaded after SetupSolution() runs, we still need to update.
            if (!_setupComplete)
                return;

            if (LoadProject(e.Project)) {
                NotifyContainerChanged();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="vsProject"></param>
        /// <returns>True if loaded any files</returns>
        private bool LoadProject(IVsProject vsProject) {
            // check for python projects
            var pyProj = PythonProject.FromObject(vsProject);
            if (pyProj == null)
                return false;

            // always register for project property changes
            pyProj.ProjectPropertyChanged -= OnTestPropertiesChanged;
            pyProj.ProjectPropertyChanged += OnTestPropertiesChanged;

            TestFrameworkType testFrameworkType = GetTestFramework(pyProj);
            if (testFrameworkType == TestFrameworkType.None) {
                return false;
            }

            pyProj.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
            pyProj.ActiveInterpreterChanged += OnActiveInterpreterChanged;
            _packageManagerEventSink.WatchPackageManagers(pyProj.GetInterpreterFactory());

            var projInfo = new ProjectInfo(pyProj);
            _projectMap[projInfo.ProjectHome] = projInfo;
            var files = FilteredTestOrSettingsFiles(vsProject);
            UpdateContainersAndListeners(files, projInfo, isAdd: true);
            return files.Any();
        }

        private static TestFrameworkType GetTestFramework(PythonProject pyProj) {
            var testFrameworkType = TestFrameworkType.None;
            try {
                string testFrameworkStr = pyProj.GetProperty(PythonConstants.TestFrameworkSetting);
                if (Enum.TryParse<TestFrameworkType>(testFrameworkStr, ignoreCase: true, out TestFrameworkType parsedFramework)) {
                    testFrameworkType = parsedFramework;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.WriteLine("Exception : " + ex.Message);
            }

            return testFrameworkType;
        }

        private void OnTestPropertiesChanged(object sender, PythonProjectPropertyChangedArgs e) {
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private void OnActiveInterpreterChanged(object sender, EventArgs e) {
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private void OnInstalledPackagesChanged(object sender, EventArgs e) {
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project == null)
                return;

            if (RemoveTestContainers(e.Project.GetProjectHome())) {
                NotifyContainerChanged();
            }

            // Unregister Properties handler
            var pyProj = PythonProject.FromObject(e.Project);
            if (pyProj != null) {
                pyProj.ProjectPropertyChanged -= OnTestPropertiesChanged;
                pyProj.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectHome"></param>
        /// <returns>True if files were removed</returns>
        private bool RemoveTestContainers(string projectHome) {
            if (projectHome != null
                && _projectMap.TryRemove(projectHome, out ProjectInfo projToRemove)) {

                var testFilesToRemove = projToRemove.GetAllContainers().Select(t => t.Source);
                UpdateContainersAndListeners(testFilesToRemove, projToRemove, isAdd: false);
                projToRemove.Dispose();
                return testFilesToRemove.Any();
            }
            return false;
        }

        private IEnumerable<string> FilteredTestOrSettingsFiles(IVsProject project) {
            return project.GetProjectItems()
                .Where(s => IsTestFile(s) || IsSettingsFile(s));
        }

        public void UpdateContainersAndListeners(IEnumerable<string> sources, ProjectInfo projInfo, bool isAdd) {
            if (projInfo == null)
                return;

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

        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e) {
            if (String.IsNullOrEmpty(e.File))
                return;

            if (IsSettingsFile(e.File)) {
                HandleSettingsChanged(e);
            }
            // VS uses temp files we need to ignore
            else if (IsTestFile(e.File)) {
                HandleSourcesChanged(e);
            }
            return;
        }

        private void HandleSourcesChanged(TestFileChangedEventArgs e) {
            var projectInfo = FindProjectInfo(e.File, e.Project);
            if (projectInfo == null)
                return;

            var sources = new List<string>() { e.File };
            switch (e.ChangedReason) {
                case TestFileChangedReason.Added:
                    UpdateContainersAndListeners(sources, projectInfo, isAdd: true);
                    break;
                case TestFileChangedReason.Changed:
                    //Need to increment version number so Test Explorer notices a change
                    UpdateContainersAndListeners(sources, projectInfo, isAdd: true);
                    break;
                case TestFileChangedReason.Removed:
                    UpdateContainersAndListeners(sources, projectInfo, isAdd: false);
                    break;
                case TestFileChangedReason.Renamed:
                    var oldFileProjectInfo = FindProjectInfo(e.OldFile);
                    UpdateContainersAndListeners(new List<string>() { e.OldFile }, oldFileProjectInfo, isAdd: false);
                    UpdateContainersAndListeners(sources, projectInfo, isAdd: true);
                    break;
                default:
                    //In changed case file watcher observed a file changed event
                    //In this case we just have to fire TestContainerChnaged event
                    break;
            }

            if (ShouldRebuild(e.File) || ShouldRebuild(e.OldFile)) {
                _isRefresh = true;
            }

            NotifyContainerChanged();
        }

        private void HandleSettingsChanged(TestFileChangedEventArgs e) {
            switch (e.ChangedReason) {
                case TestFileChangedReason.None:
                    break;
                case TestFileChangedReason.Renamed:
                    _testFilesUpdateWatcher.RemoveWatch(e.OldFile);
                    _testFilesUpdateWatcher.AddWatch(e.File);
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
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private ProjectInfo FindProjectInfo(string file, IVsProject vsProject = null) {
            if (vsProject == null) {
                // bschnurr todo: this is only looking at opened files
                var rdt = (IVsRunningDocumentTable)_serviceProvider.GetService(typeof(SVsRunningDocumentTable));
                vsProject = VsProjectExtensions.PathToProject(file, rdt);
            }

            if (vsProject != null) {
                string projectHome = vsProject.GetProjectHome();
                if (projectHome != null &&
                    _projectMap.TryGetValue(projectHome, out ProjectInfo foundProjectInfo)) {
                    return foundProjectInfo;
                }
            }

            //Renamed  old files are no longer in the project so linear search
            var projectInfo = _projectMap.Values.FirstOrDefault(p => p.TryGetContainer(file, out _));
            return projectInfo;
        }

        private bool ShouldRebuild(string file) {
            // unittest does update additional files in the directory when __init__.py is added
            // so trigger a full rebuild
            return (file != null) && file.EndsWith("__init__.py", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSettingsFile(string file) {
            if (String.IsNullOrEmpty(file))
                return false;

            return PythonConstants.PyTestFrameworkConfigFiles.Contains(Path.GetFileName(file));
        }
    }
}