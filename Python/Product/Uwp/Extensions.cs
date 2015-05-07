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
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools.Project;
using Microsoft.VisualStudioTools.Project.Automation;

namespace Microsoft.PythonTools.Uwp {
    static class Extensions {

        internal static string GetProjectProperty(this IVsHierarchy projectNode, string name) {
            return projectNode.GetProject().GetPythonProject().GetProperty(name);
        }

        internal static bool IsAppxPackageableProject(this IVsHierarchy projectNode) {
            var appxProp = projectNode.GetProjectProperty(ProjectFileConstants.AppxPackage);
            var containerProp = projectNode.GetProjectProperty(ProjectFileConstants.WindowsAppContainer);

            return Convert.ToBoolean(appxProp) && Convert.ToBoolean(containerProp);
        }

        internal static IPythonProject2 GetPythonProject(this EnvDTE.Project project) {
            return project.GetCommonProject() as IPythonProject2;
        }

        internal static EnvDTE.Project GetProject(this IVsHierarchy hierarchy) {
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

        internal static object GetCommonProject(this EnvDTE.Project project) {
            OAProject oaProj = project as OAProject;
            if (oaProj != null) {
                var common = oaProj.Project;
                if (common != null) {
                    return common;
                }
            }
            return null;
        }

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

        internal static bool IsFolder(this VSITEMSELECTION item) {
            return item.GetItemType() == VSConstants.GUID_ItemType_PhysicalFolder ||
                item.itemid == VSConstants.VSITEMID_ROOT;
        }

        internal static bool IsNonMemberItem(this VSITEMSELECTION item) {
            object obj;
            try {
                ErrorHandler.ThrowOnFailure(
                    item.pHier.GetProperty(
                        item.itemid,
                        (int)__VSHPROPID.VSHPROPID_IsNonMemberItem,
                        out obj
                    )
                );
            } catch (System.Runtime.InteropServices.COMException) {
                return false;
            }
            return (obj as bool?) ?? false;
        }

        internal static string Name(this VSITEMSELECTION item) {
            return item.pHier.GetItemName(item.itemid);
        }

        internal static string GetItemName(this IVsHierarchy hier, uint itemid) {
            object name;
            ErrorHandler.ThrowOnFailure(hier.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));
            return (string)name;
        }

        internal static VSITEMSELECTION GetParent(this VSITEMSELECTION vsItemSelection) {
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
            if (i.HasValue) {
                res.itemid = (uint)i.GetValueOrDefault();
            } else {
                var ip = parent as IntPtr?;
                res.itemid = (uint)ip.GetValueOrDefault().ToInt32();
            }
            
            res.pHier = vsItemSelection.pHier;
            return res;
        }

        internal static VSITEMSELECTION GetParentFolder(this VSITEMSELECTION vsItemSelection) {
            var parent = vsItemSelection.GetParent();
            while (!parent.IsFolder()) {
                parent = parent.GetParent();
            }
            return parent;
        }
    }
}
