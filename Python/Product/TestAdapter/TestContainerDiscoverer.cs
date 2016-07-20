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
using Microsoft.PythonTools.Projects;
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
            var pyProj = project as IPythonProjectProvider;
            if (pyProj != null) {
                return _projectInfo.ContainsKey(pyProj.Project);
            }
            return false;
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

        public TestContainer GetTestContainer(PythonProject project, string path) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(project, out projectInfo)) {
                TestContainer container;
                if (projectInfo._containers.TryGetValue(path, out container)) {
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
                var provider = e.Project as IPythonProjectProvider;
                if (provider != null) {
                    var analyzer = provider.Project.Analyzer;
                    var projectInfo = new ProjectInfo(this, provider.Project);
                    _projectInfo[provider.Project] = projectInfo;

                    foreach (var file in analyzer.Files) {
                        projectInfo.AnalysisComplete(this, new AnalysisCompleteEventArgs(file));
                    }
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        class ProjectInfo {
            private readonly PythonProject _project;
            private readonly TestContainerDiscoverer _discoverer;
            public readonly Dictionary<string, TestContainer> _containers;

            public ProjectInfo(TestContainerDiscoverer discoverer, PythonProject project) {
                _project = project;
                _discoverer = discoverer;
                _containers = new Dictionary<string, TestContainer>(StringComparer.OrdinalIgnoreCase);

                project.ProjectAnalyzerChanged += ProjectAnalyzerChanged;
                RegisterWithAnalyzer();
            }

            private void RegisterWithAnalyzer() {
                _project.Analyzer.RegisterExtension(typeof(TestAnalyzer).Assembly.CodeBase);
                _project.Analyzer.AnalysisComplete += AnalysisComplete;
            }

            public async void AnalysisComplete(object sender, AnalysisCompleteEventArgs e) {
                var testCaseData = await _project.Analyzer.SendExtensionCommandAsync(
                    TestAnalyzer.Name, 
                    TestAnalyzer.GetTestCasesCommand,
                    e.Path
                );

                if (testCaseData == null) {
                    return;
                }
                var testCases = TestAnalyzer.GetTestCases(testCaseData);

                if (testCases.Length != 0) {
                    TestContainer existing;
                    bool changed = true;
                    if (_containers.TryGetValue(e.Path, out existing)) {
                        // we have an existing entry, let's see if any of the tests actually changed.
                        if (existing.TestCases.Length == testCases.Length) {
                            changed = false;

                            for (int i = 0; i < existing.TestCases.Length; i++) {
                                if (!existing.TestCases[i].Equals(testCases[i])) {
                                    changed = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (changed) {
                        // we have a new entry or some of the tests changed
                        int version = (existing?.Version ?? 0) + 1;
                        _containers[e.Path] = new TestContainer(
                            _discoverer,
                            e.Path,
                            _project,
                            version,
                            Architecture,
                            testCases
                        );

                        ContainersChanged();
                    }
                } else if (_containers.Remove(e.Path)) {
                    // Raise containers changed event...
                    ContainersChanged();
                }
            }

            private Architecture Architecture {
                get {
                    return Architecture.X86;
                }
            }

            private void ContainersChanged() {
                _discoverer.TestContainersUpdated?.Invoke(_discoverer, EventArgs.Empty);
            }

            public void ProjectAnalyzerChanged(object sender, EventArgs e) {
                RegisterWithAnalyzer();
            }
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                var provider = e.Project as IPythonProjectProvider;
                ProjectInfo events;
                if (provider != null && _projectInfo.TryGetValue(provider.Project, out events)) {
                    var analyzer = provider.Project.Analyzer;

                    provider.Project.ProjectAnalyzerChanged -= events.ProjectAnalyzerChanged;
                    analyzer.AnalysisComplete -= events.AnalysisComplete;

                    _projectInfo.Remove(provider.Project);
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }        
    }
}
