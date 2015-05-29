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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using OleConstants = Microsoft.VisualStudio.OLE.Interop.Constants;
using VsCommands = Microsoft.VisualStudio.VSConstants.VSStd97CmdID;
using VsMenus = Microsoft.VisualStudioTools.Project.VsMenus;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Base class for Search Path nodes. 
    /// </summary>
    internal abstract class BaseSearchPathNode : CommonFolderNode {
        protected PythonProjectNode _project;
        private string _caption;
        private readonly string _path;

        public BaseSearchPathNode(PythonProjectNode project, string path, ProjectElement element)
            : base(project, element) {
            _project = project;
            _path = CommonUtils.TrimEndSeparator(path);
            this.ExcludeNodeFromScc = true;
        }

        public override Guid ItemTypeGuid {
            get {
                return CommonConstants.SearchPathItemTypeGuid;
            }
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

        /// <summary>
        /// Prevent Find in Files from searching these nodes.
        /// </summary>
        public override bool IsSearchable {
            // https://pytools.codeplex.com/workitem/2030
            // If we return true here then the search path will be added to the
            // list of files VS will search in. Because this is actually a
            // folder, VS will crash later if it tries to add a file that is in
            // this folder.
            get { return false; }
        }

#if DEV11_OR_LATER && !DEV14_OR_LATER
        protected override VSOVERLAYICON OverlayIconIndex {
            get {
                return Directory.Exists(Url) || File.Exists(Url) ?
                    base.OverlayIconIndex :
                    (VSOVERLAYICON)__VSOVERLAYICON2.OVERLAYICON_NOTONDISK;
            }
        }
#endif

#if DEV14_OR_LATER
        protected override bool SupportsIconMonikers {
            get { return true; }
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            return (Directory.Exists(Url) || File.Exists(Url)) ?
                KnownMonikers.Reference :
                KnownMonikers.ReferenceWarning;
        }
#else
        public override int ImageIndex {
            get {
                return _project.GetIconIndex(
#if DEV11_OR_LATER
                    PythonProjectImageName.SearchPath
#else
                    (Directory.Exists(Url) || File.Exists(Url)) ? 
                        PythonProjectImageName.SearchPath : 
                        PythonProjectImageName.MissingSearchPath
#endif
                );
            }
        }
#endif


        /// <summary>
        /// Search path node cannot be dragged.
        /// </summary>        
        protected internal override string PrepareSelectedNodesForClipBoard() {
            return null;
        }

        /// <summary>
        /// Search Path Node cannot be excluded.
        /// </summary>
        internal override int ExcludeFromProject() {
            return (int)OleConstants.OLECMDERR_E_NOTSUPPORTED;
        }

        /// <summary>
        /// Disable Copy/Cut/Paste commands on Search Path node.
        /// </summary>
        internal override int QueryStatusOnNode(Guid cmdGroup, uint cmd, IntPtr pCmdText, ref QueryStatusResult result) {
            if (cmdGroup == VsMenus.guidStandardCommandSet97) {
                switch ((VsCommands)cmd) {
                    case VsCommands.Copy:
                    case VsCommands.Cut:
                    case VsCommands.Paste:
                        result |= QueryStatusResult.SUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            } else if (cmdGroup == VsMenus.guidStandardCommandSet2K) {
                switch ((VSConstants.VSStd2KCmdID)cmd) {
                    case VSConstants.VSStd2KCmdID.EXCLUDEFROMPROJECT:
                        result |= QueryStatusResult.NOTSUPPORTED | QueryStatusResult.INVISIBLE;
                        return VSConstants.S_OK;
                }
            }
            return base.QueryStatusOnNode(cmdGroup, cmd, pCmdText, ref result);
        }

        public override string Url {
            get {
                return CommonUtils.GetAbsoluteFilePath(this.ProjectMgr.ProjectHome, _path);
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

        protected internal override void ShowDeleteMessage(IList<HierarchyNode> nodes, __VSDELETEITEMOPERATION action, out bool cancel, out bool useStandardDialog) {
            // Don't prompt if all the nodes are search paths
            useStandardDialog = !nodes.All(n => n is BaseSearchPathNode);
            cancel = false;
        }
    }
}
