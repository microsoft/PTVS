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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using Microsoft.VisualStudioTools.Project;
using System;
using System.Collections.Generic;
using System.Globalization;
using ErrorHandler = Microsoft.VisualStudio.ErrorHandler;

namespace Microsoft.VisualStudioTools.Navigation
{

    /// <summary>
    /// This is a specialized version of the LibraryNode that handles the dynamic languages
    /// items. The main difference from the generic one is that it supports navigation
    /// to the location inside the source code where the element is defined.
    /// </summary>
    internal abstract class CommonLibraryNode : LibraryNode
    {
        private readonly IVsHierarchy _ownerHierarchy;
        private readonly uint _fileId;
        private readonly TextSpan _sourceSpan;
        private string _fileMoniker;

        protected CommonLibraryNode(LibraryNode parent, string name, string fullName, IVsHierarchy hierarchy, uint itemId, LibraryNodeType type, IList<LibraryNode> children = null) :
            base(parent, name, fullName, type, children: children)
        {
            _ownerHierarchy = hierarchy;
            _fileId = itemId;
        }

        public TextSpan SourceSpan
        {
            get
            {
                return _sourceSpan;
            }
        }

        protected CommonLibraryNode(CommonLibraryNode node) :
            base(node)
        {
            _fileId = node._fileId;
            _ownerHierarchy = node._ownerHierarchy;
            _fileMoniker = node._fileMoniker;
            _sourceSpan = node._sourceSpan;
        }

        protected CommonLibraryNode(CommonLibraryNode node, string newFullName) :
            base(node, newFullName)
        {
            _fileId = node._fileId;
            _ownerHierarchy = node._ownerHierarchy;
            _fileMoniker = node._fileMoniker;
            _sourceSpan = node._sourceSpan;
        }

        public override uint CategoryField(LIB_CATEGORY category)
        {
            switch (category)
            {
                case (LIB_CATEGORY)_LIB_CATEGORY2.LC_MEMBERINHERITANCE:
                    if (NodeType == LibraryNodeType.Members || NodeType == LibraryNodeType.Definitions)
                    {
                        return (uint)_LIBCAT_MEMBERINHERITANCE.LCMI_IMMEDIATE;
                    }
                    break;
            }
            return base.CategoryField(category);
        }

        public override void SourceItems(out IVsHierarchy hierarchy, out uint itemId, out uint itemsCount)
        {
            hierarchy = _ownerHierarchy;
            itemId = _fileId;
            itemsCount = 1;
        }

        public override string UniqueName
        {
            get
            {
                if (string.IsNullOrEmpty(_fileMoniker))
                {
                    ErrorHandler.ThrowOnFailure(_ownerHierarchy.GetCanonicalName(_fileId, out _fileMoniker));
                }
                return string.Format(CultureInfo.InvariantCulture, "{0}/{1}", _fileMoniker, Name);
            }
        }

        public IServiceProvider Site
        {
            get
            {
                return (_ownerHierarchy as ProjectNode).Site;
            }
        }

        public ProjectNode Hierarchy
        {
            get
            {
                return _ownerHierarchy as ProjectNode;
            }
        }
    }
}
