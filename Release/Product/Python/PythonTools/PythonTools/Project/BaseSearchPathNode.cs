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
using System.IO;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Base class for Search Path nodes. 
    /// </summary>
    public abstract class BaseSearchPathNode : CommonFolderNode {
        protected CommonProjectNode _project;
        private string _caption;

        public BaseSearchPathNode(CommonProjectNode project, string path, ProjectElement element)
            : base(project, path, element) {
            _project = project;
            VirtualNodeName = CommonUtils.TrimEndSeparator(path);
            this.ExcludeNodeFromScc = true;
        }

        /// <summary>
        /// Show friendly node caption - relative path or normalized absolute path.
        /// </summary>
        public override string Caption {
            get {
                if (_caption == null) {
                    _caption = CommonUtils.CreateFriendlyDirectoryPath(this.ProjectMgr.ProjectHome, this.Url);
                }
                return _caption;
            }
        }

        /// <summary>
        /// Disable inline editing of Caption of a Search Path Node
        /// </summary>        
        public override string GetEditLabel() {
            return null;
        }

        public override object GetIconHandle(bool open) {
            return this.ProjectMgr.ImageHandler.GetIconHandle(CommonProjectNode.ImageOffset +
#if DEV11
                (int)CommonImageName.SearchPath
#else
                (Directory.Exists(Url) || File.Exists(Url) ? (int)CommonImageName.SearchPath : (int)CommonImageName.MissingSearchPath)
#endif
            );
        }

#if DEV11
        protected override VSOVERLAYICON OverlayIconIndex {
            get {
                return Directory.Exists(Url) || File.Exists(Url) ? base.OverlayIconIndex : (VSOVERLAYICON)__VSOVERLAYICON2.OVERLAYICON_NOTONDISK;
            }
        }
#endif

        /// <summary>
        /// Search path node cannot be dragged.
        /// </summary>        
        protected internal override StringBuilder PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// Search Path Node cannot be excluded.
        /// </summary>
        protected override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Disable Copy/Cut/Paste commands on Search Path node.
        /// </summary>
        protected override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Copy:
                    case VsCommands.Cut:
                    case VsCommands.Paste:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public override string Url {
            get {
                return CommonUtils.GetAbsoluteFilePath(this.ProjectMgr.ProjectHome, this.VirtualNodeName);
            }
        }

        /// <summary>
        /// Defines whether this node is valid node for painting the Search Path icon.
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
            return new CommonSearchPathNodeProperties(this);
        }
    }
}
