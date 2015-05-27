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
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;
using System.Linq;
#if DEV14_OR_LATER
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
#endif

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Node used for a Python package (a directory with __init__ in it).
    /// 
    /// Currently we just provide a specialized icon for the different folder.
    /// </summary>
    class PythonFolderNode : CommonFolderNode {
#if !DEV14_OR_LATER
        private ImageList _imageList;
#endif

        public PythonFolderNode(CommonProjectNode root, ProjectElement element)
            : base(root, element) {
        }

#if DEV14_OR_LATER
        protected override ImageMoniker GetIconMoniker(bool open) {
            if (!ItemNode.IsExcluded && AllChildren.Any(n => ModulePath.IsInitPyFile(n.Url))) {
                return open ? KnownMonikers.PackageFolderOpened : KnownMonikers.PackageFolderClosed;
            }

            return base.GetIconMoniker(open);
        }
#else
        public override object GetIconHandle(bool open) {
            if (ItemNode.IsExcluded) {
                return base.GetIconHandle(open);
            }

            for (HierarchyNode child = this.FirstChild; child != null; child = child.NextSibling) {
                if (ModulePath.IsInitPyFile(child.Url)) {
                    if (_imageList == null) {
#if DEV11_OR_LATER
                        _imageList = Utilities.GetImageList(Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Resources.PythonPackageIcons.png"));
#else
                        _imageList = Utilities.GetImageList(Assembly.GetExecutingAssembly().GetManifestResourceStream("Microsoft.Resources.PythonPackageIcons.bmp"));
#endif
                    }

                    return open ?
                        ((Bitmap)_imageList.Images[0]).GetHicon() :
                        ((Bitmap)_imageList.Images[1]).GetHicon();
                }
            }

            return base.GetIconHandle(open);
        }
#endif


        internal override int ExecCommandOnNode(Guid cmdGroup, uint cmd, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (cmdGroup == ProjectMgr.SharedCommandGuid) {
                switch ((SharedCommands)cmd) {
                    case SharedCommands.OpenCommandPromptHere:
                        var pyProj = ProjectMgr as PythonProjectNode;
                        if (pyProj != null) {
                            return pyProj.OpenCommandPrompt(FullPathToChildren);
                        }
                        break;
                }
            }

            return base.ExecCommandOnNode(cmdGroup, cmd, nCmdexecopt, pvaIn, pvaOut);
        }
    }
}
