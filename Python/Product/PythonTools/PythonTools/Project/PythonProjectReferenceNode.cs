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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Interpreter;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonProjectReferenceNode : ProjectReferenceNode {
        private readonly FileChangeManager _fileChangeListener;
        private string _observing;
        private AssemblyName _asmName;
        private bool _failedToAnalyze;
        private ProjectAssemblyReference _curReference;

        public PythonProjectReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element) {
            _fileChangeListener = new FileChangeManager(ProjectMgr.Site);
            Initialize();
        }

        public PythonProjectReferenceNode(ProjectNode project, string referencedProjectName, string projectPath, string projectReference)
            : base(project, referencedProjectName, projectPath, projectReference) {
            _fileChangeListener = new FileChangeManager(ProjectMgr.Site);
            Initialize();
        }

        private void Initialize() {
            PythonToolsPackage.Instance.SolutionEvents.ActiveSolutionConfigurationChanged += EventListener_AfterActiveSolutionConfigurationChange;
            _fileChangeListener.FileChangedOnDisk += FileChangedOnDisk;
            PythonToolsPackage.Instance.SolutionEvents.ProjectLoaded += PythonProjectReferenceNode_ProjectLoaded;
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter());
        }

        private void PythonProjectReferenceNode_ProjectLoaded(object sender, ProjectEventArgs e) {
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter());
        }

        private void EventListener_AfterActiveSolutionConfigurationChange(object sender, EventArgs e) {
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter());
        }

        private void InitializeFileChangeListener() {
            if (_observing != null) {
                _fileChangeListener.StopObservingItem(_observing);
            }

            _observing = ReferencedProjectOutputPath;
            if (_observing != null) {
                _fileChangeListener.ObserveItem(_observing);
            }
        }

        private void FileChangedOnDisk(object sender, FileChangedOnDiskEventArgs e) {
            var interp = ((PythonProjectNode)ProjectMgr).GetInterpreter();
            // remove the reference to whatever we are currently observing
            RemoveAnalyzedAssembly(interp);

            if ((e.FileChangeFlag & (VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Add | VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Time | VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Size)) != 0) {
                // kick off the analysis of the new assembly...
                AddAnalyzedAssembly(interp);
            }
        }

        private void RemoveAnalyzedAssembly(IPythonInterpreter interp) {
            if (interp != null) {
                if (_curReference != null) {
                    interp.RemoveReference(_curReference);
                    _curReference = null;
                }
            }
        }

        private void AddAnalyzedAssembly(IPythonInterpreter interp) {
            if (interp != null) {
                var asmName = AssemblyName;
                if (asmName != null) {
                    _failedToAnalyze = false;
                    _asmName = new AssemblyName(asmName);
                    _curReference = new ProjectAssemblyReference(_asmName, ReferencedProjectOutputPath);
                    var task = interp.AddReferenceAsync(_curReference);

                    task.ContinueWith(new TaskFailureHandler(TaskScheduler.FromCurrentSynchronizationContext(), this).HandleAddRefFailure);
                } else if(!(ReferencedProjectObject is Microsoft.VisualStudioTools.Project.Automation.OAProject)) {
                    _failedToAnalyze = true;
                }
            }
        }

        protected override bool CanShowDefaultIcon() {
            if (_failedToAnalyze) {
                return false;
            }

            return base.CanShowDefaultIcon();
        }

        class TaskFailureHandler {
            private readonly TaskScheduler _uiScheduler;
            private readonly PythonProjectReferenceNode _node;

            public TaskFailureHandler(TaskScheduler uiScheduler, PythonProjectReferenceNode refNode) {
                _uiScheduler = uiScheduler;
                _node = refNode;
            }

            public void HandleAddRefFailure(Task task) {
                if (task.Exception != null) {
                    Task.Factory.StartNew(MarkFailed, default(CancellationToken), TaskCreationOptions.None, _uiScheduler);
                }
            }

            public void MarkFailed() {
                _node._failedToAnalyze = true;
            }
        }

        public override void Remove(bool removeFromStorage) {
            base.Remove(removeFromStorage);
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            PythonToolsPackage.Instance.SolutionEvents.ActiveSolutionConfigurationChanged -= EventListener_AfterActiveSolutionConfigurationChange;
            PythonToolsPackage.Instance.SolutionEvents.ProjectLoaded -= PythonProjectReferenceNode_ProjectLoaded;

            _fileChangeListener.FileChangedOnDisk -= FileChangedOnDisk;
            _fileChangeListener.Dispose();
        }
    }
}
