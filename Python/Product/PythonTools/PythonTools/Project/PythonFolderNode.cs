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
using System.Linq;
using Microsoft.PythonTools.Analysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Project {
    /// <summary>
    /// Node used for a Python package (a directory with __init__ in it).
    /// 
    /// Currently we just provide a specialized icon for the different folder.
    /// </summary>
    class PythonFolderNode : CommonFolderNode {
        public PythonFolderNode(CommonProjectNode root, ProjectElement element)
            : base(root, element) {
        }

        protected override ImageMoniker GetIconMoniker(bool open) {
            if (!ItemNode.IsExcluded && AllChildren.Any(n => ModulePath.IsInitPyFile(n.Url))) {
                return open ? KnownMonikers.PackageFolderOpened : KnownMonikers.PackageFolderClosed;
            }

            return base.GetIconMoniker(open);
        }

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
