
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.TestAdapter {
    internal static class VsProjectExtensions {


        public static IVsProject PathToProject(string filePath, IVsRunningDocumentTable rdt) {
            IVsHierarchy hierarchy;
            uint itemId;
            IntPtr docData = IntPtr.Zero;
            uint cookie;
            try {
                var hr = rdt.FindAndLockDocument(
                    (uint)_VSRDTFLAGS.RDT_NoLock,
                    filePath,
                    out hierarchy,
                    out itemId,
                    out docData,
                    out cookie);
                ErrorHandler.ThrowOnFailure(hr);
            } finally {
                if (docData != IntPtr.Zero) {
                    Marshal.Release(docData);
                    docData = IntPtr.Zero;
                }
            }

            return hierarchy as IVsProject;
        }

        public static string GetNameProperty(this IVsHierarchy project) {
            object value;
            ErrorHandler.ThrowOnFailure(project.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_Name,
                out value
            ));
            return value as string;
        }

        public static string GetProjectHome(this IVsProject project) {
            Debug.Assert(project != null);
            var hier = (IVsHierarchy)project;
            object extObject;
            ErrorHandler.ThrowOnFailure(hier.GetProperty(
                (uint)VSConstants.VSITEMID.Root,
                (int)__VSHPROPID.VSHPROPID_ExtObject,
                out extObject
            ));
            var proj = extObject as EnvDTE.Project;
            if (proj == null) {
                return null;
            }
            var props = proj.Properties;
            if (props == null) {
                return null;
            }
            try {
                var projHome = props.Item("ProjectHome");
                if (projHome == null) {
                    return null;
                }

                return projHome.Value as string;
            } catch {
                return null;
            }
        }

        public static IEnumerable<IVsProject> EnumerateLoadedProjects(this IVsSolution solution) {
            var guid = new Guid(PythonConstants.ProjectFactoryGuid);
            IEnumHierarchies hierarchies;
            ErrorHandler.ThrowOnFailure((solution.GetProjectEnum(
                (uint)(__VSENUMPROJFLAGS.EPF_MATCHTYPE | __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION),
                ref guid,
                out hierarchies)));
            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            while (ErrorHandler.Succeeded(hierarchies.Next(1, hierarchy, out fetched)) && fetched == 1) {
                var project = hierarchy[0] as IVsProject;
                if (project != null) {
                    yield return project;
                }
            }
        }

        /// <summary>
        /// Get the items present in the project
        /// </summary>
        public static IEnumerable<string> GetProjectItems(this IVsProject project) {
            Debug.Assert(project != null, "Project is not null");

            // Each item in VS OM is IVSHierarchy. 
            IVsHierarchy hierarchy = (IVsHierarchy)project;

            return GetProjectItems(hierarchy, VSConstants.VSITEMID_ROOT);
        }

        /// <summary>
        /// Get project items
        /// </summary>
        private static IEnumerable<string> GetProjectItems(IVsHierarchy project, uint itemId) {

            for (var childId = GetItemId(GetPropertyValue((int)__VSHPROPID.VSHPROPID_FirstChild, itemId, project));
                 childId != VSConstants.VSITEMID_NIL;
                 childId = GetItemId(GetPropertyValue((int)__VSHPROPID.VSHPROPID_NextSibling, childId, project))) {

                if ((GetPropertyValue((int)__VSHPROPID.VSHPROPID_IsNonMemberItem, childId, project) as bool?) ?? false) {
                    continue;
                }

                foreach (string item in GetProjectItems(project, childId)) {
                    yield return item;
                }

                string childPath = GetCanonicalName(childId, project);
                yield return childPath;
            }
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

        /// <summary>
        /// Get the parameter property value
        /// </summary>
        private static object GetPropertyValue(int propid, uint itemId, IVsHierarchy vsHierarchy) {
            if (itemId == VSConstants.VSITEMID_NIL) {
                return null;
            }

            try {
                object o;
                ErrorHandler.ThrowOnFailure(vsHierarchy.GetProperty(itemId, propid, out o));

                return o;
            } catch (System.NotImplementedException) {
                return null;
            } catch (System.Runtime.InteropServices.COMException) {
                return null;
            } catch (System.ArgumentException) {
                return null;
            }
        }


        /// <summary>
        /// Get the canonical name
        /// </summary>
        private static string GetCanonicalName(uint itemId, IVsHierarchy hierarchy) {
            Debug.Assert(itemId != VSConstants.VSITEMID_NIL, "ItemId cannot be nill");

            string strRet = string.Empty;
            int hr = hierarchy.GetCanonicalName(itemId, out strRet);

            if (hr == VSConstants.E_NOTIMPL) {
                // Special case E_NOTIMLP to avoid perf hit to throw an exception.
                return string.Empty;
            } else {
                try {
                    ErrorHandler.ThrowOnFailure(hr);
                } catch (System.Runtime.InteropServices.COMException) {
                    strRet = string.Empty;
                }

                // This could be in the case of S_OK, S_FALSE, etc.
                return strRet;
            }
        }
        
    }
}
