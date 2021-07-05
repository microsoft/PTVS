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

using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Search path container node.
    /// </summary>
    [ComVisible(true)]
    internal class CommonSearchPathContainerNode : HierarchyNode {
        internal const string SearchPathsNodeVirtualName = "Search Paths";
        private PythonProjectNode _projectNode;

        public CommonSearchPathContainerNode(PythonProjectNode project)
            : base(project.ProjectMgr) {
            _projectNode = project;
            this.ExcludeNodeFromScc = true;
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new CommonSearchPathContainerNodeProperties(this);
        }

        /// <summary>
        /// Gets the default sort priority of this node.
        /// </summary>
        public override int SortPriority {
            get { return PythonConstants.SearchPathContainerNodeSortPriority; }
        }

        public override int MenuCommandId {
            get { return PythonConstants.SearchPathContainerMenuId; }
        }

        public override Guid MenuGroupId {
            get { return GuidList.guidPythonToolsCmdSet; }
        }

        /// <summary>
        /// Gets the GUID of the item type of the node.
        /// By default returns System.Guid.Empty. 
        /// Implementations should return an item type GUID from the values listed in VSConstants. 
        /// </summary>
        public override Guid ItemTypeGuid {
            //A GUID constant used to specify that the type is a non-physical folder.
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }

        /// <summary>
        /// Gets the absolute path for this node.
        /// </summary>
        public override string Url {
            // TODO: This node is not real - should we return null for Url?
            get { return SearchPathsNodeVirtualName; }
        }

        /// <summary>
        /// Gets the caption of the hierarchy node.
        /// </summary>
        public override string Caption {
            get { return Strings.SearchPaths; }
        }

        /// <summary>
        /// Disable inline editing of Caption of a SearchPathContainer Node
        /// </summary>
        public override string GetEditLabel() {
            return null;
        }

        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return KnownMonikers.Reference;
        }

        /// <summary>
        /// Search path node cannot be dragged.
        /// </summary>
        protected internal override string PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// SearchPathContainer Node cannot be excluded, it needs to be removed.
        /// </summary>
        internal override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// SearchPathContainer Node cannot be deleted.
        /// </summary>
        internal override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return false;
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the Search Paths icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon() {
            return true;
        }
    }
}
