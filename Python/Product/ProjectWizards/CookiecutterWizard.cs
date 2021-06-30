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


// This assembly is no longer being used for new wizards and is maintained for
// backwards compatibility until we can merge ImportWizard into the main DLL.
//
// All new wizards should be added to Microsoft.PythonTools.ProjectWizards.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class CookiecutterWizard : IWizard {
        private const string UserAgent = "PythonToolsForVisualStudio/" + AssemblyVersionInfo.Version;
        private const string RequiredPackage = "Microsoft.Component.CookiecutterTools";
        private const int cmdidNewProjectFromTemplate = 0x100B;

        public void BeforeOpeningFile(EnvDTE.ProjectItem projectItem) { }
        public void ProjectFinishedGenerating(EnvDTE.Project project) { }
        public void ProjectItemFinishedGenerating(EnvDTE.ProjectItem projectItem) { }
        public void RunFinished() { }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            try {
                Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
            } catch {
                // If it fails (doesn't exist/contains files/read-only), let the directory stay.
            }

            var oleProvider = automationObject as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
            if (oleProvider == null) {
                MessageBox.Show("Unable to start wizard: no automation object available.", "Visual Studio");
                throw new WizardBackoutException();
            }

            using (var serviceProvider = new ServiceProvider(oleProvider)) {
                int hr = EnsurePythonPackageLoaded(serviceProvider);
                if (ErrorHandler.Failed(hr)) {
                    MessageBox.Show(string.Format("Unable to start wizard: failed to load Python support Package (0x{0:X08})", hr), "Visual Studio");
                    throw new WizardBackoutException();
                }

                // Cookiecutter is installed by default, but can be deselected/uninstalled separately from Python component
                hr = EnsureCookiecutterPackageLoaded(serviceProvider);
                if (ErrorHandler.Failed(hr)) {
                    var dlg = new TaskDialog(serviceProvider) {
                        Title = Strings.ProductTitle,
                        MainInstruction = Strings.CookiecutterComponentRequired,
                        Content = Strings.CookiecutterComponentInstallInstructions,
                        AllowCancellation = true
                    };
                    dlg.Buttons.Add(TaskDialogButton.Cancel);
                    var download = new TaskDialogButton(Strings.DownloadAndInstall);
                    dlg.Buttons.Insert(0, download);

                    if (dlg.ShowModal() == download) {
                        InstallTools(serviceProvider);
                        throw new WizardCancelledException();
                    }
                }

                var uiShell = (IVsUIShell)serviceProvider.GetService(typeof(SVsUIShell));

                string projName = replacementsDictionary["$projectname$"];
                string directory = Path.GetDirectoryName(replacementsDictionary["$destinationdirectory$"]);

                var wizardData = replacementsDictionary["$wizarddata$"];
                var templateUri = Resolve(new Uri(wizardData));

                object inObj = projName + "|" + directory + "|" + templateUri.ToString();
                var guid = GuidList.guidCookiecutterCmdSet;
                uiShell.PostExecCommand(ref guid, cmdidNewProjectFromTemplate, 0, ref inObj);
            }
            throw new WizardCancelledException();
        }

        public bool ShouldAddProjectItem(string filePath) {
            return false;
        }

        private static int EnsurePythonPackageLoaded(IServiceProvider serviceProvider) {
            return EnsurePackageLoaded(serviceProvider, GuidList.guidPythonToolsPackage);
        }

        private static int EnsureCookiecutterPackageLoaded(IServiceProvider serviceProvider) {
            return EnsurePackageLoaded(serviceProvider, GuidList.guidCookiecutterPackage);
        }

        private static int EnsurePackageLoaded(IServiceProvider serviceProvider, Guid packageGuid) {
            var shell = (IVsShell)serviceProvider.GetService(typeof(SVsShell));

            var pkgGuid = packageGuid;
            IVsPackage pkg;
            if (ErrorHandler.Failed(shell.IsPackageLoaded(ref pkgGuid, out pkg)) || pkg == null) {
                return shell.LoadPackage(ref pkgGuid, out pkg);
            }
            return VSConstants.S_OK;
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

        private static Uri Resolve(Uri uri) {
            var hosts = new string[] { "go.microsoft.com", "aka.ms" };
            if (!hosts.Any(h => uri.Host.StartsWithOrdinal(h, ignoreCase: true))) {
                return uri;
            }

            var req = WebRequest.CreateHttp(uri);
            req.Method = "HEAD";
            req.AllowAutoRedirect = false;
            req.UserAgent = UserAgent;
            req.Timeout = 5000;
            try {
                using (var resp = req.GetResponse()) {
                    return new Uri(resp.Headers.Get("Location")) ?? uri;
                }
            } catch (Exception ex) when (!ex.IsCriticalException()) {
                MessageBox.Show(
                    Strings.CookiecutterWizardUnresolvedTemplate.FormatUI(uri.ToString()),
                    Strings.ProductTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                throw new WizardCancelledException();
            }
        }
    }
}
