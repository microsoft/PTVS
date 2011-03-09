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
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Represents seasrch path entry as a node in the Solution Explorer.
    /// </summary>
    [ComVisible(true)]
    public class CommonSearchPathNode : BaseSearchPathNode {
        private int _index;
        public CommonSearchPathNode(CommonProjectNode project, string path, int index)
            : base(project, path, project.MakeProjectElement("SearchPath", path)) {
            _index = index;
        }

        public override int SortPriority {
            get {
                return CommonConstants.SearchPathNodeMaxSortPriority + _index;
            }            
        }

        public int Index {
            get { return _index;  }
            set { _index = value; }
        }

        protected override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Delete:
                    case VsCommands.Remove:
                        string message = string.Format(
                            DynamicProjectSR.GetString(DynamicProjectSR.SearchPathRemoveConfirmation), 
                            this.Caption);
                        string title = string.Empty;
                        OLEMSGICON icon = OLEMSGICON.OLEMSGICON_WARNING;
                        OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK | OLEMSGBUTTON.OLEMSGBUTTON_OKCANCEL;
                        OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                        int res = Microsoft.VisualStudio.Shell.VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
                        if (res == 1) {
                            Remove(false);
                        }
                        return VSConstants.S_OK;
                }
            }
            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }

        /// <summary>
        /// Generic Search Path Node can only be removed from project.
        /// </summary>        
        protected override bool CanDeleteItem(__VSDELETEITEMOPERATION deleteOperation) {
            return deleteOperation == __VSDELETEITEMOPERATION.DELITEMOP_RemoveFromProject;
        }
        
        public override void Remove(bool removeFromStorage) {
            //Save this search path, because the node can be deleted after call to base Remove()
            string path = this.VirtualNodeName;
            base.Remove(removeFromStorage);
            //Remove entry from project's Search Path
            _project.RemoveSearchPathEntry(path);
        }

        public void Remove() {
            base.Remove(false);
        }
    }
}
