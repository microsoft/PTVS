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
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Projects;
using Microsoft.PythonTools.TestAdapter.Model;
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
        private readonly Dictionary<string, ProjectInfo> _projectInfo;
        private bool _firstLoad, _isDisposed;
        public const string ExecutorUriString = "executor://PythonTestExecutor/v1";
        public static readonly Uri _ExecutorUri = new Uri(ExecutorUriString);

        [ImportingConstructor]
        private TestContainerDiscoverer([Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider, [Import(typeof(IOperationState))]IOperationState operationState) {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

            _projectInfo = new Dictionary<string, ProjectInfo>();

            _solutionListener = new SolutionEventsListener(serviceProvider);
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

        public TestContainer GetTestContainer(string projectHome, string path) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(projectHome, out projectInfo)) {
                TestContainer container;
                if (projectInfo.TryGetContainer(path, out container)) {
                    return container;
                }
            }

            return null;
        }

        public LaunchConfiguration GetLaunchConfigurationOrThrow(string projectHome) {
            ProjectInfo projectInfo;
            if (_projectInfo.TryGetValue(projectHome, out projectInfo)) {
                return projectInfo.GetLaunchConfigurationOrThrow();
            }

            return null;
        }


        public void NotifyChanged() {
            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }


        #region SolutionExtenions

        /// <summary>
        /// return a list of TestContainers by querying each of the projects
        /// </summary>
        /// <param name="project"></param>


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

        /// <summary>
        /// Get the items present in the project
        /// </summary>
        public static IEnumerable<string> GetProjectItems(IVsProject project) {
            Debug.Assert(project != null, "Project is not null");

            // Each item in VS OM is IVSHierarchy. 
            IVsHierarchy hierarchy = (IVsHierarchy)project;

            return GetProjectItems(hierarchy, VSConstants.VSITEMID_ROOT);
        }

        /// <summary>
        /// Get project items
        /// </summary>
        private static IEnumerable<string> GetProjectItems(IVsHierarchy project, uint itemId) {

            object pVar = GetPropertyValue((int)__VSHPROPID.VSHPROPID_FirstChild, itemId, project);

            uint childId = GetItemId(pVar);
            while (childId != VSConstants.VSITEMID_NIL) {
                foreach (string item in GetProjectItems(project, childId)) {
                    yield return item;
                }

                string childPath = GetCanonicalName(childId, project);
                yield return childPath;

                pVar = GetPropertyValue((int)__VSHPROPID.VSHPROPID_NextSibling, childId, project);
                childId = GetItemId(pVar);
            }
        }

        /// <summary>
        /// Convert parameter object to ItemId
        /// </summary>
        private static uint GetItemId(object pvar) {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }

        /// <summary>
        /// Get the parameter property value
        /// </summary>
        private static object GetPropertyValue(int propid, uint itemId, IVsHierarchy vsHierarchy) {
            if (itemId == VSConstants.VSITEMID_NIL) {
                return null;
            }

            try {
                object o;
                ErrorHandler.ThrowOnFailure(vsHierarchy.GetProperty(itemId, propid, out o));

                return o;
            } catch (System.NotImplementedException) {
                return null;
            } catch (System.Runtime.InteropServices.COMException) {
                return null;
            } catch (System.ArgumentException) {
                return null;
            }
        }


        /// <summary>
        /// Get the canonical name
        /// </summary>
        private static string GetCanonicalName(uint itemId, IVsHierarchy hierarchy) {
            Debug.Assert(itemId != VSConstants.VSITEMID_NIL, "ItemId cannot be nill");

            string strRet = string.Empty;
            int hr = hierarchy.GetCanonicalName(itemId, out strRet);

            if (hr == VSConstants.E_NOTIMPL) {
                // Special case E_NOTIMLP to avoid perf hit to throw an exception.
                return string.Empty;
            } else {
                try {
                    ErrorHandler.ThrowOnFailure(hr);
                } catch (System.Runtime.InteropServices.COMException) {
                    strRet = string.Empty;
                }


                // This could be in the case of S_OK, S_FALSE, etc.
                return strRet;
            }
        }
#endregion

        public event EventHandler TestContainersUpdated;

        private void OnProjectLoaded(object sender, ProjectEventArgs e) {
            OnProjectLoadedAsync(e.Project).HandleAllExceptions(_serviceProvider, GetType()).DoNotWait();
        }

        private async Task OnProjectLoadedAsync(IVsProject project) {
            var pyProj = PythonProject.FromObject(project);
            if (pyProj != null) {

                var sources = GetProjectItems(project);
                var projInfo = new ProjectInfo(this, pyProj, sources);
                projInfo.UpdateTestCases();
                _projectInfo[pyProj.ProjectHome] = projInfo;
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        private void OnProjectUnloaded(object sender, ProjectEventArgs e) {
            if (e.Project != null) {
                var pyProj = PythonProject.FromObject(e.Project);
                ProjectInfo events;
                if (pyProj != null &&
                    _projectInfo.TryGetValue(pyProj.ProjectHome, out events) &&
                    _projectInfo.Remove(pyProj.ProjectHome)) {
                }
            }

            TestContainersUpdated?.Invoke(this, EventArgs.Empty);
        }

        
    }
}
