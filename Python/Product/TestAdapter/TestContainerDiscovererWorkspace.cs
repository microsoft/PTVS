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
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools.TestAdapter;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscovererWorkspace))]
    class TestContainerDiscovererWorkspace : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly IPythonWorkspaceContextProvider _workspaceContextProvider;
        private readonly ConcurrentDictionary<string, ProjectInfo> _projectMap;
        private readonly PackageManagerEventSink _packageManagerEventSink;
        private readonly IInterpreterRegistryService _interpreterRegistryService;
        private readonly Timer _deferredTestChangeNotification;
        private bool _firstLoad, _isDisposed, _isRefresh;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;

        [ImportingConstructor]
        private TestContainerDiscovererWorkspace(
            [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
            [Import(typeof(IOperationState))]IOperationState operationState,
            [Import] IPythonWorkspaceContextProvider workspaceContextProvider,
            [Import] IInterpreterOptionsService interpreterOptionsService,
            [Import] IInterpreterRegistryService interpreterRegistryService
        ) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _projectMap = new ConcurrentDictionary<string, ProjectInfo>();
            _packageManagerEventSink = new PackageManagerEventSink(interpreterOptionsService);
            _packageManagerEventSink.InstalledPackagesChanged += OnInstalledPackagesChanged;
            _interpreterRegistryService = interpreterRegistryService;
            _deferredTestChangeNotification = new Timer(OnDeferredTestChanged);
            _firstLoad = true;
            _isRefresh = false;
            _workspaceContextProvider = workspaceContextProvider ?? throw new ArgumentNullException(nameof(workspaceContextProvider));
            _workspaceContextProvider.WorkspaceClosed += OnWorkspaceClosed;
            _workspaceContextProvider.WorkspaceInitialized += OnWorkspaceLoaded;
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                _deferredTestChangeNotification.Dispose();

                if (_testFilesUpdateWatcher != null) {
                    _testFilesUpdateWatcher.FileChangedEvent -= OnWorkspaceFileChanged;
                    _testFilesUpdateWatcher.Dispose();
                    _testFilesUpdateWatcher = null;
                }

                if (_workspaceContextProvider != null) {
                    _workspaceContextProvider.WorkspaceClosed -= OnWorkspaceClosed;
                    _workspaceContextProvider.WorkspaceInitialized -= OnWorkspaceLoaded;

                    if (_workspaceContextProvider.Workspace != null) {
                        _workspaceContextProvider.Workspace.SearchPathsSettingChanged -= OnWorkspaceSettingsChange;
                        _workspaceContextProvider.Workspace.InterpreterSettingChanged -= OnWorkspaceSettingsChange;
                        _workspaceContextProvider.Workspace.TestSettingChanged -= OnWorkspaceSettingsChange;
                    }
                }
            }
        }

        public Uri ExecutorUri {
            get {
                return PythonConstants.PythonWorkspaceContainerDiscovererUri;
            }
        }

        public bool IsWorkspace => _workspaceContextProvider?.Workspace != null;

        private bool HasLoadedWorkspace() => _workspaceContextProvider?.Workspace != null;

        public IEnumerable<ITestContainer> TestContainers {
            get {
                if (HasLoadedWorkspace()
                    && (_firstLoad || _isRefresh)) {
                    // The first time through, we don't know about any loaded
                    // projects.
                    ThreadHelper.JoinableTaskFactory.Run(async () => {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        if (_firstLoad || _isRefresh) {
                            _firstLoad = false;
                            _isRefresh = false;

                            _projectMap.Clear();
                            _packageManagerEventSink.UnwatchAll();

                            SetupWorkspace(_workspaceContextProvider.Workspace);
                        };
                    });
                }

                return _projectMap.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        private void OnWorkspaceClosed(object sender, PythonWorkspaceContextEventArgs e) {
            _firstLoad = true;
            _isRefresh = false;
            if (_testFilesUpdateWatcher != null) {
                _testFilesUpdateWatcher.FileChangedEvent -= OnWorkspaceFileChanged;
                _testFilesUpdateWatcher.Dispose();
                _testFilesUpdateWatcher = null;
            }

            if (_workspaceContextProvider.Workspace != null) {
                _workspaceContextProvider.Workspace.SearchPathsSettingChanged -= OnWorkspaceSettingsChange;
                _workspaceContextProvider.Workspace.InterpreterSettingChanged -= OnWorkspaceSettingsChange;
                _workspaceContextProvider.Workspace.TestSettingChanged -= OnWorkspaceSettingsChange;
                _workspaceContextProvider.Workspace.ActiveInterpreterChanged -= OnActiveInterpreterChanged;

                if (_projectMap.TryRemove(_workspaceContextProvider.Workspace.Location, out ProjectInfo projToRemove)) {
                    projToRemove.Dispose();
                }
            }

            _projectMap.Clear();
            _packageManagerEventSink.InstalledPackagesChanged -= OnInstalledPackagesChanged;
            _packageManagerEventSink.UnwatchAll();
        }

        private void OnWorkspaceLoaded(object sender, PythonWorkspaceContextEventArgs e) {
            if (e.Workspace == null)
                return;

            // guard against duplicate loaded triggers
            e.Workspace.SearchPathsSettingChanged -= OnWorkspaceSettingsChange;
            e.Workspace.InterpreterSettingChanged -= OnWorkspaceSettingsChange;
            e.Workspace.TestSettingChanged -= OnWorkspaceSettingsChange;

            e.Workspace.SearchPathsSettingChanged += OnWorkspaceSettingsChange;
            e.Workspace.InterpreterSettingChanged += OnWorkspaceSettingsChange;
            e.Workspace.TestSettingChanged += OnWorkspaceSettingsChange;
        }

        private void SetupWorkspace(IPythonWorkspaceContext workspace) {
            if (workspace == null)
                return;

            TestFrameworkType testFrameworkType = GetTestFramework(workspace);

            if (testFrameworkType != TestFrameworkType.None) {
                var projInfo = new ProjectInfo(workspace);
                _projectMap[projInfo.ProjectHome] = projInfo;

                var oldWatcher = _testFilesUpdateWatcher;
                _testFilesUpdateWatcher = new TestFilesUpdateWatcher();
                _testFilesUpdateWatcher.FileChangedEvent += OnWorkspaceFileChanged;
                _testFilesUpdateWatcher.AddDirectoryWatch(workspace.Location);
                oldWatcher?.Dispose();

                Predicate<string> testFileFilter = (x) => 
                    PythonConstants.TestFileExtensionRegex.IsMatch(PathUtils.GetFileOrDirectoryName(x)
                );
                foreach (var file in _workspaceContextProvider.Workspace.EnumerateUserFiles(testFileFilter)) {
                    projInfo.AddTestContainer(this, file);
                }

                workspace.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
                workspace.ActiveInterpreterChanged += OnActiveInterpreterChanged;
                _packageManagerEventSink.WatchPackageManagers(workspace.CurrentFactory);
            }
        }

        private static TestFrameworkType GetTestFramework(IPythonWorkspaceContext workspace) {
            var testFrameworkType = TestFrameworkType.None;
            try {
                string testFrameworkStr = workspace.GetStringProperty(PythonConstants.TestFrameworkSetting);
                if (Enum.TryParse<TestFrameworkType>(testFrameworkStr, ignoreCase: true, out TestFrameworkType parsedFramework)) {
                    testFrameworkType = parsedFramework;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                Trace.WriteLine("Exception : " + ex.Message);
            }

            return testFrameworkType;
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

        private bool IsTestFile(string path) {
            return Path.GetExtension(path).Equals(PythonConstants.FileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSettingsFile(string file) {
            if (String.IsNullOrEmpty(file))
                return false;

            return PythonConstants.PyTestFrameworkConfigFiles.Contains(Path.GetFileName(file));
        }

        private void OnWorkspaceSettingsChange(object sender, System.EventArgs e) {
            NotifyContainerChanged();
            _isRefresh = true;
        }

        private void OnActiveInterpreterChanged(object sender, EventArgs e) {
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private void OnInstalledPackagesChanged(object sender, EventArgs e) {
            _isRefresh = true;
            NotifyContainerChanged();
        }

        private void OnWorkspaceFileChanged(object sender, TestFileChangedEventArgs e) {
            if (String.IsNullOrEmpty(e.File))
                return;

            if (IsSettingsFile(e.File)) {
                NotifyContainerChanged();
                _isRefresh = true;
                return;
            }

            if (!IsTestFile(e.File))
                return;

            IPythonWorkspaceContext workspace = _workspaceContextProvider.Workspace;
            if (workspace == null)
                return;

            var projInfo = GetProjectInfo(workspace.Location);
            if (projInfo == null || IsFileExcluded(projInfo, e.File))
                return;

            switch (e.ChangedReason) {
                case TestFileChangedReason.Added:
                    projInfo.AddTestContainer(this, e.File);
                    break;
                case TestFileChangedReason.Changed:
                    projInfo.AddTestContainer(this, e.File);
                    break;
                case TestFileChangedReason.Removed:
                    projInfo.RemoveTestContainer(e.File);
                    break;
                case TestFileChangedReason.Renamed:
                    projInfo.RemoveTestContainer(e.OldFile);
                    projInfo.AddTestContainer(this, e.File);
                    break;
                default:
                    break;
            }
            NotifyContainerChanged();
        }

        private bool IsFileExcluded(ProjectInfo projectInfo, string filePath) {
            bool isFileInVirtualEnv = _interpreterRegistryService.Configurations
                .Where(x => PathUtils.IsSubpathOf(projectInfo.ProjectHome, x.InterpreterPath))
                .Any(x => PathUtils.IsSubpathOf(x.GetPrefixPath(), filePath));

            return isFileInVirtualEnv || PathUtils.IsSubpathOf(Path.Combine(projectInfo.ProjectHome, ".vs"), filePath);
        }
    }
}