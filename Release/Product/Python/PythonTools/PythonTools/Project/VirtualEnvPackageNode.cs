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
    /// Represents a package installed in a virtual env as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    public class VirtualEnvPackageNode : HierarchyNode {
        protected PythonProjectNode _project;
        private string _caption;

        public VirtualEnvPackageNode(PythonProjectNode project, string name)
            : base(project, new VirtualProjectElement(project)) {
            _caption = name;
        }

        public override string Url {
            get { return _caption; }
        }

        public override Guid ItemTypeGuid {
            get { return VSConstants.GUID_ItemType_VirtualFolder; }
        }

        public override int MenuCommandId {
            get { return VsMenus.IDM_VS_CTXT_ITEMNODE; }
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.UninstallPythonPackage:
                        string message = string.Format(
                            "'{0}' will be uninstalled from the virtual environment",
                            this.Caption);
                        string title = string.Empty;
                        OLEMSGICON icon = OLEMSGICON.OLEMSGICON_WARNING;
                        OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK | OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL;
                        OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                        int res = Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
                        bool shouldRemove = res == 1;
                        if (shouldRemove) {
                            Remove(false);
                        }
                        return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        public new PythonProjectNode ProjectMgr {
            get {
                return (PythonProjectNode)base.ProjectMgr;
            }
        }

        /// <summary>
        /// Virtual env package nodes are removed by "uninstalling" not deleting.  This is
        /// mainly because we don't get all of the delete notifications from solution navigator,
        /// so we can't show our improved prompt.
        /// </summary>        
        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return false;
        }

        public override void Remove(bool removeFromStorage) {
            Parent.BeginPackageChange();
            ProjectMgr.EnqueueVirtualEnvRequest(
                ((VirtualEnvNode)Parent).MakePipCommand("uninstall -y " + _caption),
                "Uninstalling " + _caption,
                "Successfully uninstalled " + _caption,
                "Failed to uninstall " + _caption,
                RemoveSelf,
                Parent.PackageChangeDone
            );
        }

        private void RemoveSelf() {
            Parent.RemoveChild(this);
            ProjectMgr.OnInvalidateItems(Parent);

            Parent.PackageChangeDone();
        }

        public new VirtualEnvNode Parent {
            get {
                return (VirtualEnvNode)base.Parent;
            }
        }

        /// <summary>
        /// Show the name of the package.
        /// </summary>
        public override string Caption {
            get {
                return _caption;
            }
        }

        /// <summary>
        /// Disable inline editing of Caption of a virtual env package node
        /// </summary>        
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (int)CommonImageName.VirtualEnvPackage
            );
        }
        /// <summary>
        /// Virtual env node cannot be dragged.
        /// </summary>        
        protected internal override StringBuilder PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// Virtual env Node cannot be excluded.
        /// </summary>
        protected override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Disable Copy/Cut/Paste commands on Search Path node.
        /// </summary>
        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == GuidList.guidPythonToolsCmdSet) {
                switch (cmd) {
                    case PythonConstants.UninstallPythonPackage:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                        return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the package icon.
        /// </summary>
        /// <returns></returns>
        protected override bool CanShowDefaultIcon() {
            return true;
        }

        public override bool CanAddFiles {
            get {
                return false;
            }
        }

        protected override NodeProperties CreatePropertiesObject() {
            return new VirtualEnvPackageNodeProperties(this);
        }
    }
}
