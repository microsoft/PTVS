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
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Projects;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly SolutionEventsListener _solutionListener;
        private readonly Dictionary<PythonProject, ProjectInfo> _projectInfo;
        private bool _firstLoad, _isDisposed;
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri _ExecutorUri = new Uri(ExecutorUriString);

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState)
            : this(serviceProvider,
                   new SolutionEventsListener(serviceProvider),
                    operationState) { }

        internal bool IsProjectKnown(IVsProject project) {
            var pyProj = PythonProject.FromObject(project);
            return pyProj != null && _projectInfo.ContainsKey(pyProj);
        }

        public TestContainerDiscoverer(IServiceProvider serviceProvider,
                                       SolutionEventsListener solutionListener,
                                       IOperationState operationState) {
            ValidateArg.NotNull(serviceProvider, "serviceProvider");
            ValidateArg.NotNull(solutionListener, "solutionListener");
            ValidateArg.NotNull(operationState, "operationState");

            _projectInfo = new Dictionary<PythonProject, ProjectInfo>();

            _serviceProvider = serviceProvider;

            _solutionListener = solutionListener;
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;

            _firstLoad = true;
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                _solutionListener.Dispose();
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
                            foreach (var project in EnumerateLoadedProjects(solution)) {
                                OnProjectLoaded(null, new ProjectEventArgs(project));
                            }
                            _solutionListener.StartListeningForChanges();
                        }
                    });
                }

                return _projectInfo.Values.SelectMany(x => x.GetAllContainers());
            }
        }

        public TestContainer GetTestContainer(PythonProject project, string path) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(project, out projectInfo)) {
                TestContainer container;
                if (projectInfo.TryGetContainer(path, out container)) {
                    return container;
                }
            }

            return null;
        }

        private static IEnumerable<IVsProject> EnumerateLoadedProjects(IVsSolution solution) {
            var guid = new Guid(PythonConstants.ProjectFactoryGuid);
            IEnumHierarchies hierarchies;
            ErrorHandler.ThrowOnFailure((solution.GetProjectEnum(
                (uint)(__VSENUMPROJFLAGS.EPF_MATCHTYPE | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION),
                ref guid,
                out hierarchies)));
            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            while (ErrorHandler.Succeeded(hierarchies.Next(1, hierarchy, out fetched)) && fetched == 1) {
                var project = hierarchy[0] as IVsProject;
                if (project != null) {
                    yield return project;
                }
            }
        }

        public event EventHandler TestContainersUpdated;

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            OnProjectLoadedAsync(e.Project).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async Task OnProjectLoadedAsync(IVsProject project) {
            var pyProj = PythonProject.FromObject(project);
            if (pyProj != null) {
                var analyzer = await pyProj.GetAnalyzerAsync();
                if (analyzer != null) {
                    _projectInfo[pyProj] = new ProjectInfo(this, pyProj);
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                var pyProj = PythonProject.FromObject(e.Project);
                ProjectInfo events;
                if (pyProj != null &&
                    _projectInfo.TryGetValue(pyProj, out events) &&
                    _projectInfo.Remove(pyProj)) {
                    events.Dispose();
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        sealed class ProjectInfo : IDisposable {
            private readonly PythonProject _project;
            private readonly TestContainerDiscoverer _discoverer;
            private readonly Dictionary<string, TestContainer> _containers;
            private readonly object _containersLock = new object();
            private ProjectAnalyzer _analyzer;

            private List<string> _pendingRequests;

            public ProjectInfo(TestContainerDiscoverer discoverer, PythonProject project) {
                _project = project;
                _discoverer = discoverer;
                _containers = new Dictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);
                _pendingRequests = new List<string>();

                project.ProjectAnalyzerChanged += ProjectAnalyzerChanged;
                RegisterWithAnalyzerAsync().HandleAllExceptions(_discoverer._serviceProvider, GetType()).DoNotWait();
            }

            public void Dispose() {
                if (_analyzer != null) {
                    _analyzer.AnalysisComplete -= AnalysisComplete;
                }
                _project.ProjectAnalyzerChanged -= ProjectAnalyzerChanged;
            }

            public TestContainer[] GetAllContainers() {
                lock (_containersLock) {
                    return _containers.Select(x => x.Value).ToArray();
                }
            }

            public bool TryGetContainer(string path, out TestContainer container) {
                lock (_containersLock) {
                    return _containers.TryGetValue(path, out container);
                }
            }

            private bool RemoveContainer(string path) {
                lock (_containersLock) {
                    return _containers.Remove(path);
                }
            }

            private async Task RegisterWithAnalyzerAsync() {
                if (_analyzer != null) {
                    _analyzer.AnalysisComplete -= AnalysisComplete;
                }
                _analyzer = await _project.GetAnalyzerAsync();
                if (_analyzer != null) {
                    _analyzer.AnalysisComplete += AnalysisComplete;
                    await _analyzer.RegisterExtensionAsync(typeof(TestAnalyzer)).ConfigureAwait(false);
                    await UpdateTestCasesAsync(_analyzer.Files, false).ConfigureAwait(false);
                }
            }

            private async void AnalysisComplete(object sender, AnalysisCompleteEventArgs e) {
                await PendOrSubmitRequests(e.Path)
                    .HandleAllExceptions(_discoverer._serviceProvider, GetType())
                    .ConfigureAwait(false);
            }

            private async Task PendOrSubmitRequests(string path) {
                List<string> pendingRequests;
                int originalCount;
                lock (_containersLock) {
                    pendingRequests = _pendingRequests;
                    if (!_pendingRequests.Contains(path)) {
                        _pendingRequests.Add(path);
                    }
                    originalCount = _pendingRequests.Count;

                    if (originalCount > 50) {
                        _pendingRequests = new List<string>();
                    }
                }

                await Task.Delay(100).ConfigureAwait(false);

                lock (_containersLock) {
                    if (pendingRequests.Count != originalCount) {
                        return;
                    }
                    if (_pendingRequests == pendingRequests) {
                        _pendingRequests = new List<string>();
                    }
                }

                await UpdateTestCasesAsync(pendingRequests, true).ConfigureAwait(false);
            }

            private async Task UpdateTestCasesAsync(IEnumerable<string> paths, bool notify) {
                var analyzer = await _project.GetAnalyzerAsync();
                if (analyzer == null) {
                    return;
                }

                var testCaseData = await analyzer.SendExtensionCommandAsync(
                    TestAnalyzer.Name,
                    TestAnalyzer.GetTestCasesCommand,
                    string.Join(";", paths)
                );

                if (testCaseData == null) {
                    return;
                }

                var testCaseGroups = TestAnalyzer.GetTestCases(testCaseData).GroupBy(tc => tc.Filename);

                bool anythingToNotify = false;

                foreach (var testCases in testCaseGroups) {
                    var path = testCases.Key;
                    if (testCases.Any()) {
                        if (!TryGetContainer(path, out TestContainer existing) || !existing.TestCases.SequenceEqual(testCases)) {
                            // we have a new entry or some of the tests changed
                            int version = (existing?.Version ?? 0) + 1;
                            lock (_containersLock) {
                                _containers[path] = new TestContainer(
                                    _discoverer,
                                    path,
                                    _project,
                                    version,
                                    Architecture,
                                    testCases.ToArray()
                                );
                            }

                            anythingToNotify = true;
                        }
                    } else if (RemoveContainer(path)) {
                        // Raise containers changed event...
                        anythingToNotify = true;
                    }
                }

                if (notify && anythingToNotify) {
                    ContainersChanged();
                }
            }

            private Architecture Architecture => Architecture.Default;

            private void ContainersChanged() {
                _discoverer.TestContainersUpdated?.Invoke(_discoverer, EventArgs.Empty);
            }

            public void ProjectAnalyzerChanged(object sender, EventArgs e) {
                RegisterWithAnalyzerAsync().HandleAllExceptions(_discoverer._serviceProvider, GetType()).DoNotWait();
            }
        }
    }
}
