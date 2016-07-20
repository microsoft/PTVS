// Visual Studio Shared Project
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

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.VisualStudioTools.Project {
    /// <summary>
    /// Reference container node for project references.
    /// </summary>
    internal class CommonReferenceContainerNode : ReferenceContainerNode {
        internal CommonReferenceContainerNode(ProjectNode project)
            : base(project) {
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(ProjectElement element) {
            return new ProjectReferenceNode(this.ProjectMgr, element);
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            return new ProjectReferenceNode(this.ProjectMgr, selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new NodeProperties(this);
        }

        /// <summary>
        /// Creates a reference node.  By default we don't add references and this returns null.
        /// </summary>
        protected override ReferenceNode CreateReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            return base.CreateReferenceNode(selectorData);
        }

        /// <summary>
        /// Exposed for derived classes to re-enable reference support.
        /// </summary>
        internal ReferenceNode BaseCreateReferenceNode(ref VSCOMPONENTSELECTORDATA selectorData) {
            return base.CreateReferenceNode(selectorData);
        }
    }
}
