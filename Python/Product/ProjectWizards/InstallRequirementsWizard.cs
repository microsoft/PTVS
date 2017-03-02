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
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;
using Project = EnvDTE.Project;
using ProjectItem = EnvDTE.ProjectItem;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class InstallRequirementsWizard : IWizard {
        public void ProjectFinishedGenerating(Project project) {
            if (project.DTE.SuppressUI) {
                return;
            }

            ProjectItem requirementsTxt = null;
            try {
                requirementsTxt = project.ProjectItems.Item("requirements.txt");
            } catch (ArgumentException) {
            }

            if (requirementsTxt == null) {
                return;
            }

            var txt = requirementsTxt.FileNames[0];
            if (!File.Exists(txt)) {
                return;
            }

            try {
                InstallProjectRequirements(project, txt);
            } catch (Exception ex) {
                if (ex.IsCriticalException()) {
                    throw;
                }

                ex.ReportUnhandledException(
                    WizardHelpers.GetProvider(project.DTE), // null here is okay
                    GetType(),
                    allowUI: true
                );
            }
        }

        private static void InstallProjectRequirements(Project project, string requirementsTxt) {
            var target = project as IOleCommandTarget;
            if (target == null) {
                // Project does not implement IOleCommandTarget, so try with DTE
                InstallProjectRequirements_DTE(project, requirementsTxt);
                return;
            }

            IntPtr inObj = IntPtr.Zero;
            try {
                var guid = GuidList.guidPythonToolsCmdSet;
                inObj = Marshal.AllocCoTaskMem(16);
                Marshal.GetNativeVariantForObject(requirementsTxt, inObj);
                ErrorHandler.ThrowOnFailure(target.Exec(
                    ref guid,
                    PkgCmdIDList.cmdidInstallProjectRequirements,
                    (uint)OLECMDEXECOPT.OLECMDEXECOPT_DODEFAULT,
                    inObj,
                    IntPtr.Zero
                ));
            } finally {
                if (inObj != IntPtr.Zero) {
                    Marshal.FreeCoTaskMem(inObj);
                }
            }
        }

        private static void InstallProjectRequirements_DTE(Project project, string requirementsTxt) {
            object inObj = requirementsTxt, outObj = null;
            project.DTE.Commands.Raise(
                GuidList.guidPythonToolsCmdSet.ToString("B"),
                (int)PkgCmdIDList.cmdidInstallProjectRequirements,
                ref inObj,
                ref outObj
            );
        }

        public void BeforeOpeningFile(ProjectItem projectItem) { }
        public void ProjectItemFinishedGenerating(ProjectItem projectItem) { }
        public void RunFinished() { }

        public void RunStarted(
            object automationObject,
            Dictionary<string, string> replacementsDictionary,
            WizardRunKind runKind,
            object[] customParams
        ) { }

        public bool ShouldAddProjectItem(string filePath) {
            return true;
        }
    }
}
