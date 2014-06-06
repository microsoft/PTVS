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

#if DEV11_OR_LATER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.PythonTools.ProjectWizards.Properties;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TemplateWizard;
using Microsoft.VisualStudioTools;
using DTE = EnvDTE.DTE;
using IVsExtensibility = EnvDTE.IVsExtensibility;
using Project = EnvDTE.Project;
using ProjectItem = EnvDTE.ProjectItem;

namespace Microsoft.PythonTools.ProjectWizards {
    public sealed class CloudServiceWizard : IWizard {
        private IWizard _wizard;

#if DEV11
        const string AzureToolsDownload = "https://go.microsoft.com/fwlink/p/?linkid=323511";
#elif DEV12
        const string AzureToolsDownload = "https://go.microsoft.com/fwlink/p/?linkid=323510";
#else
#error Unsupported VS version
#endif

        public CloudServiceWizard() {
            try {
                // If we fail to find the wizard, we will redirect the user to
                // the WebPI download.
                var asm = Assembly.Load("Microsoft.VisualStudio.CloudService.Wizard,Version=1.0.0.0,Culture=neutral,PublicKeyToken=b03f5f7f11d50a3a");
                var type = asm.GetType("Microsoft.VisualStudio.CloudService.Wizard.CloudServiceWizard");
                _wizard = type.InvokeMember(null, BindingFlags.CreateInstance, null, null, new object[0]) as IWizard;
            } catch (ArgumentException) {
            } catch (BadImageFormatException) {
            } catch (IOException) {
            } catch (MemberAccessException) {
            }
        }

        public void BeforeOpeningFile(ProjectItem projectItem) {
            if (_wizard != null) {
                _wizard.BeforeOpeningFile(projectItem);
            }
        }

        public void ProjectFinishedGenerating(Project project) {
            if (_wizard != null) {
                _wizard.ProjectFinishedGenerating(project);
            }
        }

        public void ProjectItemFinishedGenerating(ProjectItem projectItem) {
            if (_wizard != null) {
                _wizard.ProjectItemFinishedGenerating(projectItem);
            }
        }

        public void RunFinished() {
            if (_wizard != null) {
                _wizard.RunFinished();
            }
        }

        public bool ShouldAddProjectItem(string filePath) {
            if (_wizard != null) {
                return _wizard.ShouldAddProjectItem(filePath);
            }
            return false;
        }

        public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams) {
            IServiceProvider provider = null;
            var dte = automationObject as DTE;
            if (dte == null) {
                var oleProvider = automationObject as Microsoft.VisualStudio.OLE.Interop.IServiceProvider;
                if (provider != null) {
                    provider = new ServiceProvider(oleProvider);
                    dte = provider.GetService(typeof(DTE)) as DTE;
                }
            }
            if (dte == null) {
                MessageBox.Show(Resources.ErrorNoDte, Resources.PythonToolsForVisualStudio);
                return;
            }
            if (provider == null) {
                provider = new ServiceProvider(dte as Microsoft.VisualStudio.OLE.Interop.IServiceProvider);
            }

            if (_wizard == null) {
                try {
                    Directory.Delete(replacementsDictionary["$destinationdirectory$"]);
                    Directory.Delete(replacementsDictionary["$solutiondirectory$"]);
                } catch {
                    // If it fails (doesn't exist/contains files/read-only), let the directory stay.
                }

                var dlg = new TaskDialog(provider) {
                    Title = Resources.PythonToolsForVisualStudio,
                    MainInstruction = Resources.AzureToolsRequired,
                    Content = Resources.AzureToolsInstallInstructions,
                    AllowCancellation = true
                };
                var download = new TaskDialogButton(Resources.DownloadAndInstall);
                dlg.Buttons.Add(download);
                dlg.Buttons.Add(TaskDialogButton.Cancel);

                if (dlg.ShowModal() == download) {
                    Process.Start(new ProcessStartInfo(AzureToolsDownload));
                    throw new WizardCancelledException();
                }

                // User cancelled, so go back to the New Project dialog
                throw new WizardBackoutException();
            }

            // Run the original wizard to get the right replacements, but
            // suppress UI to avoid the dialog that does not include our
            // projects.
            var extensibility = provider.GetService(typeof(IVsExtensibility)) as IVsExtensibility;
            extensibility.EnterAutomationFunction();
            dte.SuppressUI = true;
            try {
                _wizard.RunStarted(automationObject, replacementsDictionary, runKind, customParams);
            } finally {
                dte.SuppressUI = false;
                extensibility.ExitAutomationFunction();
            }
        }
    }
}

#endif