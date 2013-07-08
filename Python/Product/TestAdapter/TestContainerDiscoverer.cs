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
using System.IO;
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestWindow.Extensibility;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.TestAdapter {
    [Export(typeof(ITestContainerDiscoverer))]
    class TestContainerDiscoverer : ITestContainerDiscoverer {
        private IServiceProvider _serviceProvider;
        private TestFileAddRemoveListener _testFilesAddRemoveListener;
        private TestFilesUpdateWatcher _testFilesUpdateWatcher;
        private SolutionEventsListener _solutionListener;
        private readonly Dictionary<string, string> _fileRootMap;
        private readonly HashSet<string> _knownProjects;
        private bool _firstLoad;

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider)
            : this(serviceProvider,
                   new SolutionEventsListener(serviceProvider),
                   new TestFilesUpdateWatcher(),
                   new TestFileAddRemoveListener(serviceProvider)) { }

        public TestContainerDiscoverer(IServiceProvider serviceProvider,
                                       SolutionEventsListener solutionListener,
                                       TestFilesUpdateWatcher testFilesUpdateWatcher,
                                       TestFileAddRemoveListener testFilesAddRemoveListener) {
            ValidateArg.NotNull(serviceProvider, "serviceProvider");
            ValidateArg.NotNull(solutionListener, "solutionListener");
            ValidateArg.NotNull(testFilesUpdateWatcher, "testFilesUpdateWatcher");
            ValidateArg.NotNull(testFilesAddRemoveListener, "testFilesAddRemoveListener");

            _fileRootMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _knownProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _serviceProvider = serviceProvider;

            _testFilesAddRemoveListener = testFilesAddRemoveListener;
            _testFilesAddRemoveListener.TestFileChanged += OnProjectItemChanged;

            _solutionListener = solutionListener;
            _solutionListener.SolutionChanged += OnSolutionChanged;

            _testFilesUpdateWatcher = testFilesUpdateWatcher;
            _testFilesUpdateWatcher.FileChangedEvent += OnProjectItemChanged;

            _firstLoad = true;
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
                        OnSolutionChanged(null, new SolutionEventsListenerEventArgs(project, SolutionChangedReason.Load));
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
            if (!project.IsTestProject()) {
                if (EqtTrace.IsVerboseEnabled) {
                    EqtTrace.Verbose("TestContainerDiscoverer: Ignoring project {0} as it is not a test project.", project.GetProjectName());
                }

                yield break;
            }

            string path;
            project.GetMkDocument(VSConstants.VSITEMID_ROOT, out path);

            if (!_knownProjects.Contains(path)) {
                // Don't return any containers for projects we don't know about.
                yield break;
            }

            var latestWrite = project.GetProjectItemPaths().Aggregate(
                DateTime.MinValue,
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

        /// <summary>
        /// Handler to react to project load/unload events.
        /// </summary>
        private void OnSolutionChanged(object sender, SolutionEventsListenerEventArgs e) {
            if (e != null) {
                string root = null;
                if (e.Project != null) {
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
                }

                if (e.ChangedReason == SolutionChangedReason.Load) {
                    if (e.Project != null) {
                        if (_knownProjects.Add(e.Project.GetProjectPath())) {
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

                    OnTestContainersChanged();
                } else if (e.ChangedReason == SolutionChangedReason.Unload) {
                    if (e.Project != null) {
                        if (_knownProjects.Remove(e.Project.GetProjectPath())) {
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

                    OnTestContainersChanged();
                }
            }

            // Do not fire OnTestContainersChanged here.
            // This will cause us to fire this event too early before the UTE is ready to process containers and will result in an exception.
            // The UTE will query all the TestContainerDiscoverers once the solution is loaded.
        }

        /// <summary>
        /// Handler to react to test file Add/remove/rename events
        /// </summary>
        private void OnProjectItemChanged(object sender, TestFileChangedEventArgs e) {
            if (e != null && ShouldDiscover(e.File)) {
                string root = null;
                if (e.ChangedReason == TestFileChangedReason.Added) {
                    if (e.Project != null) {
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
                    }

                    if (!string.IsNullOrEmpty(root) && CommonUtils.IsSubpathOf(root, e.File)) {
                        _testFilesUpdateWatcher.AddDirectoryWatch(root);
                        _fileRootMap[e.File] = root;
                    } else {
                        _testFilesUpdateWatcher.AddWatch(e.File);
                    }

                } else if (e.ChangedReason == TestFileChangedReason.Removed) {
                    if (_fileRootMap.TryGetValue(e.File, out root)) {
                        _fileRootMap.Remove(e.File);
                        if (!_fileRootMap.Values.Contains(root)) {
                            _testFilesUpdateWatcher.RemoveWatch(root);
                        }
                    } else {
                        _testFilesUpdateWatcher.RemoveWatch(e.File);
                    }
                }

                OnTestContainersChanged();
            }
        }

        private void OnTestContainersChanged() {
            var evt = TestContainersUpdated;
            if (evt != null) {
                evt(this, EventArgs.Empty);
            }
        }
    }
}
