// Visual Studio Shared Project
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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.VisualStudioTools.Navigation {
    class ProjectLibraryNode : LibraryNode {
        private readonly CommonProjectNode _project;

        public ProjectLibraryNode(CommonProjectNode project)
            : base(null, project.Caption, project.Caption, LibraryNodeType.PhysicalContainer) {
            _project = project;
        }

        public override uint CategoryField(LIB_CATEGORY category) {
            switch (category) {
                case LIB_CATEGORY.LC_NODETYPE:
                    return (uint)_LIBCAT_NODETYPE.LCNT_PROJECT;
            }
            return base.CategoryField(category);
        }

        public override VSTREEDISPLAYDATA DisplayData {
            get {
                var res = new VSTREEDISPLAYDATA();
#if DEV14_OR_LATER
                // Use the default Reference icon for projects
                res.hImageList = IntPtr.Zero;
                res.Image = res.SelectedImage = 192;
#else
                res.hImageList = _project.ImageHandler.ImageList.Handle;
                res.Image = res.SelectedImage = (ushort)_project.ImageIndex;
#endif
                return res;
            }
        }

        public override StandardGlyphGroup GlyphType {
            get {
                return StandardGlyphGroup.GlyphCoolProject;
            }
        }
    }
}
