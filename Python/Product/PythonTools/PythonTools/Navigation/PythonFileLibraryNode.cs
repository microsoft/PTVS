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

using Microsoft.PythonTools.Intellisense;
using Microsoft.PythonTools.Language;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudioTools.Navigation;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.Navigation
{
    class PythonFileLibraryNode : LibraryNode
    {
        private readonly HierarchyNode _hierarchy;
        public PythonFileLibraryNode(LibraryNode parent, HierarchyNode hierarchy, string name, string filename)
            : base(parent, name, filename, LibraryNodeType.Namespaces, children: new PythonFileChildren((FileNode)hierarchy))
        {
            _hierarchy = hierarchy;

            ((PythonFileChildren)Children)._parent = this;
        }


        public override VSTREEDISPLAYDATA DisplayData
        {
            get
            {
                var res = new VSTREEDISPLAYDATA();

                // Use the default Module icon for modules
                res.hImageList = IntPtr.Zero;
                res.Image = res.SelectedImage = 90;
                return res;
            }
        }

        public override string Name
        {
            get
            {
                if (DuplicatedByName)
                {
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

        public override uint CategoryField(LIB_CATEGORY category)
        {
            switch (category)
            {
                case LIB_CATEGORY.LC_NODETYPE:
                    return (uint)_LIBCAT_NODETYPE.LCNT_HIERARCHY;
            }
            return base.CategoryField(category);
        }

        public override IVsSimpleObjectList2 DoSearch(VSOBSEARCHCRITERIA2 criteria)
        {
            var node = _hierarchy as PythonFileNode;
            if (node != null)
            {
                var analysis = node.TryGetAnalysisEntry();

                if (analysis != null)
                {
                    string expr = criteria.szName.Substring(criteria.szName.LastIndexOf(':') + 1);
                    var exprAnalysis = analysis.Analyzer.WaitForRequest(analysis.Analyzer.AnalyzeExpressionAsync(
                        analysis,
                        criteria.szName.Substring(criteria.szName.LastIndexOf(':') + 1),
                        new SourceLocation(1, 1)
                    ), "PythonFileLibraryNode.DoSearch");

                    if (exprAnalysis != null)
                    {
                        return EditFilter.GetFindRefLocations(analysis.Analyzer, _hierarchy.ProjectMgr.Site, expr, exprAnalysis.Variables);
                    }
                }
            }

            return null;
        }
    }

    abstract class ObjectBrowserChildren : IList<LibraryNode>
    {
        private LibraryNode[] _children;
        protected readonly FileNode _hierarchy;
        internal LibraryNode _parent;

        public ObjectBrowserChildren(FileNode hierarchy)
        {
            _hierarchy = hierarchy;
        }

        public void EnsureChildren()
        {
            if (_children == null)
            {
                IEnumerable<CompletionResult> members = GetChildren();
                List<LibraryNode> children = new List<LibraryNode>();
                foreach (var member in members.MaybeEnumerate())
                {
                    var memberChildren = new MemberChildren(_hierarchy, GetName(member.Name));
                    var node = new PythonLibraryNode(
                        _parent,
                        member,
                        _hierarchy.ProjectMgr,
                        _hierarchy.ID,
                        memberChildren
                    );
                    memberChildren._parent = node;
                    children.Add(node);
                }
                _children = children.ToArray();
            }
        }

        protected abstract IEnumerable<CompletionResult> GetChildren();
        protected abstract string GetName(string member);

        public LibraryNode this[int index]
        {
            get
            {
                EnsureChildren();
                return _children[index];
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public int Count
        {
            get
            {
                EnsureChildren();
                return _children.Length;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return true;
            }
        }

        public void Add(LibraryNode item)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(LibraryNode item)
        {
            EnsureChildren();
            return _children.Contains(item);
        }

        public void CopyTo(LibraryNode[] array, int arrayIndex)
        {
            EnsureChildren();
            _children.CopyTo(array, arrayIndex);
        }

        public IEnumerator<LibraryNode> GetEnumerator()
        {
            EnsureChildren();
            return ((IEnumerable<LibraryNode>)_children).GetEnumerator();
        }

        public int IndexOf(LibraryNode item)
        {
            EnsureChildren();
            return Array.IndexOf(_children, item);
        }

        public void Insert(int index, LibraryNode item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(LibraryNode item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _children.GetEnumerator();
        }
    }

    class PythonFileChildren : ObjectBrowserChildren
    {
        public PythonFileChildren(FileNode hierarchy) : base(hierarchy)
        {
        }

        protected override IEnumerable<CompletionResult> GetChildren()
        {
            var analysis = (_hierarchy as PythonFileNode)?.TryGetAnalysisEntry();
            if (analysis == null)
            {
                return Enumerable.Empty<CompletionResult>();
            }
            var members = analysis.Analyzer.WaitForRequest(analysis.Analyzer.GetAllAvailableMembersAsync(
                analysis,
                new SourceLocation(1, 1),
                GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DetailedInformation
            ), "PythonFileChildren.GetChildren");
            return members;
        }

        protected override string GetName(string member)
        {
            return member;
        }
    }

    class MemberChildren : ObjectBrowserChildren
    {
        private readonly string _member;

        public MemberChildren(FileNode hierarchy, string member) : base(hierarchy)
        {
            _member = member;
        }

        protected override IEnumerable<CompletionResult> GetChildren()
        {
            var analysis = (_hierarchy as PythonFileNode)?.TryGetAnalysisEntry();
            if (analysis == null)
            {
                return Enumerable.Empty<CompletionResult>();
            }
            var members = analysis.Analyzer.WaitForRequest(analysis.Analyzer.GetMembersAsync(
                analysis,
                _member,
                new SourceLocation(1, 1),
                GetMemberOptions.ExcludeBuiltins | GetMemberOptions.DetailedInformation | GetMemberOptions.DeclaredOnly |
                GetMemberOptions.NoMemberRecursion
            ), "MemberChildren.GetChildren");
            return members;
        }

        protected override string GetName(string member)
        {
            return _member + "." + member;
        }
    }
}
