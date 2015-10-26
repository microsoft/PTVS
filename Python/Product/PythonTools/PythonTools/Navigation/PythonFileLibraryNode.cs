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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Text;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation {
    class PythonFileLibraryNode : LibraryNode {
        private readonly HierarchyNode _hierarchy;
        public PythonFileLibraryNode(LibraryNode parent, HierarchyNode hierarchy, string name, string filename, LibraryNodeType libraryNodeType)
            : base(parent, name, filename, libraryNodeType) {
                _hierarchy = hierarchy;
        }

        public override VSTREEDISPLAYDATA DisplayData {
            get {
                var res = new VSTREEDISPLAYDATA();

                // Use the default Module icon for modules
                res.hImageList = IntPtr.Zero;
                res.Image = res.SelectedImage = 90;
                return res;
            }
        }

        public override string Name {
            get {
                if (DuplicatedByName) {
                    StringBuilder sb = new StringBuilder(_hierarchy.Caption);
                    sb.Append(" (");
                    sb.Append(_hierarchy.ProjectMgr.Caption);
                    sb.Append(", ");
                    PythonFileNode.GetPackageName(_hierarchy, sb);
                    sb.Append(')');

                    return sb.ToString();
                }
                return base.Name;
            }
        }

        public override uint CategoryField(LIB_CATEGORY category) {
            switch (category) {
                case LIB_CATEGORY.LC_NODETYPE:
                    return (uint)_LIBCAT_NODETYPE.LCNT_HIERARCHY;
            }
            return base.CategoryField(category);
        }

        public override IVsSimpleObjectList2 DoSearch(VSOBSEARCHCRITERIA2 criteria) {
            var node = _hierarchy as PythonFileNode;
            if(node != null) {
                var analysis = node.GetProjectEntry() as IPythonProjectEntry;

                if (analysis != null) {
                    var exprAnalysis = new ExpressionAnalysis(
                        ((PythonProjectNode)node.ProjectMgr).GetAnalyzer(),
                        criteria.szName.Substring(criteria.szName.LastIndexOf(':') + 1),
                        analysis.Analysis,
                        0,
                        null,
                        null
                    );

                    return EditFilter.GetFindRefLocations(_hierarchy.ProjectMgr.Site, exprAnalysis);
                }
            }
            
            return null;
        }
    }
}
