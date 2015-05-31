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

#if DEV14_OR_LATER
                // Use the default Module icon for modules
                res.hImageList = IntPtr.Zero;
                res.Image = res.SelectedImage = 90;
#else
                res.hImageList = _hierarchy.ProjectMgr.ImageHandler.ImageList.Handle;
                res.Image = res.SelectedImage = (ushort)_hierarchy.ImageIndex;
#endif
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
