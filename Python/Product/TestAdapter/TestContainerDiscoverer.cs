/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.PythonTools;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
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
        private readonly TestFileAddRemoveListener _testFilesAddRemoveListener;
        private readonly TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private readonly SolutionEventsListener _solutionListener;
        private readonly Dictionary<string, string> _fileRootMap;
        private readonly Dictionary<string, ProjectInfo> _knownProjects;
        private bool _firstLoad, _isDisposed, _building, _detectingChanges;
        private DateTime _lastWrite = DateTime.MinValue;

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState)
            : this(serviceProvider,
                   new SolutionEventsListener(serviceProvider),
                   new TestFilesUpdateWatcher(),
                   new TestFileAddRemoveListener(serviceProvider, new Guid(PythonConstants.ProjectFactoryGuid)),
                    operationState) { }

        public TestContainerDiscoverer(IServiceProvider serviceProvider,
                                       SolutionEventsListener solutionListener,
                                       TestFilesUpdateWatcher testFilesUpdateWatcher,
                                       TestFileAddRemoveListener testFilesAddRemoveListener,
                                       IOperationState operationState) {
            ValidateArg.NotNull(serviceProvider, "serviceProvider");
            ValidateArg.NotNull(solutionListener, "solutionListener");
            ValidateArg.NotNull(testFilesUpdateWatcher, "testFilesUpdateWatcher");
            ValidateArg.NotNull(testFilesAddRemoveListener, "testFilesAddRemoveListener");
            ValidateArg.NotNull(operationState, "operationState");

            _fileRootMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _knownProjects = new Dictionary<string, ProjectInfo>(StringComparer.OrdinalIgnoreCase);

            _serviceProvider = serviceProvider;

            _testFilesAddRemoveListener = testFilesAddRemoveListener;
            _testFilesAddRemoveListener.TestFileChanged += OnProjectItemChanged;

            _solutionListener = solutionListener;
            _solutionListener.ProjectLoaded += OnProjectLoaded;
            _solutionListener.ProjectUnloading += OnProjectUnloaded;
            _solutionListener.ProjectClosing += OnProjectUnloaded;
            _solutionListener.ProjectRenamed += OnProjectRenamed;
            _solutionListener.BuildCompleted += OnBuildCompleted;
            _solutionListener.BuildStarted += OnBuildStarted;

            _testFilesUpdateWatcher = testFilesUpdateWatcher;
            _testFilesUpdateWatcher.FileChangedEvent += OnProjectItemChanged;
            operationState.StateChanged += OperationStateChanged;

            _firstLoad = true;
        }

        private void OperationStateChanged(object sender, OperationStateChangedEventArgs e) {
            if (e.State == TestOperationStates.ChangeDetectionStarting) {
                _detectingChanges = true;
            } else if (e.State == TestOperationStates.ChangeDetectionFinished) {
                _detectingChanges = false;
            }
        }

        private void OnBuildStarted(object sender, EventArgs e) {
            _building = true;
        }

        private void OnBuildCompleted(object sender, EventArgs e) {
            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
            foreach (var project in EnumerateLoadedProjects(solution)) {
                if (OnTestContainersChanged(project)) {
                    // We only need to fire the event once as the event 
                    // is not per-project, but we shouldn't fire it if 
                    // there's not yet any projects which we've been 
                    // queried for. 
                    break;
                }
            }
            _building = false;
        }

        void IDisposable.Dispose() {
            if (!_isDisposed) {
                _isDisposed = true;
                _testFilesAddRemoveListener.Dispose();
                _testFilesUpdateWatcher.Dispose();
                _solutionListener.Dispose();
            }
        }

        public Uri ExecutorUri {
            get {
                return TestExecutor.ExecutorUri;
            }
        }

        public IEnumerable<ITestContainer> TestContainers {
            get {
                // Get current solution
                var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));

                if (_firstLoad) {
                    // The first time through, we don't know about any loaded
                    // projects.
                    _firstLoad = false;
                    foreach (var project in EnumerateLoadedProjects(solution)) {
                        OnProjectLoaded(null, new ProjectEventArgs(project));
                    }
                    _testFilesAddRemoveListener.StartListeningForTestFileChanges();
                    _solutionListener.StartListeningForChanges();
                }

                // Get all loaded projects
                return EnumerateLoadedProjects(solution).SelectMany(p => GetTestContainers(p));
            }
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

        public IEnumerable<ITestContainer> GetTestContainers(IVsProject project) {
            if (!project.IsTestProject(GuidList.guidPythonProjectGuid)) {
                if (EqtTrace.IsVerboseEnabled) {
                    EqtTrace.Verbose("TestContainerDiscoverer: Ignoring project {0} as it is not a test project.", project.GetProjectName());
                }

                yield break;
            }

            string path;
            project.GetMkDocument(VSConstants.VSITEMID_ROOT, out path);

            if (_detectingChanges) {
                SaveModifiedFiles(project);
            }

            ProjectInfo projectInfo;
            if (!_knownProjects.TryGetValue(path, out projectInfo)) {
                // Don't return any containers for projects we don't know about.
                yield break;
            }
            projectInfo.HasRequestedContainers = true;
            
            var latestWrite = project.GetProjectItemPaths().Aggregate(
                _lastWrite,
                (latest, filePath) => {
                    try {
                        var ft = File.GetLastWriteTimeUtc(filePath);
                        return (ft > latest) ? ft : latest;
                    } catch (UnauthorizedAccessException) {
                    } catch (ArgumentException) {
                    } catch (IOException) {
                    }
                    return latest;
                });
            
            var architecture = Architecture.X86;
            // TODO: Read the architecture from the project

            yield return new TestContainer(this, path, latestWrite, architecture);
        }

        private void SaveModifiedFiles(IVsProject project) {
            // save all the open files in the project...
            foreach (var itemPath in project.GetProjectItems()) {
                if (String.IsNullOrEmpty(itemPath)) {
                    continue;
                }
                var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));
                ErrorHandler.ThrowOnFailure(
                    solution.SaveSolutionElement(
                        0,
                        (IVsHierarchy)project,
                        0
                    )
                );
            }
        }

        private bool ShouldDiscover(string pathToItem) {
            if (string.IsNullOrEmpty(pathToItem)) {
                return false;
            }

            if (pathToItem.EndsWith(".pyproj", StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            if (ModulePath.IsPythonFile(pathToItem)) {
                if (EqtTrace.IsVerboseEnabled) {
                    EqtTrace.Verbose("TestContainerDiscoverer: Found a test {0}.", pathToItem);
                }

                return true;
            }

            return false;
        }

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                string root = null;
                try {
                    root = e.Project.GetProjectHome();
                } catch (Exception ex) {
                    if (EqtTrace.IsVerboseEnabled) {
                        EqtTrace.Warning("TestContainerDiscoverer: Failed to get project home {0}", ex);
                    }
                    // If we fail to get ProjectHome, we still want to track the
                    // project. We just won't get the benefits of merging
                    // watchers into a single recursive watcher.
                }

                string path;
                if (e.Project.TryGetProjectPath(out path) &&
                    !_knownProjects.ContainsKey(path)) {
                    var dteProject = ((IVsHierarchy)e.Project).GetProject();
                    var interpFact = (MSBuildProjectInterpreterFactoryProvider)dteProject.Properties.Item("InterpreterFactoryProvider").Value;

                    var projectInfo = new ProjectInfo(
                        e.Project,
                        this,
                        interpFact
                    );

                    _knownProjects.Add(path, projectInfo);

                    foreach (var p in e.Project.GetProjectItemPaths()) {
                        if (!string.IsNullOrEmpty(root) && CommonUtils.IsSubpathOf(root, p)) {
                            _testFilesUpdateWatcher.AddDirectoryWatch(root);
                            _fileRootMap[p] = root;
                        } else {
                            _testFilesUpdateWatcher.AddWatch(p);
                        }
                    }
                }
            }

            OnTestContainersChanged(e.Project);
        }
        
        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                string root = null;
                try {
                    root = e.Project.GetProjectHome();
                } catch (Exception ex) {
                    if (EqtTrace.IsVerboseEnabled) {
                        EqtTrace.Warning("TestContainerDiscoverer: Failed to get project home {0}", ex);
                    }
                    // If we fail to get ProjectHome, we still want to track the
                    // project. We just won't get the benefits of merging
                    // watchers into a single recursive watcher.
                }

                ProjectInfo projectInfo;
                string projectPath;
                if (e.Project.TryGetProjectPath(out projectPath) &&
                    _knownProjects.TryGetValue(projectPath, out projectInfo)) {
                    _knownProjects.Remove(projectPath);

                    projectInfo.Detach();

                    foreach (var p in e.Project.GetProjectItemPaths()) {
                        if (string.IsNullOrEmpty(root) || !CommonUtils.IsSubpathOf(root, p)) {
                            _testFilesUpdateWatcher.RemoveWatch(p);
                        }
                        _fileRootMap.Remove(p);
                    }
                    if (!string.IsNullOrEmpty(root)) {
                        _testFilesUpdateWatcher.RemoveWatch(root);
                    }
                }
            }

            OnTestContainersChanged(e.Project);
        }

        private void OnProjectRenamed(object sender, ProjectEventArgs e) {
            OnProjectUnloaded(this, e);
            OnProjectLoaded(this, e);
        }

        /// <summary>
        /// Handler to react to test file Add/remove/rename events
        /// </summary>
        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e) {
            if (e != null && ShouldDiscover(e.File)) {
                string root = null;
                switch (e.ChangedReason) {
                    case TestFileChangedReason.Added:
                        Debug.Assert(e.Project != null);
                        if (e.Project.IsTestProject(GuidList.guidPythonProjectGuid)) {
                            root = e.Project.GetProjectHome();

                            if (!string.IsNullOrEmpty(root) && CommonUtils.IsSubpathOf(root, e.File)) {
                                _testFilesUpdateWatcher.AddDirectoryWatch(root);
                                _fileRootMap[e.File] = root;
                            } else {
                                _testFilesUpdateWatcher.AddWatch(e.File);
                            }

                            OnTestContainersChanged(e.Project);
                        }
                        break;
                    case TestFileChangedReason.Removed:
                        Debug.Assert(e.Project != null);

                        if (_fileRootMap.TryGetValue(e.File, out root)) {
                            _fileRootMap.Remove(e.File);
                            if (!_fileRootMap.Values.Contains(root)) {
                                _testFilesUpdateWatcher.RemoveWatch(root);
                            }
                        } else {
                            _testFilesUpdateWatcher.RemoveWatch(e.File);
                        }

                        // https://pytools.codeplex.com/workitem/1546
                        // track the last delete as an update as our file system scan won't see it
                        _lastWrite = DateTime.Now.ToUniversalTime();

                        OnTestContainersChanged(e.Project);
                        break;
#if DEV12_OR_LATER
                    // Dev12 renames files instead of overwriting them when
                    // saving, so we need to listen for renames where the new
                    // path is part of the project.
                    case TestFileChangedReason.Renamed:
#endif
                    case TestFileChangedReason.Changed:
                        OnTestContainersChanged(GetTestProjectFromFile(e.File));
                        break;
                }

            }
        }

        /// <summary>
        /// Given a filename that might be in an open project returns the test
        /// project which contains it, or null if it is not in a test project.
        /// </summary>
        private IVsProject GetTestProjectFromFile(string filename) {
            var solution = (IVsSolution)_serviceProvider.GetService(typeof(SVsSolution));

            foreach (var project in EnumerateLoadedProjects(solution)) {
                string projectPath;
                try {
                    if (!project.TryGetProjectPath(out projectPath)) {
                        projectPath = null;
                    }
                } catch (ObjectDisposedException) {
                    projectPath = null;
                }
                
                if (!string.IsNullOrEmpty(projectPath) && CommonUtils.IsSamePath(projectPath, filename)) {
                    return project;
                }
                
                bool isTestProject;
                try {
                    isTestProject = project.IsTestProject(GuidList.guidPythonProjectGuid);
                } catch (ObjectDisposedException) {
                    isTestProject = false;
                }
                
                var hierarchy = project as IVsHierarchy;
                uint itemid;
                if (isTestProject && 
                    hierarchy != null &&
                    ErrorHandler.Succeeded(hierarchy.ParseCanonicalName(filename, out itemid))) {
                    return project;
                }
            }

            return null;
        }

        /// <summary>
        /// Raises the test containers changed event, returns true if the event was delivered
        /// or would be delivered and there were no listeners.
        /// </summary>
        /// <param name="project">The project which the event is being raised for</param>
        private bool OnTestContainersChanged(IVsProject project) {
            // https://pytools.codeplex.com/workitem/1271
            // When test explorer kicks off a run it kicks off a test discovery
            // phase, which kicks off a build, which results in us saving files.
            // If we raise the files changed event then test explorer immediately turns
            // around and queries us for the changed files.  Then it continues
            // along with the test discovery phase it was already initiating, and 
            // discovers that no changes have occured - because it already updated
            // to the latest changes when we informed it our containers had changed.  
            // Therefore if we are both building and detecting changes then we 
            // don't want to raise the event, instead it'll query us in a little 
            // bit and get the most recent changes.
            ProjectInfo projectInfo;
            string projectPath;
            if (project != null &&
                project.TryGetProjectPath(out projectPath) && 
                _knownProjects.TryGetValue(projectPath, out projectInfo) &&
                projectInfo.HasRequestedContainers) {
                
                if (!_building || !_detectingChanges) {
                    var evt = TestContainersUpdated;
                    if (evt != null) {
                        evt(this, EventArgs.Empty);
                    }
                    return true;
                }
            }
            return false;
        }

        internal bool IsProjectKnown(IVsProject project) {
            string projectPath;
            if (project.TryGetProjectPath(out projectPath)) {
                return _knownProjects.ContainsKey(projectPath);
            }
            return false;
        }

        class ProjectInfo {
            public readonly IVsProject Project;
            public readonly TestContainerDiscoverer Discoverer;
            public readonly MSBuildProjectInterpreterFactoryProvider FactoryProvider;
            public IPythonInterpreterFactory ActiveInterpreter;
            /// <summary>
            /// used to track when the test window has asked us for tests from this project, 
            /// so we don't deliver events before it's ready.  It will ask when the project 
            /// is opened the 1st time, and then we can start delivering events as things
            /// change.  Fixes 2nd round of https://pytools.codeplex.com/workitem/1252
            /// </summary>
            public bool HasRequestedContainers;    

            public ProjectInfo(IVsProject project, TestContainerDiscoverer discoverer, MSBuildProjectInterpreterFactoryProvider factoryProvider) {
                Project = project;
                Discoverer = discoverer;
                FactoryProvider = factoryProvider;
                ActiveInterpreter = FactoryProvider.ActiveInterpreter;

                Attach();
                HookNewDatabaseAvailable();
            }

            private void ActiveInterpreterChanged(object sender, EventArgs args) {
                UnhookDatabaseCurrentChanged();

                ActiveInterpreter = FactoryProvider.ActiveInterpreter;

                HookNewDatabaseAvailable();

                Discoverer.OnTestContainersChanged(Project);
            }

            private void HookNewDatabaseAvailable() {
                var dbInterp = ActiveInterpreter as PythonInterpreterFactoryWithDatabase;
                if (dbInterp != null) {
                    dbInterp.NewDatabaseAvailable += DatabaseChanged;
                }
            }

            private void UnhookDatabaseCurrentChanged() {
                var dbInterp = ActiveInterpreter as PythonInterpreterFactoryWithDatabase;
                if (dbInterp != null) {
                    dbInterp.NewDatabaseAvailable -= DatabaseChanged;
                }
            }

            private void DatabaseChanged(object sender, EventArgs args) {
                Discoverer.OnTestContainersChanged(Project);
            }

            internal void Attach() {
                FactoryProvider.ActiveInterpreterChanged += ActiveInterpreterChanged;
            }

            internal void Detach() {
                FactoryProvider.ActiveInterpreterChanged -= ActiveInterpreterChanged;
            }
        }
    }
}
