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
using System.Diagnostics;

using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands2K = Microsoft.VisualStudio.VSConstants.VSStd2KCmdID;
using VSConstants = Microsoft.VisualStudio.VSConstants;

namespace Microsoft.PythonTools.Project {

    public class CommonFolderNode : FolderNode {
        private CommonProjectNode _project;

        public CommonFolderNode(CommonProjectNode root, string path, ProjectElement element)
            : base(root, path, element) {
            _project = root;
        }

        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            //Hide Exclude from Project command, show everything else normal Folder node supports
            if (cmdGroup == Microsoft.PythonTools.Project.VsMenus.guidStandardCommandSet2K) {
                if ((VsCommands2K)cmd == CommonConstants.OpenFolderInExplorerCmdId) {
                    result |= QueryStatusResult.SUPPORTED | QueryStatusResult.ENABLED;
                    return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == Microsoft.PythonTools.Project.VsMenus.guidStandardCommandSet2K) {
                if ((VsCommands2K)cmd == CommonConstants.OpenFolderInExplorerCmdId) {
                    Process.Start(this.Url);
                    return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        public override void RenameFolder(string newName) {
            _project.SuppressFileChangeNotifications();
            try {
                base.RenameFolder(newName);
            } finally {
                _project.RestoreFileChangeNotifications();
            }
        }

        /// <summary>
        /// Common Folder Node can only be deleted from file system.
        /// </summary>        
        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_DeleteFromStorage;
        }
        
#if DEV11
        public override object GetProperty(int propId) {
            CommonFolderNode.BoldStartupOnIcon(propId, this);

            return base.GetProperty(propId);
        }
#else
        public override int SetProperty(int propid, object value) {
            BoldStartupOnExpand(propid, this);

            return base.SetProperty(propid, value);
        }
#endif

        internal static void BoldStartupOnExpand(int propId, HierarchyNode parent) {
            // We can't bold the startup item while we're loading the project because the
            // UI hierarchy isn't initialized yet.  So instead we wait until we get a request
            // for an icon for the startup item's parent, and then we bold it.

            if (propId == (int)__VSHPROPID.VSHPROPID_Expanded) {
                SetBoldStartup(parent);
            }
        }

        internal static void BoldStartupOnIcon(int propId, HierarchyNode parent) {
            // We can't bold the startup item while we're loading the project because the
            // UI hierarchy isn't initialized yet.  So instead we wait until we get a request
            // for an icon for the startup item's parent, and then we bold it.

            if (propId == (int)__VSHPROPID.VSHPROPID_IconIndex || propId == (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex) {
                SetBoldStartup(parent);
            }
        }

        private static void SetBoldStartup(HierarchyNode parent) {
            string startupFile;
            CommonProjectNode comProj = (CommonProjectNode)parent.ProjectMgr;
            HierarchyNode startupItem;
            if (!comProj._boldedStartupItem &&
                (startupFile = comProj.GetStartupFile()) != null &&
                (startupItem = parent.FindChild(CommonUtils.GetAbsoluteFilePath(comProj.ProjectFolder, startupFile), false)) != null) {

                // we're expanding the parent of the 
                comProj.BoldStartupItem(startupItem);
            }
        }
    }
}
