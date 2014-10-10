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
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Microsoft.VisualStudioTools.Project;

namespace Microsoft.PythonTools.ML {
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Description("Python Machine Learning Support Package")]
    // This attribute is used to register the informations needed to show the this package
    // in the Help/About dialog of Visual Studio.
    [InstalledProductRegistration("#110", "#112", AssemblyVersionInfo.Version, IconResourceID = 400)]
    // This attribute is needed to let the shell know that this package exposes some menus.
    [Guid(GuidList.guidPythonMLPkgString)]
    [ProvideAutoLoad("ADFC4E64-0397-11D1-9F4E-00A0C911004F")]
    [ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82")]
    sealed class MLSupportPackage : Package {
        internal static MLSupportPackage Instance;
        public MLSupportPackage() {
            Instance = this;
        }

        protected override void Initialize() {
            base.Initialize();

            OleMenuCommandService mcs = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;

            CommandID toolwndCommandID = new CommandID(GuidList.guidPythonMLCmdSet, PkgCmdIDList.AddAzureMLServiceTemplate);
            OleMenuCommand menuToolWin = new OleMenuCommand(InvokeAddAzureMLService, toolwndCommandID);
            menuToolWin.BeforeQueryStatus += QueryStatusAddAzureMLService;
            mcs.AddCommand(menuToolWin);

            toolwndCommandID = new CommandID(GuidList.guidPythonMLCmdSet, PkgCmdIDList.AddAzureMLServiceTemplateToFile);
            menuToolWin = new OleMenuCommand(InvokeAddAzureMLServiceToFile, toolwndCommandID);
            menuToolWin.BeforeQueryStatus += QueryStatusAddAzureMLServiceToFile;
            mcs.AddCommand(menuToolWin);
        }

        // TODO: Break functionality out to it's own class
        private void QueryStatusAddAzureMLService(object sender, EventArgs args) {
            var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;

            var item = GetSelectedItem();
            if (item == null) {
                oleMenuCmd.Supported = oleMenuCmd.Visible = false;
                return;
            }
            object name;
            ErrorHandler.ThrowOnFailure(item.Value.pHier.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_TypeName, out name));

            if (!(name is string) ||
                !((string)name == "PythonProject") ||
                GetProjectKind(item) == ProjectKind.None ||
                IsCommandDisabledForFolder(item)) {
                oleMenuCmd.Supported = oleMenuCmd.Visible = false;
                return;
            }

            oleMenuCmd.Supported = oleMenuCmd.Visible = true;
        }

        private void QueryStatusAddAzureMLServiceToFile(object sender, EventArgs args) {
            var oleMenuCmd = (Microsoft.VisualStudio.Shell.OleMenuCommand)sender;

            var item = GetSelectedItem();
            if (item == null || IsCommandDisabledForFile(item)) {
                oleMenuCmd.Supported = oleMenuCmd.Visible = false;
            } else {
                oleMenuCmd.Supported = oleMenuCmd.Visible = true;
            }
        }

        private static bool IsCommandDisabledForFolder(VSITEMSELECTION? item) {
            if (item.Value.IsFolder()) {
                return false;
            }
            return true;
        }

        private static bool IsCommandDisabledForFile(VSITEMSELECTION? item) {
            if (item.Value.IsFile() &&
                String.Equals(Path.GetExtension(item.Value.GetCanonicalName()), ".py", StringComparison.OrdinalIgnoreCase)) {
                return false;
            }
            return true;
        }

        enum ProjectKind {
            None,
            Flask,
            Bottle,
            Django,
            Worker
        }

        /// <summary>
        /// Command used when user invokes to add an azure ML service to a file or the
        /// project node.
        /// 
        /// By default we bring the dialog up to be configured to add to a new file in
        /// the folder.
        /// </summary>
        private void InvokeAddAzureMLService(object sender, EventArgs args) {
            var item = GetSelectedItem().Value;

            var dlg = CreateAddServiceDialog(item);
            // collect the items in the folder...
            foreach (var child in item.GetChildren()) {
                if (String.Equals(Path.GetExtension(child), ".py", StringComparison.OrdinalIgnoreCase)) {
                    dlg.AddTargetFile(Path.GetFileName(child));
                }
            }

            var res = dlg.ShowModal();
            if (res == true) {
                string importName;
                if (dlg.AddToCombo.SelectedIndex == 0) {
                    // add to new file..
                    AddToNewFile(item, dlg);
                    importName = dlg.ServiceName.Text;
                } else {
                    // user selected a file in the folder, add the code to it.
                    var projectItem = GetProjectItems(item).Item(dlg.AddToCombo.Text);
                    AddToExisting(projectItem, dlg.GenerateServiceCode());
                    importName = Path.GetFileNameWithoutExtension(dlg.AddToCombo.Text);
                }

                GenerateExtraFiles(dlg, importName);
            }
        }

        /// <summary>
        /// Command used when user invoeks to add an azure ML service to a .py file.
        /// 
        /// By default we bring the service up configured to add to the file.
        /// </summary>
        private void InvokeAddAzureMLServiceToFile(object sender, EventArgs args) {
            var item = GetSelectedItem().Value;
            var dlg = CreateAddServiceDialog(item);

            // file should be the default
            dlg.AddTargetFile(Path.GetFileName(item.GetCanonicalName()));
            dlg.AddToCombo.SelectedIndex = 1;

            var res = dlg.ShowModal();
            if (res == true) {
                string importName;
                if (dlg.AddToCombo.SelectedIndex == 0) {
                    // add to new file..
                    AddToNewFile(
                        dlg.GenerateServiceCode(), 
                        GetExtensionObject(item).Collection, 
                        dlg.ServiceName.Text + ".py"
                    );
                    importName = dlg.ServiceName.Text;
                } else {
                    // add to the existing file that the user right clicked on.
                    var extObject = GetExtensionObject(item);
                    AddToExisting(extObject, dlg.GenerateServiceCode());
                    importName = Path.GetFileNameWithoutExtension(item.GetCanonicalName());
                }

                GenerateExtraFiles(dlg, importName);
            }
        }

        private static void AddToNewFile(string code, ProjectItems items, string filename) {
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, code);

            var projectItem = items.AddFromTemplate(tempFile, filename);
            var window = projectItem.Open();
            window.Activate();
        }

        private void GenerateExtraFiles(AddAzureServiceDialog dlg, string importName) {
            var item = GetSelectedItem().Value;
            var dteProject = GetProject(item.pHier);

            if (dlg.AddDashboardDisplay.IsChecked.Value) {
                var targetFolder = GetTargetFolder(dteProject, dlg.DashboardTargetFolder.Text);

                switch (GetProjectKind(item)) {
                    case ProjectKind.Bottle:
                        AddToNewFile(
                            dlg.GenerateBottleDashboardTemplate(),
                            targetFolder, 
                            dlg.ServiceName.Text + "_dashboard" + ".tpl"
                        );

                        // TODO: Find actual routes file
                        AddToExisting(
                            dteProject.ProjectItems.Item("routes.py"), 
                            dlg.GenerateBottleDashboardRoute(importName)
                        );
                        break;
                }
            }

            if (dlg.AddInputForm.IsChecked.Value) {
                switch (GetProjectKind(item)) {
                    case ProjectKind.Bottle:
                        AddToNewFile(
                            dlg.GenerateBottleFormTemplate(),
                            GetTargetFolder(dteProject, dlg.InputTargetFolder.Text),
                            dlg.ServiceName.Text + "_form" + ".tpl"
                        );

                        if (!dlg.AddDashboardDisplay.IsChecked.Value) {
                            // we need the dashboard template to view the results.
                            AddToNewFile(
                                dlg.GenerateBottleDashboardTemplate(),
                                GetTargetFolder(dteProject, dlg.InputTargetFolder.Text),
                                dlg.ServiceName.Text + "_dashboard" + ".tpl"
                            );
                        }

                        AddToExisting(
                            dteProject.ProjectItems.Item("routes.py"),
                            dlg.GenerateBottleFormRoute(importName)
                        );
                        break;
                }
            }
        }

        private static EnvDTE.ProjectItems GetTargetFolder(EnvDTE.Project dteProject, string folder) {
            var curItems = dteProject.ProjectItems;
            foreach (var folderPath in folder.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries)) {
                curItems = curItems.Item(folderPath).ProjectItems;
            }
            return curItems;
        }

        private static AddAzureServiceDialog CreateAddServiceDialog(VSITEMSELECTION item) {
            switch (GetProjectKind(item)) {
                case ProjectKind.Bottle:
                    return new AddAzureServiceDialog("views", true);
                default:
                    return new AddAzureServiceDialog(null, false);
            }
        }

        private static void AddToNewFile(VSITEMSELECTION item, AddAzureServiceDialog dlg) {
            var code = dlg.GenerateServiceCode();
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, code);

            var projectItem = GetProjectItems(item).AddFromTemplate(
                tempFile,
                dlg.ServiceName.Text + ".py"
            );
            var window = projectItem.Open();
            window.Activate();
        }

        private static void AddToExisting(ProjectItem extObject, string code) {
            // TODO: Need to handle failure here.
            var window = extObject.Open();
            window.Activate();

            TextSelection selection = (TextSelection)extObject.Document.Selection;
            selection.SelectAll();
            var text = selection.Text;

            selection.EndOfDocument();

            selection.NewLine();
            selection.Insert(code);
        }

        internal static EnvDTE.ProjectItems GetProjectItems(VSITEMSELECTION selection) {
            object project;

            ErrorHandler.ThrowOnFailure(
                selection.pHier.GetProperty(
                    selection.itemid,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            if (project is EnvDTE.ProjectItem) {
                return ((EnvDTE.ProjectItem)project).ProjectItems;
            }
            return ((EnvDTE.Project)project).ProjectItems;
        }


        internal static EnvDTE.ProjectItem GetExtensionObject(VSITEMSELECTION selection) {
            object project;

            ErrorHandler.ThrowOnFailure(
                selection.pHier.GetProperty(
                    selection.itemid,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.ProjectItem);
        }

        internal static EnvDTE.Project GetProject(IVsHierarchy hierarchy) {
            object project;

            ErrorHandler.ThrowOnFailure(
                hierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out project
                )
            );

            return (project as EnvDTE.Project);
        }

        private static ProjectKind GetProjectKind(VSITEMSELECTION? item) {
            if (item == null) {
                throw new InvalidOperationException();
            }

            string projectTypeGuids;
            ErrorHandler.ThrowOnFailure(
                ((IVsAggregatableProject)item.Value.pHier).GetAggregateProjectTypeGuids(out projectTypeGuids)
            );

            var guidStrs = projectTypeGuids.Split(';');
            foreach (var guidStr in guidStrs) {
                Guid projectGuid;
                if (Guid.TryParse(guidStr, out projectGuid)) {
                    if (projectGuid == GuidList.FlaskGuid) {
                        return ProjectKind.Flask;
                    } else if (projectGuid == GuidList.BottleGuid) {
                        return ProjectKind.Bottle;
                    } else if (projectGuid == GuidList.DjangoGuid) {
                        return ProjectKind.Django;
                    } else if (projectGuid == GuidList.WorkerRoleGuid) {
                        return ProjectKind.Worker;
                    }
                }
            }

            return ProjectKind.None;
        }

        /// <summary>
        /// Gets all of the currently selected items.
        /// </summary>
        /// <returns></returns>
        private VSITEMSELECTION? GetSelectedItem() {
            IVsMonitorSelection monitorSelection = GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

            IntPtr hierarchyPtr = IntPtr.Zero;
            IntPtr selectionContainer = IntPtr.Zero;
            try {
                uint selectionItemId;
                IVsMultiItemSelect multiItemSelect = null;
                ErrorHandler.ThrowOnFailure(monitorSelection.GetCurrentSelection(out hierarchyPtr, out selectionItemId, out multiItemSelect, out selectionContainer));

                if (selectionItemId != VSConstants.VSITEMID_NIL && hierarchyPtr != IntPtr.Zero) {
                    IVsHierarchy hierarchy = Marshal.GetObjectForIUnknown(hierarchyPtr) as IVsHierarchy;

                    if (selectionItemId != VSConstants.VSITEMID_SELECTION) {
                        // This is a single selection. Compare hirarchy with our hierarchy and get node from itemid
                        return new VSITEMSELECTION() { itemid = selectionItemId, pHier = hierarchy };
                    }
                }
            } finally {
                if (hierarchyPtr != IntPtr.Zero) {
                    Marshal.Release(hierarchyPtr);
                }
                if (selectionContainer != IntPtr.Zero) {
                    Marshal.Release(selectionContainer);
                }
            }
            return null;
        }
    }

}
