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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    sealed class PythonProjectReferenceNode : ProjectReferenceNode {
        public static PythonProjectReferenceNode Create(ProjectNode root, ProjectElement element) {
            var node = new PythonProjectReferenceNode(root, element);
            node.Initialize();
            return node;
        }

        public static PythonProjectReferenceNode Create(ProjectNode root, string referencedProjectName, string projectPath, string projectReference) {
            var node = new PythonProjectReferenceNode(root, referencedProjectName, projectPath, projectReference);
            node.Initialize();
            return node;
        }

        private PythonProjectReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element) { }

        private PythonProjectReferenceNode(ProjectNode project, string referencedProjectName, string projectPath, string projectReference)
            : base(project, referencedProjectName, projectPath, projectReference) { }

        private void Initialize() {
            var solutionEvents = ProjectMgr.Site.GetSolutionEvents();
            solutionEvents.ActiveSolutionConfigurationChanged += EventListener_AfterActiveSolutionConfigurationChange;
            solutionEvents.BuildCompleted += EventListener_BuildCompleted;
            solutionEvents.ProjectLoaded += EventListener_ProjectLoaded;

            var solution = (IVsSolution)ProjectMgr.Site.GetService(typeof(SVsSolution));
            var guid = ReferencedProjectGuid;

            UpdateSearchPathAsync().DoNotWait();
        }

        private void Invalidate() {
            if (ProjectMgr.IsClosing) {
                return;
            }
            ProjectMgr.OnInvalidateItems(Parent);

            UpdateSearchPathAsync().DoNotWait();
        }

        private async Task UpdateSearchPathAsync() {
            var searchPath = ReferencedProjectObject?.GetPythonProject()?.ProjectHome;
            if (string.IsNullOrEmpty(searchPath)) {
                searchPath = await GetOutputPathAsync();
            }

            (ProjectMgr as PythonProjectNode)?.OnInvalidateSearchPath(searchPath, this);
        }

        private async Task<string> GetOutputPathAsync(int retries = 10) {
            while (true) {
                await Task.Delay(50);
                try {
                    return PathUtils.GetParent(ReferencedProjectOutputPath);
                } catch (Exception ex) when (!ex.IsCriticalException()) {
                    Debug.WriteLine(ex.ToUnhandledExceptionMessage(GetType()));
                }

                if (--retries < 0) {
                    Debug.Fail("failed to get output path");
                    return null;
                }
            }
        }

        internal override string ReferencedProjectOutputPath {
            get {
                var outputs = ReferencedProjectBuildOutputs.ToArray();
                return outputs.FirstOrDefault(o => ".pyd".Equals(Path.GetExtension(o), StringComparison.OrdinalIgnoreCase)) ??
                    outputs.FirstOrDefault();
            }
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

        public override bool Remove(bool removeFromStorage) {
            if (base.Remove(removeFromStorage)) {
                (ProjectMgr as PythonProjectNode)?.OnInvalidateSearchPath(null, this);
                return true;
            }
            return false;
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
