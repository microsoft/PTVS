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
            this.VirtualNodeName = path;
            this.ExcludeNodeFromScc = true;
        }

        /// <summary>
        /// Show friendly node caption - relative path or normalized absolute path.
        /// </summary>
        public override string Caption {
            get {
                if (_caption == null) {
                    _caption = CommonUtils.CreateFriendlyDirectoryPath(
                        Path.GetDirectoryName(this.ProjectMgr.BaseURI.Uri.LocalPath), this.Url);
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
            return this.ProjectMgr.ImageHandler.GetIconHandle(
                CommonProjectNode.ImageOffset + (Directory.Exists(this.Url)? 
                (int)CommonImageName.SearchPath :
                (int)CommonImageName.MissingSearchPath));
        }

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
                return Path.Combine(Path.GetDirectoryName(this.ProjectMgr.Url), this.VirtualNodeName) + "\\";
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
