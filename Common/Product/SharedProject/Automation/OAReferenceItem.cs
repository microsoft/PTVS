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

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.Project.Automation {
    /// <summary>
    /// Represents the automation object equivalent to a ReferenceNode object
    /// </summary>
    [ComVisible(true), CLSCompliant(false)]
    public class OAReferenceItem : OAProjectItem {
        #region ctors
        internal OAReferenceItem(OAProject project, ReferenceNode node)
            : base(project, node) {
        }

        #endregion

        private new ReferenceNode Node {
            get {
                return (ReferenceNode)base.Node;
            }
        }

        #region overridden methods
        /// <summary>
        /// Not implemented. If called throws invalid operation exception.
        /// </summary>
        public override void Delete() {
            throw new InvalidOperationException();
        }


        /// <summary>
        /// Not implemented. If called throws invalid operation exception.
        /// </summary>
        /// <param name="viewKind"> A Constants. vsViewKind indicating the type of view to use.</param>
        /// <returns></returns>
        public override EnvDTE.Window Open(string viewKind) {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Gets or sets the name of the object.
        /// </summary>
        public override string Name {
            get {
                return base.Name;
            }
            set {
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Gets the ProjectItems collection containing the ProjectItem object supporting this property.
        /// </summary>
        public override EnvDTE.ProjectItems Collection {
            get {
                // Get the parent node (ReferenceContainerNode)
                ReferenceContainerNode parentNode = this.Node.Parent as ReferenceContainerNode;
                Debug.Assert(parentNode != null, "Failed to get the parent node");

                // Get the ProjectItems object for the parent node
                if (parentNode != null) {
                    // The root node for the project
                    return ((OAReferenceFolderItem)parentNode.GetAutomationObject()).ProjectItems;
                }

                return null;
            }
        }
        #endregion
    }
}
