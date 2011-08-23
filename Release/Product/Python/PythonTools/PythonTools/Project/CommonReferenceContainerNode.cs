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

using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Reference container node for project references.
    /// </summary>
    public class CommonReferenceContainerNode : ReferenceContainerNode {
        public CommonReferenceContainerNode(ProjectNode project)
            : base(project) {
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(ProjectElement element) {
            return new CommonReferenceNode(this.ProjectMgr, element);
        }

        protected override ProjectReferenceNode CreateProjectReferenceNode(VSCOMPONENTSELECTORDATA selectorData) {
            return new CommonReferenceNode(this.ProjectMgr, selectorData.bstrTitle, selectorData.bstrFile, selectorData.bstrProjRef);
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
        protected ReferenceNode BaseCreateReferenceNode(ref VSCOMPONENTSELECTORDATA selectorData) {
            return base.CreateReferenceNode(selectorData);
        }
    }
}
