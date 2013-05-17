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
using Microsoft.PythonTools.Django.TemplateParsing;
using Microsoft.PythonTools.Project;
using Microsoft.PythonTools.Project.Automation;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudioTools.Project.Automation;

namespace Microsoft.PythonTools.Django {
    static class Extensions {
        internal static IPythonProject GetPythonProject(this EnvDTE.Project project) {
            return project.GetCommonProject() as IPythonProject;
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

        internal static string GetFilePath(this ITextBuffer textBuffer) {
            ITextDocument textDocument;
            TemplateProjectionBuffer projBuffer;
            if (textBuffer.Properties.TryGetProperty<ITextDocument>(typeof(ITextDocument), out textDocument)) {
                return textDocument.FilePath;
            } else if(textBuffer.Properties.TryGetProperty<TemplateProjectionBuffer>(typeof(TemplateProjectionBuffer), out projBuffer)) {
                return projBuffer.DiskBuffer.GetFilePath();
            } else {
                return null;
            }
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
            res.itemid = (uint)((IntPtr)parent).ToInt32();
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
