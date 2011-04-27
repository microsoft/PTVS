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
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.PythonTools.Project {   
    /// <summary>
    /// Search path container node.
    /// </summary>
    [ComVisible(true)]
    public class CommonSearchPathContainerNode : HierarchyNode {
        internal const string SearchPathsNodeVirtualName = "Search Paths";
        private CommonProjectNode _projectNode;

        public CommonSearchPathContainerNode(CommonProjectNode project)
            : base(project.ProjectMgr) {
            _projectNode = project;
            this.VirtualNodeName = SearchPathsNodeVirtualName;
            this.ExcludeNodeFromScc = true;
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new NodeProperties(this);
        }

        /// <summary>
        /// Gets the default sort priority of this node.
        /// By default returns HierarchyNode. 
        /// </summary>
        public override int SortPriority {
            get {
                return CommonConstants.SearchPathContainerNodeSortPriority;
            }
        }

        public override int MenuCommandId {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
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
            get { return this.VirtualNodeName; }
        }

        /// <summary>
        /// Gets the caption of the hierarchy node.        
        /// </summary>
        public override string Caption {
            get { return DynamicProjectSR.GetString(DynamicProjectSR.SearchPaths); }
        }

        /// <summary>
        /// Disable inline editing of Caption of a SearchPathContainer Node
        /// </summary>        
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (int)CommonImageName.SearchPathContainer);
        }

        /// <summary>
        /// Search path node cannot be dragged.
        /// </summary>        
        protected internal override StringBuilder PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// SearchPathContainer Node cannot be excluded, it needs to be removed.
        /// </summary>
        protected override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case CommonConstants.AddSearchPathCommandId:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case CommonConstants.AddSearchPathCommandId:
                        return _projectNode.AddSearchPath();
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        /// <summary>
        /// SearchPathContainer Node cannot be deleted.
        /// </summary>        
        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
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
