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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VSLangProj;

namespace Microsoft.VisualStudioTools.Project.Automation
{
    /// <summary>
    /// Represents a project reference of the solution
    /// </summary>
    [ComVisible(true)]
    public class OAProjectReference : OAReferenceBase
    {
        internal OAProjectReference(ProjectReferenceNode projectReference) :
            base(projectReference)
        {
        }

        internal new ProjectReferenceNode BaseReferenceNode {
            get { return (ProjectReferenceNode)base.BaseReferenceNode; }
        }

        #region Reference override
        public override string Culture
        {
            get { return string.Empty; }
        }
        public override string Name
        {
            get { return BaseReferenceNode.ReferencedProjectName; }
        }
        public override string Identity
        {
            get
            {
                return BaseReferenceNode.Caption;
            }
        }
        public override string Path
        {
            get
            {
                return BaseReferenceNode.ReferencedProjectOutputPath;
            }
        }
        public override EnvDTE.Project SourceProject
        {
            get
            {
                if (Guid.Empty == BaseReferenceNode.ReferencedProjectGuid)
                {
                    return null;
                }
                IVsHierarchy hierarchy = VsShellUtilities.GetHierarchy(BaseReferenceNode.ProjectMgr.Site, BaseReferenceNode.ReferencedProjectGuid);
                if (null == hierarchy)
                {
                    return null;
                }
                object extObject;
                if (Microsoft.VisualStudio.ErrorHandler.Succeeded(
                        hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out extObject)))
                {
                    return extObject as EnvDTE.Project;
                }
                return null;
            }
        }
        public override prjReferenceType Type
        {
            // TODO: Write the code that finds out the type of the output of the source project.
            get { return prjReferenceType.prjReferenceTypeAssembly; }
        }
        public override string Version
        {
            get { return string.Empty; }
        }
        #endregion
    }
}
