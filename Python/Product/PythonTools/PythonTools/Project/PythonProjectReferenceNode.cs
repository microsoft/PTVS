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
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    class PythonProjectReferenceNode : ProjectReferenceNode {
        public PythonProjectReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element) {
            Initialize();
        }

        public PythonProjectReferenceNode(ProjectNode project, string referencedProjectName, string projectPath, string projectReference)
            : base(project, referencedProjectName, projectPath, projectReference) {
            Initialize();
        }

        private void Initialize() {
            var solutionEvents = ProjectMgr.Site.GetSolutionEvents();
            solutionEvents.ActiveSolutionConfigurationChanged += EventListener_AfterActiveSolutionConfigurationChange;
            solutionEvents.BuildCompleted += EventListener_BuildCompleted;
            solutionEvents.ProjectLoaded += EventListener_ProjectLoaded;

            var solution = (IVsSolution)ProjectMgr.Site.GetService(typeof(SVsSolution));
            var guid = ReferencedProjectGuid;
            IVsHierarchy hier;
            int hr = solution.GetProjectOfGuid(ref guid, out hier);
            if (ErrorHandler.Succeeded(hr)) {
                var searchPath = PathUtils.GetParent(ReferencedProjectOutputPath);
                (ProjectMgr as PythonProjectNode)?.OnInvalidateSearchPath(searchPath, this);
            }
        }

        private void Invalidate() {
            if (ProjectMgr.IsClosing) {
                return;
            }
            ProjectMgr.OnInvalidateItems(Parent);

            var searchPath = PathUtils.GetParent(ReferencedProjectOutputPath);
            (ProjectMgr as PythonProjectNode)?.OnInvalidateSearchPath(searchPath, this);
        }

        private void EventListener_BuildCompleted(object sender, EventArgs e) {
            Invalidate();
        }

        private void EventListener_ProjectLoaded(object sender, ProjectEventArgs e) {
            Guid proj;
            if (ErrorHandler.Succeeded(((IVsHierarchy)e.Project).GetGuidProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ProjectIDGuid,
                out proj
            )) && ReferencedProjectGuid == proj) {
                Invalidate();
            }
        }

        private void EventListener_AfterActiveSolutionConfigurationChange(object sender, EventArgs e) {
            Invalidate();
        }

        protected override void Dispose(bool disposing) {
            base.Dispose(disposing);

            if (disposing) {
                var solutionEvents = ProjectMgr.Site.GetSolutionEvents();
                solutionEvents.ActiveSolutionConfigurationChanged -= EventListener_AfterActiveSolutionConfigurationChange;
                solutionEvents.BuildCompleted -= EventListener_BuildCompleted;
                solutionEvents.ProjectLoaded -= EventListener_ProjectLoaded;
            }
        }
    }
}
