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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools {
    static class Extensions {
        internal static Guid GetItemType(this VSITEMSELECTION vsItemSelection) {
            Guid typeGuid;
            try {
                ErrorHandler.ThrowOnFailure(
                    vsItemSelection.pHier.GetGuidProperty(
                        vsItemSelection.itemid,
                        (int)__VSHPROPID.VSHPROPID_TypeGuid,
                        out typeGuid
                    )
                );
            } catch (System.Runtime.InteropServices.COMException) {
                return Guid.Empty;
            }
            return typeGuid;
        }

        public static IEnumerable<string> GetChildren(this VSITEMSELECTION item) {
            return GetChildren(item.pHier, item.itemid);
        }

        /// <summary>
        /// Get children items
        /// </summary>
        private static IEnumerable<string> GetChildren(IVsHierarchy project, uint itemId) {
            for (var childId = GetItemId(GetPropertyValue((int)__VSHPROPID.VSHPROPID_FirstChild, itemId, project));
                childId != VSConstants.VSITEMID_NIL;
                childId = GetItemId(GetPropertyValue((int)__VSHPROPID.VSHPROPID_NextSibling, childId, project))) {

                if ((GetPropertyValue((int)__VSHPROPID.VSHPROPID_IsNonMemberItem, childId, project) as bool?) ?? false) {
                    continue;
                }

                yield return GetCanonicalName(childId, project);
            }
        }

        private static object GetPropertyValue(int propid, uint itemId, IVsHierarchy vsHierarchy) {
            if (itemId == VSConstants.VSITEMID_NIL) {
                return null;
            }

            object o;
            if (ErrorHandler.Failed(vsHierarchy.GetProperty(itemId, propid, out o))) {
                return null;
            }
            return o;
        }


        internal static string GetCanonicalName(this VSITEMSELECTION selection) {
            return GetCanonicalName(selection.itemid, selection.pHier);
        }

        /// <summary>
        /// Get the canonical name
        /// </summary>
        private static string GetCanonicalName(uint itemId, IVsHierarchy hierarchy) {
            Debug.Assert(itemId != VSConstants.VSITEMID_NIL, "ItemId cannot be nill");

            string strRet = string.Empty;
            if (ErrorHandler.Failed(hierarchy.GetCanonicalName(itemId, out strRet))) {
                return string.Empty;
            }
            return strRet;
        }

        /// <summary>
        /// Convert parameter object to ItemId
        /// </summary>
        private static uint GetItemId(object pvar) {
            if (pvar == null) return VSConstants.VSITEMID_NIL;
            if (pvar is int) return (uint)(int)pvar;
            if (pvar is uint) return (uint)pvar;
            if (pvar is short) return (uint)(short)pvar;
            if (pvar is ushort) return (uint)(ushort)pvar;
            if (pvar is long) return (uint)(long)pvar;
            return VSConstants.VSITEMID_NIL;
        }

        internal static bool IsFolder(this VSITEMSELECTION item) {
            return item.GetItemType() == VSConstants.GUID_ItemType_PhysicalFolder ||
                item.itemid == VSConstants.VSITEMID_ROOT;
        }

        internal static bool IsFile(this VSITEMSELECTION item) {
            return item.GetItemType() == VSConstants.GUID_ItemType_PhysicalFile ||
                item.itemid == VSConstants.VSITEMID_ROOT;
        }
    }
}
