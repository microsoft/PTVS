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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Microsoft.PythonTools.Project {
    public class CommonReferenceNode : ProjectReferenceNode {
        public CommonReferenceNode(ProjectNode root, ProjectElement element)
            : base(root, element) { }

        public CommonReferenceNode(ProjectNode project, string referencedProjectName, string projectPath, string projectReference)
            : base(project, referencedProjectName, projectPath, projectReference) { }

        /// <summary>
        /// Gets a Project type string for a specified project instance guid
        /// </summary>
        /// <returns>The project type string</returns>
        protected string GetProjectType() {
            IVsHierarchy hierarchy = VsShellUtilities.GetHierarchy(this.ProjectMgr.Site, this.ReferencedProjectGuid);
            object projectType;
            ErrorHandler.ThrowOnFailure(hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_TypeName, out projectType));
            return projectType as string;
        }

        /// <summary>
        /// Evaluates all file node children of the project and returns true if anyone has subtype set to Form
        /// </summary>
        /// <returns>true if a file node with subtype Form is found</returns>
        protected bool HasFormItems() {
            if (!File.Exists(this.ReferencedProjectOutputPath)) {
                List<CommonFileNode> fileNodes = new List<CommonFileNode>();
                this.ProjectMgr.FindNodesOfType<CommonFileNode>(fileNodes);
                foreach (CommonFileNode node in fileNodes) {
                    if (node.IsFormSubType) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a reference can be added to the project. 
        /// It calls base to see if the reference is not already there,
        /// and that it is not circular reference.
        /// If the target project is a dynamic project too we can not add the project reference 
        /// because this scenario is not supported yet.
        /// </summary>
        /// <param name="errorHandler">The error handler delegate to return</param>
        /// <returns>false if reference cannot be added, otherwise true</returns>
        protected override bool CanAddReference(out CannotAddReferenceErrorMessage errorHandler) {
            //finally we must evaluate the the rules applied on the base class
            if (!base.CanAddReference(out errorHandler)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shows Visual Studio message box with error message regarding project to project reference.
        /// The message box is not show in case the method has been called from automation
        /// </summary>
        private void ShowProjectReferenceErrorMessage() {
            if (!Utilities.IsInAutomationFunction(this.ProjectMgr.Site)) {
                string message = DynamicProjectSR.GetString(DynamicProjectSR.ProjectReferenceError, CultureInfo.CurrentUICulture);
                string title = string.Empty;
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
            }
        }

        /// <summary>
        /// Shows Visual Studio message box with error message regarding project to project reference. Target Project must be built before
        /// adding the the project to project reference.
        /// The message box is not show in case the method has been called from automation
        /// </summary>
        private void ShowProjectReferenceErrorMessage2() {
            if (!Utilities.IsInAutomationFunction(this.ProjectMgr.Site)) {
                string message = DynamicProjectSR.GetString(DynamicProjectSR.ProjectReferenceError2, CultureInfo.CurrentUICulture);
                string title = string.Empty;
                OLEMSGICON icon = OLEMSGICON.OLEMSGICON_CRITICAL;
                OLEMSGBUTTON buttons = OLEMSGBUTTON.OLEMSGBUTTON_OK;
                OLEMSGDEFBUTTON defaultButton = OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST;
                VsShellUtilities.ShowMessageBox(this.ProjectMgr.Site, title, message, icon, buttons, defaultButton);
            }
        }
    }
}
