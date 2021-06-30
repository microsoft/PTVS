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
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation {

    /// <summary>
    /// This interface defines the service that finds Python files inside a hierarchy
    /// and builds the informations to expose to the class view or object browser.
    /// </summary>
    [Guid(PythonConstants.LibraryManagerServiceGuid)]
    internal interface IPythonLibraryManager : ILibraryManager {
    }

    /// <summary>
    /// Implementation of the service that builds the information to expose to the symbols
    /// navigation tools (class view or object browser) from the Python files inside a
    /// hierarchy.
    /// </summary>
    [Guid(PythonConstants.LibraryManagerGuid)]
    internal class PythonLibraryManager : LibraryManager, IPythonLibraryManager {
        private readonly Dictionary<PythonProjectNode, AnalysisCompleteHandler> _handlers;

        public PythonLibraryManager(CommonPackage/*!*/ package)
            : base(package) {
            _handlers = new Dictionary<PythonProjectNode, AnalysisCompleteHandler>();
        }

        public override LibraryNode CreateFileLibraryNode(LibraryNode parent, HierarchyNode hierarchy, string name, string filename) {
            return new PythonFileLibraryNode(parent, hierarchy, hierarchy.Caption, filename);
        }

        public override void RegisterHierarchy(IVsHierarchy hierarchy) {
            var project = hierarchy.GetProject()?.GetPythonProject();
            if (project != null) {
                lock (_handlers) {
                    if (!_handlers.ContainsKey(project)) {
                        _handlers[project] = new AnalysisCompleteHandler(this, project);
                    }
                }
            }

            base.RegisterHierarchy(hierarchy);
        }

        public override void UnregisterHierarchy(IVsHierarchy hierarchy) {
            var project = hierarchy.GetProject()?.GetPythonProject();
            if (project != null) {
                lock (_handlers) {
                    AnalysisCompleteHandler handler;
                    if (_handlers.TryGetValue(project, out handler)) {
                        _handlers.Remove(project);
                        handler.Dispose();
                    }
                }
            }

            base.UnregisterHierarchy(hierarchy);
        }

        protected override void OnNewFile(LibraryTask task) {
            if (IsNonMemberItem(task.ModuleID.Hierarchy, task.ModuleID.ItemID)) {
                return;
            }

            var project = task.ModuleID.Hierarchy
                    .GetProject()?
                    .GetPythonProject();
            if (project == null) {
                return;
            }

            AnalysisCompleteHandler handler;
            lock (_handlers) {
                if (!_handlers.TryGetValue(project, out handler)) {
                    _handlers[project] = handler = new AnalysisCompleteHandler(this, project);
                }
            }

            handler.AddTask(task);
        }

        sealed class AnalysisCompleteHandler : IDisposable {
            public readonly PythonProjectNode Project;
            private readonly PythonLibraryManager _owner;
            private readonly Dictionary<string, LibraryTask> _tasks;
            private VsProjectAnalyzer _analyzer;

            public AnalysisCompleteHandler(PythonLibraryManager owner, PythonProjectNode project) {
                Project = project ?? throw new ArgumentNullException(nameof(project));
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
                _tasks = new Dictionary<string, LibraryTask>(StringComparer.OrdinalIgnoreCase);
                Project.ProjectAnalyzerChanging += Project_ProjectAnalyzerChanging;
                _analyzer = Project.TryGetAnalyzer();
                if (_analyzer != null) {
                    _analyzer.AnalysisComplete += Analyzer_AnalysisComplete;
                }
            }

            public void AddTask(LibraryTask task) {
                lock (_tasks) {
                    _tasks[task.FileName] = task;
                }
            }

            public void Dispose() {
                lock (this) {
                    Project.ProjectAnalyzerChanging -= Project_ProjectAnalyzerChanging;
                    if (_analyzer != null) {
                        _analyzer.AnalysisComplete -= Analyzer_AnalysisComplete;
                    }
                }
            }

            private void Project_ProjectAnalyzerChanging(object sender, AnalyzerChangingEventArgs e) {
                lock (this) {
                    if (_analyzer != null) {
                        Debug.Assert(_analyzer == e.Old, "Changing from wrong analyzer");
                        _analyzer.AnalysisComplete -= Analyzer_AnalysisComplete;
                    }
                    _analyzer = e.New as VsProjectAnalyzer;
                    if (_analyzer != null) {
                        _analyzer.AnalysisComplete += Analyzer_AnalysisComplete;
                    }
                }
            }

            public void Analyzer_AnalysisComplete(object sender, Projects.AnalysisCompleteEventArgs e) {
                LibraryTask task;
                lock (_tasks) {
                    if (!_tasks.TryGetValue(e.Path, out task)) {
                        return;
                    }
                }

                _owner.FileParsed(task);
            }
        }
    }
}
