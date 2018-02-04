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
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;
using Project = EnvDTE.Project;
using ProjectItem = EnvDTE.ProjectItem;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class CloudServiceWizard : IWizard {
        private const string RequiredPackage = "Microsoft.VisualStudio.Component.Azure.Waverton";

        private IWizard _wizard;

        [Conditional("DEBUG")]
        private void AssertWizard([CallerMemberName] string caller = null) {
            if (_wizard == null) {
                Debug.Fail($"{caller ?? "Function"} called with no wizard");
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) {
            AssertWizard();
            _wizard?.BeforeOpeningFile(projectItem);
        }

        public void ProjectFinishedGenerating(Project project) {
            AssertWizard();
            _wizard?.ProjectFinishedGenerating(project);
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem) {
            AssertWizard();
            _wizard?.ProjectItemFinishedGenerating(projectItem);
        }

        public void RunFinished() {
            AssertWizard();
            _wizard?.RunFinished();
        }

        public bool ShouldAddProjectItem(string filePath) {
            AssertWizard();
            return _wizard?.ShouldAddProjectItem(filePath) ?? false;
        }

        private static bool AreToolsInstalled(IServiceProvider provider) {
#if DEV15_OR_LATER
            var setupService = provider.GetService(typeof(SVsSetupCompositionService)) as IVsSetupCompositionService;
            // If we fail to get the setup service, we're probably in a whole lot of trouble
            // Likely the "install" step will fail too, but at least we'll have given users a
            // hint and they might go to Setup and repair things.
            return setupService?.IsPackageInstalled(RequiredPackage) ?? false;
#else
            return true;
#endif
        }

        private static void InstallTools(IServiceProvider provider) {
            var svc = (IVsTrackProjectRetargeting2)provider.GetService(typeof(SVsTrackProjectRetargeting));
            if (svc != null) {
                IVsProjectAcquisitionSetupDriver driver;
                if (ErrorHandler.Succeeded(svc.GetSetupDriver(VSConstants.SetupDrivers.SetupDriver_VS, out driver)) &&
                    driver != null) {
                    var task = driver.Install(RequiredPackage);
                    if (task != null) {
                        task.Start();
                        throw new WizardCancelledException();
                    }
                }
            }
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            var provider = WizardHelpers.GetProvider(automationObject);

            try {
                if (AreToolsInstalled(provider)) {
                    // If we fail to find the wizard, we will redirect the user to
                    // install the required packages.
                    var asm = Assembly.Load("Microsoft.VisualStudio.CloudService.Wizard,Version=1.0.0.0,Culture=neutral,PublicKeyToken=b03f5f7f11d50a3a");
                    var type = asm.GetType("Microsoft.VisualStudio.CloudService.Wizard.CloudServiceWizard");
                    _wizard = type.InvokeMember(null, BindingFlags.CreateInstance, null, null, new object[0], CultureInfo.CurrentCulture) as IWizard;
                }
            } catch (ArgumentException) {
            } catch (BadImageFormatException) {
            } catch (IOException) {
            } catch (MemberAccessException) {
            }

            if (_wizard == null) {
                try {
                    Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                    Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
                } catch {
                    // If it fails (doesn't exist/contains files/read-only), let the directory stay.
                }

                var dlg = new TaskDialog(provider) {
                    Title = Strings.ProductTitle,
                    MainInstruction = Strings.AzureToolsRequired,
                    Content = Strings.AzureToolsInstallInstructions,
                    AllowCancellation = true
                };
                dlg.Buttons.Add(TaskDialogButton.Cancel);
                var download = new TaskDialogButton(Strings.DownloadAndInstall);
                dlg.Buttons.Insert(0, download);

                if (dlg.ShowModal() == download) {
                    InstallTools(provider);
                    throw new WizardCancelledException();
                }

                // User cancelled, so go back to the New Project dialog
                throw new WizardBackoutException();
            }

            // Run the original wizard to get the right replacements
            _wizard.RunStarted(automationObject, replacementsDictionary, runKind, customParams);
        }
    }
}
