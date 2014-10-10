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
