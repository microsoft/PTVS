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

namespace Microsoft.PythonTools.Django
{
    static class Extensions
    {
        internal static IPythonProject GetPythonProject(this EnvDTE.Project project)
        {
            return project.GetCommonProject() as IPythonProject;
        }

        internal static EnvDTE.Project GetProject(this IVsHierarchy hierarchy)
        {
            object project;

            ErrorHandler.ThrowOnFailure(
                hierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.Project);
        }

        internal static object GetCommonProject(this EnvDTE.Project project)
        {
            OAProject oaProj = project as OAProject;
            if (oaProj != null)
            {
                var common = oaProj.Project;
                if (common != null)
                {
                    return common;
                }
            }
            return null;
        }

        internal static Guid GetItemType(this VSITEMSELECTION vsItemSelection)
        {
            Guid typeGuid;
            try
            {
                ErrorHandler.ThrowOnFailure(
                    vsItemSelection.pHier.GetGuidProperty(
                        vsItemSelection.itemid,
                        (int)__VSHPROPID.VSHPROPID_TypeGuid,
                        out typeGuid
                    )
                );
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return Guid.Empty;
            }
            return typeGuid;
        }

        internal static bool IsFolder(this VSITEMSELECTION item)
        {
            return item.GetItemType() == VSConstants.GUID_ItemType_PhysicalFolder ||
                item.itemid == VSConstants.VSITEMID_ROOT;
        }

        internal static bool IsNonMemberItem(this VSITEMSELECTION item)
        {
            object obj;
            try
            {
                ErrorHandler.ThrowOnFailure(
                    item.pHier.GetProperty(
                        item.itemid,
                        (int)__VSHPROPID.VSHPROPID_IsNonMemberItem,
                        out obj
                    )
                );
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return false;
            }
            return (obj as bool?) ?? false;
        }

        internal static string Name(this VSITEMSELECTION item)
        {
            return item.pHier.GetItemName(item.itemid);
        }

        internal static string GetItemName(this IVsHierarchy hier, uint itemid)
        {
            object name;
            ErrorHandler.ThrowOnFailure(hier.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));
            return (string)name;
        }

        internal static VSITEMSELECTION GetParent(this VSITEMSELECTION vsItemSelection)
        {
            object parent;
            ErrorHandler.ThrowOnFailure(
                vsItemSelection.pHier.GetProperty(
                    vsItemSelection.itemid,
                    (int)__VSHPROPID.VSHPROPID_Parent,
                    out parent
                )
            );

            var res = new VSITEMSELECTION();
            var i = parent as int?;
            if (i.HasValue)
            {
                res.itemid = (uint)i.GetValueOrDefault();
            }
            else
            {
                var ip = parent as IntPtr?;
                res.itemid = (uint)ip.GetValueOrDefault().ToInt32();
            }

            res.pHier = vsItemSelection.pHier;
            return res;
        }

        internal static VSITEMSELECTION GetParentFolder(this VSITEMSELECTION vsItemSelection)
        {
            var parent = vsItemSelection.GetParent();
            while (!parent.IsFolder())
            {
                parent = parent.GetParent();
            }
            return parent;
        }
    }
}
