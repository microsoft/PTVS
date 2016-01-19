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
using System.IO;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.TemplateWizard;
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
                Title = string.Format("{0} - {1}", project.Name, Strings.ProductTitle),
                MainInstruction = Strings.InstallRequirementsHeading,
                Content = Strings.InstallRequirementsMessage,
                EnableHyperlinks = true,
                AllowCancellation = true,
            };

            var venv = new TaskDialogButton(
                Strings.InstallRequirementsIntoVirtualEnv,
                Strings.InstallRequirementsIntoVirtualEnvTip
            );
            description = description ?? Strings.DefaultInterpreterDescription;
            var install = new TaskDialogButton(
                string.Format(Strings.InstallRequirementsIntoGlobalEnv, description),
                Strings.InstallRequirementsIntoGlobalEnvTip
            );
            var goAway = new TaskDialogButton(Strings.InstallRequirementsNowhere);
            td.Buttons.Add(venv);
            td.Buttons.Add(install);
            td.Buttons.Add(goAway);

            try {
                td.ExpandedInformation = File.ReadAllText(txt);
                td.CollapsedControlText = Strings.InstallRequirementsShowPackages;
                td.ExpandedControlText = Strings.InstallRequirementsHidePackages;
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
                        Strings.InstallRequirementsFailed,
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
