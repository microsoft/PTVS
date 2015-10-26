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

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using EnvDTE;

namespace Microsoft.VisualStudioTools.Project.Automation {
    /// <summary>
    /// Represents an automation object for a folder in a project
    /// </summary>
    [ComVisible(true)]
    public class OAFolderItem : OAProjectItem {
        #region ctors
        internal OAFolderItem(OAProject project, FolderNode node)
            : base(project, node) {
        }

        #endregion

        private new FolderNode Node {
            get {
                return (FolderNode)base.Node;
            }
        }


        #region overridden methods
        public override ProjectItems Collection {
            get {
                ProjectItems items = new OAProjectItems(this.Project, this.Node.Parent);
                return items;
            }
        }

        public override ProjectItems ProjectItems {
            get {
                return new OAProjectItems(Project, Node);
            }
        }
        #endregion
    }
}
