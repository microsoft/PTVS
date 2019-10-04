// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Linq;
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
