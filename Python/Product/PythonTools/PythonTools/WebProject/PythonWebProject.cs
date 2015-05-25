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
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Flavor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using IServiceProvider = System.IServiceProvider;

namespace Microsoft.PythonTools.Project.Web {
    [Guid("742BB562-7AEE-4FC7-8CD2-48D66C8CC435")]
    partial class PythonWebProject :
        FlavoredProjectBase,
        IOleCommandTarget,
        IVsProjectFlavorCfgProvider,
        IVsProject,
        IVsFilterAddProjectItemDlg
    {
        private PythonToolsPackage _package;
        internal IVsProject _innerProject;
        internal IVsProject3 _innerProject3;
        private IVsProjectFlavorCfgProvider _innerVsProjectFlavorCfgProvider;
        private static Guid PythonProjectGuid = new Guid(PythonConstants.ProjectFactoryGuid);
        private IOleCommandTarget _menuService;

        public PythonWebProject() {
        }

        internal PythonToolsPackage Package {
            get { return _package; }
            set {
                Debug.Assert(_package == null);
                if (_package != null) {
                    throw new InvalidOperationException("PythonWebProject.Package must only be set once");
                }
                _package = value;
            }
        }

        #region IVsAggregatableProject

        /// <summary>
        /// Do the initialization here (such as loading flavor specific
        /// information from the project)
        /// </summary>
        protected override void InitializeForOuter(string fileName, string location, string name, uint flags, ref Guid guidProject, out bool cancel) {
            base.InitializeForOuter(fileName, location, name, flags, ref guidProject, out cancel);

            var proj = _innerVsHierarchy.GetProject();

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

        protected override int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, VisualStudio.OLE.Interop.OLECMD[] prgCmds, IntPtr pCmdText) {
            if (pguidCmdGroup == GuidList.guidOfficeSharePointCmdSet) {
                for (int i = 0; i < prgCmds.Length; i++) {
                    // Report it as supported so that it's not routed any
                    // further, but disable it and make it invisible.
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_INVISIBLE);
                }
                return VSConstants.S_OK;
            }

            return base.QueryStatusCommand(itemid, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
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
            if (this.serviceProvider == null) {
                this.serviceProvider = (IServiceProvider)Package;
            }

            // Now let the base implementation set the inner object
            base.SetInnerProject(innerIUnknown);

            // Get access to the menu service used by FlavoredProjectBase. We
            // need to forward IOleCommandTarget functions to this object, since
            // we override the FlavoredProjectBase implementation with no way to
            // call it directory.
            // (This must run after we called base.SetInnerProject)
            _menuService = (IOleCommandTarget)((IServiceProvider)this).GetService(typeof(IMenuCommandService));
            if (_menuService == null) {
                throw new InvalidOperationException("Cannot initialize Web project");
            }
        }

        protected override int GetProperty(uint itemId, int propId, out object property) {
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
            switch(id8) {
                case __VSHPROPID8.VSHPROPID_SupportsIconMonikers:
                    property = true;
                    return VSConstants.S_OK;
            }
#endif

            return base.GetProperty(itemId, propId, out property);
        }

        private static Guid[] PropertyPagesToAdd = new[] {
            new Guid(PythonConstants.WebPropertyPageGuid)
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

        int IOleCommandTarget.Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
            if (pguidCmdGroup == GuidList.guidWebPackgeCmdId) {
                if (nCmdID == 0x101 /*  EnablePublishToWindowsAzureMenuItem*/) {
                    var shell = (IVsShell)((IServiceProvider)this).GetService(typeof(SVsShell));
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
                                    SR.GetString(SR.AddWebRoleSupportFiles),
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

            return _menuService.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        private bool AddWebRoleSupportFiles() {
            var uiShell = (IVsUIShell)((IServiceProvider)this).GetService(typeof(SVsUIShell));
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

            return _menuService.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        #region IVsProjectFlavorCfgProvider Members

        public int CreateProjectFlavorCfg(IVsCfg pBaseProjectCfg, out IVsProjectFlavorCfg ppFlavorCfg) {
            // We're flavored with a Web Application project and our normal
            // project...  But we don't want the web application project to
            // influence our config as that alters our debug launch story.  We
            // control that w/ the web project which is actually just letting
            // the base Python project handle it. So we keep the base Python
            // project config here.
            IVsProjectFlavorCfg webCfg;
            ErrorHandler.ThrowOnFailure(
                _innerVsProjectFlavorCfgProvider.CreateProjectFlavorCfg(
                    pBaseProjectCfg,
                    out webCfg
                )
            );
            ppFlavorCfg = new PythonWebProjectConfig(pBaseProjectCfg, webCfg);
            return VSConstants.S_OK;
        }

        #endregion

        #region IVsProject Members

        int IVsProject.AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult) {
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
            return _innerProject.OpenItem(itemid, rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
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
}
