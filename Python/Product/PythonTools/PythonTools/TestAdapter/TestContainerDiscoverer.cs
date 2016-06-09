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
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.TestAdapter;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    [Export(typeof(TestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer, IDisposable {
        private readonly IServiceProvider _serviceProvider;
        private readonly SolutionEventsListener _solutionListener;
        private readonly Dictionary<PythonProjectNode, ProjectInfo> _projectInfo;
        private bool _firstLoad, _isDisposed;
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri _ExecutorUri = new Uri(ExecutorUriString);

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState)
            : this(serviceProvider,
                   new SolutionEventsListener(serviceProvider),
                    operationState) { }

        internal bool IsProjectKnown(IVsProject project) {
            var pyProj = project.GetPythonProject();
            if (pyProj != null) {
                return _projectInfo.ContainsKey(pyProj);
            }
            return false;
        }

        public TestContainerDiscoverer(IServiceProvider serviceProvider,
                                       SolutionEventsListener solutionListener,
                                       IOperationState operationState) {
            ValidateArg.NotNull(serviceProvider, "serviceProvider");
            ValidateArg.NotNull(solutionListener, "solutionListener");
            ValidateArg.NotNull(operationState, "operationState");

            _projectInfo = new Dictionary<PythonProjectNode, ProjectInfo>();

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
                    // Get current solution
                    var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
                    // The first time through, we don't know about any loaded
                    // projects.
                    _firstLoad = false;
                    foreach (var project in EnumerateLoadedProjects(solution)) {
                        OnProjectLoaded(null, new ProjectEventArgs(project));
                    }
                    _solutionListener.StartListeningForChanges();
                }

                return _projectInfo.Values.SelectMany(x => x._containers).Select(x => x.Value);
            }
        }

        public TestContainer GetTestContainer(PythonProjectNode project, string path) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(project, out projectInfo)) {
                var analysis = project.GetAnalyzer().GetAnalysisEntryFromPath(path);
                TestContainer container;
                if (analysis != null && projectInfo._containers.TryGetValue(analysis, out container)) {
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
            if (e.Project != null) {
                var pyProj = e.Project.GetPythonProject();
                if (pyProj != null) {
                    var analyzer = pyProj.GetAnalyzer();
                    var projectInfo = new ProjectInfo(this, pyProj);
                    pyProj.InterpreterDbChanged += projectInfo.DatabaseChanged;
                    _projectInfo[pyProj] = projectInfo;

                    foreach (var file in analyzer.LoadedFiles) {
                        if (file.Value.IsAnalyzed) {
                            projectInfo.AnalysisComplete(this, new AnalysisCompleteEventArgs(file.Value));
                        }
                    }
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        class ProjectInfo {
            private readonly PythonProjectNode _project;
            private readonly TestContainerDiscoverer _discoverer;
            public readonly Dictionary<AnalysisEntry, TestContainer> _containers;

            public ProjectInfo(TestContainerDiscoverer discoverer, PythonProjectNode project) {
                _project = project;
                _discoverer = discoverer;
                _containers = new Dictionary<AnalysisEntry, TestContainer>();

                project.ProjectAnalyzerChanged += ProjectAnalyzerChanged;
                HookAnalysisComplete();
            }

            private void HookAnalysisComplete() {
                _project.GetAnalyzer().AnalysisComplete += AnalysisComplete;
            }

            public async void AnalysisComplete(object sender, AnalysisCompleteEventArgs e) {
                var testCases = await _project.GetAnalyzer().GetTestCasesAsync(e.AnalysisEntry.Path);
                if (testCases == null) {
                    // Call failed for some reason (e.g. the analzyer crashed)...
                    return;
                }
                
                if (testCases.tests.Length != 0) {
                    TestContainer existing;
                    bool changed = true;
                    if (_containers.TryGetValue(e.AnalysisEntry, out existing)) {
                        // we have an existing entry, let's see if any of the tests actually changed.
                        if (existing.TestCases.Length == testCases.tests.Length) {
                            changed = false;

                            for (int i = 0; i < existing.TestCases.Length; i++) {
                                if (!existing.TestCases[i].Equals(testCases.tests[i])) {
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (changed) {
                        // we have a new entry or some of the tests changed
                        int version = (existing?.Version ?? 0) + 1;
                        _containers[e.AnalysisEntry] = new TestContainer(
                            _discoverer,
                            e.AnalysisEntry.Path,
                            _project,
                            version,
                            Architecture,
                            testCases.tests
                        );

                        ContainersChanged();
                    }
                } else if (_containers.Remove(e.AnalysisEntry)) {
                    // Raise containers changed event...
                    ContainersChanged();
                }
            }

            private Architecture Architecture {
                get {
                    return _project.ActiveInterpreter.Configuration.Architecture == System.Reflection.ProcessorArchitecture.Amd64 ?
                        Architecture.X64 : Architecture.X86;
                }
            }

            private void ContainersChanged() {
                _discoverer.TestContainersUpdated?.Invoke(_discoverer, EventArgs.Empty);
            }

            public void DatabaseChanged(object sender, EventArgs args) {
                ContainersChanged();
            }

            public void ProjectAnalyzerChanged(object sender, EventArgs e) {
                HookAnalysisComplete();
            }
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                var pyProj = e.Project.GetPythonProject();
                ProjectInfo events;
                if (pyProj != null && _projectInfo.TryGetValue(pyProj, out events)) {
                    var analyzer = pyProj.GetAnalyzer();

                    pyProj.ProjectAnalyzerChanged -= events.ProjectAnalyzerChanged;
                    analyzer.AnalysisComplete -= events.AnalysisComplete;

                    _projectInfo.Remove(pyProj);
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }        
    }
}
