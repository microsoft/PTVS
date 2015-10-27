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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Microsoft.VisualStudioTools.Project.Automation {
    /// <summary>
    /// Contains OAReferenceItem objects 
    /// </summary>
    [ComVisible(true), CLSCompliant(false)]
    public class OAReferenceFolderItem : OAProjectItem {
        #region ctors
        internal OAReferenceFolderItem(OAProject project, ReferenceContainerNode node)
            : base(project, node) {
        }

        #endregion

        private new ReferenceContainerNode Node {
            get {
                return (ReferenceContainerNode)base.Node;
            }
        }

        #region overridden methods
        /// <summary>
        /// Returns the project items collection of all the references defined for this project.
        /// </summary>
        public override EnvDTE.ProjectItems ProjectItems {
            get {
                return new OANavigableProjectItems(this.Project, this.Node);
            }
        }


        #endregion
    }
}
