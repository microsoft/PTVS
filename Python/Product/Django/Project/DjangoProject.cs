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
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;

namespace Microsoft.PythonTools.Django.Project {
    [Guid("564253E9-EF07-4A40-89CF-790E61F53368")]
    partial class DjangoProject :
        FlavoredProjectBase,
        IOleCommandTarget,
        IVsProjectFlavorCfgProvider,
        IVsProject,
        IDjangoProject,
        IVsFilterAddProjectItemDlg
    {
        internal DjangoPackage _package;
        internal IVsProject _innerProject;
        internal IVsProject3 _innerProject3;
        private IVsProjectFlavorCfgProvider _innerVsProjectFlavorCfgProvider;
        private static Guid PythonProjectGuid = new Guid(PythonConstants.ProjectFactoryGuid);
        private OleMenuCommandService _menuService;
        private List<OleMenuCommand> _commands = new List<OleMenuCommand>();
        private DjangoAnalyzer _analyzer;

#if HAVE_ICONS
        private static ImageList _images;
#endif

        public DjangoProject() {
        }

        public DjangoAnalyzer Analyzer {
            get {
                if (_analyzer == null) {
                    _analyzer = new DjangoAnalyzer(this);
                }
                return _analyzer;
            }
        }

        #region IVsAggregatableProject

        /// <summary>
        /// Do the initialization here (such as loading flavor specific
        /// information from the project)
        /// </summary>      
        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel) {
            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);

            // register the open command with the menu service provided by the base class.  We can't just handle this
            // internally because we kick off the context menu, pass ourselves as the IOleCommandTarget, and then our
            // base implementation dispatches via the menu service.  So we could either have a different IOleCommandTarget
            // which handles the Open command programmatically, or we can register it with the menu service.  
            CommandID menuCommandID = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.Open);
            OleMenuCommand menuItem = new OleMenuCommand(OpenFile, null, OpenFileBeforeQueryStatus, menuCommandID);
            AddCommand(menuItem);

            menuCommandID = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.ViewCode);
            menuItem = new OleMenuCommand(OpenFile, null, OpenFileBeforeQueryStatus, menuCommandID);
            AddCommand(menuItem);

            menuCommandID = new CommandID(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.ECMD_VIEWMARKUP);
            menuItem = new OleMenuCommand(OpenFile, null, OpenFileBeforeQueryStatus, menuCommandID);
            AddCommand(menuItem);

            menuCommandID = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.AddNewItem);
            menuItem = new OleMenuCommand(AddNewItem, menuCommandID);
            AddCommand(menuItem);

            var pyProj = _innerVsHierarchy.GetProject().GetPythonProject();
            if (pyProj != null) {
                Analyzer.OnNewAnalyzer(pyProj.GetProjectAnalyzer().Project);
                pyProj.ProjectAnalyzerChanging += OnProjectAnalyzerChanging;
            }

            object extObject;
            ErrorHandler.ThrowOnFailure(
                _innerVsHierarchy.GetProperty(
                    VSConstants.VSITEMID_ROOT,
                    (int)__VSHPROPID.VSHPROPID_ExtObject,
                    out extObject
                )
            );

            var proj = extObject as EnvDTE.Project;
            if (proj != null) {
                try {
                    dynamic webAppExtender = proj.get_Extender("WebApplication");
                    if (webAppExtender != null) {
                        webAppExtender.StartWebServerOnDebug = false;
                    }
                } catch (COMException) {
                    // extender doesn't exist...
                }
            }
        }

        #endregion

        private void OnProjectAnalyzerChanging(object sender, AnalyzerChangingEventArgs e) {
            var pyProj = sender as IPythonProject;
            if (pyProj != null) {
                _analyzer.OnNewAnalyzer(e.New);
            }
        }


        private void AddCommand(OleMenuCommand menuItem) {
            _menuService.AddCommand(menuItem);
            _commands.Add(menuItem);
        }

        protected override void Close() {
            if (_menuService != null) {
                foreach (var command in _commands) {
                    _menuService.RemoveCommand(command);
                }
            }
            _commands.Clear();
            _analyzer.Dispose();
            base.Close();
        }

        private void OpenFileBeforeQueryStatus(object sender, EventArgs e) {
            var oleMenu = sender as OleMenuCommand;
            oleMenu.Supported = false;

            foreach (var vsItemSelection in GetSelectedItems()) {
                object name;
                ErrorHandler.ThrowOnFailure(vsItemSelection.pHier.GetProperty(vsItemSelection.itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));

                if (IsHtmlFile(vsItemSelection.Name())) {
                    oleMenu.Supported = true;
                }
            }
        }

        private bool IsHtmlFile(IVsHierarchy iVsHierarchy, uint itemid) {
            object name;
            ErrorHandler.ThrowOnFailure(iVsHierarchy.GetProperty(itemid, (int)__VSHPROPID.VSHPROPID_Name, out name));

            return IsHtmlFile(name);
        }

        private static bool IsHtmlFile(object name) {
            string strName = name as string;
            if (strName != null) {
                var ext = Path.GetExtension(strName);
                if (String.Equals(ext, ".html", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(ext, ".htm", StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }
            return false;
        }

        private void OpenFile(object sender, EventArgs e) {
            var oleMenu = sender as OleMenuCommand;
            oleMenu.Supported = false;

            foreach (var vsItemSelection in GetSelectedItems()) {
                if (IsHtmlFile(vsItemSelection.Name())) {
                    ErrorHandler.ThrowOnFailure(OpenWithDjangoEditor(vsItemSelection.itemid));
                } else {
                    ErrorHandler.ThrowOnFailure(OpenWithDefaultEditor(vsItemSelection.itemid));
                }
            }
        }

        private void AddNewItem(object sender, EventArgs e) {
            var items = GetSelectedItems().ToArray();
            if (items.Length == 1) {
                // Make sure we pass a folder item to the dialog. This is what the client project would
                // have done.

                var item = items[0];

                if (!item.IsFolder()) {
                    item = item.GetParentFolder();
                }
                uint itemid = item.itemid;

                int iDontShowAgain = 0;
                string strBrowseLocations = "";

                Guid projectGuid = typeof(DjangoProject).GUID;

                uint uiFlags = (uint)(__VSADDITEMFLAGS.VSADDITEM_AddNewItems | __VSADDITEMFLAGS.VSADDITEM_SuggestTemplateName | __VSADDITEMFLAGS.VSADDITEM_AllowHiddenTreeView);


                IVsAddProjectItemDlg addItemDialog = (IVsAddProjectItemDlg)((System.IServiceProvider)this).GetService(typeof(IVsAddProjectItemDlg));
                string filter = "";
                // Note we pass "Web" as the default category to select. The dialog only uses it if it hasn't already saved a default value.
                string defCategory = "Web";
                string folderName = item.Name();
                addItemDialog.AddProjectItemDlg(itemid,
                    ref projectGuid,
                    this,
                    uiFlags, defCategory,
                    null,
                    ref strBrowseLocations,
                    ref filter,
                    out iDontShowAgain);
            }

        }

        /// <summary>
        /// Gets all of the currently selected items.
        /// </summary>
        /// <returns></returns>
        private IEnumerable<VSITEMSELECTION> GetSelectedItems() {
            IVsMonitorSelection monitorSelection = _package.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;

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
                        if (Utilities.IsSameComObject(this, hierarchy)) {
                            yield return new VSITEMSELECTION() { itemid = selectionItemId, pHier = hierarchy };
                        }
                    } else if (multiItemSelect != null) {
                        // This is a multiple item selection.
                        // Get number of items selected and also determine if the items are located in more than one hierarchy

                        uint numberOfSelectedItems;
                        int isSingleHierarchyInt;
                        ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectionInfo(out numberOfSelectedItems, out isSingleHierarchyInt));
                        bool isSingleHierarchy = (isSingleHierarchyInt != 0);

                        // Now loop all selected items and add to the list only those that are selected within this hierarchy
                        if (!isSingleHierarchy || (isSingleHierarchy && Utilities.IsSameComObject(this, hierarchy))) {
                            Debug.Assert(numberOfSelectedItems > 0, "Bad number of selected itemd");
                            VSITEMSELECTION[] vsItemSelections = new VSITEMSELECTION[numberOfSelectedItems];
                            uint flags = (isSingleHierarchy) ? (uint)__VSGSIFLAGS.GSI_fOmitHierPtrs : 0;
                            ErrorHandler.ThrowOnFailure(multiItemSelect.GetSelectedItems(flags, numberOfSelectedItems, vsItemSelections));

                            foreach (VSITEMSELECTION vsItemSelection in vsItemSelections) {
                                yield return new VSITEMSELECTION() { itemid = vsItemSelection.itemid, pHier = hierarchy };
                            }
                        }
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
        }

        private int OpenWithDefaultEditor(uint selectionItemId) {
            Guid view = Guid.Empty;
            IVsWindowFrame frame;
            int hr = ((IVsProject)_innerVsHierarchy).OpenItem(
                selectionItemId,
                ref view,
                IntPtr.Zero,
                out frame
            );
            if (ErrorHandler.Succeeded(hr)) {
                hr = frame.Show();
            }
            return hr;
        }

        private int OpenWithDjangoEditor(uint selectionItemId) {
            Guid ourEditor = typeof(DjangoEditorFactory).GUID;
            Guid view = Guid.Empty;
            IVsWindowFrame frame;
            int hr = ((IVsProject3)_innerVsHierarchy).ReopenItem(
                selectionItemId,
                ref ourEditor,
                null,
                ref view,
                new IntPtr(-1),
                out frame
            );
            if (ErrorHandler.Succeeded(hr)) {
                hr = frame.Show();
            }
            return hr;
        }

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidDjangoCmdSet) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case PkgCmdIDList.cmdidStartNewApp:
                            var items = GetSelectedItems();
                            if (CanAddAppToSelectedNode(items)) {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                            } else {
                                prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            }
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidOfficeSharePointCmdSet) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    // Report it as supported so that it's not routed any further, but disable it and make it invisible.
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_INVISIBLE);
                }
                return VSConstants.S_OK;
            }

            return base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        private bool CanAddAppToSelectedNode(IEnumerable<VSITEMSELECTION> items) {
            if (items.Count() == 1 && !items.First().IsNonMemberItem()) {
                var selectedType = GetSelectedItemType();
                if (selectedType == VSConstants.GUID_ItemType_PhysicalFolder || selectedType == PythonProjectGuid) {
                    return true;
                }
            }
            return false;
        }

        protected override int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == VsMenus.guidVsUIHierarchyWindowCmds) {
                switch ((VSConstants.VsUIHierarchyWindowCmdIds)nCmdID) {
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_RightClick:
                        int res;
                        if (TryHandleRightClick(pvaIn, out res)) {
                            return res;
                        }
                        break;
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick:
                    case VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey:
                        // open the document if it's an HTML file
                        if (IsHtmlFile(_innerVsHierarchy, itemid)) {
                            int hr = OpenWithDjangoEditor(itemid);

                            if (ErrorHandler.Succeeded(hr)) {
                                return hr;
                            }
                        }
                        break;

                }
            } else if (pguidCmdGroup == GuidList.guidDjangoCmdSet) {
                switch (nCmdID) {
                    case PkgCmdIDList.cmdidStartNewApp:
                        StartNewApp();
                        return VSConstants.S_OK;
                }
            }

            return base.ExecCommand(itemid, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private string GetNewAppNameFromUser(string lastName = null) {
            var dialog = new NewAppDialog();
            dialog.ViewModel.Name = lastName ?? string.Empty;
            return (dialog.ShowModal() ?? false) ? dialog.ViewModel.Name : null;
        }

        private string ResolveAppNameCollisionWithUser(EnvDTE.ProjectItems items, string name, out bool cancel) {
            while (true) {
                try {
                    if (items.Item(name) == null) {
                        break;
                    }
                } catch (ArgumentException) {
                    break;
                }

                var td = new TaskDialog(new ServiceProvider(GetSite())) {
                    Title = Resources.PythonToolsForVisualStudio,
                    MainInstruction = string.Format(Resources.DjangoAppAlreadyExistsTitle, name),
                    Content = string.Format(Resources.DjangoAppAlreadyExistsInstruction, name),
                    AllowCancellation = true
                };
                var cont = new TaskDialogButton(
                    Resources.DjangoAppAlreadyExistsCreateAnyway,
                    Resources.DjangoAppAlreadyExistsCreateAnywaySubtitle
                );
                var retry = new TaskDialogButton(Resources.SelectAnotherName);
                td.Buttons.Add(cont);
                td.Buttons.Add(retry);
                td.Buttons.Add(TaskDialogButton.Cancel);

                var clicked = td.ShowModal();
                if (clicked == cont) {
                    break;
                } else if (clicked == retry) {
                    name = GetNewAppNameFromUser(name);
                    if (string.IsNullOrEmpty(name)) {
                        cancel = true;
                        return null;
                    }
                } else {
                    cancel = true;
                    return null;
                }
            }

            cancel = false;
            return name;
        }

        private void StartNewApp() {
            var selectedItems = GetSelectedItems();
            var name = GetNewAppNameFromUser();
            if (!string.IsNullOrEmpty(name)) {
                object projectObj;
                ErrorHandler.ThrowOnFailure(
                    _innerVsHierarchy.GetProperty(
                        VSConstants.VSITEMID_ROOT,
                        (int)__VSHPROPID.VSHPROPID_ExtObject,
                        out projectObj
                    )
                );

                object selectedObj;
                var selectedNode = selectedItems.First();
                ErrorHandler.ThrowOnFailure(
                    selectedNode.pHier.GetProperty(
                        selectedNode.itemid,
                        (int)__VSHPROPID.VSHPROPID_ExtObject,
                        out selectedObj
                    )
                );

                var project = projectObj as EnvDTE.Project;
                if (project != null) {
                    EnvDTE.ProjectItems parentItems;
                    if (selectedObj == projectObj) {
                        parentItems = project.ProjectItems;
                    } else {
                        parentItems = ((EnvDTE.ProjectItem)selectedObj).ProjectItems;
                    }

                    // TODO: Use the actual Django version
                    var sln = (EnvDTE80.Solution2)project.DTE.Solution;
#if DEV10
                    var newAppTemplate = sln.GetProjectItemTemplate("DjangoNewAppFiles14.zip", "{888888a0-9f3d-457c-b088-3a5042f75d52}");
#else
                    var newAppTemplate = sln.GetProjectItemTemplate("DjangoNewAppFiles14.zip", "Python");
#endif

                    bool cancel;
                    name = ResolveAppNameCollisionWithUser(parentItems, name, out cancel);
                    if (!cancel) {
                        parentItems.AddFromTemplate(newAppTemplate, name);
                    }
                }
            }
        }

        private bool TryHandleRightClick(IntPtr pvaIn, out int res) {
            Guid itemType = GetSelectedItemType();

            if (TryShowContextMenu(pvaIn, itemType, out res)) {
                return true;
            }

            return false;
        }

        private Guid GetSelectedItemType() {
            Guid itemType = Guid.Empty;
            foreach (var vsItemSelection in GetSelectedItems()) {
                Guid typeGuid = vsItemSelection.GetItemType();

                if (itemType == Guid.Empty) {
                    itemType = typeGuid;
                } else if (itemType != typeGuid) {
                    // we have multiple item types
                    itemType = Guid.Empty;
                    break;
                }
            }
            return itemType;
        }

        private bool TryShowContextMenu(IntPtr pvaIn, Guid itemType, out int res) {
            if (itemType == PythonProjectGuid) {
                // multiple Python prjoect nodes selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_PROJNODE/*IDM_VS_CTXT_WEBPROJECT*/);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFile) {
                // multiple files selected
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_ITEMNODE);
                return true;
            } else if (itemType == VSConstants.GUID_ItemType_PhysicalFolder) {
                res = ShowContextMenu(pvaIn, VsMenus.IDM_VS_CTXT_FOLDERNODE);
                return true;
            }
            res = VSConstants.E_FAIL;
            return false;
        }

        private int ShowContextMenu(IntPtr pvaIn, int ctxMenu) {
            object variant = Marshal.GetObjectForNativeVariant(pvaIn);
            UInt32 pointsAsUint = (UInt32)variant;
            short x = (short)(pointsAsUint & 0x0000ffff);
            short y = (short)((pointsAsUint & 0xffff0000) / 0x10000);

            POINTS points = new POINTS();
            points.x = x;
            points.y = y;

            return ShowContextMenu(ctxMenu, VsMenus.guidSHLMainMenu, points);
        }

        /// <summary>
        /// Shows the specified context menu at a specified location.
        /// </summary>
        /// <param name="menuId">The context menu ID.</param>
        /// <param name="groupGuid">The GUID of the menu group.</param>
        /// <param name="points">The location at which to show the menu.</param>
        internal int ShowContextMenu(int menuId, Guid menuGroup, POINTS points) {
            IVsUIShell shell = _package.GetService(typeof(SVsUIShell)) as IVsUIShell;

            Debug.Assert(shell != null, "Could not get the UI shell from the project");
            if (shell == null) {
                return VSConstants.E_FAIL;
            }
            POINTS[] pnts = new POINTS[1];
            pnts[0].x = points.x;
            pnts[0].y = points.y;
            return shell.ShowContextMenu(0, ref menuGroup, menuId, pnts, (Microsoft.VisualStudio.OLE.Interop.IOleCommandTarget)this);
        }

        protected override void SetInnerProject(IntPtr innerIUnknown) {
            var inner = Marshal.GetObjectForIUnknown(innerIUnknown);

            // The reason why we keep a reference to those is that doing a QI after being
            // aggregated would do the AddRef on the outer object.
            _innerVsProjectFlavorCfgProvider = inner as IVsProjectFlavorCfgProvider;
            _innerProject = inner as IVsProject;
            _innerProject3 = inner as IVsProject3;
            _innerVsHierarchy = inner as IVsHierarchy;

            // Ensure we have a service provider as this is required for menu items to work
            if (this.serviceProvider == null)
                this.serviceProvider = (System.IServiceProvider)this._package;

            // Now let the base implementation set the inner object
            base.SetInnerProject(innerIUnknown);

            // Add our commands (this must run after we called base.SetInnerProject)
            _menuService = ((System.IServiceProvider)this).GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
        }

        protected override int GetProperty(uint itemId, int propId, out object property) {
#if HAVE_ICONS
            switch ((__VSHPROPID)propId) {
                case __VSHPROPID.VSHPROPID_IconIndex:
                    // replace the default icon w/ our own icon for HTML files.
                    // We can't return an index into an image list that we own because
                    // the image list is owned by the root node.  So we just fail this
                    // call for HTML files, which causes a request for VSHPROPID_IconHandle
                    // where we give the actual icon.
                    if (IsHtmlFile(innerVsHierarchy, itemId)) {
                        property = 26;
                        return VSConstants.DISP_E_MEMBERNOTFOUND;
                    }
                    break;
                case __VSHPROPID.VSHPROPID_IconHandle:
                    if (IsHtmlFile(innerVsHierarchy, itemId)) {
                        property = (Images.Images[26] as Bitmap).GetHicon();
                        return VSConstants.S_OK;
                    }
                    break;
            }
#endif
            switch ((__VSHPROPID2)propId) {
                case __VSHPROPID2.VSHPROPID_CfgPropertyPagesCLSIDList: {
                    var res = base.GetProperty(itemId, propId, out property);
                    if (ErrorHandler.Succeeded(res)) {
                        var guids = GetGuidsFromList(property as string);
                        guids.RemoveAll(g => CfgSpecificPropertyPagesToRemove.Contains(g));
                        guids.AddRange(CfgSpecificPropertyPagesToAdd);
                        property = MakeListFromGuids(guids);
                    }
                    return res;
                }
                case __VSHPROPID2.VSHPROPID_PropertyPagesCLSIDList: {
                    var res = base.GetProperty(itemId, propId, out property);
                    if (ErrorHandler.Succeeded(res)) {
                        var guids = GetGuidsFromList(property as string);
                        guids.RemoveAll(g => PropertyPagesToRemove.Contains(g));
                        guids.AddRange(PropertyPagesToAdd);
                        property = MakeListFromGuids(guids);
                    }
                    return res;
                }
            }

#if DEV14_OR_LATER
            var id8 = (__VSHPROPID8)propId;
            switch (id8) {
                case __VSHPROPID8.VSHPROPID_SupportsIconMonikers:
                    property = true;
                    return VSConstants.S_OK;
            }
#endif

            return base.GetProperty(itemId, propId, out property);
        }

        private static Guid[] PropertyPagesToAdd = new[] {
            GuidList.guidDjangoPropertyPage
        };

        private static Guid[] CfgSpecificPropertyPagesToAdd = new Guid[0];

        private static HashSet<Guid> PropertyPagesToRemove = new HashSet<Guid> { 
            new Guid("{8C0201FE-8ECA-403C-92A3-1BC55F031979}"),   // typeof(DeployPropertyPageComClass)
            new Guid("{ED3B544C-26D8-4348-877B-A1F7BD505ED9}"),   // typeof(DatabaseDeployPropertyPageComClass)
            new Guid("{909D16B3-C8E8-43D1-A2B8-26EA0D4B6B57}"),   // Microsoft.VisualStudio.Web.Application.WebPropertyPage
            new Guid("{379354F2-BBB3-4BA9-AA71-FBE7B0E5EA94}"),   // Microsoft.VisualStudio.Web.Application.SilverlightLinksPage
        };

        internal static HashSet<Guid> CfgSpecificPropertyPagesToRemove = new HashSet<Guid> { 
            new Guid("{A553AD0B-2F9E-4BCE-95B3-9A1F7074BC27}"),   // Package/Publish Web 
            new Guid("{9AB2347D-948D-4CD2-8DBE-F15F0EF78ED3}"),   // Package/Publish SQL 
        };

        private static List<Guid> GetGuidsFromList(string guidList) {
            if (string.IsNullOrEmpty(guidList)) {
                return new List<Guid>();
            }

            Guid value;
            return guidList.Split(';')
                .Select(str => Guid.TryParse(str, out value) ? (Guid?)value : null)
                .Where(g => g.HasValue)
                .Select(g => g.Value)
                .ToList();
        }

        private static string MakeListFromGuids(IEnumerable<Guid> guidList) {
            return string.Join(";", guidList.Select(g => g.ToString("B")));
        }

#if HAVE_ICONS
        /// <summary>
        /// Gets an ImageHandler for the project node.
        /// </summary>
        public ImageList Images {
            get {
                if (_images == null) {
                    var imageStream = typeof(DjangoProject).Assembly.GetManifestResourceStream("Microsoft.PythonTools.Django.Resources.imagelis.bmp");

                    ImageList imageList = new ImageList();
                    imageList.ColorDepth = ColorDepth.Depth24Bit;
                    imageList.ImageSize = new Size(16, 16);
                    Bitmap bitmap = new Bitmap(imageStream);
                    imageList.Images.AddStrip(bitmap);
                    imageList.TransparentColor = Color.Magenta;
                    _images = imageList;
                }

                return _images;
            }
        }
#endif

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == GuidList.guidWebPackgeCmdId) {
                if (nCmdID == 0x101 /*  EnablePublishToWindowsAzureMenuItem*/) {
                    var shell = (IVsShell)((System.IServiceProvider)this).GetService(typeof(SVsShell));
                    var webPublishPackageGuid = GuidList.guidWebPackageGuid;
                    IVsPackage package;

                    int res = shell.LoadPackage(ref webPublishPackageGuid, out package);
                    if (!ErrorHandler.Succeeded(res)) {
                        return res;
                    }

                    var cmdTarget = package as IOleCommandTarget;
                    if (cmdTarget != null) {
                        res = cmdTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                        if (ErrorHandler.Succeeded(res)) {
                            // TODO: Check flag to see if we were notified
                            // about being added as a web role.
                            if (!AddWebRoleSupportFiles()) {
                                VsShellUtilities.ShowMessageBox(
                                    this,
                                    Resources.AddWebRoleSupportFiles,
                                    null,
                                    OLEMSGICON.OLEMSGICON_INFO,
                                    OLEMSGBUTTON.OLEMSGBUTTON_OK,
                                    OLEMSGDEFBUTTON.OLEMSGDEFBUTTON_FIRST
                                );
                            }
                        }
                        return res;
                    }
                }
            }

            return ((IOleCommandTarget)_menuService).Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool AddWebRoleSupportFiles() {
            var uiShell = (IVsUIShell)((System.IServiceProvider)this).GetService(typeof(SVsUIShell));
            var emptyGuid = Guid.Empty;
            var result = new[] { VSADDRESULT.ADDRESULT_Failure };
            IntPtr dlgOwner;
            if (ErrorHandler.Failed(uiShell.GetDialogOwnerHwnd(out dlgOwner))) {
                dlgOwner = IntPtr.Zero;
            }

            var fullTemplate = ((EnvDTE80.Solution2)_package.DTE.Solution).GetProjectItemTemplate(
                "AzureCSWebRole.zip",
                PythonConstants.LanguageName
            );

            return ErrorHandler.Succeeded(_innerProject3.AddItemWithSpecific(
                (uint)VSConstants.VSITEMID.Root,
                VSADDITEMOPERATION.VSADDITEMOP_RUNWIZARD,
                "bin",
                1,
                new[] { fullTemplate },
                dlgOwner,
                0u,
                ref emptyGuid,
                string.Empty,
                ref emptyGuid,
                result
            )) && result[0] == VSADDRESULT.ADDRESULT_Success;
        }

        int IOleCommandTarget.QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidEureka) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x102: // View in Web Page Inspector from Eureka web tools
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidVenusCmdId) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x034: /* add app assembly folder */
                        case 0x035: /* add app code folder */
                        case 0x036: /* add global resources */
                        case 0x037: /* add local resources */
                        case 0x038: /* add web refs folder */
                        case 0x039: /* add data folder */
                        case 0x040: /* add browser folders */
                        case 0x041: /* theme */
                        case 0x054: /* package settings */
                        case 0x055: /* context package settings */

                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == GuidList.guidWebAppCmdId) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch (prgCmds[i].cmdID) {
                        case 0x06A: /* check accessibility */
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == VSConstants.VSStd2K) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd2KCmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd2KCmdID.SETASSTARTPAGE:
                        case VSConstants.VSStd2KCmdID.CHECK_ACCESSIBILITY:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            } else if (pguidCmdGroup == VSConstants.GUID_VSStandardCommandSet97) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    switch ((VSConstants.VSStd97CmdID)prgCmds[i].cmdID) {
                        case VSConstants.VSStd97CmdID.PreviewInBrowser:
                        case VSConstants.VSStd97CmdID.BrowseWith:
                            prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_INVISIBLE | OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_DEFHIDEONCTXTMENU | OLECMDF.OLECMDF_ENABLED);
                            return VSConstants.S_OK;
                    }
                }
            }

            return ((IOleCommandTarget)_menuService).QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #region IVsProjectFlavorCfgProvider Members

        public int CreateProjectFlavorCfg(IVsCfg pBaseProjectCfg, out IVsProjectFlavorCfg ppFlavorCfg) {
            // We're flavored with a Web Application project and our normal project...  But we don't
            // want the web application project to influence our config as that alters our debug
            // launch story.  We control that w/ the Django project which is actually just letting the
            // base Python project handle it.  So we keep the base Python project config here.
            IVsProjectFlavorCfg webCfg;
            ErrorHandler.ThrowOnFailure(
                _innerVsProjectFlavorCfgProvider.CreateProjectFlavorCfg(
                    pBaseProjectCfg,
                    out webCfg
                )
            );
            ppFlavorCfg = new DjangoProjectConfig(pBaseProjectCfg, webCfg);
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsProject Members

        int IVsProject.AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult) {
            if (cFilesToOpen == 1 && Path.GetFileName(rgpszFilesToOpen[0]).Equals("DjangoNewAppFiles.vstemplate", StringComparison.OrdinalIgnoreCase)) {
                object selectedObj;
                ErrorHandler.ThrowOnFailure(
                    _innerVsHierarchy.GetProperty(
                        itemidLoc,
                        (int)__VSHPROPID.VSHPROPID_ExtObject,
                        out selectedObj
                    )
                );

                EnvDTE.ProjectItems items = null;
                var project = selectedObj as EnvDTE.Project;
                if (project != null) {
                    items = project.ProjectItems;
                } else {
                    var selection = selectedObj as EnvDTE.ProjectItem;
                    if (selection != null) {
                        items = selection.ProjectItems;
                    }
                }

                if (items != null) {
                    bool cancel;
                    pszItemName = ResolveAppNameCollisionWithUser(items, pszItemName, out cancel);
                    if (cancel) {
                        return VSConstants.E_ABORT;
                    }
                }
            }
            return _innerProject.AddItem(itemidLoc, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, pResult);
        }

        int IVsProject.GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot, out string pbstrItemName) {
            return _innerProject.GenerateUniqueItemName(itemidLoc, pszExt, pszSuggestedRoot, out pbstrItemName);
        }

        int IVsProject.GetItemContext(uint itemid, out VisualStudio.OLE.Interop.IServiceProvider ppSP) {
            return _innerProject.GetItemContext(itemid, out ppSP);
        }

        int IVsProject.GetMkDocument(uint itemid, out string pbstrMkDocument) {
            return _innerProject.GetMkDocument(itemid, out pbstrMkDocument);
        }

        int IVsProject.IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid) {
            return _innerProject.IsDocumentInProject(pszMkDocument, out pfFound, pdwPriority, out pitemid);
        }

        int IVsProject.OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
            if (_innerProject3 != null && IsHtmlFile(_innerVsHierarchy.GetItemName(itemid))) {
                // force HTML files opened w/o an editor type to be opened w/ our editor factory.
                Guid guid = GuidList.guidDjangoEditorFactory;
                return _innerProject3.OpenItemWithSpecific(
                    itemid,
                    0,
                    ref guid,
                    null,
                    rguidLogicalView,
                    punkDocDataExisting,
                    out ppWindowFrame
                );
            }

            return _innerProject.OpenItem(itemid, rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }

        #endregion

        #region IDjangoProject Members

        public ProjectSmuggler GetDjangoProject() {
            return new ProjectSmuggler(this);
        }

        #endregion

        #region IVsFilterAddProjectItemDlg Members

        int IVsFilterAddProjectItemDlg.FilterListItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterListItemByTemplateFile(ref Guid rguidProjectItemTemplates, string pszTemplateFile, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterTreeItemByLocalizedName(ref Guid rguidProjectItemTemplates, string pszLocalizedName, out int pfFilter) {
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        int IVsFilterAddProjectItemDlg.FilterTreeItemByTemplateDir(ref Guid rguidProjectItemTemplates, string pszTemplateDir, out int pfFilter) {
            // https://pytools.codeplex.com/workitem/1313
            // ASP.NET will filter some things out, including .css files, which we don't want it to do.
            // So we shut that down by not forwarding this to any inner projects, which is fine, because
            // Python projects don't implement this interface either.
            pfFilter = 0;
            return VSConstants.S_OK;
        }

        #endregion
    }

    class TagInfo {
        public readonly string Documentation;
        public readonly IPythonProjectEntry Entry;
        public TagInfo(string doc, IPythonProjectEntry entry) {
            Documentation = doc;
            Entry = entry;
        }

    }

}
