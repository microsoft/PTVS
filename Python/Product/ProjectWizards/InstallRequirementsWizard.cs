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

            string description = null;
            try {
                var p = project.Properties.Item("InterpreterDescription");
                if (p != null) {
                    description = p.Value as string;
                }
            } catch (ArgumentException) {
            }

            if (string.IsNullOrEmpty(description)) {
                bool isValid = false;
                try {
                    var p = project.Properties.Item("InterpreterId");
                    isValid = p != null && !string.IsNullOrEmpty(p.Value as string);
                } catch (ArgumentException) {
                }
                if (!isValid) {
                    // We don't have a usable interpreter, so there's no point
                    // going ahead.
                    // Fall out - the user will find what they need when they
                    // try and run or edit, but until then there's no reason to
                    // block them or duplicate all of our help messages into yet
                    // another assembly.
                    return;
                }
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

            var td = new TaskDialog(provider) {
                Title = string.Format("{0} - {1}", project.Name, Resources.PythonToolsForVisualStudio),
                MainInstruction = Resources.InstallRequirementsHeading,
                Content = Resources.InstallRequirementsMessage,
                EnableHyperlinks = true,
                AllowCancellation = true,
            };

            var venv = new TaskDialogButton(
                Resources.InstallRequirementsIntoVirtualEnv,
                Resources.InstallRequirementsIntoVirtualEnvTip
            );
            description = description ?? Resources.DefaultInterpreterDescription;
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
                object inObj = (object)true, outObj = null;
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
                    TaskDialog.ForException(
                        provider,
                        ex,
                        Resources.InstallRequirementsFailed,
                        PythonConstants.IssueTrackerUrl
                    ).ShowModal();
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
