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

using System;
using System.ComponentModel.Composition;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    [Export(typeof(IVsHierarchyItemManager))]
    public class MockVsHierarchyItemManager : IVsHierarchyItemManager
    {
        public event EventHandler<HierarchyItemEventArgs> AfterInvalidateItems { add { } remove { } }

        public IVsHierarchyItem GetHierarchyItem(IVsHierarchy hierarchy, uint itemid)
        {
            throw new NotImplementedException();
        }

        public bool IsChangingItems
        {
            get { throw new NotImplementedException(); }
        }

        public event EventHandler<HierarchyItemEventArgs> OnItemAdded { add { } remove { } }

        public bool TryGetHierarchyItem(IVsHierarchy hierarchy, uint itemid, out IVsHierarchyItem item)
        {
            item = null;
            return false;
        }

        public bool TryGetHierarchyItemIdentity(IVsHierarchy hierarchy, uint itemid, out IVsHierarchyItemIdentity identity)
        {
            throw new NotImplementedException();
        }
    }
}
