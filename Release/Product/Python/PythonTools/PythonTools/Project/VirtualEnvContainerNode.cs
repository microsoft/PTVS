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
using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Virtual environment container node.
    /// </summary>
    [ComVisible(true)]
    internal class VirtualEnvContainerNode : HierarchyNode {
        internal const string VirtualEnvNodeVirtualName = "Virtual Environments";
        private PythonProjectNode _projectNode;
        private bool _boldedOnStartup;

        public VirtualEnvContainerNode(PythonProjectNode project)
            : base(project.ProjectMgr) {
            _projectNode = project;
            this.ExcludeNodeFromScc = true;
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new VirtualEnvContainerNodeContainerNodeProperties(this);
        }

        /// <summary>
        /// Gets the default sort priority of this node.
        /// By default returns HierarchyNode. 
        /// </summary>
        public override int SortPriority {
            get {
                return PythonConstants.VirtualEnvContainerNodeSortPriority;
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
            // TODO: This node is not real - should we return null for Url?
            get { return VirtualEnvNodeVirtualName; }
        }

        /// <summary>
        /// Gets the caption of the hierarchy node.        
        /// </summary>
        public override string Caption {
            get { return DynamicProjectSR.GetString(DynamicProjectSR.VirtualEnvs); }
        }

        /// <summary>
        /// Disable inline editing of Caption of a SearchPathContainer Node
        /// </summary>        
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (int)CommonImageName.VirtualEnvContainer);
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
        protected override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.AddVirtualEnv:
                    case PythonConstants.CreateVirtualEnv:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }

            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.AddVirtualEnv:
                        return _projectNode.AddVirtualEnv();
                    case PythonConstants.CreateVirtualEnv:
                        return _projectNode.CreateVirtualEnv();
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
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

        public new PythonProjectNode ProjectMgr {
            get {
                return ((PythonProjectNode)base.ProjectMgr);
            }
        }

        internal void CheckStartupBold() {
            // We can't bold the activate item while we're loading the project because the
            // UI hierarchy isn't initialized yet.  So instead we wait until we get a request
            // for an icon for the startup item's parent, and then we bold it.

            if (!_boldedOnStartup) {
                for (var child = this.FirstChild; child != null; child = child.NextSibling) {
                    if (((VirtualEnvNode)child).IsVirtualEnvEnabled) {
                        ProjectMgr.BoldItem(child, true);
                        break;
                    }
                }
                _boldedOnStartup = true;
            }
        }
    }
}
