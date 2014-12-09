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
using System.IO;
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
        private ProjectReference _curReference;

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
            var solutionEvents = ProjectMgr.Site.GetSolutionEvents();
            solutionEvents.ActiveSolutionConfigurationChanged += EventListener_AfterActiveSolutionConfigurationChange;
            solutionEvents.BuildCompleted += EventListener_BuildCompleted;
            _fileChangeListener.FileChangedOnDisk += FileChangedOnDisk;
            solutionEvents.ProjectLoaded += PythonProjectReferenceNode_ProjectLoaded;
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreterWithProjectReferences);
        }

        private void EventListener_BuildCompleted(object sender, EventArgs e) {
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreterWithProjectReferences);
            ProjectMgr.OnInvalidateItems(Parent);
        }

        private void PythonProjectReferenceNode_ProjectLoaded(object sender, ProjectEventArgs e) {
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreterWithProjectReferences);
            ProjectMgr.OnInvalidateItems(Parent);
        }

        private void EventListener_AfterActiveSolutionConfigurationChange(object sender, EventArgs e) {
            InitializeFileChangeListener();
            AddAnalyzedAssembly(((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreterWithProjectReferences);
            ProjectMgr.OnInvalidateItems(Parent);
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
            var interp = ((PythonProjectNode)ProjectMgr).GetInterpreter() as IPythonInterpreterWithProjectReferences;
            // remove the reference to whatever we are currently observing
            RemoveAnalyzedAssembly(interp);

            if ((e.FileChangeFlag & (VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Add | VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Time | VisualStudio.Shell.Interop._VSFILECHANGEFLAGS.VSFILECHG_Size)) != 0) {
                // kick off the analysis of the new assembly...
                AddAnalyzedAssembly(interp);
            }
        }

        private void RemoveAnalyzedAssembly(IPythonInterpreterWithProjectReferences interp) {
            if (interp != null) {
                if (_curReference != null) {
                    interp.RemoveReference(_curReference);
                    _curReference = null;
                }
            }
        }

        private async void AddAnalyzedAssembly(IPythonInterpreterWithProjectReferences interp) {
            if (interp != null) {
                var asmName = AssemblyName;
                var outFile = ReferencedProjectOutputPath;
                _failedToAnalyze = false;
                _curReference = null;

                if (!string.IsNullOrEmpty(asmName)) {
                    _asmName = new AssemblyName(asmName);
                    _curReference = new ProjectAssemblyReference(_asmName, ReferencedProjectOutputPath);
                } else if (File.Exists(outFile)) {
                    _asmName = null;
                    _curReference = new ProjectReference(outFile, ProjectReferenceKind.ExtensionModule);
                } else {
                    if (ReferencedProjectObject == null ||
                        !Utilities.GuidEquals(PythonConstants.ProjectFactoryGuid, ReferencedProjectObject.Kind)) {
                        // Only failed if the reference isn't to another Python
                        // project.
                        _failedToAnalyze = true;
                    }
                }

                if (_curReference != null) {
                    try {
                        await interp.AddReferenceAsync(_curReference);
                    } catch (Exception) {
                        _failedToAnalyze = true;
                    }
                }
            }
        }

        protected override bool CanShowDefaultIcon() {
            if (_failedToAnalyze) {
                return false;
            }

            return base.CanShowDefaultIcon();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            var solutionEvents = ProjectMgr.Site.GetSolutionEvents();
            solutionEvents.ActiveSolutionConfigurationChanged -= EventListener_AfterActiveSolutionConfigurationChange;
            solutionEvents.BuildCompleted -= EventListener_BuildCompleted;
            solutionEvents.ProjectLoaded -= PythonProjectReferenceNode_ProjectLoaded;

            _fileChangeListener.FileChangedOnDisk -= FileChangedOnDisk;
            _fileChangeListener.Dispose();
        }
    }
}
