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
using System.Collections.Generic;

namespace Microsoft.VisualStudioTools.MockVsTests
{
    public static class HierarchyExtensions
    {
        /// <summary>
        /// Get the top-level items present in the project
        /// </summary>
        public static IEnumerable<HierarchyItem> GetHierarchyItems(this IVsHierarchy project)
        {
            if (project == null)
            {
                throw new ArgumentNullException("project");
            }

            // Each item in VS OM is IVSHierarchy. 
            IVsHierarchy hierarchy = (IVsHierarchy)project;

            return GetChildItems(hierarchy, VSConstants.VSITEMID_ROOT);
        }

        /// <summary>
        /// Get project items
        /// </summary>
        internal static IEnumerable<HierarchyItem> GetChildItems(this IVsHierarchy project, uint itemId)
        {
            for (var childId = GetItemId(GetPropertyValue(project, (int)__VSHPROPID.VSHPROPID_FirstChild, itemId));
                childId != VSConstants.VSITEMID_NIL;
                childId = GetItemId(GetPropertyValue(project, (int)__VSHPROPID.VSHPROPID_NextSibling, childId)))
            {

                yield return new HierarchyItem(project, childId);
            }
        }

        /// <summary>
        /// Convert parameter object to ItemId
        /// </summary>
        private static uint GetItemId(object pvar)
        {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }

        /// <summary>
        /// Get the parameter property value
        /// </summary>
        public static object GetPropertyValue(this IVsHierarchy vsHierarchy, int propid, uint itemId)
        {
            if (itemId == VSConstants.VSITEMID_NIL)
            {
                return null;
            }

            object o;
            if (ErrorHandler.Failed(vsHierarchy.GetProperty(itemId, propid, out o)))
            {
                return null;
            }
            return o;
        }
    }
}
