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
using System.IO;
using Microsoft.PythonTools.ProjectWizards.Properties;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudioTools;
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

            var provider = WizardHelpers.GetProvider(project.DTE);
            if (provider == null) {
                return;
            }

            string description = Resources.DefaultInterpreterDescription;
            try {
                var p = project.Properties.Item("InterpreterDescription");
                description = (p != null ? p.Value as string : null) ?? description;
            } catch (ArgumentException) {
            }

            var td = new TaskDialog(provider) {
                Title = Resources.PythonToolsForVisualStudio,
                MainInstruction = Resources.InstallRequirementsHeading,
                Content = Resources.InstallRequirementsMessage,
                EnableHyperlinks = true,
                AllowCancellation = true,
            };

            // TODO: Remove for 2.1 RC
            td.Footer = "\t" + Resources.InstallRequirementsFooter;

            var venv = new TaskDialogButton(
                Resources.InstallRequirementsIntoVirtualEnv,
                Resources.InstallRequirementsIntoVirtualEnvTip
            );
            var install = new TaskDialogButton(
                string.Format(Resources.InstallRequirementsIntoGlobalEnv, description),
                Resources.InstallRequirementsIntoGlobalEnvTip
            );
            var goAway = new TaskDialogButton(Resources.InstallRequirementsNowhere);
            td.Buttons.Add(venv);
            td.Buttons.Add(install);
            td.Buttons.Add(goAway);

            try {
                td.ExpandedInformation = File.ReadAllText(txt);
                td.CollapsedControlText = Resources.InstallRequirementsShowPackages;
                td.ExpandedControlText = Resources.InstallRequirementsHidePackages;
            } catch (IOException) {
            } catch (NotSupportedException) {
            } catch (UnauthorizedAccessException) {
            }

            var btn = td.ShowModal();
            int cmdId = 0;
            if (btn == venv) {
                cmdId = (int)PkgCmdIDList.cmdidAddVirtualEnv;
            } else if (btn == install) {
                cmdId = (int)PkgCmdIDList.cmdidInstallRequirementsTxt;
            }
            if (cmdId != 0) {
                object inObj = null, outObj = null;
                try {
                    project.DTE.Commands.Raise(
                        GuidList.guidPythonToolsCmdSet.ToString("B"),
                        cmdId,
                        ref inObj,
                        ref outObj
                    );
                } catch (Exception ex) {
                    if (ex.IsCriticalException()) {
                        throw;
                    }
                    TaskDialog.ForException(provider, ex, Resources.InstallRequirementsFailed).ShowModal();
                }
            }
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
